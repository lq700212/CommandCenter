using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CommandCenter.Models;
using CommandCenter.Utils;
using NModbus;
using NModbus.Data;

namespace CommandCenter.Services
{
    /// <summary>
    /// PLC 通讯服务（V1.12.11 起改为 Modbus TCP 从站）。
    ///
    /// 【角色反转】现场 PLC（汇川）做 Modbus TCP 主站，上位机做从站——
    ///   上位机监听本机 502 端口，等汇川主站 TCP 连入并读写上位机的保持寄存器区。
    ///   原方案是上位机做主站主动 ReadHoldingRegisters/WriteSingleRegister 读写 PLC，
    ///   现全部改为读写上位机自己的 SlaveDataStore 寄存器区（不发起任何 Modbus 请求）。
    ///
    /// 【Modbus 协议约束】Modbus 是主从问答协议，从站不能主动给主站发消息；
    ///   所有"上位机→PLC"的数据都靠 PLC 主站轮询来读上位机寄存器区。
    ///
    /// 【V2.7 协议（docs/CommandCenter.md §5.5）：请求-结果-复位三拍式】
    ///   PLC 只写（上位机读）：40001 扫码请求(0/1)；相机通道【每台相机一路，V2.12.6】：
    ///   上相机 40002(1~255=点位)、下相机 40003（每台相机的请求/结果地址配在相机表
    ///   CameraConfig.PlcRequestAddress/PlcResultAddress，V2.13.4 起全量显式、不再按列表序号自动，
    ///   见 5.2）；
    ///   PLC 只读（上位机写）：40004 扫码结果；相机结果：上相机 40005、下相机 40006；
    ///                         40007 型号序号 + 40008~40012 产品型号(10 字符 ASCII，每寄存器 2 字符，高字节在前)；
    ///                         40013 起 扫码 SN 序列号(ASCII，编码规则同型号字符串，V2.15.17，
    ///                         默认 12 寄存器=24 字符，见 PlcConfig.ScanSerialNumberAddress/Len)。
    ///   一次完整握手：PLC 写请求≠0 → 上位机处理完写结果≠0 → PLC 读结果(扫码还读型号/SN)并复位请求=0
    ///   → 上位机看到请求回 0 再复位结果=0，进入下一请求。扫码通道与各相机通道互斥串行处理。
    ///   【替代的旧协议（V1.12.11~V1.12.16，已全部删除）】D99 扫码到位/D100 相机到位/D101 开始/
    ///   D102 完成/D103~D108 配方/D110~112 计数——均不再使用；计数改为纯本地功能，
    ///   配方概念已删除（V2.9，配方由 PLC 侧按产品型号切换，上位机只传型号）。
    ///
    /// 【NModbus 3.0.83 API】TCP 从站用 ModbusTcpSlaveNetwork（不是 ModbusTcpSlave，本 fork 无此类），
    ///   构造 new ModbusTcpSlaveNetwork(TcpListener, IModbusFactory, IModbusLogger)；
    ///   从站实例 new ModbusSlave(unitId, SlaveDataStore, handlers)；network.AddSlave(slave) 挂载；
    ///   监听 network.ListenAsync(CancellationToken)（后台线程承载，Cancel 停止）；
    ///   DataStore 用 SlaveDataStore.HoldingRegisters(PointSource&lt;ushort&gt;)，读写走 ReadPoints/WritePoints
    ///   （无索引器，与主站 ReadHoldingRegisters 的 0-based 起始地址一致）。
    ///
    /// 【线程模型】从站监听在后台线程（ListenAsync 是异步 Task，在此 GetAwaiter().GetResult() 阻塞承载，
    ///   Cancel 退出）；DataStore 读写用 _lock 串行化（避免业务轮询与 PLC 写入并发竞态）；
    ///   业务层(ProductionCoordinator)仍用 PositionTimer 每 200ms 轮询请求寄存器，等价于原主动读 PLC。
    ///
    /// 【对外接口】ReadScanRequest/ReadCameraRequest（读 PLC 请求）、
    ///   WriteScanResult/WriteCameraResult/WriteProductModel/WriteSerialNumber（写结果/型号/SN）、
    ///   ReadRegister/WriteRegister（功能测试通用读写）。语义：IsConnected/EnsureConnected 表示
    ///   "从站监听是否已就绪"，HasMasterConnected 表示"汇川主站是否已 TCP 连入"。
    /// </summary>
    public class PlcService : IDisposable
    {
        private readonly PlcConfig _cfg;
        private readonly object _lock = new object();

        // V2.14.14：当前产品型号（建站即写/切型号即写用）。
        // 背景：上位机写型号原本只在"收到 PLC 扫码请求→扫码通道推进"时触发，PLC 若不触发扫码
        // 流程就读不到型号区（40007=0、40008~40012=0）。为让 PLC 随时能读到当前型号，改为：
        //   ① 从站建站成功（EnsureConnected）后立即把本字段写入型号区（上电/断线重建/热更重建都覆盖）；
        //   ② MainForm 主界面切型号（SwitchModel）时更新本字段并立即写一次（见 MainForm.SwitchModel）。
        // 型号为空（配置缺/未设置）时建站即写跳过，避免覆盖 PLC 侧既有型号区。
        private string _currentModel = "";

        // V2.15.17：最近一次写入 SN 区的序列号缓存（"建站即写"用，与 _currentModel 同一套设定）。
        // 背景：从站断线重建后 DataStore 是全新全 0 的，若 PLC 主站还没来得及读走上一件的 SN，
        // 重建瞬间 SN 区会被清空。这里记住最后一次写入的 SN（含"失败清零"的空串），建站成功后
        // 自动回写一次——PLC 重连后读到的仍是最近一拍的 SN 状态。热更整体重建 PlcService 时本缓存
        // 归空（新实例不回写、SN 区保持全 0）：旧 SN 是否已被消费未知，写空比回填旧值安全。
        private string _currentSerial = "";

        /// <summary>已释放标记：Dispose 后后台监听/轮询立即放弃</summary>
        private volatile bool _disposed;

        // ──────────────── Modbus TCP 从站资源（NModbus 3.0.83 API）────────────────
        private TcpListener _listener;
        private IModbusTcpSlaveNetwork _network; // 由 factory.CreateSlaveNetwork 创建（自带非 null logger）
        private IModbusSlave _slave;   // 由 factory.CreateSlave 创建（自带默认功能服务），不直接 new（见 EnsureConnected）
        private SlaveDataStore _dataStore;        // 直接持有 DataStore，便于业务层读写
        private CancellationTokenSource _cts;
        private Thread _listenThread;
        private volatile bool _listening;

        /// <summary>连接状态变化事件（UI 订阅刷新指示灯；语义=从站监听是否就绪）</summary>
        public event EventHandler<bool> ConnectionChanged;

        /// <summary>主站连入状态变化事件（V1.12.11 三态灯数据源；语义=汇川主站是否已 TCP 连入本机 502）</summary>
        public event EventHandler<bool> MasterConnectionChanged;

        /// <summary>当前是否已有 PLC 主站 TCP 会话连入（由后台轮询 Masters 维护，见 MasterPollTick）</summary>
        public bool HasMasterConnected { get; private set; }

        /// <summary>轮询"主站是否连入"的后台定时器（1s；从站网络 Masters 列表随主站连接/断开变化）</summary>
        private System.Threading.Timer _masterPollTimer;

        /// <summary>当前是否已就绪（从站监听已启动）。语义等价于原来的"已连上 PLC"。</summary>
        public bool IsConnected { get; private set; }

        public PlcService(PlcConfig cfg) => _cfg = cfg;

        /// <summary>日志/界面区分用标签：监听 IP:端口（多设备时能分清是哪台）</summary>
        public string IpLabel => $"{_cfg.IpAddress}:{_cfg.Port}";

        private bool _lastFailed; // 上一次监听启动是否失败（日志降噪）

        // V2.13.8：各相机"结果寄存器"地址（DataStore 索引）缓存，供上电/从站重建初始化时统一清 0。
        // 地址来自各相机配置 PlcResultAddress（0=未配置跳过），由 MainForm.BuildServices 注册。
        private readonly List<ushort> _cameraResultAddrs = new List<ushort>();

        /// <summary>
        /// 注册各相机结果寄存器地址（V2.13.8，MainForm.BuildServices 建好相机后调用）：
        /// 供"上电/从站重建初始化"把上位机自己的相机结果寄存器清零（见 ResetResultRegisters）。
        /// 只收集 PlcResultAddress &gt; 0 的（0=未配置结果通道，跳过该台）。
        /// 热更时 PlcService 整体重建并重新注册（ApplyRuntimeConfig → BuildServices），地址不残留。
        /// </summary>
        public void SetCameraResultAddresses(IEnumerable<CameraConfig> cameras)
        {
            _cameraResultAddrs.Clear();
            if (cameras == null) return;
            foreach (var cam in cameras)
                if (cam != null && cam.PlcResultAddress > 0)
                    _cameraResultAddrs.Add((ushort)cam.PlcResultAddress);
        }

        /// <summary>
        /// 确保从站监听已启动（语义等价于原来的"确保连上 PLC"）。
        /// 监听已启动返回 true；监听启动失败(端口占用/权限等)返回 false，后台会重试。
        /// ★ 不在 UI 线程做阻塞网络 IO：监听启动是瞬时绑定，ListenAsync 在后台线程承载。
        /// </summary>
        public bool EnsureConnected()
        {
            if (_disposed) return false;
            lock (_lock)
            {
                if (_listening && _network != null) return true;

                // 先清旧资源
                // ★ V2.14.23 热更断连修复：必须调用 _network.Dispose() 而不能只 Stop listener——
                //   NModbus 3.0.83 的 ModbusTcpSlaveNetwork 实现了 IDisposable，其 Dispose() 会停止
                //   TcpListener 并逐个关闭所有已连入的 PLC 主站 TCP 会话（ModbusMasterTcpConnection）。
                //   旧代码只 _listener.Stop() + _cts.Cancel()：_cts.Cancel 只触发 NModbus 取消回调
                //   Stop 监听器，已 accept 的 master socket 不会被关闭 → PLC 主站认为 TCP 连接还活着、
                //   不会重连新从站 → SettingsForm 保存（ApplyRuntimeConfig→_plc.Dispose→重建）后
                //   黄灯常亮、PLC 发请求上位机收不到。补上 _network.Dispose() 让旧主站 socket 真正
                //   关闭，PLC 立即感知断连并重新连入新从站，黄灯转绿。
                try { _cts?.Cancel(); } catch { }
                try { _network?.Dispose(); } catch { }    // 关旧从站网络（含全部 master 连接 + listener）
                try { _listener?.Stop(); } catch { }      // 双保险：network.Dispose 内部已 Stop listener
                StopMasterPoll();   // 停旧轮询，重建成功后重新启动（防旧 Timer 读半新 _network）
                _cts = null; _listener = null; _network = null; _slave = null; _dataStore = null;

                try
                {
                    // 监听绑定 IP：配置空/0.0.0.0 → 监听所有网卡；否则按配置 IP 绑定指定网卡
                    IPAddress ip = string.IsNullOrWhiteSpace(_cfg.IpAddress) || _cfg.IpAddress == "0.0.0.0"
                        ? IPAddress.Any
                        : IPAddress.Parse(_cfg.IpAddress);
                    _listener = new TcpListener(ip, _cfg.Port);

                    // 建从站数据区 + 从站实例
                    _dataStore = new SlaveDataStore();
                    // ★ 不能 new ModbusSlave(unitId, dataStore, null) 直接 new：
                    //   NModbus 3.0.83 构造函数第三参 handlers(IEnumerable<IModbusFunctionService>)
                    //   要求非 null，传 null 会抛 ArgumentNullException → 从站监听启动失败、
                    //   界面永远没有 PLC 连接信息（日志表现"值不能为 null。参数名: handlers"）。
                    //   改用 factory.CreateSlave(unitId, dataStore)：工厂内部自动挂载全部默认
                    //   功能服务（03 读保持寄存器/06 写单个/10 写多个/15/16 等），
                    //   等价于旧注释"handlers 传 null 用默认功能码"的本意。
                    var factory = new ModbusFactory();
                    _slave = factory.CreateSlave(_cfg.UnitId, _dataStore);

                    // 建从站网络（一个监听端口可挂多个 UnitId 从站，本现场单从站够用）。
                    // ★ 用 factory.CreateSlaveNetwork(listener) 创建：工厂内部自动带上非 null 的
                    //   IModbusLogger，避免直接 new ModbusTcpSlaveNetwork(listener, factory, null)
                    //   因 logger 为 null 抛 ArgumentNullException（与上方 handlers 同类的坑）。
                    _network = factory.CreateSlaveNetwork(_listener);
                    _network.AddSlave(_slave);

                    // 启动监听（后台线程承载 ListenAsync，Cancel 控制停止）
                    _cts = new CancellationTokenSource();
                    _listening = true;
                    _listenThread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = "PlcSlaveListen"
                    };
                    _listenThread.Start();

                    _lastFailed = false;
                    SetConnected(true);
                    StartMasterPoll();   // 启动"主站连入"轮询（界面三态灯的数据源）
                    // V2.13.8：上电/从站重建初始化——把自己的结果寄存器先写 0。
                    // 现场需求：PLC 与上位机断电重启后，结果寄存器不能残留上次的 1/2/3 被误当新结果。
                    // 从站 DataStore 虽是新创建的（默认 0），但为防御"监听重建/异常残留"等场景，
                    // 显式把扫码结果（40004）与各相机结果（40005/40006…）清 0，PLC 主站上电读到
                    // 的一定是复位态。DataStore 已就绪（建站成功），写 0 一定有效。
                    ResetResultRegisters();
                    // V2.14.14：从站建站成功后立即把当前型号写进型号区（40007=序号 +
                    // 40008~40012=字符串）。背景：PLC 若不触发扫码流程,上位机原本只在扫码通道
                    // 推进时才写型号,PLC 读到的型号区恒为 0；现在建站即写,PLC 随时能读到当前型号。
                    // 型号为空时跳过（WriteProductModel("") 会写 0,没必要覆盖）。
                    if (_currentModel.Length > 0)
                        WriteProductModel(_currentModel);
                    // V2.15.17：建站即写 SN（与型号同一套设定）——断线重建后 DataStore 全 0，
                    // 把最近一次写入的 SN 回写进 SN 区，PLC 重连后读到的仍是最近一拍的 SN 状态；
                    // 缓存为空串（上电/失败清零/热更重建）时跳过，不无谓覆盖。
                    if (_currentSerial.Length > 0)
                        WriteSerialNumber(_currentSerial);
                    LogHelper.Info($"PLC 从站监听已启动 {ip}:{_cfg.Port}（UnitId={_cfg.UnitId}），等待汇川主站连入");
                    return true;
                }
                catch (Exception ex)
                {
                    SetConnected(false);
                    StopMasterPoll();
                    try { _cts?.Cancel(); } catch { }
                    try { _network?.Dispose(); } catch { }    // V2.14.23：释放已创建的网络对象，防 master 残留
                    try { _listener?.Stop(); } catch { }
                    _cts = null; _listener = null; _network = null; _slave = null; _dataStore = null;
                    _listening = false;
                    if (!_lastFailed)
                    {
                        _lastFailed = true;
                        LogHelper.Warn($"PLC 从站监听启动失败 {_cfg.IpAddress}:{_cfg.Port}，原因：{ex.Message}");
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// 从站监听后台循环：NModbus 3 的 ListenAsync 返回 Task（内部 accept 主站连接并处理请求），
        /// 在后台线程阻塞等待；Cancel/Dispose 时令其退出。
        /// </summary>
        private void ListenLoop()
        {
            try
            {
                _network.ListenAsync(_cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) { /* 正常停止：Dispose/重建触发 Cancel */ }
            catch (Exception ex)
            {
                if (!_disposed)
                    LogHelper.Warn($"PLC 从站监听异常退出：{ex.Message}");
            }
            finally
            {
                _listening = false;
                SetConnected(false);
            }
        }

        private void SetConnected(bool value)
        {
            if (IsConnected != value)
            {
                IsConnected = value;
                ConnectionChanged?.Invoke(this, value);
            }
        }

        // ════════════════ 主站连入检测（V1.12.11，三态灯数据源）════════════════

        /// <summary>
        /// 轮询"PLC 主站是否已连入"（后台 1s）：从站网络内部维护已连入的 TCP 主站列表（Masters），
        /// 边沿变化时触发 MasterConnectionChanged 事件并记日志，UI 据此点亮三态灯。
        /// 【为什么需要它】从站模式下 IsConnected 只表示"监听已就绪"，主站连没连进来是另一回事；
        ///   没有这个检测，界面永远无法知道"PLC 主站是否真的在通讯"（无法主动 ping/连 PLC）。
        /// 【V2.10.5 KeepAlive】NModbus 从站对主站会话不设 keepalive、读循环阻塞等待请求——
        ///   汇川主站拔网线/断电（静默断连，无 FIN/RST）时死会话不会自动从 Masters 清理，
        ///   三态灯会一直停在"主站已连"绿。这里遍历 Masters 给每个主站会话启用 TCP KeepAlive
        ///   （幂等），TCP 栈判死后会话读写异常、NModbus 会自动踢掉该会话 → 下一次轮询 Masters
        ///   Count 归零 → 三态灯转红，主站恢复连入后再转绿。
        /// </summary>
        private void MasterPollTick(object state)
        {
            if (_disposed) return;
            bool has = false;
            try
            {
                var nw = _network;
                if (nw != null && nw.Masters != null && nw.Masters.Count > 0)
                {
                    has = true;
                    // V2.10.5：给每个已连入的主站会话启用 KeepAlive（幂等，重复调用无害）
                    foreach (var master in nw.Masters)
                        TcpKeepAlive.Configure(master);
                }
            }
            catch { /* 网络对象可能正被重建，下个周期再读 */ }

            if (has != HasMasterConnected)
            {
                HasMasterConnected = has;
                MasterConnectionChanged?.Invoke(this, has);
                LogHelper.Info(has
                    ? $"PLC 主站已连入从站（{IpLabel}），通讯建立"
                    : $"PLC 主站连接已断开（{IpLabel}），等待主站重新连入");
            }
        }

        /// <summary>启动"主站连入"轮询（监听启动成功后调用；1s 周期）。</summary>
        private void StartMasterPoll()
        {
            if (_masterPollTimer == null)
                _masterPollTimer = new System.Threading.Timer(MasterPollTick, null, 0, 1000);
            else
                _masterPollTimer.Change(0, 1000);
        }

        /// <summary>停止"主站连入"轮询（资源重建/Dispose 时调用，防残留后台轮询读已释放对象）。</summary>
        private void StopMasterPoll()
        {
            try { _masterPollTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite); } catch { }
        }

        // ════════════════ V2.7 协议业务方法（读写自己 DataStore，方向见类注释）════════════════

        /// <summary>
        /// 读取扫码请求（V2.7，PLC 写协议 40001 = 索引 1）：返回是否 PLC 请求扫码。
        /// 读到 true 表示 PLC 把 40001 置 1、要求上位机触发扫码枪取 SN；
        /// 处理完成并写结果后由 ProductionCoordinator 等 PLC 复位请求回 0。
        /// </summary>
        public bool ReadScanRequest(out bool requested)
        {
            requested = false;
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) return false;
                requested = ReadLocal(_cfg.ScanRequestAddress) != 0;
                return true;
            }
        }

        /// <summary>写扫码结果（V2.7，上位机写索引 4 = 协议 40004，PLC 来读）：0=默认/复位，1=扫码OK，2=扫码NG。</summary>
        public void WriteScanResult(int code) => WriteLocalSafe(_cfg.ScanResultAddress, (ushort)code);

        /// <summary>读某台相机的拍照请求（V2.12.6 起每台相机一路通道）：返回点位编号（1~255），0=无请求。
        /// 【V2.13.4 起地址全部显式】请求地址 = 相机配置 PlcRequestAddress（不再按列表序号自动推导）：
        ///   0=未配置该相机通道 → 按"无请求"返回、不误判（新增相机不填地址就不参与轮询）。
        /// 处理完成并写结果后由 ProductionCoordinator 等 PLC 复位请求回 0。</summary>
        /// <param name="cam">相机配置（携带 PLC 请求地址；null 安全）</param>
        public bool ReadCameraRequest(CameraConfig cam, out int stationNo)
        {
            stationNo = 0;
            ushort addr = (ushort)(cam?.PlcRequestAddress > 0 ? cam.PlcRequestAddress : 0);
            if (addr == 0) return true;   // 未配置通道：视为无请求，不占资源不误报
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) return false;
                stationNo = ReadLocal(addr);
                return true;
            }
        }

        /// <summary>写某台相机的拍照结果（V2.12.6 起每台相机一路通道）：0=默认/复位，1=OK，2=NG，
        /// 3=点位禁用跳过。结果地址 = 相机配置 PlcResultAddress（不再按列表序号自动推导）；
        /// 0=未配置结果通道 → 跳过该台（不写、也不报错）。</summary>
        public void WriteCameraResult(CameraConfig cam, int code)
        {
            ushort addr = (ushort)(cam?.PlcResultAddress > 0 ? cam.PlcResultAddress : 0);
            if (addr > 0) WriteLocalSafe(addr, (ushort)code);
        }

        /// <summary>
        /// 设置当前产品型号（V2.14.14）：更新内部 `_currentModel`。
        /// 【调用时机】MainForm 组装服务时传入初始型号（BuildServices），主界面切型号（SwitchModel）
        /// 时更新并立即写型号区。从站建站成功（EnsureConnected）时也会用本字段把当前型号写进型号区，
        /// 让 PLC 在不触发扫码流程的情况下也能读到当前型号（见 EnsureConnected 内建站即写逻辑）。
        /// </summary>
        /// <param name="model">当前产品型号（如 "U171"），为空则建站即写跳过（不覆盖 PLC 侧型号区）</param>
        public void SetCurrentModel(string model)
        {
            _currentModel = model ?? "";
        }

        /// <summary>
        /// 写产品型号（V2.14.13 协议升级）：
        ///   ① 型号序号 → `ProductModelIndexAddress`（默认索引 7 = 协议 40007）：按型号名查
        ///      `_cfg.ModelIndexes` 映射表得序号（默认 Z121=1、U171=2），型号没配序号写 0；
        ///   ② 型号 ASCII 字符串 → `ProductModelAddress` 起（默认索引 8 = 协议 40008，连续
        ///      ProductModelLen 个寄存器），编码规则见 `PackAsciiToRegisters`（V2.15.17 抽公共）。
        /// 型号为空时序号与型号区都写 0（PLC 读到空型号），不崩。
        /// </summary>
        /// <param name="model">产品型号（如 "Z121"），超长自动截断</param>
        /// <returns>从站就绪(true)/未就绪(false)</returns>
        public bool WriteProductModel(string model)
        {
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) return false;

                // ① 型号序号（协议 40007）：查"型号→序号"映射，命中写序号、未命中写 0
                int modelIndex = ResolveModelIndex(model);
                if (_cfg.ProductModelIndexAddress > 0)
                    WriteLocal(_cfg.ProductModelIndexAddress, (ushort)modelIndex);

                // ② 型号 ASCII（协议 40008 起）：寄存器数钳位 1~20 防异常配置，打包走公共方法
                int len = Math.Max(1, Math.Min(20, _cfg.ProductModelLen));
                WriteLocalMulti(_cfg.ProductModelAddress, PackAsciiToRegisters(model, len));
                return true;
            }
        }

        /// <summary>
        /// 写扫码 SN 序列号（V2.15.17 协议扩展）：ASCII 写入 `ScanSerialNumberAddress` 起
        /// （默认索引 13 = 协议 40013，紧跟型号区 40008~40012 之后）连续 ScanSerialNumberLen 个
        /// 寄存器，编码规则与产品型号字符串**完全一致**（每寄存器 2 字符、高字节在前、不足补 0x00、
        /// PLC 以 0x00 作结束符）。无独立握手信号——PLC 在扫码结果 40004 读到 1 时读本区即得本件 SN，
        /// 与"产品名称"的传递方式同一套设定。serial 为空串时整区清 0（表示本件无有效 SN）。
        /// 【调用时机】① 扫码 OK / 人工补录覆盖时写实际 SN；② 扫码失败/超时清空；③ 从站建站成功
        /// 回写缓存（EnsureConnected，与 SetCurrentModel 同设定）。每次成功写入都刷新 `_currentSerial`
        /// 缓存供断线重建后回写。
        /// </summary>
        /// <param name="serial">序列号 SN（如 "Z12120260820001"），超长自动截断并记 WARN</param>
        /// <returns>从站就绪(true)/未就绪(false)</returns>
        public bool WriteSerialNumber(string serial)
        {
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) return false;

                // 寄存器数钳位 1~50 防异常配置（50 寄存器=100 字符，远超常见条码长度）
                int len = Math.Max(1, Math.Min(50, _cfg.ScanSerialNumberLen));
                // 超容量截断要留痕：现场排查"PLC 读到的 SN 缺尾"先看这条日志
                byte[] bytes = Encoding.ASCII.GetBytes(serial ?? "");
                if (bytes.Length > len * 2)
                    LogHelper.Warn($"SN 超 SN 区容量已截断：{bytes.Length} 字符 > {len * 2}（可调大 plc.scanSerialNumberLen），写入前 {len * 2} 字符");
                WriteLocalMulti(_cfg.ScanSerialNumberAddress, PackAsciiToRegisters(serial, len));

                _currentSerial = serial ?? "";   // 刷新缓存（含清零场景），建站即写用它回写最近状态
                return true;
            }
        }

        /// <summary>
        /// 把字符串按协议打包成寄存器数组（V2.15.17 自 WriteProductModel 抽出，型号/SN 共用，
        /// 禁止两处各写一套）：每寄存器存 2 个 ASCII 字符——高字节=前一字符、低字节=后一字符；
        /// 不足 regCount×2 个字符的尾部补 0x00（PLC 以 0x00 作字符串结束符）；超长的截断丢弃并返回。
        /// 非 ASCII 字符会被 Encoding.ASCII 替成 '?'（0x3F），条码/型号正常全为 ASCII 不受影响。
        /// </summary>
        /// <param name="text">待写入的字符串（null 安全，按空串处理=全 0）</param>
        /// <param name="regCount">目标寄存器个数（调用方已钳位）</param>
        private static ushort[] PackAsciiToRegisters(string text, int regCount)
        {
            ushort[] regs = new ushort[regCount];
            byte[] bytes = Encoding.ASCII.GetBytes(text ?? "");
            int charCount = Math.Min(bytes.Length, regCount * 2);
            for (int i = 0; i < charCount; i++)
            {
                ushort v = bytes[i]; // 单字节 ASCII，直接放进高字节；低字节留 0x00
                if (i % 2 == 0)
                    regs[i / 2] = (ushort)(v << 8); // 高字节=前一字符
                else
                    regs[i / 2] |= v;                // 低字节=后一字符
            }
            return regs;
        }

        /// <summary>
        /// 按型号名查"型号→PLC 序号"映射（V2.14.13）：在 `_cfg.ModelIndexes` 里忽略大小写匹配
        /// 型号名，命中返回该型号序号（&gt;0）；型号为空/没配序号返回 0（PLC 端视为未配置）。
        /// </summary>
        private int ResolveModelIndex(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return 0;
            var item = _cfg?.ModelIndexes?.FirstOrDefault(m =>
                m != null && string.Equals(m.ModelName, model.Trim(), StringComparison.OrdinalIgnoreCase));
            return item != null && item.ModelIndex > 0 ? item.ModelIndex : 0;
        }

        // ──────────────── 通用 D 地址读写（开发者模式窗体 DeveloperModeForm 用）────────────────
        // 从站模式下"读/写 PLC 任意寄存器"改为读写上位机自己 DataStore 寄存器区，
        // 验证从站数据存储读写正常（PLC 主站随后会读到这些值）。

        /// <summary>通用读：读取指定 D 地址的单个保持寄存器（自己 DataStore）。
        /// 从站未就绪（DataStore 为 null）返回 false，避免功能测试误报"读到 0"为成功（V1.12.13）。</summary>
        public bool ReadRegister(ushort dAddress, out ushort value)
        {
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) { value = 0; return false; }
                value = ReadLocal(dAddress);
                return true;
            }
        }

        /// <summary>通用写：写入指定 D 地址的单个保持寄存器（自己 DataStore）。
        /// 从站未就绪（DataStore 为 null）返回 false，避免功能测试误报写入成功（V1.12.13）。</summary>
        public bool WriteRegister(ushort dAddress, ushort value)
        {
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) return false;
                WriteLocal(dAddress, value);
                return true;
            }
        }

        // ════════════════ 本地 DataStore 读写（核心：不发起 Modbus 请求）════════════════

        /// <summary>
        /// 读自己 DataStore 的保持寄存器（单个）。
        /// ★ 地址说明（V2.12.3 定稿，替换 V2.12.2 的"减 40000 换算"）：
        ///   PLC(汇川) 主站按【协议号】写/读（40001 扫码请求、40002 上相机请求、40003 下相机请求…），
        ///   NModbus 从站 DataStore 的 ReadPoints(start) 的 start 是【DataStore 索引】，
        ///   现场实测 PLC 写协议 40002 → DataStore[2]（功能测试页 txtReadAddr 填 2 即读到）。
        ///   所以【配置里的地址字段直接存索引】（协议号 = 索引 + 40000），这里拿到地址就是索引，
        ///   直接用、不做任何换算（协议号 → 索引 的 40000 段操作已删除，填 2 就是 2）。
        /// </summary>
        private ushort ReadLocal(ushort address)
        {
            var regs = _dataStore?.HoldingRegisters;
            if (regs == null) return 0;
            try
            {
                ushort[] arr = regs.ReadPoints(address, 1);
                return (arr != null && arr.Length > 0) ? arr[0] : (ushort)0;
            }
            catch
            {
                // 越界/未就绪：返回 0，不崩（PLC 可能尚未连入、DataStore 未就绪）
                return 0;
            }
        }

        private void WriteLocal(ushort address, ushort value)
        {
            var regs = _dataStore?.HoldingRegisters;
            if (regs == null) return;
            try
            {
                regs.WritePoints(address, new ushort[] { value });
            }
            catch
            {
                // 越界：吞掉，不崩（保持与 ReadLocal 一致的容错策略）
            }
        }

        private void WriteLocalMulti(ushort address, ushort[] values)
        {
            var regs = _dataStore?.HoldingRegisters;
            if (regs == null) return;
            try
            {
                regs.WritePoints(address, values);
            }
            catch
            {
                // 越界：吞掉
            }
        }

        /// <summary>带日志的本地写（业务关键寄存器写入失败时记一条，便于发现 DataStore 未就绪）。</summary>
        private void WriteLocalSafe(ushort address, ushort value)
        {
            try
            {
                lock (_lock)
                    WriteLocal(address, value);
            }
            catch (Exception ex)
            {
                LogHelper.Warn($"写本地寄存器 D{address} 失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 上电/从站重建初始化：把自己（上位机）的结果寄存器全部先写 0（V2.13.8）。
        /// 【背景】现场要求"PLC 与上位机都把自己的结果寄存器先写 0，防止断电重启后残留旧值
        ///   （上次的 1/2/3）被误当成新结果"。PLC 侧由 PLC 梯形图上电清 0；上位机侧就是本方法：
        ///   从站监听一就绪，把扫码结果（ScanResultAddress，协议 40004）与各相机结果
        ///   （PlcResultAddress，协议 40005/40006…）清 0，PLC 主站连入后读到的一定是复位态。
        /// 【调用时机】EnsureConnected 每次成功重建从站后调用（覆盖软件启动、断线重建、热更重建）；
        ///   正常监听期间不重复调用（幂等写 0，无害）。DataStore 未就绪时 WriteLocalSafe 静默忽略。
        /// </summary>
        private void ResetResultRegisters()
        {
            if (_cfg != null && _cfg.ScanResultAddress > 0)
                WriteLocalSafe(_cfg.ScanResultAddress, 0);
            foreach (var addr in _cameraResultAddrs)
                WriteLocalSafe(addr, 0);
            // V2.15.17：上电初始化顺带把 SN 数据区整区清 0 + 作废缓存——防断电重启后 SN 区残留
            // 上一件的旧 SN 被 PLC 当成新件误读（与结果寄存器复位同一防御思路；DataStore 新建本就
            // 全 0，这里显式写 0 兜"监听重建/异常残留"场景）。缓存作废后建站即写不会回填旧 SN。
            if (_cfg != null && _cfg.ScanSerialNumberAddress > 0 && _cfg.ScanSerialNumberLen > 0)
            {
                int len = Math.Max(1, Math.Min(50, _cfg.ScanSerialNumberLen));
                WriteLocalMulti(_cfg.ScanSerialNumberAddress, new ushort[len]);
                _currentSerial = "";
            }
            LogHelper.Info("上电初始化：上位机结果寄存器已全部复位为 0（扫码结果 + " +
                _cameraResultAddrs.Count + " 个相机通道 + SN 区）");
        }

        /// <summary>
        /// 清掉从站监听资源，强制下次 EnsureConnected 完整重建。
        /// 【必须在 lock 内调用】
        /// ★ V2.14.23 热更断连修复：必须调用 _network.Dispose() 而不能只 Stop listener——
        ///   NModbus 3.0.83 的 ModbusTcpSlaveNetwork.Dispose() 会关闭所有已连入的 PLC 主站 TCP 会话
        ///   （ModbusMasterTcpConnection）。旧代码只 _listener.Stop() + _cts.Cancel()，已 accept 的
        ///   master socket 不会被关闭，PLC 主站误以为连接仍活着、不重连新从站 → 热更后黄灯常亮、
        ///   请求收不到（详见 EnsureConnected 顶部注释）。这里 Dispose 掉旧网络，PLC 立即断连重连。
        /// </summary>
        private void ResetConnection()
        {
            _listening = false;
            try { _cts?.Cancel(); } catch { }
            try { _network?.Dispose(); } catch { }    // 关旧从站网络（含全部 master 连接 + listener）
            try { _listener?.Stop(); } catch { }      // 双保险：network.Dispose 内部已 Stop listener
            StopMasterPoll();
            _cts = null; _listener = null; _network = null; _slave = null; _dataStore = null;
        }

        public void Dispose()
        {
            _disposed = true;
            // 限时抢锁：后台监听线程可能正阻塞在 ListenAsync 上，Cancel 会让其退出
            if (Monitor.TryEnter(_lock, TimeSpan.FromMilliseconds(300)))
            {
                try
                {
                    ResetConnection();
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
            else
            {
                LogHelper.Warn("PLC Dispose 未能拿到锁（后台监听繁忙），改走锁外强停");
                try { _cts?.Cancel(); } catch { }
                try { _network?.Dispose(); } catch { }    // V2.14.23：锁外同样要关旧网络（含 master 连接）
                try { _listener?.Stop(); } catch { }
                StopMasterPoll();
            }
            try { _masterPollTimer?.Dispose(); } catch { }
        }
    }
}
