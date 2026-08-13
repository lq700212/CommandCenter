using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    /// 【V2.7 协议（docs/CommandCenter.md §5.5）：请求-结果-复位三拍式握手】
    /// 现场 PLC(汇川)做 Modbus TCP 主站、上位机做从站。PLC 分阶段发请求，上位机逐通道处理：
    ///   ┌─ 通道① 扫码（40001 请求 → 40004 结果 + 40007~40011 型号）─────────────┐
    ///   │ PLC 写 40001=1 → 上位机触发扫码枪/等手动 SN → 写型号 + 写结果(1=OK/2=NG) │
    ///   │ → PLC 读到结果后写 40001=0 → 上位机看到 40001 归 0 → 写 40004=0 复位     │
    ///   └───────────────────────────────────────────────────────────────────────┘
    ///   ┌─ 通道② 上相机拍照（40002 请求 → 40005 结果）──────────────────────────┐
    ///   │ PLC 写 40002=点位 → 上位机触发相机0 拍该点位→归档→显示→写结果(1/2/3)  │
    ///   │ → PLC 读到结果写 40002=0 → 上位机看到归 0 → 写 40005=0 复位            │
    ///   └───────────────────────────────────────────────────────────────────────┘
    ///   ┌─ 通道③ 下相机拍照（40003 请求 → 40006 结果），逻辑同上相机 ────────────┐
    ///   结果值：0=默认/复位，1=OK，2=NG，3=点位禁用跳过（V1.12.28：禁用点位不拍照、
    ///   不显示、不计数，直接回 3 让 PLC 走下一工位）。
    ///   三个通道互斥串行（一次只处理一个，_activeCh），符合 PLC 串行时序；
    ///   请求只有被"认领"的通道处理，其余通道的请求在活动通道完成前不抢占。
    ///
    /// 【相机映射】相机按下标对应通道：相机0=上相机(40002/40005)、相机1=下相机(40003/40006)。
    ///   第三台及以上相机暂无 PLC 驱动通道（文档预留 40012+ 扩展），不参与请求驱动，
    ///   仍可手动（功能测试窗体）触发验证。
    ///
    /// 【点位与窗口】PLC 请求里带点位编号（40002/40003 的值），上位机经窗口映射
    ///   WindowStationMap 找到对应显示窗口（找不到兜底"点位=窗口编号"）；该窗口被禁用
    ///   （WindowEnabled=false）时视为"该点位跳过"，直接写结果 3。
    ///
    /// 【线程】请求轮询在后台线程（PositionTimer），相机触发/取图/归档在 Task 后台线程，
    ///   通过 _chanResult（volatile）回传结果给轮询线程写 PLC；界面刷新走事件
    ///   （InspectionFinished 由订阅方 Invoke 回 UI 线程）。本类不接触任何控件，纯业务编排。
    /// </summary>
    public class ProductionCoordinator : IDisposable
    {
        private readonly PlcService _plc;
        private readonly List<KeyenceIV4Camera> _cameras;   // 每台相机一个服务实例
        private readonly List<CameraConfig> _cameraCfgs;    // 对应的相机配置（程序映射/FTP目录等）
        private readonly ImageStore _imageStore;
        private readonly DisplayConfig _display;
        private readonly List<int> _windowStationMap;       // 窗口→存图点位映射（配置）
        private readonly List<bool> _windowEnabled;         // 窗口→是否启用（V1.12.28）
        private readonly string _productModel;              // 固定产品型号（V2.7，每次扫码写入 PLC）

        private readonly System.Threading.Timer _positionTimer;  // 请求轮询（后台线程）
        private volatile bool _running;   // 总开关
        private volatile bool _disposed;  // 已释放标记
        private int _seqNo;               // 全局检测序号（非线程敏感，轮询线程自增）

        // ── V2.7 三通道状态机 ──
        // 所有状态字段只在"轮询线程"修改；相机拍照 Task 只写 _chanResult（volatile），
        // 轮询线程读取后落 PLC 结果寄存器，因此 _chStep/_activeCh 无需加锁。
        private const int ChNone = -1;    // 无活动通道（空闲，等新请求）
        private const int ChScan = 0;     // 通道① 扫码
        private const int ChCamUp = 1;    // 通道② 上相机（相机下标 0）
        private const int ChCamDown = 2;  // 通道③ 下相机（相机下标 1）
        private volatile int _activeCh = ChNone; // 当前活动通道
        private volatile int _chStep;     // 通道内步骤：见各通道推进逻辑
        private volatile int _chanResult = -1;   // 相机拍照结果：-1=未出，1=OK，2=NG，3=跳过
        private int _pendStation;         // 相机通道当前点位（轮询线程读写）

        // ── 扫码通道 ──
        private readonly List<IScanner> _scanners = new List<IScanner>();
        private volatile bool _serialReceived;  // 本次扫码是否已收到 SN（扫码枪事件/手动输入置位）
        private bool _scanHooked;               // 是否已订阅扫码枪事件（防重复订阅）
        private DateTime _scanArriveUtc;        // 扫码请求受理时刻（判断 SN 等待超时）

        /// <summary>扫码等待 SN 的超时（毫秒）：扫码请求到位后产品迟迟没被扫到（没贴码/扫码枪没读到），
        /// 超时写结果 2（扫码 NG）上报 PLC，避免流程卡死。</summary>
        private const int ScanWaitMs = 30000;

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
        /// </summary>
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
                                     List<int> windowStationMap,
                                     List<bool> windowEnabled,
                                     string productModel)
        {
            _plc = plc;
            _cameras = cameras ?? new List<KeyenceIV4Camera>();
            _cameraCfgs = cameraCfgs ?? new List<CameraConfig>();
            _imageStore = imageStore;
            _display = display;
            _windowStationMap = windowStationMap;
            _windowEnabled = windowEnabled;
            _productModel = productModel ?? "";

            // 请求轮询：后台线程 200ms 一问 PLC。
            // ★ 必须用 System.Threading.Timer：此前用 Forms.Timer 在 UI 线程同步读 PLC，
            //   不可达 IP 时把界面整个卡住（点"系统设置"半天没反应就是这原因）。
            _positionTimer = new System.Threading.Timer(
                PositionTimer_Tick, null,
                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
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

        /// <summary>扫码枪读码事件（工作线程）：置"本次 SN 已到"标志，扫码通道据此推进。</summary>
        private void OnScannerCode(object sender, string code)
        {
            _serialReceived = true;
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

        /// <summary>空闲轮询：按 扫码 → 上相机 → 下相机 优先级认领一个非 0 请求。</summary>
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
            // 通道② 上相机请求（40002）
            ok = _plc.ReadCamUpRequest(out int upStation);
            if (ok && upStation > 0)
            {
                BeginCameraChannel(ChCamUp, upStation);
                return;
            }
            // 通道③ 下相机请求（40003）
            ok = _plc.ReadCamDownRequest(out int downStation);
            if (ok && downStation > 0)
            {
                BeginCameraChannel(ChCamDown, downStation);
                return;
            }
        }

        // ════════════════ 通道① 扫码 ════════════════

        /// <summary>受理扫码请求：清 SN 标志、触发扫码枪，进入"等 SN"步骤。</summary>
        private void BeginScanChannel()
        {
            _activeCh = ChScan;
            _chStep = 0;                    // 步骤0：等 SN
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
                if (_serialReceived)
                {
                    _plc.WriteProductModel(_productModel);
                    _plc.WriteScanResult(1);      // 扫码 OK
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

        // ════════════════ 通道②③ 相机拍照 ════════════════

        /// <summary>
        /// 受理相机拍照请求：解析点位→窗口，判断是否跳过（无相机/点位禁用）；
        /// 正常则启动后台 Task 触发拍照，轮询线程在 Task 出结果后写 PLC。
        /// </summary>
        /// <param name="channel">ChCamUp / ChCamDown</param>
        /// <param name="stationNo">PLC 请求里的点位编号（1~255）</param>
        private void BeginCameraChannel(int channel, int stationNo)
        {
            int camIdx = channel; // 通道号==相机下标：上=0、下=1（V2.7 固定映射）

            // 跳过判定：请求点位无对应启用窗口（禁用/未配）或该通道没有相机
            bool skip = !TryResolveActiveWindow(stationNo, out int windowIndex);
            if (!skip && (camIdx >= _cameras.Count || camIdx >= _cameraCfgs.Count))
                skip = true;

            _activeCh = channel;
            _pendStation = stationNo;

            if (skip)
            {
                // 点位禁用/无相机：不拍照、不显示、不计数，直接写结果 3（跳过）告诉 PLC 走下一工位
                _chanResult = 3;
                _chStep = 1;
                LogHelper.Info($"点位{stationNo} 已禁用或无相机，上报跳过(3)（相机通道 {(channel == ChCamUp ? "上" : "下")}）");
                SetState($"点位{stationNo} 已禁用，跳过拍照");
                return;
            }

            _chanResult = -1;
            _chStep = 1;    // 步骤1：拍照进行中（Task 出结果后写 PLC）
            SetState($"点位{stationNo} 触发 {(camIdx == 0 ? "上相机" : "下相机")} 拍照");
            LogHelper.Info($"收到 PLC 拍照请求：通道{(channel == ChCamUp ? "上" : "下")}，点位{stationNo}（窗口{windowIndex}）");

            // 触发+取图+归档+显示 全部在后台线程，完成后只回传 _chanResult 给轮询线程
            System.Threading.Tasks.Task.Run(() => DoCameraShot(channel, camIdx, stationNo, windowIndex));
        }

        /// <summary>相机通道推进：拍照完成出结果 → 写 PLC 结果 → 等 PLC 复位请求 → 复位结果。</summary>
        private void StepCameraChannel()
        {
            if (_chStep == 1)
            {
                // 拍照 Task 已出结果（1=OK / 2=NG / 3=跳过）→ 写对应通道结果寄存器
                if (_chanResult >= 0)
                {
                    int code = _chanResult;
                    _chanResult = -1;
                    if (_activeCh == ChCamUp)
                        _plc.WriteCamUpResult(code);
                    else
                        _plc.WriteCamDownResult(code);
                    _chStep = 2;
                    SetState($"点位{_pendStation} 已上报结果({code})，等待 PLC 复位请求");
                }
                return;
            }

            // 步骤2：等 PLC 把请求寄存器复位为 0 → 复位结果寄存器，通道完成
            bool ok = _activeCh == ChCamUp
                ? _plc.ReadCamUpRequest(out int up) && up == 0
                : _plc.ReadCamDownRequest(out int down) && down == 0;
            if (ok)
            {
                if (_activeCh == ChCamUp)
                    _plc.WriteCamUpResult(0);
                else
                    _plc.WriteCamDownResult(0);
                _activeCh = ChNone;
                SetState("等待 PLC 请求");
            }
        }

        /// <summary>
        /// 单相机单点位拍照全流程（后台 Task 内执行）：
        /// 切程序(如配置) → 触发+读判定 → 取图(轮询 FTP 扫目录 / TCP BR) → 归档 → 显示 → 回结果。
        /// 任何失败都收敛为结果 2（NG），绝不抛异常（防止 _chanResult 永远不落）。
        /// </summary>
        private void DoCameraShot(int channel, int camIdx, int stationNo, int windowIndex)
        {
            var cfg = _cameraCfgs[camIdx];
            var cam = _cameras[camIdx];
            int code = 2;                 // 默认 NG，成功路径改 1
            string archived = null;
            string resultText = "";
            try
            {
                // ① 触发前的输出格式 + 程序切换（V1.12.18/V1.12.25）：
                //    OutputFormat 非空才发（OF,nn），失败即中止；程序号由"点位→程序号"映射表决定，
                //    命中才切（PW,nnn），未命中保持相机当前程序。程序没切对就触发会对应错点位，宁可不拍。
                if (!string.IsNullOrWhiteSpace(cfg.OutputFormat)
                    && !cam.SetOutputFormat(cfg.OutputFormat))
                {
                    code = 2;
                    LogHelper.Warn($"相机[{camIdx}] 点位{stationNo} 设置判定输出格式失败（OF,{cfg.OutputFormat.Trim()}）");
                    ErrorRaised?.Invoke($"相机[{camIdx}] 点位{stationNo} 设置输出格式失败");
                    return;
                }
                int programNo = ResolveProgramForStation(cfg, stationNo);
                if (programNo >= 0 && !cam.SwitchProgram(programNo))
                {
                    code = 2;
                    LogHelper.Warn($"相机[{camIdx}] 点位{stationNo} 切换程序失败（PW,{programNo:D3}）");
                    ErrorRaised?.Invoke($"相机[{camIdx}] 点位{stationNo} 切换程序失败");
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
                        LogHelper.Warn($"相机[{camIdx}] 点位{stationNo} 触发/读判定失败：{outcome.Detail}");
                        ErrorRaised?.Invoke($"相机[{camIdx}] 点位{stationNo} 触发/读判定失败");
                        return;
                    }
                    LogHelper.Info($"相机[{camIdx}] 点位{stationNo} 判定：{(isOk ? "OK" : "NG")} 结果={resultText}");
                    if (!isOk)
                        ErrorRaised?.Invoke($"相机[{camIdx}] 点位{stationNo} 判定 NG，结果={resultText}");
                }
                else
                {
                    // 退化模式：只 T1 触发，判定不详，图到即记 OK（现场临时用）
                    triggerOk = cam.SendTrigger();
                    isOk = true;
                    if (!triggerOk)
                    {
                        LogHelper.Warn($"相机[{camIdx}] 点位{stationNo} 触发失败");
                        ErrorRaised?.Invoke($"相机[{camIdx}] 点位{stationNo} 触发失败");
                        return;
                    }
                }

                // ③ 取图 + 归档（Ftp：轮询取图目录拿最新对；Tcp：BR 同步读回）
                bool hasImage = false;
                if (IsTcpImage(cfg))
                {
                    var img = cam.ReadImage();
                    if (img.Succeeded && img.ImageData != null)
                    {
                        archived = _imageStore.SaveImageBytes(img.ImageData, stationNo, isOk, LatestSerialNumber);
                        hasImage = archived != null;
                        if (!hasImage)
                            LogHelper.Warn($"相机[{camIdx}] 点位{stationNo} 图像归档失败（TCP 取图）");
                    }
                    else
                    {
                        LogHelper.Warn($"相机[{camIdx}] 点位{stationNo} TCP 取图失败：" + img.Detail);
                        ErrorRaised?.Invoke($"相机[{camIdx}] 点位{stationNo} TCP 取图失败");
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
                        LogHelper.Warn($"相机[{camIdx}] 点位{stationNo} FTP 取图目录未找到新图");
                        ErrorRaised?.Invoke($"相机[{camIdx}] 点位{stationNo} FTP 取图目录未找到新图");
                    }
                    else
                    {
                        archived = _imageStore.SaveImageFilePair(jpeg, iv4p, stationNo, isOk, LatestSerialNumber);
                        if (archived != null)
                        {
                            // 归档成功 → 删除 FTP 源文件（"处理即删"，防同点位新旧图混淆）；删失败不阻断
                            ImageStore.DeleteSourceFile(jpeg, $"点位{stationNo}");
                            ImageStore.DeleteSourceFile(iv4p, $"点位{stationNo}");
                        }
                        hasImage = archived != null;
                    }
                }
                if (!hasImage) return; // 无图 → 保持 code=2（NG）

                // ④ 显示 + 计数（抛给 UI 线程刷新对应窗口）
                _seqNo++;
                var data = new WindowData
                {
                    SeqNo = _seqNo,
                    IsOk = isOk,
                    ImagePath = archived,
                    CapturedAt = DateTime.Now,
                    SerialNumber = LatestSerialNumber,
                    ResultText = resultText,
                    StationNo = stationNo
                };
                InspectionFinished?.Invoke(data, windowIndex);
                code = isOk ? 1 : 2; // 有图 → 按判定定结果：1=OK，2=NG（文档 40005/40006 语义）
                LogHelper.Info($"点位{stationNo} 检测完成：{(isOk ? "OK" : "NG")} → {archived}（窗口{windowIndex}）");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"相机[{camIdx}] 点位{stationNo} 拍照异常", ex);
                ErrorRaised?.Invoke($"相机[{camIdx}] 点位{stationNo} 拍照异常：" + ex.Message);
            }
            finally
            {
                _chanResult = code; // 回传轮询线程落 PLC 结果（volatile，可见）
            }
        }

        /// <summary>
        /// FTP 模式等图：触发后轮询该相机取图目录，直到出现"修改时间不早于触发时刻"的
        /// jpeg（视为本次新图）或等待超时；超时仍取最新一对兜底（有旧图残留也照常归档）。
        /// 相机推图到 FTP 有延迟，立即扫可能取到旧图或空目录，故必须按时间窗判断。
        /// </summary>
        /// <returns>jpeg 完整路径（无则空字符串），iv4p 通过 out 返回（可为 null）</returns>
        private string WaitForFtpImage(CameraConfig cfg, DateTime triggerUtc, out string iv4p)
        {
            iv4p = null;
            int waitMs = Math.Max(2000, cfg.ImageWaitMs); // 至少 2s，防配置过小立刻判失败
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
        /// 由点位编号找到"应刷新/应检测"的启用窗口（V1.12.28，替代旧的环形窗口分配）：
        /// ① 在窗口映射 WindowStationMap 里找所有==该点位的窗口，取第一个【启用】的；
        /// ② 映射里没有该点位（自定义点位不在映射内）→ 兜底"点位=窗口编号"且该窗口启用；
        /// ③ 都找不到启用窗口 → 返回 false（调用方按"点位禁用跳过"处理，不拍照不计数）。
        /// </summary>
        private bool TryResolveActiveWindow(int stationNo, out int windowIndex)
        {
            windowIndex = -1;
            int count = _windowCount();
            if (_windowStationMap != null)
            {
                for (int i = 0; i < Math.Min(_windowStationMap.Count, count); i++)
                {
                    if (_windowStationMap[i] == stationNo && IsWindowEnabled(i + 1))
                    {
                        windowIndex = i + 1;
                        return true;
                    }
                }
            }
            if (stationNo >= 1 && stationNo <= count && IsWindowEnabled(stationNo))
            {
                windowIndex = stationNo;
                return true;
            }
            return false;
        }

        /// <summary>某号窗口是否启用（V1.12.28）：配置缺省/越界一律视为启用（新窗口默认开）。</summary>
        private bool IsWindowEnabled(int w)
        {
            if (_windowEnabled == null) return true;
            if (w < 1 || w > _windowEnabled.Count) return true;
            return _windowEnabled[w - 1];
        }

        /// <summary>显示窗口总数（Rows×Columns，至少 1）。</summary>
        private int _windowCount() => Math.Max(1, _display.Rows * _display.Columns);

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
            _positionTimer?.Dispose();
            _imageStore.Dispose();
        }
    }
}
