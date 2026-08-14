using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Services
{
    /// <summary>
    /// 生产流程协调器：把 PLC 请求（扫码/上相机/下相机）、扫码得 SN、相机触发取图、结果上报串成循环。
    ///
///     【V2.7 协议（docs/CommandCenter.md §5.5）：请求-结果-复位三拍式握手】
    /// 现场 PLC(汇川)做 Modbus TCP 主站、上位机做从站。PLC 分阶段发请求，上位机逐通道处理：
    ///   ┌─ 通道① 扫码（40001 请求 → 40004 结果 + 40007~40011 型号）─────────────┐
    ///   │ PLC 写 40001=1 → 上位机触发扫码枪/等手动 SN → 写型号 + 写结果(1=OK/2=NG) │
    ///   │ → PLC 读到结果后写 40001=0 → 上位机看到 40001 归 0 → 写 40004=0 复位     │
    ///   └───────────────────────────────────────────────────────────────────────┘
    ///   ┌─ 相机通道（V2.12.6 起【每台相机一路】，见相机表 PlcRequestAddress/PlcResultAddress）─┐
    ///   │ 第1台 40002 请求→40005 结果；第2台 40003→40006；第3台起 40008…/40012…（显式配置后）│
    ///   │ PLC 写请求=点位 → 上位机触发该相机拍点位→归档→显示→写结果(1/2/3)      │
    ///   │ → PLC 读到结果写请求=0 → 上位机看到归 0 → 写结果=0 复位（三拍同扫码）  │
    ///   └───────────────────────────────────────────────────────────────────────┘
    ///   结果值：0=默认/复位，1=OK，2=NG，3=点位禁用跳过（V1.12.28：禁用点位不拍照、
    ///   不显示、不计数，直接回 3 让 PLC 走下一工位）。
    ///   所有通道互斥串行（一次只处理一个，_activeCh），符合 PLC 串行时序；
    ///   请求只有被"认领"的通道处理，其余通道的请求在活动通道完成前不抢占。
    ///
    ///     【相机通道 ↔ 相机（V2.13.4 定稿，取代 V2.12.6 的"通道号=下标+1"）】：每个相机通道的
    ///     PLC 请求/结果地址显式配在该相机自己的配置里（PlcRequestAddress/PlcResultAddress，不再按
    ///     列表序号自动推导），_activeCh 直接存相机 ID（CameraId，上=2/下=1）；无相机ID的旧配置
    ///     回退"列表位置+1"保证唯一。曾把"协议通道号 1/2"当相机下标用导致错位/越界（V2.12.5 已修、
    ///     V2.12.6 根治、V2.13.4 再彻底改为相机ID，列表顺序从此与 PLC 通道无关）。
    ///
    /// 【点位与窗口】PLC 请求里带点位编号，该点位是【相机局部点位号】
    ///   （相机各自从 1 起、会重复），上位机按"该相机 ID + 这台相机的点位表"定位窗口
    ///   （见 TryResolveActiveWindow，窗口=相机点位表条目，前上相机后下相机分组）；
    ///   该窗口被禁用（WindowEnabled=false）时视为"该点位跳过"，直接写结果 3。
    ///   存图点位 = 相机点位号（文件名 {点位}），按相机的 {相机} 目录层隔离（见 ImageStore）。
    ///
    /// 【线程】请求轮询在后台线程（PositionTimer），相机触发/取图/归档在 Task 后台线程。
    ///   V2.13.7 起相机结果"判定即写"：Task 里判定（T2）一返回就立即 WriteCameraResult 落 PLC 结果
    ///   寄存器，不再等取图/归档；取图/归档/显示成为纯异步补充材料（图缺失只影响显示/存图，
    ///   不回退结果）。_chanResult（volatile）仍由 Task 回传、轮询线程兜底再写一次（幂等），
    ///   并新增 _taskDone 标记控制通道释放（见 StepCameraChannel）。界面刷新走事件
    ///   （InspectionFinished 由订阅方 Invoke 回 UI 线程）。本类不接触任何控件，纯业务编排。
    /// </summary>
    public class ProductionCoordinator : IDisposable
    {
        private readonly PlcService _plc;
        private readonly List<KeyenceIV4Camera> _cameras;   // 每台相机一个服务实例
        private readonly List<CameraConfig> _cameraCfgs;    // 对应的相机配置（程序映射/FTP目录等）
        private readonly ImageStore _imageStore;
        private readonly List<bool> _windowEnabled;         // 窗口→是否启用（V1.12.28）
        private readonly string _productModel;              // 固定产品型号（V2.7，每次扫码写入 PLC）
        /// <summary>窗口↔点位独立映射（V2.13，当前型号解析结果）：Points[i] = 窗口 i+1 对应的
        /// (相机ID CameraId, 点位号)。PLC 请求点位据此反查唯一窗口（见 TryResolveActiveWindow）。</summary>
        private readonly List<WindowPointItem> _windowPointMap;

        private readonly System.Threading.Timer _positionTimer;  // 请求轮询（后台线程）
        private volatile bool _running;   // 总开关
        private volatile bool _disposed;  // 已释放标记
        private int _seqNo;               // 全局检测序号（非线程敏感，轮询线程自增）

        // ── V2.7 三通道状态机 ──
        // 所有状态字段只在"轮询线程"修改；相机拍照 Task 只写 _chanResult（volatile），
        // 轮询线程读取后落 PLC 结果寄存器，因此 _chStep/_activeCh 无需加锁。
        private const int ChNone = -1;    // 无活动通道（空闲，等新请求）
        private const int ChScan = 0;     // 通道① 扫码（40001/40004）
        // ★ 相机通道（V2.13.4 起 _activeCh 直接存【相机ID CameraId】，上=2/下=1；见类头注释）。
        //   曾存"相机下标+1"（V2.12.6），点位反查再按 camIdx-1 换算——列表顺序绑定通道；
        //   V2.13.4 彻底改为相机ID：PLC 地址由相机配置显式给出，列表顺序自由。
        private volatile int _activeCh = ChNone; // 当前活动通道（ChScan=扫码、>0=相机ID）
        private volatile int _chStep;     // 通道内步骤：见各通道推进逻辑
        private volatile int _chanResult = -1;   // 相机拍照结果：-1=未出，1=OK，2=NG，3=跳过
        // V2.13.7：相机拍照 Task 是否【完全】结束（判定+取图+归档+显示都做完）。
        // 判定即写后结果提前落 PLC、通道可进"等复位"，但【通道释放】必须等 Task 收尾，
        // 防止"上一拍还在归档、下一拍请求已进来"导致同相机并发取图/混图（见 StepCameraChannel）。
        private volatile bool _taskDone;
        private int _pendStation;         // 相机通道当前点位（轮询线程读写）

        // ── 扫码通道 ──
        private readonly List<IScanner> _scanners = new List<IScanner>();
        private volatile bool _serialReceived;  // 是否已收到 SN（扫码枪事件/手动输入置位）。
        //   V2.14.9 语义调整："已收到但尚未消费本轮"的码。扫码枪持续读码时码可能先于
        //   PLC 扫码请求到达，务必保留到本轮消费（BeginScanChannel 不再无条件清零）；
        //   消费后（StepScanChannel 写扫码 OK）即清，防残留污染下一轮。
        //   V2.14.9 加"码时间窗"：收到码时记录到达时刻 _serialArrivedUtc，BeginScanChannel 判断
        //   该码是否在"请求到达前 ScanCodeKeepMs 内"扫到——是→本件提前扫到，保留直接消费；
        //   否→上一件残留（重复扫/产品未离开/旧手动输入），丢弃重新等新码，杜绝串号/误 OK。
        private DateTime _serialArrivedUtc;     // 最近一次收到码/手动输入的时刻（码时间窗判断用；
                                                //   扫码枪工作线程写、轮询线程读，毫秒级偏差可接受）
        private bool _scanHooked;               // 是否已订阅扫码枪事件（防重复订阅）
        private DateTime _scanArriveUtc;        // 扫码请求受理时刻（判断 SN 等待超时）

        // ── FTP 取图信号加速（V2.13.6 恢复事件驱动）──
        // ImageStore 为每台相机 FTP 目录挂 FileSystemWatcher（MainForm.BuildServices 里 AddMonitor 启动），
        // 新图到达触发 FtpFileArrived → 本类置位该相机的信号 → WaitForFtpImage 立即醒来重扫目录
        // （消除了纯轮询最长 200ms 的被动延迟）；事件漏报/失效时 200ms 超时轮询照常兜底，两者互补。
        // 每拍（WaitForFtpImage）开始前 Reset 一次，保证"本次触发后的新图事件"才唤醒本拍。
        private ManualResetEventSlim[] _ftpArrive = new ManualResetEventSlim[0];
        private bool _ftpHooked;                // 是否已订阅 FtpFileArrived（防重复订阅）

        /// <summary>扫码等待 SN 的超时（毫秒）：扫码请求到位后产品迟迟没被扫到（没贴码/扫码枪没读到），
        /// 超时写结果 2（扫码 NG）上报 PLC，避免流程卡死。</summary>
        private const int ScanWaitMs = 30000;

        /// <summary>扫码码时间窗（毫秒，V2.14.9）：PLC 扫码请求到达前，该窗口内扫到的码视为
        /// "本件提前扫到"予以保留（产品在枪前扫过、请求后到位）；窗口之外更早到达的码一律视为
        /// 上一件残留（同件重复扫/产品未走开/操作员提前输的旧 SN）直接丢弃，防止串号、防止误 OK。
        /// 取值权衡：过小会把"枪离到位远、间隔大"的本件码也当残留丢掉（复现 NG）；过大又防不住
        /// 残留。现场若"过枪→PLC 请求"间隔明显大于 2s，请调大此值并同步注释。</summary>
        private const int ScanCodeKeepMs = 2000;

        /// <summary>到位轮询周期（毫秒）：连上 PLC 时用</summary>
        private const int PollMs = 200;

        /// <summary>连接失败后的重试用期（毫秒）：放慢节奏，避免高频无效尝试刷爆日志</summary>
        private const int SlowPollMs = 1000;

        /// <summary>检测完成事件：携带一次结果（含图片路径、OK/NG、序号、点位号）。每张图各抛一次。</summary>
        public event Action<WindowData, int> InspectionFinished;

        /// <summary>检测流程异常提醒（参数为提示文本）</summary>
        public event Action<string> ErrorRaised;

        /// <summary>流程状态文本（空闲/扫码/拍照中），UI 可显示</summary>
        public event Action<string> StateChanged;

        /// <summary>一条产品被扫码进来的序列号透传（若扫码枪关闭则 UI 手动输入）</summary>
        public string LatestSerialNumber { get; set; } = "";

        /// <summary>
        /// 手动输入/更新当前产品序列号（V1.12.17，UI 线程调用）。
        /// 与扫码枪收码等效：置 _serialReceived=true，处于"扫码等 SN"通道时下一拍即完成扫码。
        /// 【V2.14.9】同步记录到达时刻 _serialArrivedUtc，纳入码时间窗判定（否则手动输入的码
        /// 会被当成"旧残留"在 BeginScanChannel 丢弃）。
        /// </summary>
        public void SetManualSerial(string code)
        {
            LatestSerialNumber = code ?? "";
            _serialReceived = true;
            _serialArrivedUtc = DateTime.UtcNow;
            LogHelper.Info("手动输入序列号：" + LatestSerialNumber);
        }

        public ProductionCoordinator(PlcService plc,
                                     List<KeyenceIV4Camera> cameras,
                                     List<CameraConfig> cameraCfgs,
                                     ImageStore imageStore,
                                     List<bool> windowEnabled,
                                     string productModel,
                                     List<ModelWindowPointMap> windowPointMaps)
        {
            _plc = plc;
            _cameras = cameras ?? new List<KeyenceIV4Camera>();
            _cameraCfgs = cameraCfgs ?? new List<CameraConfig>();
            _imageStore = imageStore;
            _windowEnabled = windowEnabled;
            _productModel = productModel ?? "";
            // V2.13：窗口↔点位独立映射（按型号分表）。解析当前型号的映射（缺表/长度不对回退
            // 默认铺排），PLC 请求点位据此反查唯一窗口（见 TryResolveActiveWindow）。
            _windowPointMap = Models.DisplayConfig.ResolveWindowPointMap(
                _cameraCfgs, _productModel, windowPointMaps);

            // V2.13.6：为每台相机准备一个"FTP 新图到达"信号（数组至少 1 个防越界），
            // 订阅 ImageStore 的新图事件——相机推图到目录的瞬间即可唤醒等图流程，不必等下一个轮询周期。
            int n = Math.Max(1, _cameraCfgs.Count);
            _ftpArrive = new ManualResetEventSlim[n];
            for (int i = 0; i < n; i++) _ftpArrive[i] = new ManualResetEventSlim(false);
            if (!_ftpHooked && _imageStore != null)
            {
                _imageStore.FtpFileArrived += OnFtpFileArrived;
                _ftpHooked = true;
            }

            // 请求轮询：后台线程 200ms 一问 PLC。
            // ★ 必须用 System.Threading.Timer：此前用 Forms.Timer 在 UI 线程同步读 PLC，
            //   不可达 IP 时把界面整个卡住（点"系统设置"半天没反应就是这原因）。
            _positionTimer = new System.Threading.Timer(
                PositionTimer_Tick, null,
                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// ImageStore 的 FTP 新图事件回调（V2.13.6 恢复信号加速）。运行在 FileSystemWatcher 监听线程，
        /// 只做"置位对应相机信号"这一件非阻塞的事——真正的取图/归档仍在等图的 Task 线程里做。
        /// <paramref name="cameraIndex"/> = 相机列表下标（AddMonitor 注册时相机的下标），与 _ftpArrive 对齐。
        /// </summary>
        private void OnFtpFileArrived(int cameraIndex, string path)
        {
            if (cameraIndex >= 0 && cameraIndex < _ftpArrive.Length)
                _ftpArrive[cameraIndex].Set();
        }

        /// <summary>开始运行：订阅扫码枪事件 + 启动 PLC 请求轮询。</summary>
        public void Start()
        {
            _running = true;
            HookScannerEvents();
            SafeChange(_positionTimer, 0, PollMs); // 立即首轮，之后每 200ms
            SetState("等待 PLC 请求");
        }

        /// <summary>
        /// 注入扫码枪列表（V1.12.16，MainForm 在 BuildServices 里创建完扫码枪后调用）。
        /// 只在 Start 前调用一次；热更时新协调器会注入新列表、旧协调器 Dispose 已退订旧事件。
        /// </summary>
        public void AttachScanners(IEnumerable<IScanner> scanners)
        {
            UnhookScannerEvents();
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

        /// <summary>扫码枪读码事件（工作线程）：置"本次 SN 已到"标志，扫码通道据此推进。
        /// 【V2.14.9】同步记录码到达时刻 _serialArrivedUtc——BeginScanChannel 靠它判断该码
        /// 是否在"请求前 ScanCodeKeepMs 窗口内"（本件提前扫到）还是"更早的残留"（丢弃防串号）。</summary>
        private void OnScannerCode(object sender, string code)
        {
            _serialReceived = true;
            _serialArrivedUtc = DateTime.UtcNow;
        }

        /// <summary>暂停流程（界面手动暂停时调用，停在空闲）。</summary>
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
                SetState("等待 PLC 请求");
            }
        }

        /// <summary>
        /// 请求轮询（后台线程）：空闲时轮询三个请求通道，非空闲时推进当前通道。
        /// PLC 从站未就绪时降频到 SlowPollMs 重试，就绪自动恢复 PollMs。
        /// </summary>
        private void PositionTimer_Tick(object state)
        {
            if (!_running || _disposed) return;

            if (!_plc.EnsureConnected())
            {
                SafeChange(_positionTimer, SlowPollMs, SlowPollMs);
                return;
            }
            SafeChange(_positionTimer, PollMs, PollMs);

            try
            {
                if (_activeCh == ChNone)
                    PollNewRequest();        // 空闲：看有没有新请求
                else if (_activeCh == ChScan)
                    StepScanChannel();       // 扫码通道推进
                else
                    StepCameraChannel();     // 上/下相机通道推进
            }
            catch (Exception ex)
            {
                LogHelper.Error("PLC 请求轮询异常", ex);
            }
        }

        /// <summary>空闲轮询：先看扫码请求，再按相机列表顺序轮询每台相机自己的通道请求（V2.12.6 每相机一路）。</summary>
        private void PollNewRequest()
        {
            bool ok;
            // 通道① 扫码请求（40001）
            ok = _plc.ReadScanRequest(out bool scanReq);
            if (ok && scanReq)
            {
                BeginScanChannel();
                return;
            }
            // 各相机通道（V2.12.6 每相机一路；V2.13.4 起地址全部显式配在相机表，未配置则恒无请求）。
            // 只认领第一张被触发的相机（互斥串行）。列表顺序仅决定轮询先后，不再决定通道地址。
            for (int i = 0; i < _cameraCfgs.Count; i++)
            {
                if (_cameraCfgs[i] == null) continue;   // 空安全：配置被手改成 null 元素时跳过
                ok = _plc.ReadCameraRequest(_cameraCfgs[i], out int stationNo);
                if (ok && stationNo > 0)
                {
                    BeginCameraChannel(CameraIdFor(_cameraCfgs[i], i), stationNo);
                    return;
                }
            }
        }

        // ════════════════ 通道① 扫码 ════════════════

        /// <summary>受理扫码请求：触发扫码枪，进入"等 SN"步骤。
        /// 【V2.14.9 时序修复 + 码时间窗】
        ///   ① 不再无条件清 _serialReceived——现场节奏可能是"枪先扫到码、PLC 后发请求"
        ///      （枪持续读码，产品在枪前晃一下就出了码，随后 PLC 请求才到位）。若在这里无条件
        ///      清零，会把这颗"已经到手的码"丢掉，之后 30s 等不到新码 → 误报扫码 NG(2)。
        ///   ② 但也不能照单全收：上一件消费后可能残留旧码（同件重复扫/产品未走开/操作员提前
        ///      输的旧 SN），直接保留会造成串号或误 OK。故引入**码时间窗**——只保留"请求到达前
        ///      ScanCodeKeepMs(2s) 内"扫到的码（视为本件提前扫到，下一步轮询立即消费出 OK）；
        ///      窗口之外更早的码一律丢弃，重新等本件新码。
        ///   ③ 真正的清零改到"码已消费后"（StepScanChannel OK 分支写结果 1 时），消费即清。</summary>
        private void BeginScanChannel()
        {
            _activeCh = ChScan;
            _chStep = 0;                    // 步骤0：等 SN
            _scanArriveUtc = DateTime.UtcNow;   // 超时基准：本轮扫码请求到达时刻（30s 判 NG 用）

            // 码时间窗过滤（V2.14.9）：已有码，但太旧（超过请求前 ScanCodeKeepMs）→ 当残留丢弃。
            if (_serialReceived &&
                (DateTime.UtcNow - _serialArrivedUtc).TotalMilliseconds > ScanCodeKeepMs)
            {
                LogHelper.Warn($"丢弃超过码时间窗({ScanCodeKeepMs}ms)的残留码（上一件残留/重复扫/旧手动输入），重新等待本件扫码");
                _serialReceived = false;
            }

            if (_scanners.Count > 0)
            {
                // 有扫码枪：发触发指令开始读码（TCP 场景重发 LON 开激光；串口为空操作），
                // 下一步轮询等 SerialNumberScanned 事件置 _serialReceived。
                foreach (var sc in _scanners)
                {
                    try { sc.SendTrigger(); }
                    catch (Exception ex) { LogHelper.Warn("扫码枪触发异常：" + ex.Message); }
                }
                SetState("PLC 请求扫码：等待 SN");
            }
            else
            {
                // 无扫码枪：SN 走手动输入（SetManualSerial），等待期间不报超时误伤
                SetState("PLC 请求扫码：等待手动输入 SN");
            }
            LogHelper.Info("收到 PLC 扫码请求（40001=1）");
        }

        /// <summary>扫码通道推进：等 SN → 写型号+结果 → 等 PLC 复位请求 → 复位结果。</summary>
        private void StepScanChannel()
        {
            if (_chStep == 0)
            {
                // 等 SN：扫到（扫码枪/手动输入）→ OK；超时 → NG 兜底不卡流程
                // 【V2.14.9 时序修复】码"先扫到、请求后到"也在第一步被消费：
                // BeginScanChannel 不再清零，若请求到达时 _serialReceived 已是 true
                // （请求前枪已扫到码），这里立即走 OK——不丢已到手的码。
                if (_serialReceived)
                {
                    _plc.WriteProductModel(_productModel);
                    _plc.WriteScanResult(1);      // 扫码 OK
                    _serialReceived = false;      // V2.14.9：码已消费即清零，防止残留 true 污染下一轮
                                                 // （否则下一轮 BeginScanChannel 不清，会把上一轮的码
                                                 //  当成新一轮的 SN 误报 OK——见 BeginScanChannel 注释）
                    _chStep = 1;
                    SetState("扫码完成，等待 PLC 复位请求");
                    LogHelper.Info($"扫码 OK：SN={LatestSerialNumber}，已上报型号[{_productModel}]与结果(1)");
                }
                else if ((DateTime.UtcNow - _scanArriveUtc).TotalMilliseconds >= ScanWaitMs)
                {
                    _plc.WriteProductModel(_productModel);
                    _plc.WriteScanResult(2);      // 扫码 NG（超时）
                    _chStep = 1;
                    LogHelper.Warn($"扫码等待 SN 超时（{ScanWaitMs}ms），上报扫码 NG(2)");
                    ErrorRaised?.Invoke("扫码等待 SN 超时：未取得序列号，已上报扫码 NG");
                    SetState("扫码超时上报 NG，等待 PLC 复位请求");
                }
                return;
            }

            // 步骤1：等 PLC 把请求 40001 复位为 0（说明已读走结果/型号）→ 复位结果 40004=0
            if (_plc.ReadScanRequest(out bool still) && !still)
            {
                _plc.WriteScanResult(0);
                _activeCh = ChNone;
                SetState("等待 PLC 请求");
                LogHelper.Info("PLC 已复位扫码请求，上位机复位扫码结果");
            }
        }

        // ════════════════ 相机通道 拍照（V2.12.6 起每台相机一路通道）════════════════

        /// <summary>
        /// 受理某台相机的拍照请求：解析点位→窗口，判断是否跳过（无相机/点位禁用）；
        /// 正常则启动后台 Task 触发拍照，轮询线程在 Task 出结果后写 PLC。
        /// </summary>
        /// <param name="cameraId">相机 ID（CameraConfig.CameraId，上=2/下=1；见 CameraIdFor）</param>
        /// <param name="stationNo">PLC 请求里的点位编号（1~255）</param>
        private void BeginCameraChannel(int cameraId, int stationNo)
        {
            // 按相机ID反查该相机在列表里的位置（取配置/服务实例）；找不到=该ID未配置相机 → 跳过
            int camIdx = IndexOfCamera(cameraId);
            if (camIdx < 0)
            {
                _activeCh = ChNone;
                LogHelper.Warn($"收到相机ID={cameraId} 的拍照请求，但配置里没有该相机，忽略");
                return;
            }

            // 跳过判定：请求点位无对应启用窗口（禁用/未配）或该相机不存在
            bool skip = !TryResolveActiveWindow(cameraId, stationNo, out int windowIndex);

            _activeCh = cameraId;   // 相机通道标识 = 相机ID（V2.13.4，见类头注释）
            _pendStation = stationNo;

            string camLabel = CameraLabel(camIdx);
            if (skip)
            {
                // 点位禁用/无相机：不拍照、不显示、不计数，直接写结果 3（跳过）告诉 PLC 走下一工位
                _chanResult = 3;
                _chStep = 1;
                _taskDone = true;   // 无 Task 后台工作，通道随时可复位（等 PLC 复位请求即可）
                LogHelper.Info($"点位{stationNo} 已禁用或无相机，上报跳过(3)（{camLabel}）");
                SetState($"点位{stationNo} 已禁用，跳过拍照");
                return;
            }

            _chanResult = -1;
            _chStep = 1;    // 步骤1：拍照进行中（判定即写，Task 里判定一出就落结果，不等图归档）
            _taskDone = false;  // V2.13.7：Task 结束（判定+取图+归档+显示全做完）才允许通道释放
            SetState($"点位{stationNo} 触发 {camLabel} 拍照");
            LogHelper.Info($"收到 PLC 拍照请求：{camLabel}，点位{stationNo}（窗口{windowIndex}）");

            // 触发+取图+归档+显示 全部在后台线程，完成后只回传 _chanResult 给轮询线程
            System.Threading.Tasks.Task.Run(() => DoCameraShot(camIdx, stationNo, windowIndex));
        }

        /// <summary>相机显示名（日志/状态用）：有名称显名称、无名称优先 CameraId 真编号、
        /// 其次"相机N"（V2.13.4）；index 越界兜底"相机N"。</summary>
        private string CameraLabel(int camIdx)
        {
            if (camIdx >= 0 && camIdx < _cameraCfgs.Count && _cameraCfgs[camIdx] != null)
            {
                var cfg = _cameraCfgs[camIdx];
                if (!string.IsNullOrWhiteSpace(cfg.Name)) return cfg.Name.Trim();
                if (cfg.CameraId > 0) return $"相机{cfg.CameraId}";
            }
            return $"相机{camIdx + 1}";
        }

        /// <summary>取某台相机的"身份ID"（V2.13.4 统一）：有 CameraId（>0）用真编号，
        /// 否则回退"列表位置+1"（旧配置/新相机未填编号时保证唯一可反查）。所有按相机定位
        /// 的键（_activeCh、窗口映射反查、编辑点位候选）都必须走这里，保证两端一致。</summary>
        private int CameraIdFor(CameraConfig cfg, int camIdx)
        {
            if (cfg != null && cfg.CameraId > 0) return cfg.CameraId;
            return camIdx + 1;
        }

        /// <summary>按相机ID反查相机在列表里的下标（0 起）；找不到返回 -1。
        /// 【V2.13.4】相机ID是身份键，列表顺序自由；PLC 请求/窗口映射都按相机ID定位相机，
        /// 只有"取配置/服务实例"才需要这个下标。</summary>
        private int IndexOfCamera(int cameraId)
        {
            if (cameraId <= 0 || _cameraCfgs == null) return -1;
            for (int i = 0; i < _cameraCfgs.Count; i++)
            {
                if (_cameraCfgs[i] != null && CameraIdFor(_cameraCfgs[i], i) == cameraId)
                    return i;
            }
            return -1;
        }

        /// <summary>相机通道推进：拍照完成出结果 → 写 PLC 结果 → 等 PLC 复位请求 → 复位结果。
        /// 三拍对结果 1/2/3 一视同仁（PLC 读到 3 也必须复位请求，否则通道永不释放，见 §5.3）。
        /// 【V2.13.4】_activeCh 存相机ID，先反查该相机在列表的位置（取配置/服务实例）。</summary>
        private void StepCameraChannel()
        {
            int camIdx = IndexOfCamera(_activeCh);   // 相机ID → 相机列表下标（_activeCh=相机ID）
            var cfg = (camIdx >= 0 && camIdx < _cameraCfgs.Count) ? _cameraCfgs[camIdx] : null;
            if (cfg == null)
            {
                // 相机ID对不上任何配置（配置被改/相机被删）：复位通道，避免卡死
                _activeCh = ChNone;
                _chStep = 0;
                _chanResult = -1;
                LogHelper.Warn($"相机ID={_activeCh} 找不到配置，相机通道已复位");
                return;
            }

            if (_chStep == 1)
            {
                // 步骤1：判定已出结果（1=OK / 2=NG / 3=跳过）→ 写本相机通道的结果寄存器。
                // 【V2.13.7 判定即写】Task 里判定（T2）一返回就 WriteCameraResult 立即落 PLC，
                // 不等取图/归档（图是异步补的）。这里由轮询线程兜底再写一次（幂等，值不变）：
                //   ① 万一 Task 提前写失败，这里补上；② skip 分支（_chanResult=3）没有 Task，
                //      只有这里能写。写完后进 step2 等 PLC 复位请求。
                if (_chanResult >= 0)
                {
                    int code = _chanResult;
                    _chanResult = -1;
                    _plc.WriteCameraResult(cfg, code);
                    _chStep = 2;
                    SetState($"点位{_pendStation} 已上报结果({code})，等待 PLC 复位请求");
                }
                return;
            }

            // 步骤2：等 PLC 把请求寄存器复位为 0 → 复位结果寄存器，通道完成。
            // 【V2.13.7 防并发闸门】除 PLC 复位请求外，还必须等拍照 Task 完全结束（_taskDone）——
            // 判定即写后 PLC 可能很快读走结果并复位请求，但取图/归档/显示可能还在后台进行；
            // 若此刻就释放通道，PLC 下一拍请求进来会再开一个 Task，同一相机两个 Task 并发取图/删源
            // （"处理即删"会互相删掉对方刚归档的图），窗口错乱。等 Task 收尾再放行。
            bool reqReset = _plc.ReadCameraRequest(cfg, out int still) && still == 0;
            if (reqReset && _taskDone)
            {
                _plc.WriteCameraResult(cfg, 0);
                _activeCh = ChNone;
                _chStep = 0;
                SetState("等待 PLC 请求");
            }
        }

        /// <summary>
        /// 单相机单点位拍照全流程（后台 Task 内执行）：
        /// 切程序(如配置) → 触发+读判定 → 取图(轮询 FTP 扫目录 / TCP BR) → 归档 → 显示 → 回结果。
        /// 任何失败都收敛为结果 2（NG），绝不抛异常（防止 _chanResult 永远不落）。
        /// </summary>
        private void DoCameraShot(int camIdx, int stationNo, int windowIndex)
        {
            var cfg = _cameraCfgs[camIdx];
            var cam = _cameras[camIdx];
            int code = 2;                 // 默认 NG，成功路径改 1
            string archived = null;
            string resultText = "";
            // 显示用内存缩略图（V2.13.2 显示提速）：FTP 模式下 jpeg 一到位就提前从源文件加载，
            // 随显示事件带给 UI——显示不再等"jpeg+iv4p 归档复制 + 删源"全部完成。null=未加载/失败。
            Image preview = null;
            // 存图点位号（V2.12.1 定稿）：统一用【相机点位号】stationNo 进文件名 {点位}——
            // 点位由相机点位表唯一决定，上下相机点位号各自从 1 起会重复（如上相机 1~18、下相机 1~4），
            // 同名文件靠 ImageStore 的目录 {相机} 层按相机隔开（见 ImageStore 类注释），不再用全局窗口编号。
            // windowIndex 仅用于"显示窗口定位 / WindowEnabled/是否跳过"判定。
            int storeStation = stationNo;
            // 相机名（存图目录 {相机} 层 / 日志归属用；配置名空时优先 CameraId 真编号、其次"相机N"）
            string camName = string.IsNullOrWhiteSpace(cfg.Name)
                ? (cfg.CameraId > 0 ? $"相机{cfg.CameraId}" : $"相机{camIdx + 1}")
                : cfg.Name.Trim();
            try
            {
                // ① 触发前的输出格式 + 程序切换（V1.12.18/V1.12.25）：
                //    OutputFormat 非空才发（OF,nn），失败即中止；程序号由"点位→程序号"映射表决定，
                //    命中才切（PW,nnn），未命中保持相机当前程序。程序没切对就触发会对应错点位，宁可不拍。
                if (!string.IsNullOrWhiteSpace(cfg.OutputFormat)
                    && !cam.SetOutputFormat(cfg.OutputFormat))
                {
                    code = 2;
                    LogHelper.Warn($"相机[{camName}] 点位{stationNo} 设置判定输出格式失败（OF,{cfg.OutputFormat.Trim()}）");
                    ErrorRaised?.Invoke($"相机[{camName}] 点位{stationNo} 设置输出格式失败");
                    return;
                }
                int programNo = ResolveProgramForStation(cfg, stationNo);
                if (programNo >= 0 && !cam.SwitchProgram(programNo))
                {
                    code = 2;
                    LogHelper.Warn($"相机[{camName}] 点位{stationNo} 切换程序失败（PW,{programNo:D3}）");
                    ErrorRaised?.Invoke($"相机[{camName}] 点位{stationNo} 切换程序失败");
                    return;
                }

                // ② 触发 + 读判定
                bool triggerOk;
                bool isOk;
                if (cfg.ReadResultFromCamera)
                {
                    var outcome = cam.TriggerAndRead();
                    triggerOk = outcome.Succeeded;
                    isOk = outcome.IsOk;
                    resultText = outcome.ResultText ?? "";
                    if (!triggerOk)
                    {
                        LogHelper.Warn($"相机[{camName}] 点位{stationNo} 触发/读判定失败：{outcome.Detail}");
                        ErrorRaised?.Invoke($"相机[{camName}] 点位{stationNo} 触发/读判定失败");
                        return;
                    }
                    LogHelper.Info($"相机[{camName}] 点位{stationNo} 判定：{(isOk ? "OK" : "NG")} 结果={resultText}");
                    if (!isOk)
                        ErrorRaised?.Invoke($"相机[{camName}] 点位{stationNo} 判定 NG，结果={resultText}");
                }
                else
                {
                    // 退化模式：只 T1 触发，判定不详，图到即记 OK（现场临时用）
                    triggerOk = cam.SendTrigger();
                    isOk = true;
                    if (!triggerOk)
                    {
                        LogHelper.Warn($"相机[{camName}] 点位{stationNo} 触发失败");
                        ErrorRaised?.Invoke($"相机[{camName}] 点位{stationNo} 触发失败");
                        return;
                    }
                }

                // ⭐【V2.13.7 判定即写】判定（T2 的 RT 响应 / 退化 T1）此刻已返回，PLC 要的就是这个
                // OK/NG 结论——立即落 PLC 结果并回传状态机，不再等"取图→归档→删源"（那可能 0.5~2s）。
                //  ・code 提前按判定定死（原来在取图后才 code=isOk?1:2），后续取图/归档失败【不回退】
                //    结果：图中途没到只影响窗口显示/存图，PLC 仍按相机判定收结果（图缺失有日志+警告）。
                //  ・_plc.WriteCameraResult 内部有锁，与轮询线程 step1 的兜底写同名幂等、安全并发。
                //  ・通道释放不依赖这次写（见 StepCameraChannel 的 _taskDone 闸门），不会并发混图。
                code = isOk ? 1 : 2;
                _chanResult = code;
                _plc.WriteCameraResult(cfg, code);
                LogHelper.Info($"相机[{camName}] 点位{stationNo} 判定即写已落 PLC：{(isOk ? "OK(1)" : "NG(2)")}");

                // ③ 取图 + 归档（Ftp：轮询取图目录拿最新对；Tcp：BR 同步读回）——纯异步补充材料，
                //  只影响窗口显示与存图，不参与 PLC 结果（结果已在②末尾写掉）
                bool hasImage = false;
                if (IsTcpImage(cfg))
                {
                    var img = cam.ReadImage();
                    if (img.Succeeded && img.ImageData != null)
                    {
                        archived = _imageStore.SaveImageBytes(img.ImageData, storeStation, isOk, LatestSerialNumber, camName);
                        hasImage = archived != null;
                        if (!hasImage)
                            LogHelper.Warn($"相机[{camName}] 点位{stationNo} 图像归档失败（TCP 取图）");
                    }
                    else
                    {
                        LogHelper.Warn($"相机[{camName}] 点位{stationNo} TCP 取图失败：" + img.Detail);
                        ErrorRaised?.Invoke($"相机[{camName}] 点位{stationNo} TCP 取图失败");
                    }
                }
                else
                {
                    // FTP：触发时刻记下，轮询扫该相机取图目录找"本次新图"（修改时间不早于触发时刻），
                    // 与功能测试窗体一致避免刚触发就扫到旧图；超时兜底取最新一对。
                    DateTime triggerUtc = DateTime.UtcNow;
                    string iv4p;
                    string jpeg = WaitForFtpImage(cfg, triggerUtc, out iv4p);
                    if (string.IsNullOrEmpty(jpeg))
                    {
                        LogHelper.Warn($"相机[{camName}] 点位{stationNo} FTP 取图目录未找到新图");
                        ErrorRaised?.Invoke($"相机[{camName}] 点位{stationNo} FTP 取图目录未找到新图");
                    }
                    else
                    {
                        // 【V2.13.2 显示提速】jpeg 一到位立刻从 FTP 源加载内存缩略图（异步显示链路，
                        // 不等归档）。源文件此刻尚未被删、且 SaveImageFilePair 用 FileShare.ReadWrite，
                        // 即使相机仍在写也最多加载失败为 null——失败则由 UI 回退按归档副本加载，无副作用；
                        // 若读到半截文件，LoadThumbnailSafe 解码失败返回 null，同样回退。归档不受影响。
                        preview = ProductionCoordinator.LoadThumbnailSafe(jpeg);
                        archived = _imageStore.SaveImageFilePair(jpeg, iv4p, storeStation, isOk, LatestSerialNumber, camName);
                        if (archived != null)
                        {
                            // 归档成功 → 删除 FTP 源文件（"处理即删"，防同点位新旧图混淆）；删失败不阻断。
                            // 注意：preview 已在归档前读完并持有内存副本，删源不影响显示。
                            ImageStore.DeleteSourceFile(jpeg, $"相机[{camName}] 点位{stationNo}");
                            ImageStore.DeleteSourceFile(iv4p, $"相机[{camName}] 点位{stationNo}");
                        }
                        hasImage = archived != null;
                    }
                }
                if (!hasImage)
                {
                    preview?.Dispose(); // 归档失败：提前加载的预览图没被显示事件带走，立即释放防句柄泄漏
                    // 无图 → 结果已在"判定即写"阶段落 PLC（1/2），这里直接返回不改变结果：
                    // code 保持判定值（isOk?1:2），finally 落 _chanResult 与已写值一致（幂等）。
                    return;
                }

                // ④ 显示 + 计数（抛给 UI 线程刷新对应窗口）
                _seqNo++;
                var data = new WindowData
                {
                    SeqNo = _seqNo,
                    IsOk = isOk,
                    ImagePath = archived,
                    PreviewImage = preview,
                    CapturedAt = DateTime.Now,
                    SerialNumber = LatestSerialNumber,
                    ResultText = resultText,
                    StationNo = stationNo
                };
                InspectionFinished?.Invoke(data, windowIndex);
                LogHelper.Info($"点位{stationNo} 检测完成：{(isOk ? "OK" : "NG")} → {archived}（窗口{windowIndex}）");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"相机[{camName}] 点位{stationNo} 拍照异常", ex);
                ErrorRaised?.Invoke($"相机[{camName}] 点位{stationNo} 拍照异常：" + ex.Message);
            }
            finally
            {
                _chanResult = code; // 兜底：与判定即写阶段已写出的 PLC 结果一致（幂等）；失败路径靠这里落 NG
                _taskDone = true;   // V2.13.7：拍照 Task 完全结束（判定+取图+归档+显示），放行通道复位
            }
        }

        /// <summary>
        /// FTP 模式等图：触发后等该相机取图目录出现"修改时间不早于触发时刻"的 jpeg（视为本次新图），
        /// 或等待超时；超时仍取最新一对兜底（有旧图残留也照常归档）。
        ///
        /// 【V2.13.6 信号加速】相机推图到 FTP 有延迟，纯轮询每隔 200ms 才扫一次目录，最坏多等一拍。
        /// 现在 ImageStore 的 FileSystemWatcher 一发现新文件就会 Set 本相机的 _ftpArrive 信号，
        /// 下面用 Wait(200) 等待"信号或超时"——图一到立即醒来重扫马上拿到，事件漏报再靠 200ms 兜底轮询。
        /// 每拍开始先 Reset：把上一拍残留的信号清掉，保证"本次触发之后的新图事件"才唤醒本拍。
        /// </summary>
        /// <returns>jpeg 完整路径（无则空字符串），iv4p 通过 out 返回（可为 null）</returns>
        private string WaitForFtpImage(CameraConfig cfg, DateTime triggerUtc, out string iv4p)
        {
            iv4p = null;
            int waitMs = Math.Max(2000, cfg.ImageWaitMs); // 至少 2s，防配置过小立刻判失败
            int camIdx = _cameraCfgs.IndexOf(cfg);        // 相机在下标 → 对应信号
            bool hasSignal = camIdx >= 0 && camIdx < _ftpArrive.Length;
            if (hasSignal) _ftpArrive[camIdx].Reset();    // 清掉上一拍残留信号
            var stopwatch = Stopwatch.StartNew();
            var pair = new ImageStore.LatestPairResult();
            while (stopwatch.ElapsedMilliseconds < waitMs)
            {
                var c = _imageStore.FindLatestPair(FtpDirFor(cfg));
                if (!string.IsNullOrEmpty(c.JpegPath) && IsNewerThanTrigger(c.JpegPath, triggerUtc))
                {
                    pair = c;
                    break;
                }
                // 等"该相机 FTP 有新文件"信号，最多 200ms：事件到立即重扫（加速），
                // 无事件则 200ms 后照常轮询（兜底，事件漏报不失图）。
                if (hasSignal)
                    _ftpArrive[camIdx].Wait(200);
                else
                    Thread.Sleep(200);
            }
            if (string.IsNullOrEmpty(pair.JpegPath))
                pair = _imageStore.FindLatestPair(FtpDirFor(cfg)); // 超时兜底：取最新一对
            iv4p = pair.IvpPath;
            return pair.JpegPath;
        }

        /// <summary>判断文件是否是"本次触发之后新推的图"：修改时间（UTC）不早于触发时刻即视为新图。
        /// 文件读取失败视为"不是新图"（防把正在写入/被占用打不开的半成品图当成新图）。容差 1 秒。</summary>
        private static bool IsNewerThanTrigger(string path, DateTime triggerUtc)
        {
            try
            {
                return File.GetLastWriteTimeUtc(path) >= triggerUtc.AddSeconds(-1);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>某相机的 FTP 取图目录：优先相机配置 FtpUploadDir，为空回退全局兜底目录。</summary>
        private string FtpDirFor(CameraConfig cfg)
        {
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.FtpUploadDir))
                return cfg.FtpUploadDir;
            return _imageStore.DefaultFtpDir;
        }

        /// <summary>判断相机是否走 TCP/BR 直读取图（V1.7.0）：配置 ImageSource=="Tcp"（大小写不敏感）。
        /// 其余取值（含空/null/其他文字）一律按 Ftp 兜底，旧配置无需迁移。</summary>
        private static bool IsTcpImage(CameraConfig cfg) =>
            cfg != null
            && !string.IsNullOrWhiteSpace(cfg.ImageSource)
            && cfg.ImageSource.Trim().Equals("Tcp", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 把"某台相机拍到某个点位"解析成显示窗口编号（V2.12.1 起统一相机表驱动，不再分自适应/非自适应；
        /// V2.13 起支持手动编辑的"窗口↔(相机,点位)"独立映射，见 DisplayConfig.WindowPointMaps；
        /// V2.13.4 起关联键 = 相机ID CameraId，不再用列表下标）。
        ///
        /// 点位由相机点位表唯一决定：上下相机点位号各自从 1 起会重复（如上相机 1~18、下相机 1~4）。
        /// 定位方式（V2.13 起）：
        ///   - 默认（未手动编辑）：按"前上相机后下相机"分组，窗口 = 相机点位表条目位置
        ///     （= DisplayConfig.DefaultWindowPointMap 的铺排，与旧逻辑等价）；
        ///   - 手动编辑/交换过（WindowPointForm）：查该型号的 WindowPointMaps 表，
        ///     找"相机=本相机ID且点位=请求点位"的唯一窗口（同一"相机+点位"只分配给一个窗口）。
        /// 找不到窗口：
        ///   - 该点位不归本相机拍（另一台相机的点位）→ 返回 false，调用方按"跳过"处理（写结果 3）；
        /// 该窗口被禁用（WindowEnabled=false）→ 返回 false（同样是跳过，不拍照不计数）。
        /// </summary>
        private bool TryResolveActiveWindow(int cameraId, int stationNo, out int windowIndex)
        {
            windowIndex = -1;
            // V2.13 独立映射反查：遍历当前型号的窗口→(相机,点位)表，找"相机ID=cameraId 且点位=stationNo"
            // 的窗口编号（下标+1）。默认铺排就是"前上相机后下相机"分组，行为与旧版一致。
            if (_windowPointMap != null)
            {
                for (int i = 0; i < _windowPointMap.Count; i++)
                {
                    var it = _windowPointMap[i];
                    if (it != null && it.CameraId == cameraId && it.StationNo == stationNo)
                    {
                        windowIndex = i + 1;
                        return IsWindowEnabled(windowIndex);
                    }
                }
                return false;   // 映射里没有该"相机+点位" → 不归本相机拍/未分配窗口 → 跳过
            }

            // 兜底（_windowPointMap 为 null 的极端情况）：退回按相机点位表条目位置定位
            int camIdx = IndexOfCamera(cameraId);
            if (camIdx < 0 || camIdx >= _cameraCfgs.Count) return false;
            var table = _cameraCfgs[camIdx].ProgramsFor(_productModel);
            if (table == null) return false;
            int pos = -1;
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i] != null && table[i].StationNo == stationNo) { pos = i; break; }
            }
            if (pos < 0) return false;          // 该相机点位表里没有此点位 → 不归本相机拍
            var starts = DisplayConfig.AutoFitCameraStarts(_cameraCfgs, _productModel);
            if (camIdx >= starts.Count) return false;
            windowIndex = starts[camIdx] + pos; // 起始窗口 + 表内位置（表第 1 条=该相机起始窗口）
            return IsWindowEnabled(windowIndex);
        }

        /// <summary>某号窗口是否启用（V1.12.28）：配置缺省/越界一律视为启用（新窗口默认开）。</summary>
        private bool IsWindowEnabled(int w)
        {
            if (_windowEnabled == null) return true;
            if (w < 1 || w > _windowEnabled.Count) return true;
            return _windowEnabled[w - 1];
        }

        /// <summary>显示窗口总数（V2.12.1 统一）：各相机按当前型号点位表条目数之和（至少 1），
        /// 与主窗体 BuildWindowGrid / WindowPointForm / 设置页预览走同一套（DisplayConfig.WindowCountFor，
        /// 自适应与否都一样——点位由相机点位表唯一决定，窗口只是把点位条目顺序铺排）。</summary>
        private int _windowCount()
        {
            return DisplayConfig.WindowCountFor(_cameraCfgs, _productModel);
        }

        /// <summary>
        /// 查某相机"点位→程序号"映射表（V1.12.25；V2.8 起按产品型号分表）：
        ///   ① 优先在 ModelStationPrograms 里找"与当前产品型号同名"的那张表，命中就在该表查点位；
        ///   ② 型号没配表 / 型号表里没该点位 → 回退默认表 StationPrograms（旧兼容 + 不区分型号场景）；
        ///   ③ 都未命中返回 -1（=不切换程序，保持相机当前程序，不发 PW）。
        /// 表里配了哪些点位就是这台相机负责拍哪些点位；没配的点位一律不切换。
        /// </summary>
        private int ResolveProgramForStation(CameraConfig cfg, int stationNo)
        {
            if (cfg == null) return -1;

            // ① 按当前产品型号查型号表（V2.8）：型号名大小写不敏感匹配
            if (!string.IsNullOrWhiteSpace(_productModel) && cfg.ModelStationPrograms != null)
            {
                foreach (var m in cfg.ModelStationPrograms)
                {
                    if (m != null && m.Programs != null
                        && string.Equals(m.ModelName, _productModel, StringComparison.OrdinalIgnoreCase))
                    {
                        return FindProgram(m.Programs, stationNo);
                    }
                }
            }

            // ② 回退默认表（无型号/型号没配表/型号表里没有该点位 → 用 StationPrograms）
            if (cfg.StationPrograms == null) return -1;
            return FindProgram(cfg.StationPrograms, stationNo);
        }

        /// <summary>在"点位→程序号"表里查点位：命中且程序号 &gt;=0 返回程序号，否则 -1（不切换）。</summary>
        private static int FindProgram(List<StationProgramItem> table, int stationNo)
        {
            if (table == null) return -1;
            foreach (var item in table)
            {
                if (item != null && item.StationNo == stationNo && item.ProgramNo >= 0)
                    return item.ProgramNo;
            }
            return -1;
        }

        /// <summary>
        /// 取最近一张图的内存副本（UI 显示用），避免 GDI+ 锁定文件。
        /// FileShare.ReadWrite：归档失败回退用 FTP 源文件显示时，文件可能仍在被写/被占用，
        /// Read 共享读不到会返回 null 导致窗口空白。
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

        /// <summary>
        /// 加载图片并降采样为"显示用缩略图"（V2.13.2，性能优化）：
        /// 基恩士相机原图很大（常见 2592×1944，甚至更大），若直接把全尺寸原图交给
        /// PictureBox 显示，每次"读盘 + GDI+ 解码 + Zoom 等比绘制"都在 UI 线程做，
        /// 会明显卡顿/拖慢画面刷新。本方法把图片先等比缩到最大边不超过 maxDim 的小图
        /// （显示窗口通常只有几百像素宽，超过 1280 纯属无用开销；该尺寸在"双击全屏
        /// 放大"查看缺陷时仍基本清晰），解码/绘制成本从"大图"降到"小图"，内存也省。
        /// 与 LoadImageSafe 一样用 FileShare.ReadWrite 打开，文件被占用也能读。
        /// 【上限可调】maxDim 默认 1280：足够普通窗口锐利显示 + 全屏可看清；若现场
        ///   全屏需更大细节可调大（代价是解码/绘制更慢，与本节思想相反，慎用）。
        /// 失败返回 null（不抛异常），由调用方静默降级（窗口保持空态/上一张图）。
        /// </summary>
        /// <param name="path">图片完整路径（jpeg/jpg/png 等 GDI+ 可解码格式）</param>
        /// <param name="maxDim">缩略图最大边像素数（默认 1280）</param>
        /// <returns>等比例缩小的新 Bitmap（调用方负责 Dispose）；异常时返回 null</returns>
        public static Image LoadThumbnailSafe(string path, int maxDim = 1280)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var src = Image.FromStream(fs))
                {
                    // 等比计算目标尺寸：任意一边超过 maxDim 才缩小，否则按原尺寸拷贝一份；
                    // 拷贝的目的是让返回位图的存活不再依赖已 Dispose 的源流（src 生命周期只在方法内）。
                    int w = src.Width, h = src.Height;
                    if (w > maxDim || h > maxDim)
                    {
                        double ratio = Math.Min((double)maxDim / w, (double)maxDim / h);
                        w = Math.Max(1, (int)(w * ratio));
                        h = Math.Max(1, (int)(h * ratio));
                    }
                    var bmp = new Bitmap(w, h);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        // 高质量双三次重采样：缩小后保细节（现场要看缺陷，宁可多花一点绘制时间）。
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    return bmp;
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
            catch (ObjectDisposedException) { }
            catch (Exception) { }
        }

        private readonly object _stateLock = new object();
        private string _lastState = "";

        /// <summary>发流程状态（日志 + 事件）。相同的状态不重复发（防日志刷爆）。</summary>
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
            _disposed = true;
            _running = false;
            UnhookScannerEvents();
            // V2.13.6：退订 FTP 新图事件、释放信号量。注意【不 Dispose _imageStore】——
            // ImageStore 归 MainForm 主窗体所有（SwitchModel/热更时被多代协调器复用），
            // 之前协调器 Dispose 顺手关掉它会导致切型号后相机 FTP 监听失效（图照拍但事件加速丢失）。
            if (_ftpHooked && _imageStore != null)
            {
                _imageStore.FtpFileArrived -= OnFtpFileArrived;
                _ftpHooked = false;
            }
            foreach (var s in _ftpArrive)
            {
                try { s.Dispose(); } catch { }
            }
            _ftpArrive = new ManualResetEventSlim[0];
            _positionTimer?.Dispose();
        }
    }
}
