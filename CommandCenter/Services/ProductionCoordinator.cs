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
    /// 生产流程协调器：把 PLC 到位信号、扫码得 SN、多台相机触发、图像监听、结果上报串成循环。
    ///
    /// 【V1.12.16 两阶段流程（与现场需求一致）】完整产线节奏是"先扫码、后拍照"：
    ///   ① 空闲期后台轮询"扫码枪到位"(D99，PLC 主站写 1)；
    ///   ② 读到扫码到位→清复位→触发扫码枪读码（有扫码枪时 SendTrigger + 等 SerialNumberScanned
    ///      事件到位；无扫码枪时 SN 走手动输入，扫码到位即通过）→ 进入等相机阶段；
    ///   ③ 空闲期后台轮询"相机到位"(D100)；
    ///   ④ 读到相机到位→清复位→【对所有已配置相机并行触发（V1.8.3 起）】；
    ///   ⑤ 每台相机独立：IV4 指令 T2 直接回 OK/NG（未开启时退化为"图到即 OK"），记各自判定；
    ///   ⑥ 取图（V1.7.0 每台相机按 ImageSource 二选一）：
    ///      - Ftp（默认）：等相机 FTP 新图上传（共用总超时 = 各相机 ImageWaitMs 的最大值）；
    ///      - Tcp：触发后立即发 BR 指令在同一连接上同步读回最新图像（免 FTP 落盘中转）；
    ///      每个点位各存各的图（目录按模板：年/月/日/SN/OK|NG，文件名按点位号）→ Done=1(完成)；
    ///      某相机图超时/触发或取图失败→该点位标失败，全部失败才 Done=2(取像异常)；
    ///   ⑦ 回到①（扫码阶段）循环。
    ///
    /// 【V1.12.11 角色反转】现场 PLC(汇川)做 Modbus 主站、上位机做从站。本类的 _plc 调用
    ///   全部保留原签名，底层已改为读写上位机自己 DataStore 寄存器区（不发起 Modbus 请求）：
    ///   "到位信号"由 PLC 主站写入上位机 D100/D99，本类轮询读自己 DataStore；完成/计数/配方
    ///   由本类写自己 DataStore，PLC 主站轮询来读。业务流程不变，只是数据来源/去向从远端
    ///   PLC 寄存器变成本地 DataStore。
    ///
    /// 【多相机】CameraConfig 配几台就触几台。一台"到位"= 一排点位一次检测，
    ///   每台相机的新图（各自 FTP 目录）到齐后才整体收尾；图以独立 WindowData 逐个抛给 UI
    ///   （每个点位一个 WindowData，刷新一个显示窗口）。
    ///
    /// 【线程】轮询、等待均在后台线程执行，通过事件把结果抛给 UI（由订阅方 Invoke 到界面线程）。
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

        // ── V1.12.16 两阶段流程状态（先扫码得 SN，再拍照）──
        // _phase 只在 PositionTimer 后台线程读写，但相机拍照收尾（FinishAll）在 FTP/超时线程
        // 会把它重置回"等扫码"阶段，因此声明为 volatile 保证跨线程可见、不读旧缓存值。
        private const int PhaseScanWait = 0;    // 空闲：等"扫码枪到位"信号（D99）
        private const int PhaseScanPending = 1; // 已扫到位、正在等 SN（SerialNumberScanned 事件）
        private const int PhaseCameraWait = 2;  // 空闲：等"相机到位"信号（D100）
        private volatile int _phase = PhaseScanWait;

        /// <summary>扫码等待 SN 的超时（毫秒）：扫码到位后产品迟迟没被扫到（没贴码/扫码枪没读到），
        /// 超时仍进入拍照阶段（SN 沿用当前 LatestSerialNumber），避免流程被卡死。</summary>
        private const int ScanWaitMs = 30000;

        private readonly List<IScanner> _scanners = new List<IScanner>(); // 扫码枪列表（由 MainForm.AttachScanners 注入）
        private volatile bool _serialReceived;  // 本次扫码到位后是否已收到 SN（SerialNumberScanned 置位）
        private bool _scanHooked;               // 是否已订阅扫码枪事件（防重复订阅）
        private DateTime _scanArriveUtc;        // 扫码到位时间戳（判断 SN 等待是否超时）
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

        /// <summary>
        /// 手动输入/更新当前产品序列号（V1.12.17，UI 线程调用）。
        /// 场景：没有扫码枪、或扫码枪没读到码时，操作员双击主界面标题栏"序列号"框手动录入 SN。
        /// 与扫码枪收码（OnScannerCode）等效推进两阶段流程：
        ///   ① 更新 LatestSerialNumber（标题栏显示 + 存图 {SN} 目录）；
        ///   ② 置 _serialReceived=true：若正处于"等 SN"阶段（PhaseScanPending，扫码枪有但没读到码），
        ///      下一轮轮询即视为"已取得 SN"进入等相机阶段；若在其它阶段（空闲等扫码/等相机），
        ///      该标志会在下次扫码到位时被 _serialReceived=false 重置，无副作用。
        /// 线程：UI 线程写 string（引用赋值原子）+ volatile bool，后台轮询线程读到即推进，无需加锁。
        /// </summary>
        /// <param name="code">手动录入的序列号（调用方保证非空；这里仍做一次空兜底）</param>
        public void SetManualSerial(string code)
        {
            LatestSerialNumber = code ?? "";
            _serialReceived = true;
            LogHelper.Info("手动输入序列号：" + LatestSerialNumber);
        }

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
        /// 开始运行：给每台相机注册 FTP 监听 + 订阅扫码枪事件 + 启动 PLC 到位轮询。
        /// </summary>
        public void Start()
        {
            _running = true;
            _imageStore.FtpFileArrived += OnFtpFileArrived;
            HookScannerEvents(); // 订阅扫码枪 SerialNumberScanned（记"SN 已到"标志，供两阶段推进）
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
            SetState("等待 PLC 扫码枪到位信号");
        }

        /// <summary>
        /// 注入扫码枪列表（V1.12.16，MainForm 在 BuildServices 里创建完扫码枪后调用）。
        /// 只在 Start 前调用一次；热更时新协调器会注入新列表、旧协调器 Dispose 已退订旧事件。
        /// 若本协调器之前已订阅过旧列表（热更复用实例场景），先退订再换新列表，防事件叠加。
        /// </summary>
        public void AttachScanners(IEnumerable<IScanner> scanners)
        {
            UnhookScannerEvents(); // 先退订旧列表事件，防重复订阅
            _scanners.Clear();
            if (scanners != null)
                _scanners.AddRange(scanners);
        }

        /// <summary>订阅每台扫码枪的 SerialNumberScanned：只记"SN 已到"标志（最新值由 UI 订阅方维护）。</summary>
        private void HookScannerEvents()
        {
            if (_scanHooked) return;
            _scanHooked = true;
            foreach (var sc in _scanners)
                sc.SerialNumberScanned += OnScannerCode;
        }

        /// <summary>退订扫码枪事件（Dispose 或换列表时调用），防热更/关闭时事件叠加或悬挂。</summary>
        private void UnhookScannerEvents()
        {
            if (!_scanHooked) return;
            _scanHooked = false;
            foreach (var sc in _scanners)
                sc.SerialNumberScanned -= OnScannerCode;
        }

        /// <summary>扫码枪读码事件（工作线程）：置"本次 SN 已到"标志，两阶段状态机据此推进等相机阶段。
        /// 只置标志、不在此更新 LatestSerialNumber（文本由 MainForm.OnSerialScanned 统一维护）。</summary>
        private void OnScannerCode(object sender, string code)
        {
            _serialReceived = true;
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
                SetState(_phase == PhaseCameraWait ? "等待 PLC 相机到位信号" : "等待 PLC 扫码枪到位信号");
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

            // ── V1.12.16 两阶段：先扫码得 SN，再相机拍照 ──
            // 阶段① 等"扫码枪到位"(D99)：读到→触发扫码→复位信号→等 SN
            if (_phase == PhaseScanWait)
            {
                if (_plc.ReadScanMoveDone())
                {
                    _plc.ClearScanMoveDone();               // 复位握手，防反复触发
                    _serialReceived = false;
                    _scanArriveUtc = DateTime.UtcNow;
                    if (_scanners.Count > 0)
                    {
                        // 有扫码枪：发触发指令开始读码（TCP 场景重发 LON 开激光；串口为空操作），
                        // 下一步轮询等 SerialNumberScanned 事件置 _serialReceived。
                        foreach (var sc in _scanners)
                        {
                            try { sc.SendTrigger(); }
                            catch (Exception ex) { LogHelper.Warn("扫码枪触发异常：" + ex.Message); }
                        }
                        _phase = PhaseScanPending;
                        SetState("扫码枪到位，等待扫码...");
                    }
                    else
                    {
                        // 无扫码枪：SN 走手动输入/模拟，扫码到位即视为通过，直接等相机到位
                        _phase = PhaseCameraWait;
                        SetState("等待 PLC 相机到位信号");
                    }
                }
                return;
            }

            // 阶段② 等 SN：扫到即进入等相机阶段；超时兜底（产品没贴码/扫码枪没读到）不卡流程
            if (_phase == PhaseScanPending)
            {
                if (_serialReceived)
                {
                    _phase = PhaseCameraWait;
                    SetState("扫码完成，等待 PLC 相机到位信号");
                }
                else if ((DateTime.UtcNow - _scanArriveUtc).TotalMilliseconds >= ScanWaitMs)
                {
                    LogHelper.Warn("扫码等待 SN 超时（" + ScanWaitMs + "ms），继续拍照（SN 沿用当前值）");
                    ErrorRaised?.Invoke("扫码等待 SN 超时：未取得序列号，继续拍照（SN 沿用当前值）");
                    _phase = PhaseCameraWait;
                    SetState("等待 PLC 相机到位信号");
                }
                return;
            }

            // 阶段③ 等"相机到位"(D100)：读到→并行触发拍照（原主流程逻辑保留）
            if (_phase == PhaseCameraWait)
            {
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
                LogHelper.Error("相机到位处理异常", ex);
                Interlocked.Exchange(ref _busy, 0);
                SetState("等待 PLC 相机到位信号");
            }
            } // 关闭 if (_phase == PhaseCameraWait)
        }

        /// <summary>
        /// 触发并取图单台相机（V1.8.3 起由并行任务调用）。
        /// 只写自己的 _pends[idx] 快照，与其它相机任务互不干扰；内部自带 try-catch，
        /// 任何异常都收敛为该相机的 FailReason，绝不把异常抛回 WaitAll。
        /// 【V1.12.18 先切程序再触发】触发前若配置了程序先发 PW 切换相机程序，再发 T2 触发+读判定；
        ///   若配置了 OutputFormat 则先发 OF 固化判定输出格式（联调对齐用）。
        /// 【V1.12.25 点位级切程序（替代固定 ProgramNo）】现场"28 个窗口点位由两台相机分工拍摄"：
        ///   每台相机有自己的"点位→程序号"映射表（CameraConfig.StationPrograms，设置页编辑）。
        ///   触发前先算"本轮本相机要填的窗口"（FinishAll 里窗口按相机下标顺序环形分配：
        ///   相机 idx → _nextWindowIndex + idx），据此解析出点位号，再查本相机映射表：
        ///   命中→先 PW 切到该点位对应程序再触发；未命中→该点位不归本相机拍（或还没配映射），
        ///   不切换、保持相机当前程序（与旧固定 ProgramNo 的"一刀切"不同，避免误切到别的点位程序）。
        /// </summary>
        /// <param name="idx">相机在配置里的下标（0 起）</param>
        private void TriggerOneCamera(int idx)
        {
            var cfg = _cameraCfgs[idx];
            var p = _pends[idx];
            try
            {
                // 触发前的相机程序切换 + 输出格式设置（V1.12.18）：
                // 这些指令彼此独立，任何一步失败都收敛为 FailReason 报错、不继续触发
                // （程序没切对就触发，判定/取图会对应到错误点位，宁可不拍）。
                if (!string.IsNullOrWhiteSpace(cfg.OutputFormat)
                    && !_cameras[idx].SetOutputFormat(cfg.OutputFormat))
                {
                    p.TriggerOk = false;
                    p.FailReason = $"相机[{idx}]设置判定输出格式失败（OF,{cfg.OutputFormat.Trim()}）";
                    return;
                }
                // V1.12.25：本轮本相机要填的窗口（FinishAll 窗口按相机下标顺序环形分配）→ 点位 → 程序号
                int stationNo = ResolveStation((( _nextWindowIndex - 1 + idx) % _windowCount) + 1);
                int programNo = ResolveProgramForStation(cfg, stationNo);
                if (programNo >= 0
                    && !_cameras[idx].SwitchProgram(programNo))
                {
                    p.TriggerOk = false;
                    p.FailReason = $"相机[{idx}]切换程序失败（点位{stationNo}→PW,{programNo:D3}）";
                    return;
                }

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
        /// 某台相机 FTP 新文件到达。参数带相机索引，定位到对应 pending 快照填图。
        /// 【V1.12.18 双文件约定】现场相机每次拍照往 FTP 取图目录推两个文件：
        ///   jpeg（显示/归档主体）+ iv4p（基恩士复盘私有格式，原样归档）。
        ///   文件名不写死（V1.12.24 起：可能是 0000，也可能是 0084 等任意编号），
        ///   按扩展名分派：jpeg 填 FtpJpegPath、iv4p 填 FtpIvpPath，两个都到齐才算 IsSnapped。
        /// （FileSystemWatcher 对两个文件各触发一次 Created/Renamed，到达顺序不保证。）
        /// 事件来自 FileSystemWatcher 线程；_finished 保护保证不会与超时回调重复收尾。
        /// 【V1.12.24 放错机制】本回调只负责"图到了→提前收尾"的信号加速；归档取图时
        ///   FinishAll 会【重新扫目录取最新文件】，事件漏报也不会丢图（见 TryResolveFtpSources）。
        /// </summary>
        private void OnFtpFileArrived(int cameraIndex, string fullPath)
        {
            if (_disposed || _busy == 0) return; // 已释放/非流程内到达的图忽略（相机制试图之类）
            var p = _pends.FirstOrDefault(x => x.CameraIndex == cameraIndex);
            if (p == null || !p.TriggerOk || p.IsSnapped) return; // 无关相机/触发失败/已到齐过都忽略

            // 按扩展名分派：.iv4p 进 iv4p 槽，其余（.jpeg/.jpg/.png…）一律当显示主体 jpeg
            string ext = Path.GetExtension(fullPath ?? "");
            if (!string.IsNullOrEmpty(ext) && ext.Equals(".iv4p", StringComparison.OrdinalIgnoreCase))
                p.FtpIvpPath = fullPath;
            else
                p.FtpJpegPath = fullPath;

            // 双文件都到齐才视为"图已到手"（正常必有；若 iv4p 偶发缺失走超时兜底，见 FinishAll）
            if (string.IsNullOrEmpty(p.FtpJpegPath) || string.IsNullOrEmpty(p.FtpIvpPath))
                return;
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

                    // 图是否到手：TCP 模式看 ImageBytes；FTP 模式看"目录扫描/事件记录能否找到 jpeg"。
                    // V1.12.24 放错机制：FTP 取图一律【扫相机取图目录取修改时间最新的 jpeg+iv4p】，
                    // 不再预设文件名（相机可能推 0084.jpeg 之类任意编号），FileSystemWatcher 事件
                    // 漏报/错过也能兜底取到；事件记录的路径仅作目录扫描失败时的回退。
                    var cfg = _cameraCfgs != null && p.CameraIndex >= 0 && p.CameraIndex < _cameraCfgs.Count
                        ? _cameraCfgs[p.CameraIndex]
                        : null;
                    bool isFtp = cfg == null || !IsTcpImage(cfg); // 配置缺失时按 FTP 兜底（默认取图方式）
                    string jpegSource = null, iv4pSource = null;
                    bool ftpResolved = isFtp && TryResolveFtpSources(p, cfg, out jpegSource, out iv4pSource);

                    bool hasImage = p.TriggerOk
                        && (p.ImageBytes != null || ftpResolved);
                    // 触发成功却没图：补齐失败原因（超时未到 / 目录里真没有图），供下方报错提示
                    if (!hasImage && p.TriggerOk && string.IsNullOrEmpty(p.FailReason))
                        p.FailReason = isFtp ? "相机 FTP 取图目录未找到 jpeg（事件与目录扫描均无）" : "相机无图像数据";

                    string archived = null;
                    if (hasImage)
                    {
                        // 归档：TCP 模式图在内存字节里，直接解码转存正式目录（不落 FTP 中转文件）；
                        //       FTP 模式把 jpeg+iv4p 双文件原样复制到正式目录（V1.12.18）。
                        archived = (p.ImageBytes != null)
                            ? _imageStore.SaveImageBytes(p.ImageBytes, stationNo, p.IsOk, LatestSerialNumber)
                            : _imageStore.SaveImageFilePair(jpegSource, iv4pSource, stationNo, p.IsOk, LatestSerialNumber);
                        if (archived == null)
                        {
                            // 归档失败兜底：FTP 模式回退用源文件当结果（图至少能显示）；
                            // TCP 模式无源文件，明确报错让该窗口走失败占位（等现场实测确认格式后自然消除）。
                            if (!string.IsNullOrEmpty(jpegSource))
                                archived = jpegSource;
                            else
                                ErrorRaised?.Invoke($"点位{stationNo} 图像归档失败（TCP 取图，内存解码失败）");
                        }
                        else if (p.ImageBytes == null)
                        {
                            // FTP 模式：归档成功 → 删除 FTP 取图目录源文件（中转暂存区"处理即删"）。
                            // 删除的是【实际归档的那对】（目录扫描结果），不是事件记录路径——两者可能
                            // 不同（事件漏报时扫描结果才是本轮真图）；失败只记日志不阻断。删除必须放在
                            // 归档成功之后，否则复制失败会把图弄丢。
                            DeleteFtpSource(jpegSource, stationNo);
                            DeleteFtpSource(iv4pSource, stationNo);
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
                _phase = PhaseScanWait; // 相机阶段收尾完成 → 回到等"扫码到位"阶段，开始下一循环
                SetState("等待 PLC 扫码枪到位信号");
            }
        }

        /// <summary>
        /// 解析 FTP 模式本次归档用的源文件对（V1.12.24 放错机制，增强取图鲁棒性）。
        /// ① 优先扫描该相机 FTP 取图目录，取"修改时间最新"的 jpeg + iv4p——【不写死文件名】，
        ///    相机把图推成 0084.jpeg/0084.iv4p 等任意编号都能取到；同时 FileSystemWatcher
        ///    事件漏报/错过时也靠它兜底拿到图；
        /// ② 目录扫描取不到 jpeg（目录不存在 / FTP 网盘未挂载 / 目录真是空的）时，回退用
        ///    事件记录的路径（OnFtpFileArrived 抓到的 p.FtpJpegPath / p.FtpIvpPath）。
        /// </summary>
        /// <param name="p">相机 pending 快照（含事件记录的路径，可能为 null）</param>
        /// <param name="cfg">相机配置（决定 FTP 目录；为空则用全局兜底目录）</param>
        /// <param name="jpeg">解析出的 jpeg 源文件完整路径（归档主体）</param>
        /// <param name="iv4p">解析出的 iv4p 源文件完整路径（可为 null=目录里没有 iv4p）</param>
        /// <returns>true=拿到 jpeg（可归档）；false=扫描与事件都拿不到图</returns>
        private bool TryResolveFtpSources(PendingCamera p, CameraConfig cfg,
                                          out string jpeg, out string iv4p)
        {
            jpeg = null;
            iv4p = null;
            try
            {
                var latest = _imageStore.FindLatestPair(FtpDirFor(cfg));
                jpeg = latest.JpegPath;
                iv4p = latest.IvpPath;
                if (string.IsNullOrEmpty(jpeg)
                    && p != null && !string.IsNullOrEmpty(p.FtpJpegPath) && File.Exists(p.FtpJpegPath))
                {
                    // 目录扫描无 jpeg：回退事件记录路径兜底（目录不存在/空/网盘未挂载等场景）
                    jpeg = p.FtpJpegPath;
                    iv4p = p.FtpIvpPath;
                }
                return !string.IsNullOrEmpty(jpeg);
            }
            catch (Exception ex)
            {
                LogHelper.Error("解析 FTP 取图源文件异常", ex);
                return false;
            }
        }

        /// <summary>某相机的 FTP 取图目录：优先相机配置 FtpUploadDir，为空回退全局兜底（与 Start 里注册监听一致）。</summary>
        private string FtpDirFor(CameraConfig cfg)
        {
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.FtpUploadDir))
                return cfg.FtpUploadDir;
            return _imageStore.DefaultFtpDir;
        }

        /// <summary>
        /// 删除 FTP 取图目录里的单个源文件（V1.12.18，"中转暂存区处理即删"）。
        /// 文件不存在/删除失败一律静默记日志、不抛异常、不阻断收尾：
        ///   - 不存在：本来就已删（重复删除场景），正常；
        ///   - 被占用删除失败：下一轮拍照同名覆盖时会自然复用目录，多留一个文件无害，
        ///     但会影响"文件名恒定 0000"的假设，故仍记日志供现场排查。
        /// 必须在归档复制成功之后调用（调用方保证），否则复制失败会把图弄丢。
        /// </summary>
        private static void DeleteFtpSource(string path, int stationNo)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    LogHelper.Info($"点位{stationNo} 已删除 FTP 取图源文件：{path}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warn($"点位{stationNo} 删除 FTP 源文件失败（不影响本次结果）：{path} → {ex.Message}");
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
        /// 查某相机"点位→程序号"映射表（V1.12.25）：命中返回该点位对应的程序号；未命中返回 -1（=不切换）。
        /// 【语义】StationPrograms 表里配了哪些点位，就是这台相机负责拍哪些点位；没配的点位
        /// 表示"不归本相机拍 或 映射还没配"，触发时保持相机当前程序（不发 PW），
        /// 由 PLC"到位"时序保证只有负责该点位的相机真正触发拍照。
        /// 表内条目按 StationNo 精确匹配；同一相机不允许重复点位（设置页已做去重，这里再兜一层）。
        /// </summary>
        /// <param name="cfg">相机配置（含本相机的点位→程序号映射表）</param>
        /// <param name="stationNo">本次要拍的点位号（已按窗口映射解析好）</param>
        /// <returns>程序号（0~127，0 合法）；未命中返回 -1</returns>
        private int ResolveProgramForStation(CameraConfig cfg, int stationNo)
        {
            if (cfg == null || cfg.StationPrograms == null) return -1;
            foreach (var item in cfg.StationPrograms)
            {
                if (item != null && item.StationNo == stationNo && item.ProgramNo >= 0)
                    return item.ProgramNo;
            }
            return -1;
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
            UnhookScannerEvents(); // 退订扫码枪事件，防热更/关闭后悬挂或叠加
            _positionTimer?.Dispose();
            _imageWaitTimer?.Dispose();
            _imageStore.FtpFileArrived -= OnFtpFileArrived;
            _imageStore.Dispose();
        }

        /// <summary>
        /// 一台相机一次检测的暂存快照：判定、图像（FTP 双文件或 TCP 字节）、失败原因等，
        /// 在触发到收尾之间跨线程读取。
        /// （只被协调器内部使用，刻意不设锁——图到达与超时回调对同一快照的写都是"幂等填值"，
        ///  由 _finished 双收尾保护兜底，最坏情况只是日志里少一条。
        ///  IsSnapped 声明为 volatile：OnFtpFileArrived（FTP 线程）写、FinishAll（超时/收尾线程）
        ///  读，volatile 保证该标志的读一定看到最新的写，避免极小窗口读到旧值误判"图未到"。）
        /// 【V1.12.18 双文件约定】FTP 模式每张照片对应 jpeg + iv4p 两个文件（现场与基恩士确认，
        ///   V1.12.24 起文件名不写死 0000，相机可能推任意编号）：
        ///   FtpJpegPath=显示/归档主体（事件抓到的 jpeg），FtpIvpPath=基恩士复盘私有格式（事件抓到的 iv4p），
        ///   归档时 FinishAll 优先【重扫目录取修改时间最新的一对】做源文件，事件路径仅兜底。
        /// </summary>
        private class PendingCamera
        {
            public int CameraIndex;    // 相机在配置里的下标（0 起）
            public string ImageSource; // 本相机取图模式："Ftp"（等 FTP 推图）/"Tcp"（BR 同步读图）
            public bool TriggerOk;     // 触发是否成功（Ftp 模式：成功才等这张图；Tcp 模式：取图失败也会被置 false）
            public bool IsOk;          // IV4 判定结论（触发失败时无意义）
            public string ResultText;  // 8 位判定文本
            public string FtpJpegPath; // Ftp 模式：FTP 取图目录里的 jpeg 完整路径（到图后填，显示/归档主体）
            public string FtpIvpPath;  // Ftp 模式：FTP 取图目录里的 iv4p 完整路径（基恩士复盘用，原样归档）
            public byte[] ImageBytes;  // Tcp 模式：BR 读回的最新图像字节（24bit BMP，无则 null）
            public volatile bool IsSnapped; // 是否已拿到图（Ftp 模式=jpeg 新图到；Tcp 模式=BR 读回）。volatile 见类注释
            public string FailReason;  // 触发失败/取图失败/等图超时原因
        }
    }
}