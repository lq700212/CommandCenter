using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    ///   所有"上位机→PLC"的数据都靠 PLC 主站轮询来读上位机寄存器区。配方下发亦然——
    ///   上位机写配方号+标志位到自己寄存器区，PLC 轮询读到标志位=1 后读配方号、切换、写 0 回执。
    ///
    /// 【握手寄存器区（沿用原 D 地址，读写方向反转）】
    ///   D100 到位信号：PLC 主站写 1 → 上位机 ReadMoveDone() 读自己 DataStore 后 ClearMoveDone() 清 0；
    ///   D101 开始信号：上位机 SetStartSignal() 写自己区，PLC 来读；
    ///   D102 完成信号：上位机 SetDone(1=成功/2=取像异常) 写自己区，PLC 来读；
    ///   D103~D(103+len-1) 配方号：上位机 WriteRecipe() 写自己区(ASCII 每寄存器 2 字符)；
    ///   D108 配方更新标志：上位机写 1(有新配方待切换)，PLC 读走后写 0 回执；
    ///   D110 总数 / D111 OK / D112 NG：上位机 ReportCounts() 写自己区，PLC 来读。
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
    ///   业务层(ProductionCoordinator)仍用 PositionTimer 每 200ms 轮询 ReadMoveDone()，等价于原主动读 PLC。
    ///
    /// 【对外接口签名保持不变】EnsureConnected/IsConnected/ConnectionChanged/ReadMoveDone/ClearMoveDone/
    ///   SetStartSignal/SetDone/WriteRecipe/ReportCounts/ReadRegister/WriteRegister 全部保留原签名，
    ///   调用方(Coordinator/MainForm/DevTestForm/Monitor)改动最小。
    ///   语义变化：IsConnected/EnsureConnected 从"已连上 PLC"变为"从站监听已就绪"。
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
        /// </summary>
        private void MasterPollTick(object state)
        {
            if (_disposed) return;
            bool has = false;
            try
            {
                var nw = _network;
                if (nw != null && nw.Masters != null && nw.Masters.Count > 0) has = true;
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

        // ════════════════ 业务握手方法（签名不变，底层改读写自己 DataStore）════════════════

        /// <summary>
        /// 读取相机到位信号寄存器（D 地址，现读自己 DataStore）。
        /// 返回 true 表示 PLC 主站已写 1 告知"相机运动到位"。读到后应尽快 ClearMoveDone() 复位。
        /// </summary>
        public bool ReadMoveDone()
        {
            lock (_lock)
                return ReadLocal(_cfg.MoveDoneAddress) != 0;
        }

        /// <summary>把到位信号写 0 复位（写自己 DataStore），防止同一信号被重复处理。</summary>
        public void ClearMoveDone()
        {
            lock (_lock)
                WriteLocal(_cfg.MoveDoneAddress, 0);
        }

        /// <summary>
        /// 读取"扫码枪运动到位"信号（V1.12.16 两阶段流程新增，D 地址，读自己 DataStore）。
        /// 返回 true 表示 PLC 主站已写 1 告知"机器人带扫码枪到位、可以扫码"。
        /// 读到并扫完 SN 后应尽快 ClearScanMoveDone() 复位，流程才进入"等相机到位"阶段。
        /// </summary>
        public bool ReadScanMoveDone()
        {
            lock (_lock)
                return ReadLocal(_cfg.ScanMoveDoneAddress) != 0;
        }

        /// <summary>把"扫码枪到位"信号写 0 复位（自己 DataStore），防止同一信号被重复处理。</summary>
        public void ClearScanMoveDone()
        {
            lock (_lock)
                WriteLocal(_cfg.ScanMoveDoneAddress, 0);
        }

        /// <summary>通知 PLC 开始工作（开始信号置 1，写自己 DataStore，PLC 来读）。</summary>
        public void SetStartSignal(bool on = true) => WriteLocalSafe(_cfg.StartSignalAddress, (ushort)(on ? 1 : 0));

        /// <summary>通知 PLC 拍照完成（写自己 DataStore，PLC 来读）。code：1=成功，2=取像失败，0=复位。</summary>
        public void SetDone(int code) => WriteLocalSafe(_cfg.DoneSignalAddress, (ushort)code);

        /// <summary>
        /// 把配方号写到自己寄存器区（D RecipeAddress 起始的连续寄存器，ASCII 数字串每字 2 字符），
        /// 并置配方更新标志位(D108)=1，PLC 主站轮询读到标志位后读配方号、切换、写 0 回执。
        /// </summary>
        /// <param name="recipeId">配方编号，如 1</param>
        public bool WriteRecipe(int recipeId)
        {
            string text = recipeId.ToString();
            // RecipeLen 配置支持 1~20（AppConfig 注释约定）；超界按 20 截断，
            // 防异常配置（如手改成 65535）分配超大数组 + 写入越界被静默吞掉（V1.12.13）。
            int len = Math.Max(1, Math.Min(20, (int)_cfg.RecipeLen));
            ushort[] regs = new ushort[len]; // 先填空格，再写 ASCII
            for (int i = 0; i < len; i++)
            {
                int start = i * 2;
                byte hi = 0x20, lo = 0x20; // 空格
                if (start < text.Length) hi = (byte)text[start];
                if (start + 1 < text.Length) lo = (byte)text[start + 1];
                regs[i] = (ushort)((hi << 8) | lo); // 高字节在前
            }
            lock (_lock)
            {
                // 从站未就绪（监听没起来/DataStore 为 null）：配方根本没写进去，返回 false
                if (_dataStore?.HoldingRegisters == null) return false;
                // 先写配方号，再置标志位（顺序重要：避免 PLC 读到标志位=1 时配方号还没写完）
                WriteLocalMulti(_cfg.RecipeAddress, regs);
                WriteLocal(_cfg.RecipeFlagAddress, 1);
            }
            // 从站模式：写入本地 DataStore 一定成功，但"PLC 能否立即拉取"取决于主站是否已连入。
            // 主站未连入时返回 false，让界面如实提示"已缓存待拉取"（而非误报"已下发 PLC"），
            // 否则操作员会误以为配方已切到 PLC 而实际主站断着（V1.12.13）。
            return HasMasterConnected;
        }

        /// <summary>
        /// 上报检测计数到上位机自己寄存器区（总数/OK/NG 三个寄存器，PLC 主站来读）。
        /// 从站模式下写入本地 DataStore 一定成功，这里仍保留日志结构以兼容原语义。
        /// </summary>
        public void ReportCounts(int total, int ok, int ng)
        {
            lock (_lock)
            {
                WriteLocal(_cfg.TotalCountAddress, (ushort)total);
                WriteLocal(_cfg.OkCountAddress, (ushort)ok);
                WriteLocal(_cfg.NgCountAddress, (ushort)ng);
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
        /// ★ 地址偏移：NModbus PointSource.ReadPoints(start, count) 的 start 是 0-based 协议地址，
        ///   与原主站 ReadHoldingRegisters(UnitId, address, 1) 的 address 一致，故直接传 address（不加 1）。
        ///   【V1.12.14 现场实测确认】汇川主站读地址与 D 地址一一对应、零偏移（写 D101 读 101 即见），
        ///   此处直接传 D 地址即为正确做法，无需任何 +40001/±1 换算；若将来换 PLC 出现错位，
        ///   统一在此处调整，业务层无感。
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
