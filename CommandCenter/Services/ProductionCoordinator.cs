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
    /// 【主流程(与现场要求一致)】
    ///   ① 空闲期后台轮询 PLC 到位寄存器；
    ///   ② 读到"到位"→ 立即清复位 → 【对所有已配置相机依次触发】；
    ///   ③ 每台相机独立：IV4 指令 T2 直接回 OK/NG（未开启时退化为"FTP 图到即 OK"），记各自判定；
    ///   ④ 等各相机 FTP 新图上传（共用总超时 = 各相机 ImageWaitMs 的最大值）→
    ///      每个点位各存各的图（目录按模板：年/月/日/SN/OK|NG，文件名按点位号）→ Done=1(完成)；
    ///      某相机图超时/触发失败→该点位标失败，全部失败才 Done=2(取像异常)；
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
        private int _nextWindowIndex;    // 下一个要刷新的窗口（1..rows*cols 环形）
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
            // 每台相机各建监听：目录优先相机自己的 FtpUploadDir，为空回退全局 FtpRootDir
            for (int i = 0; i < _cameraCfgs.Count; i++)
            {
                string dir = string.IsNullOrWhiteSpace(_cameraCfgs[i].FtpUploadDir)
                    ? _imageStore.DefaultFtpDir
                    : _cameraCfgs[i].FtpUploadDir;
                _imageStore.AddMonitor(dir, i);
            }
            SafeChange(_positionTimer, 0, PollMs); // 立即首轮，之后每 200ms
            SetState("等待 PLC 到位信号");
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
                SetState("等待 PLC 到位信号");
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

                // ===== 对所有相机依次触发，各自记录判定/失败原因 =====
                _pends = new List<PendingCamera>();
                for (int i = 0; i < _cameras.Count; i++)
                {
                    var cfg = _cameraCfgs[i];
                    var p = new PendingCamera
                    {
                        CameraIndex = i,
                        ResultText = ""
                    };
                    _pends.Add(p);

                    try
                    {
                        if (cfg.ReadResultFromCamera)
                        {
                            // 首选：T2 一次完成"触发+读判定"，OK/NG 直接来自 IV4
                            var outcome = _cameras[i].TriggerAndRead();
                            p.TriggerOk = outcome.Succeeded;
                            if (outcome.Succeeded)
                            {
                                p.IsOk = outcome.IsOk;
                                p.ResultText = outcome.ResultText ?? "";
                                LogHelper.Info($"相机[{i}]判定：{(outcome.IsOk ? "OK" : "NG")} 结果={p.ResultText}" +
                                               (string.IsNullOrEmpty(outcome.Detail) ? "" : " " + outcome.Detail));
                                if (!outcome.IsOk)
                                    ErrorRaised?.Invoke($"相机[{i}]判定 NG，结果={p.ResultText}");
                            }
                            else
                            {
                                p.FailReason = $"相机[{i}]触发/读判定失败：" + outcome.Detail;
                            }
                        }
                        else
                        {
                            // 退化模式：只 T1 触发，判定不详，FTP 图到即记 OK（现场临时用）
                            p.TriggerOk = _cameras[i].SendTrigger();
                            p.IsOk = true;
                            if (!p.TriggerOk)
                                p.FailReason = $"相机[{i}]触发失败";
                        }
                    }
                    catch (Exception ex)
                    {
                        p.TriggerOk = false;
                        p.FailReason = $"相机[{i}]触发异常：" + ex.Message;
                    }
                }

                // 全部触发失败：不必傻等图超时，立刻收尾（各相机都带 FailReason）
                if (_pends.All(p => !p.TriggerOk))
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
                SetState("等待 PLC 到位信号");
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

                    bool hasImage = p.TriggerOk && p.IsSnapped && !string.IsNullOrEmpty(p.FtpPath);
                    string archived = null;
                    if (hasImage)
                    {
                        archived = ArchiveImage(p, stationNo);
                        if (archived == null) archived = p.FtpPath; // 归档失败不致命：仍以 FTP 原图当结果
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
                SetState("等待 PLC 到位信号");
            }
        }

        /// <summary>
        /// 归档图片：把 FTP 新图读入内存并按模板转存到正式目录（年/月/日/SN/OK|NG + 点位号.png）。
        /// 用内存解码避免 FTP 源文件可能被相机重写的文件占用问题。失败返回 null。
        /// </summary>
        /// <param name="p">本点位触发/判定快照</param>
        /// <param name="stationNo">本次存图点位（来自窗口点位映射，见 ResolveStation）</param>
        private string ArchiveImage(PendingCamera p, int stationNo)
        {
            try
            {
                using (var src = Image.FromFile(p.FtpPath))
                using (var copy = new Bitmap(src))
                {
                    return _imageStore.SaveImage(copy, stationNo, p.IsOk, LatestSerialNumber);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("图片归档失败", ex);
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
        /// 取最近一张图的内存副本（UI 显示用），避免 GDI+ 锁定文件。
        /// </summary>
        public static Image LoadImageSafe(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
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

        private void SetState(string text)
        {
            LogHelper.Info("流程状态：" + text);
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
        /// 一台相机一次检测的暂存快照：判定、FTP 图路径、失败原因等，在触发到收尾之间跨线程读取。
        /// （只被协调器内部使用，刻意不设锁——图到达与超时回调对同一快照的写都是"幂等填值"，
        ///  由 _finished 双收尾保护兜底，最坏情况只是日志里少一条。）
        /// </summary>
        private class PendingCamera
        {
            public int CameraIndex;    // 相机在配置里的下标（0 起）
            public bool TriggerOk;     // 触发是否成功（成功才等这张图）
            public bool IsOk;          // IV4 判定结论（触发失败时无意义）
            public string ResultText;  // 8 位判定文本
            public string FtpPath;     // FTP 新图完整路径（到图后填）
            public bool IsSnapped;     // 是否已等到 FTP 图
            public string FailReason;  // 触发失败/等图超时原因
        }
    }
}