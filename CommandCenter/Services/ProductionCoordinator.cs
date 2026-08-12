using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Services
{
    /// <summary>
    /// 生产流程协调器：把 PLC 到位信号、多台相机触发、图像监听、结果上报串成一个循环。
    ///
    /// 【V1.12.11 角色反转】现场 PLC(汇川)做 Modbus 主站、上位机做从站。本类的 _plc 调用
    ///   全部保留原签名，底层已改为读写上位机自己 DataStore 寄存器区（不发起 Modbus 请求）：
    ///   "到位信号"由 PLC 主站写入上位机 D100，本类轮询读自己 DataStore；完成/计数/配方
    ///   由本类写自己 DataStore，PLC 主站轮询来读。业务流程不变，只是数据来源/去向从远端
    ///   PLC 寄存器变成本地 DataStore。
    ///
    /// 【主流程(与现场要求一致)】
    ///   ① 空闲期后台轮询到位寄存器（自己 DataStore 的 D100，PLC 主站写入 1 表示到位）；
    ///   ② 读到"到位"→ 立即清复位 → 【对所有已配置相机并行触发（V1.8.3 起）】；
    ///   ③ 每台相机独立：IV4 指令 T2 直接回 OK/NG（未开启时退化为"图到即 OK"），记各自判定；
    ///   ④ 取图（V1.7.0 每台相机按 ImageSource 二选一）：
    ///      - Ftp（默认）：等相机 FTP 新图上传（共用总超时 = 各相机 ImageWaitMs 的最大值）；
    ///      - Tcp：触发后立即发 BR 指令在同一连接上同步读回最新图像（免 FTP 落盘中转）；
    ///      每个点位各存各的图（目录按模板：年/月/日/SN/OK|NG，文件名按点位号）→ Done=1(完成)；
    ///      某相机图超时/触发或取图失败→该点位标失败，全部失败才 Done=2(取像异常)；
    ///   ⑤ 回到①循环。
    ///
    /// 【多相机】CameraConfig 配几台就触几台。一台"到位"= 一排点位一次检测，
    ///   每台相机的新图（各自 FTP 目录）到齐后才整体收尾；图以独立 WindowData 逐个抛给 UI
    ///   （每个点位一个 WindowData，刷新一个显示窗口）。
    ///
    /// 【线程】
    ///   轮询、等待均在后台线程执行，通过事件把结果抛给 UI（由订阅方 Invoke 到界面线程）。
    ///   本类不接触任何控件，纯业务编排，便于换界面复用。
    /// </summary>
    public class ProductionCoordinator : IDisposable
    {
        private readonly PlcService _plc;
        private readonly List<KeyenceIV4Camera> _cameras;   // 每台相机一个服务实例
        private readonly List<CameraConfig> _cameraCfgs;    // 对应的相机配置（点位号/FTP目录等）
        private readonly ImageStore _imageStore;
        private readonly DisplayConfig _display;
        private readonly List<int> _windowStationMap;       // 窗口→存图点位映射（配置，可能为 null 由调用方兜底）

        private readonly System.Threading.Timer _positionTimer;  // 到位轮询（后台线程）
        private readonly System.Threading.Timer _imageWaitTimer; // 等图超时单发（到期触发收尾）
        private volatile int _busy;      // 忙碌标志：0=空闲，1=处理中（Interlocked 原子，跨线程安全）
        private volatile bool _running;  // 总开关
        private int _seqNo;              // 全局检测序号
        private int _nextWindowIndex = 1;  // 下一个要刷新的窗口（1..rows*cols 环形），初始 1 保证"第一个拍照位=点位1"
        private readonly int _windowCount; // 显示窗口总数 = rows*cols

        // 一次检测的所有相机快照（触发成功到收尾之间会被 FTP 线程/超时线程读取）
        private List<PendingCamera> _pends = new List<PendingCamera>();
        private int _finished;           // 双收尾保护：0=待收尾，1=已收尾（Interlocked）

        /// <summary>已释放标志：关窗 Dispose 后再见到的后台回调立即终止（volatile 跨线程可见）</summary>
        private volatile bool _disposed;

        /// <summary>到位轮询周期（毫秒）：连上 PLC 时用</summary>
        private const int PollMs = 200;

        /// <summary>连接失败后的重试用期（毫秒）：放慢节奏，避免高频无效尝试刷爆日志</summary>
        private const int SlowPollMs = 1000;

        /// <summary>检测完成事件：携带一次结果（含图片路径、OK/NG、序号、点位号）。每张图各抛一次。</summary>
        public event Action<WindowData, int> InspectionFinished;

        /// <summary>检测流程异常提醒（参数为提示文本）</summary>
        public event Action<string> ErrorRaised;

        /// <summary>流程状态文本（空闲/等待到位/拍照中），UI 可显示</summary>
        public event Action<string> StateChanged;

        /// <summary>一条产品被扫码进来的序列号透传（若扫码枪关闭则 UI 手动输入）</summary>
        public string LatestSerialNumber { get; set; } = "";

        public ProductionCoordinator(PlcService plc,
                                     List<KeyenceIV4Camera> cameras,
                                     List<CameraConfig> cameraCfgs,
                                     ImageStore imageStore,
                                     DisplayConfig display,
                                     List<int> windowStationMap)
        {
            _plc = plc;
            _cameras = cameras;
            _cameraCfgs = cameraCfgs;
            _imageStore = imageStore;
            _display = display;
            _windowStationMap = windowStationMap;
            _windowCount = Math.Max(1, display.Rows * display.Columns);

            // 到位轮询：后台线程 200ms 一问 PLC。
            // ★ 必须用 System.Threading.Timer：此前用 Forms.Timer 在 UI 线程同步读 PLC，
            //   不可达 IP 时把界面整个卡住（点"系统设置"半天没反应就是这原因）。
            _positionTimer = new System.Threading.Timer(
                PositionTimer_Tick, null,
                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

            // 等图超时备弹：单发，默认失能，触发成功后装弹一次
            _imageWaitTimer = new System.Threading.Timer(
                ImageWaitTimeout, null,
                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// 开始运行：给每台相机注册 FTP 监听 + 启动 PLC 到位轮询。
        /// </summary>
        public void Start()
        {
            _running = true;
            _imageStore.FtpFileArrived += OnFtpFileArrived;
            // 每台相机各建监听：目录优先相机自己的 FtpUploadDir，为空回退全局 FtpRootDir。
            // TCP/BR 直读取图模式的相机不注册 FTP 监听（图由 BR 指令同步读回，见触发循环），
            // 避免它在 FTP 目录里的任何历史文件被误当作本次新图。
            for (int i = 0; i < _cameraCfgs.Count; i++)
            {
                if (IsTcpImage(_cameraCfgs[i])) continue;
                string dir = string.IsNullOrWhiteSpace(_cameraCfgs[i].FtpUploadDir)
                    ? _imageStore.DefaultFtpDir
                    : _cameraCfgs[i].FtpUploadDir;
                _imageStore.AddMonitor(dir, i);
            }
            SafeChange(_positionTimer, 0, PollMs); // 立即首轮，之后每 200ms
            SetState("等待 PLC 主站到位信号");
        }

        /// <summary>暂停流程（界面手动暂停时调用，保留在 Idle）。</summary>
        public void Pause()
        {
            _running = false;
            SafeChange(_positionTimer, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            SetState("已暂停");
        }

        /// <summary>恢复流程。</summary>
        public void Resume()
        {
            if (!_running)
            {
                _running = true;
                SafeChange(_positionTimer, 0, PollMs);
                SetState("等待 PLC 主站到位信号");
            }
        }

        /// <summary>
        /// 到位轮询（后台线程）：只在空闲时读 PLC，读到到位进入一次检测。
        /// PLC 连不上时降频到 SlowPollMs 重试，连接恢复自动回 PollMs。
        /// </summary>
        private void PositionTimer_Tick(object state)
        {
            if (!_running || _disposed) return; // 已暂停/已释放：不再轮询

            // PLC 连不上：放慢重试节奏，别每秒扑空（且不会卡任何线程）
            if (!_plc.EnsureConnected())
            {
                SafeChange(_positionTimer, SlowPollMs, SlowPollMs);
                return;
            }
            // 已连上：恢复快速轮询
            SafeChange(_positionTimer, PollMs, PollMs);

            // 忙碌中忽略新信号，避免"等待取像"期间重复触发另一轮拍照
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                SetState("已触发，等待相机取像...");
                return;
            }

            try
            {
                if (!_plc.ReadMoveDone())
                {
                    // 还没到位：归还空闲标志，下一轮再查
                    Interlocked.Exchange(ref _busy, 0);
                    return;
                }

                SetState("相机到位，触发拍照");
                _plc.ClearMoveDone();                   // 复位握手，否则会反复触发

                // ===== 对所有相机并行触发（V1.8.3 起），各自记录判定/失败原因 =====
                _pends = new List<PendingCamera>();
                for (int i = 0; i < _cameras.Count; i++)
                {
                    _pends.Add(new PendingCamera
                    {
                        CameraIndex = i,
                        ImageSource = _cameraCfgs[i].ImageSource, // 本相机取图模式（Ftp/Tcp），收尾时决定如何归档
                        ResultText = ""
                    });
                }

                // 【V1.8.3 修复】多相机并行触发：此前串行 for，每台"触发+取图"是同步阻塞的
                // （T2 最坏 ResponseTimeoutMs、BR 再 ResponseTimeoutMs），N 台相机总耗时线性累加，
                // 现场节拍快时新的到位信号会被 _busy 挡住漏检。改为每台相机一个 Task 并行触发，
                // 总耗时 ≈ 最慢那台相机的时间；每台相机只写自己 _pends[i] 快照，互不干扰。
                // 相机服务内部各自有锁 + 超时，Task.Run 走线程池，绝不在 UI 线程。
                var tasks = new System.Threading.Tasks.Task[_cameras.Count];
                for (int i = 0; i < _cameras.Count; i++)
                {
                    int idx = i; // 闭包锁定副本
                    tasks[idx] = System.Threading.Tasks.Task.Run(() => TriggerOneCamera(idx));
                }
                try { System.Threading.Tasks.Task.WaitAll(tasks); }
                catch (Exception ex) { LogHelper.Error("相机触发任务异常", ex); }

                // 全部触发失败：不必傻等图超时，立刻收尾（各相机都带 FailReason）
                if (_pends.All(p => !p.TriggerOk))
                {
                    FinishAll(null);
                    return;
                }

                // V1.7.2 修复：Tcp 取图模式的图在触发时就同步读回（IsSnapped 已置位），
                // 若所有"需要等图的相机"都到图或已失败，就无需再等 ImageWaitMs 超时，
                // 立即收尾。此前这里只判断"全部触发失败"，Tcp 模式会白白等满超时
                // （甚至把 Done 信号拖后），现场表现为"拍完照半天才上报完成"。
                if (_pends.All(x => !x.TriggerOk || x.IsSnapped))
                {
                    FinishAll(null);
                    return;
                }

                SetState("已触发，等待图像...");

                // 等图总超时 = 各相机 ImageWaitMs 的最大值，先到的图先落 pending，到齐即收尾
                int totalWaitMs = _cameraCfgs.Where(c => c.ImageWaitMs > 0)
                                             .Select(c => c.ImageWaitMs)
                                             .DefaultIfEmpty(10000)
                                             .Max();
                SafeChange(_imageWaitTimer, totalWaitMs, System.Threading.Timeout.Infinite);
            }
            catch (Exception ex)
            {
                LogHelper.Error("到位处理异常", ex);
                Interlocked.Exchange(ref _busy, 0);
                SetState("等待 PLC 主站到位信号");
            }
        }

        /// <summary>
        /// 触发并取图单台相机（V1.8.3 起由并行任务调用）。
        /// 只写自己的 _pends[idx] 快照，与其它相机任务互不干扰；内部自带 try-catch，
        /// 任何异常都收敛为该相机的 FailReason，绝不把异常抛回 WaitAll。
        /// </summary>
        /// <param name="idx">相机在配置里的下标（0 起）</param>
        private void TriggerOneCamera(int idx)
        {
            var cfg = _cameraCfgs[idx];
            var p = _pends[idx];
            try
            {
                if (cfg.ReadResultFromCamera)
                {
                    // 首选：T2 一次完成"触发+读判定"，OK/NG 直接来自 IV4
                    var outcome = _cameras[idx].TriggerAndRead();
                    p.TriggerOk = outcome.Succeeded;
                    if (outcome.Succeeded)
                    {
                        p.IsOk = outcome.IsOk;
                        p.ResultText = outcome.ResultText ?? "";
                        LogHelper.Info($"相机[{idx}]判定：{(outcome.IsOk ? "OK" : "NG")} 结果={p.ResultText}" +
                                       (string.IsNullOrEmpty(outcome.Detail) ? "" : " " + outcome.Detail));
                        if (!outcome.IsOk)
                            ErrorRaised?.Invoke($"相机[{idx}]判定 NG，结果={p.ResultText}");
                    }
                    else
                    {
                        p.FailReason = $"相机[{idx}]触发/读判定失败：" + outcome.Detail;
                    }
                }
                else
                {
                    // 退化模式：只 T1 触发，判定不详，FTP 图到即记 OK（现场临时用）
                    p.TriggerOk = _cameras[idx].SendTrigger();
                    p.IsOk = true;
                    if (!p.TriggerOk)
                        p.FailReason = $"相机[{idx}]触发失败";
                }

                // 取图（V1.7.0）：Ftp 模式保持"等 FTP 新图"（上面已触发，等 OnFtpFileArrived 回调）；
                // Tcp 模式在触发成功后立即发 BR 指令，同步读回相机最新图像（24bit 位图）。
                // 【时序注意】BR 读的是"最新图像"，紧跟在本次 T2 触发后调用才会对应本帧；
                //   若现场节拍很快或相机有外部触发源插入新帧，需实测确认 BR 与本帧的对应关系。
                if (p.TriggerOk && IsTcpImage(cfg))
                {
                    var img = _cameras[idx].ReadImage();
                    if (img.Succeeded && img.ImageData != null)
                    {
                        p.ImageBytes = img.ImageData;   // 图已在内存
                        p.IsSnapped = true;             // 等效 FTP 模式"新图已到"
                        LogHelper.Info($"相机[{idx}] TCP 取图成功：{img.DataSize}B（触发编号={img.DataTriggerNo}）");
                    }
                    else
                    {
                        p.FailReason = $"相机[{idx}] TCP 取图失败：" + img.Detail;
                        p.TriggerOk = false;            // 无图 → 该点位按失败处理，不进入等图收尾
                        ErrorRaised?.Invoke(p.FailReason);
                    }
                }
            }
            catch (Exception ex)
            {
                p.TriggerOk = false;
                p.FailReason = $"相机[{idx}]触发异常：" + ex.Message;
            }
        }

        /// <summary>各相机图像等待总超时回调：视作未到图的相机取像失败，整体收尾。</summary>
        private void ImageWaitTimeout(object state)
        {
            if (_running && !_disposed)
                FinishAll("等待相机图像超时");
        }

        /// <summary>
        /// 某台相机 FTP 新图到达。参数带相机索引，定位到对应 pending 快照填图。
        /// 事件来自 FileSystemWatcher 线程；_finished 保护保证不会与超时回调重复收尾。
        /// </summary>
        private void OnFtpFileArrived(int cameraIndex, string fullPath)
        {
            if (_disposed || _busy == 0) return; // 已释放/非流程内到达的图忽略（相机制试图之类）
            var p = _pends.FirstOrDefault(x => x.CameraIndex == cameraIndex);
            if (p == null || !p.TriggerOk || p.IsSnapped) return; // 无关相机/触发失败/已到过图都忽略
            p.FtpPath = fullPath;
            p.IsSnapped = true;
            // 所有"需要等图"的相机都到位 → 整体收尾（忽略触发失败的那些，它们已经失败）
            if (_pends.All(x => !x.TriggerOk || x.IsSnapped))
                FinishAll(null);
        }

        /// <summary>
        /// 一次检测整体收尾：逐点位归档图片 → 通知 PLC → 统计 → 抛事件。
        /// 可能由"最后一张图到达"或"等图超时"触发，只有第一个进入的生效，其余直接返回。
        /// </summary>
        /// <param name="globalFailReason">整体失败原因（超时等）；成功传 null。逐相机细节在各自 FailReason。</param>
        private void FinishAll(string globalFailReason)
        {
            // 双收尾保护：超时回调与 FTP 到达可能同时命中，只认第一次
            if (Interlocked.Exchange(ref _finished, 1) != 0)
                return;

            try
            {
                bool anyImage = false; // 任意一台有图即整体"检测完成(1)"，全无图才是"取像异常(2)"
                foreach (var p in _pends)
                {
                    if (p.TriggerOk && !p.IsSnapped && string.IsNullOrEmpty(p.FailReason))
                        p.FailReason = globalFailReason ?? "等待相机图像超时"; // 超时补记点位失败原因

                    // 本次结果落在哪个窗口（1..N 环形）→ 该窗口的点位即存图点位（可自定义，见 WindowStationMap）
                    int targetWindow = _nextWindowIndex;
                    _nextWindowIndex = (_nextWindowIndex % _windowCount) + 1;
                    int stationNo = ResolveStation(targetWindow);

                    // 图是否到手：FTP 模式看 FtpPath，TCP 模式看 ImageBytes（两者有其一即 hasImage）
                    bool hasImage = p.TriggerOk && p.IsSnapped
                        && (p.ImageBytes != null || !string.IsNullOrEmpty(p.FtpPath));
                    string archived = null;
                    if (hasImage)
                    {
                        // 归档：TCP 模式图在内存字节里，直接解码转存正式目录（不落 FTP 中转文件）；
                        //       FTP 模式读源文件转存（见 ArchiveImage）。
                        archived = (p.ImageBytes != null)
                            ? _imageStore.SaveImageBytes(p.ImageBytes, stationNo, p.IsOk, LatestSerialNumber)
                            : ArchiveImage(p, stationNo);
                        if (archived == null)
                        {
                            // 归档失败兜底：FTP 模式回退用源文件当结果（图至少能显示）；
                            // TCP 模式无源文件，明确报错让该窗口走失败占位（等现场实测确认格式后自然消除）。
                            if (!string.IsNullOrEmpty(p.FtpPath))
                                archived = p.FtpPath;
                            else
                                ErrorRaised?.Invoke($"点位{stationNo} 图像归档失败（TCP 取图，内存解码失败）");
                        }
                    }
                    anyImage |= !string.IsNullOrEmpty(archived);

                    try
                    {
                        _seqNo++;
                        var data = new WindowData
                        {
                            SeqNo = _seqNo,
                            IsOk = p.IsOk,
                            ImagePath = archived,
                            CapturedAt = DateTime.Now,
                            SerialNumber = LatestSerialNumber,
                            ResultText = p.ResultText ?? "",
                            StationNo = stationNo
                        };
                        InspectionFinished?.Invoke(data, targetWindow);

                        if (!hasImage && p.FailReason != null)
                            ErrorRaised?.Invoke(p.FailReason);
                        else if (hasImage)
                            LogHelper.Info($"点位{stationNo} 检测完成：{(p.IsOk ? "OK" : "NG")} → {archived}");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("点位收尾异常", ex);
                    }
                }

                _plc.SetDone(anyImage ? 1 : 2);   // 1=检测完成（含NG）、2=取像异常（全部点位失败）
            }
            catch (Exception ex)
            {
                LogHelper.Error("检测收尾异常", ex);
                ErrorRaised?.Invoke("检测收尾异常：" + ex.Message);
            }
            finally
            {
                // 归还资源：停掉等图超时、复原忙碌与收尾标志
                SafeChange(_imageWaitTimer, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                Interlocked.Exchange(ref _busy, 0);
                Interlocked.Exchange(ref _finished, 0);
                SetState("等待 PLC 主站到位信号");
            }
        }

        /// <summary>
        /// 归档图片：把 FTP 新图读入内存并按模板转存到正式目录（年/月/日/SN/OK|NG + 点位号.png）。
        /// 【V1.8.3 修复】FTP 服务端写完文件才触发 Created/Renamed，但有时事件先于写完到达，
        ///   Image.FromFile 会抛"文件被占用/损坏"导致图丢失。改为：FileShare.ReadWrite 方式打开
        ///   （正在写也能读），复制到内存后统一用 Image.FromStream 解码；读取失败短延迟重试最多
        ///   3 次（共约 1.2s），仍失败返回 null。
        /// 用内存解码同时避免 FTP 源文件可能被相机重写的文件占用问题。失败返回 null。
        /// </summary>
        /// <param name="p">本点位触发/判定快照</param>
        /// <param name="stationNo">本次存图点位（来自窗口点位映射，见 ResolveStation）</param>
        private string ArchiveImage(PendingCamera p, int stationNo)
        {
            byte[] bytes = null;
            Exception lastEx = null;
            // 重试读取：事件早于文件写完时，等待 FTP 落盘（每次 Sleep 400ms，最多 3 次）
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (var fs = new FileStream(p.FtpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var ms = new MemoryStream())
                    {
                        fs.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                    break;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Thread.Sleep(400);
                }
            }

            if (bytes == null)
            {
                LogHelper.Error("图片归档失败（读取 FTP 源文件多次仍失败）", lastEx);
                ErrorRaised?.Invoke($"点位{stationNo} 图片归档失败：" + p.FtpPath);
                return null;
            }

            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var src = Image.FromStream(ms))
                using (var copy = new Bitmap(src))
                {
                    return _imageStore.SaveImage(copy, stationNo, p.IsOk, LatestSerialNumber);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("图片归档失败（解码/保存出错）", ex);
                ErrorRaised?.Invoke($"点位{stationNo} 图片归档失败：" + p.FtpPath);
                return null;
            }
        }

        /// <summary>
        /// 解析某号窗口的存图点位：优先取配置的窗口映射 WindowStationMap[窗口号-1]；
        /// 映射缺失 / 越界（窗口数中途改小、旧配置等）时兜底"点位=窗口编号"。
        /// </summary>
        private int ResolveStation(int windowIndex)
        {
            if (_windowStationMap != null
                && windowIndex - 1 >= 0
                && windowIndex - 1 < _windowStationMap.Count
                && _windowStationMap[windowIndex - 1] > 0)
            {
                return _windowStationMap[windowIndex - 1];
            }
            return windowIndex;
        }

        /// <summary>
        /// 判断相机是否走 TCP/BR 直读取图（V1.7.0）：配置 ImageSource=="Tcp"（大小写不敏感）。
        /// 其余取值（含空/null/其他文字）一律按 Ftp 兜底，旧配置无需迁移、行为不变。
        /// </summary>
        private static bool IsTcpImage(CameraConfig cfg) =>
            cfg != null
            && !string.IsNullOrWhiteSpace(cfg.ImageSource)
            && cfg.ImageSource.Trim().Equals("Tcp", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 取最近一张图的内存副本（UI 显示用），避免 GDI+ 锁定文件。
        /// 【V1.8.3 修复】FileShare.ReadWrite：归档失败回退用 FTP 源文件显示时，文件可能
        ///   仍在被写/被占用，Read 共享读不到会返回 null 导致窗口空白。
        /// </summary>
        public static Image LoadImageSafe(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    ms.Position = 0;
                    return Image.FromStream(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>timer.Change 的安全包装：关窗 Dispose（UI 线程）与后台回调并发时，
        /// Change 可能撞到已 Dispose 的 Timer 抛 ObjectDisposedException，一律安静忽略。</summary>
        private static void SafeChange(System.Threading.Timer timer, int dueTime, int period)
        {
            try { timer.Change(dueTime, period); }
            catch (ObjectDisposedException) { } // 已释放：背景回调终止，无需再调度
            catch (Exception) { } // 兼容其他释放期异常，一律忽略
        }

        private readonly object _stateLock = new object();
        private string _lastState = "";

        /// <summary>发流程状态（日志 + 事件）。相同的状态不重复发（V1.7.2 修复）：
        /// 忙时到位轮询每 200ms 抢占失败都会调本方法，若每次都记一条"已触发，等待相机取像..."
        /// 会把日志刷爆；按文本去重后只在状态真正切换时各记一条。</summary>
        private void SetState(string text)
        {
            lock (_stateLock)
            {
                if (_lastState == text) return;
                _lastState = text;
                LogHelper.Info("流程状态：" + text);
            }
            StateChanged?.Invoke(text);
        }

        public void Dispose()
        {
            // 先刹车再释放：_disposed/_running 置位后，正在后台执行的回调
            // 在下一处检查/Change 时会自行退出，不会在已 Dispose 的 Timer 上继续调度。
            _disposed = true;
            _running = false;
            _positionTimer?.Dispose();
            _imageWaitTimer?.Dispose();
            _imageStore.FtpFileArrived -= OnFtpFileArrived;
            _imageStore.Dispose();
        }

        /// <summary>
        /// 一台相机一次检测的暂存快照：判定、图像（FTP 路径或 TCP 字节）、失败原因等，
        /// 在触发到收尾之间跨线程读取。
        /// （只被协调器内部使用，刻意不设锁——图到达与超时回调对同一快照的写都是"幂等填值"，
        ///  由 _finished 双收尾保护兜底，最坏情况只是日志里少一条。
        ///  IsSnapped 声明为 volatile：OnFtpFileArrived（FTP 线程）写、FinishAll（超时/收尾线程）
        ///  读，volatile 保证该标志的读一定看到最新的写，避免极小窗口读到旧值误判"图未到"。）
        /// </summary>
        private class PendingCamera
        {
            public int CameraIndex;    // 相机在配置里的下标（0 起）
            public string ImageSource; // 本相机取图模式："Ftp"（等 FTP 推图）/"Tcp"（BR 同步读图）
            public bool TriggerOk;     // 触发是否成功（Ftp 模式：成功才等这张图；Tcp 模式：取图失败也会被置 false）
            public bool IsOk;          // IV4 判定结论（触发失败时无意义）
            public string ResultText;  // 8 位判定文本
            public string FtpPath;     // Ftp 模式：FTP 新图完整路径（到图后填）
            public byte[] ImageBytes;  // Tcp 模式：BR 读回的最新图像字节（24bit BMP，无则 null）
            public volatile bool IsSnapped; // 是否已拿到图（Ftp 模式=新图到；Tcp 模式=BR 读回）。volatile 见类注释
            public string FailReason;  // 触发失败/取图失败/等图超时原因
        }
    }
}