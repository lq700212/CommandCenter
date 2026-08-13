using System;
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
    ///   PLC 只写（上位机读）：40001 扫码请求(0/1)、40002 上相机拍照请求(1~255=点位)、40003 下相机拍照请求；
    ///   PLC 只读（上位机写）：40004 扫码结果、40005 上相机结果、40006 下相机结果、
    ///                         40007~40011 产品型号(10 字符 ASCII，每寄存器 2 字符，高字节在前)。
    ///   一次完整握手：PLC 写请求≠0 → 上位机处理完写结果≠0 → PLC 读结果(相机还读型号)并复位请求=0
    ///   → 上位机看到请求回 0 再复位结果=0，进入下一请求。扫描/上相机/下相机三个通道互斥串行处理。
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
    /// 【对外接口】ReadScanRequest/ReadCamUpRequest/ReadCamDownRequest（读 PLC 请求）、
    ///   WriteScanResult/WriteCamUpResult/WriteCamDownResult/WriteProductModel（写结果/型号）、
    ///   ReadRegister/WriteRegister（功能测试通用读写）。语义：IsConnected/EnsureConnected 表示
    ///   "从站监听是否已就绪"，HasMasterConnected 表示"汇川主站是否已 TCP 连入"。
    /// </summary>
    public class PlcService : IDisposable
    {
        private readonly PlcConfig _cfg;
        private readonly object _lock = new object();

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
                try { _cts?.Cancel(); } catch { }
                try { _listener?.Stop(); } catch { }
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
                    LogHelper.Info($"PLC 从站监听已启动 {ip}:{_cfg.Port}（UnitId={_cfg.UnitId}），等待汇川主站连入");
                    return true;
                }
                catch (Exception ex)
                {
                    SetConnected(false);
                    StopMasterPoll();
                    try { _cts?.Cancel(); } catch { }
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
        /// 读取扫码请求（V2.7，PLC 写 40001）：返回是否 PLC 请求扫码。
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

        /// <summary>读取上相机拍照请求（V2.7，PLC 写 40002）：返回点位编号（1~255），0=无请求。</summary>
        public bool ReadCamUpRequest(out int stationNo)
        {
            stationNo = 0;
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) return false;
                stationNo = ReadLocal(_cfg.CamUpRequestAddress);
                return true;
            }
        }

        /// <summary>读取下相机拍照请求（V2.7，PLC 写 40003）：返回点位编号（1~255），0=无请求。</summary>
        public bool ReadCamDownRequest(out int stationNo)
        {
            stationNo = 0;
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) return false;
                stationNo = ReadLocal(_cfg.CamDownRequestAddress);
                return true;
            }
        }

        /// <summary>写扫码结果（V2.7，上位机写 40004，PLC 来读）：0=默认/复位，1=扫码OK，2=扫码NG。</summary>
        public void WriteScanResult(int code) => WriteLocalSafe(_cfg.ScanResultAddress, (ushort)code);

        /// <summary>写上相机拍照结果（V2.7，上位机写 40005，PLC 来读）：0=默认/复位，1=OK，2=NG，3=点位禁用跳过。</summary>
        public void WriteCamUpResult(int code) => WriteLocalSafe(_cfg.CamUpResultAddress, (ushort)code);

        /// <summary>写下相机拍照结果（V2.7，上位机写 40006，PLC 来读）：取值同上相机结果。</summary>
        public void WriteCamDownResult(int code) => WriteLocalSafe(_cfg.CamDownResultAddress, (ushort)code);

        /// <summary>
        /// 写产品型号字符串（V2.7，上位机写 40007~40011，PLC 来读）。
        /// 编码：每寄存器存 2 个 ASCII 字符，高字节=前字符、低字节=后字符；最多写
        /// ProductModelLen×2 个字符，不足的尾部补 0x00（PLC 以 0x00 作字符串结束符）。
        /// 型号为空时整段写 0（PLC 读到空型号），不崩。
        /// </summary>
        /// <param name="model">产品型号（如 "Z1212"），超长自动截断</param>
        /// <returns>从站就绪(true)/未就绪(false)</returns>
        public bool WriteProductModel(string model)
        {
            lock (_lock)
            {
                if (_dataStore?.HoldingRegisters == null) return false;
                int len = Math.Max(1, Math.Min(20, _cfg.ProductModelLen)); // 寄存器数 1~20，防异常配置
                ushort[] regs = new ushort[len];
                byte[] bytes = Encoding.ASCII.GetBytes(model ?? "");       // 空型号→全 0
                int charCount = Math.Min(bytes.Length, len * 2);
                for (int i = 0; i < charCount; i++)
                {
                    ushort v = bytes[i]; // 单字节 ASCII，直接放进高字节；低字节留 0x00
                    if (i % 2 == 0)
                        regs[i / 2] = (ushort)(v << 8); // 高字节=前一字符
                    else
                        regs[i / 2] |= v;                // 低字节=后一字符
                }
                WriteLocalMulti(_cfg.ProductModelAddress, regs);
                return true;
            }
        }

        // ──────────────── 通用 D 地址读写（DevTestForm 功能测试用）────────────────
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
        /// ★ 地址约定（V2.7 文档确认无偏移）：NModbus PointSource.ReadPoints(start, count) 的 start
        ///   是 0-based 协议地址，PLC 主站写/读的地址号与它一一对应、零换算——PLC 写 40001，
        ///   上位机 ReadPoints(40001) 即读到（与 V1.12.14 现场实测 D 地址零偏移同规则），
        ///   配置里的 40001~40011 直接作为 start 使用，无需 ±40001/±1 换算。若将来换 PLC 出现错位，
        ///   统一在此处调整（如 start = address - 40001），业务层无感。
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
        /// 清掉从站监听资源，强制下次 EnsureConnected 完整重建。
        /// 【必须在 lock 内调用】
        /// </summary>
        private void ResetConnection()
        {
            _listening = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
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
                try { _listener?.Stop(); } catch { }
                StopMasterPoll();
            }
            try { _masterPollTimer?.Dispose(); } catch { }
        }
    }
}
