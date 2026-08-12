using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CommandCenter.Models;
using CommandCenter.Utils;
using NModbus;

namespace CommandCenter.Services
{
    /// <summary>
    /// PLC 通讯服务：与汇川 PLC(Modbus TCP 从站) 交互。
    ///
    /// 【对接说明】(汇川 D 寄存器 → Modbus 保持寄存器)
    ///   汇川把 D 区映射到 Modbus 4x 保持寄存器区，D0→40001、D100→40101……对应绝对地址 = 40001 + D地址。
    ///   NModbus 的 ReadHoldingRegisters 起始地址就是"相对地址"（D 地址），库内部已按功能码 03 处理，无需 +40001。
    ///   本类统一暴露 "D 地址"（即 AppConfig.PlcConfig 里填的数值），减少现场换算。
    ///
    /// 【握手流程】(配合 MainForm 使用)
    ///   ① 上位机定时 ReadMoveDone() 轮询，读到 1 表示"PLC 告知相机运动到位"；
    ///   ② 读到后立即清 0（握手复位，防止重复触发），再触发相机拍照；
    ///   ③ 照片保存成功写 SetDone(1)，取像失败写 SetDone(2)；
    ///   ④ 切换配方时 WriteRecipe(配方号) 到 RecipeAddress 起始的连续寄存器。
    ///
    /// 【线程安全】多为单线程轮询 + 偶发写，用 _lock 把每个完整操作串行化，
    ///   避免 NModbus 底层 TcpClient 被并发读写导致 IOException。
    /// </summary>
    public class PlcService : IDisposable
    {
        private readonly PlcConfig _cfg;
        private TcpClient _tcp;
        private IModbusMaster _master;
        private readonly object _lock = new object();

        /// <summary>已释放标记：Dispose 后任何后台重连动作立即放弃（volatile 跨线程可见）</summary>
        private volatile bool _disposed;

        /// <summary>连接状态变化事件（UI 可订阅刷新指示灯）</summary>
        public event EventHandler<bool> ConnectionChanged;

        /// <summary>当前是否已连接</summary>
        public bool IsConnected { get; private set; }

        public PlcService(PlcConfig cfg) => _cfg = cfg;

        /// <summary>日志/界面区分用标签：IP:端口（多设备时能分清是哪台）</summary>
        public string IpLabel => $"{_cfg.IpAddress}:{_cfg.Port}";

        private bool _lastFailed; // 上一次连接是否失败（用于日志降噪）

        /// <summary>
        /// 确保已连接：未连接或连接断开时重建 TcpClient 与 Modbus 主站。
        /// 返回 true 表示连接可用。内部不抛异常。
        /// ★ 关键 ①：Connect 用 BeginConnect + WaitOne 强制超时（_cfg.TimeoutMs），否则对不可达 IP，
        ///   TcpClient.Connect 默认会卡几十秒，把调用线程（尤其 UI 线程）整个冻住 —— 这就是"软件卡"的元凶之一。
        /// ★ 关键 ②：整个方法体用 lock(_lock)：到位轮询(后台线程) 与 收尾写信号(其他线程) 会并发走到这里，
        ///   若不互斥，一个线程 Close/重建 _tcp 时，另一个线程会拿到已关闭的旧引用去 EndConnect，
        ///   造成 NullReferenceException。
        /// </summary>
        public bool EnsureConnected()
        {
            if (_disposed) return false; // 已释放：后台心跳/轮询/重连直接放弃
            // 与 SafeRead/SafeWrite 共用同一把锁；C# lock 可重入，嵌套调用不死锁
            lock (_lock)
            {
                try
                {
                    if (_tcp != null && _tcp.Connected && _master != null)
                        return true;

                    _tcp?.Close();
                    _tcp = new TcpClient();
                    _tcp.ReceiveTimeout = _cfg.TimeoutMs;
                    _tcp.SendTimeout = _cfg.TimeoutMs;
                    string err;
                    if (!TryConnect(_tcp, _cfg.IpAddress, _cfg.Port, _cfg.TimeoutMs, out err))
                        throw new Exception(err);

                    var factory = new ModbusFactory();
                    _master = factory.CreateMaster(_tcp);
                    _master.Transport.Retries = 1;          // 失败重发 1 次
                    _master.Transport.ReadTimeout = _cfg.TimeoutMs;
                    _master.Transport.WriteTimeout = _cfg.TimeoutMs;

                    _lastFailed = false;
                    SetConnected(true);
                    LogHelper.Info($"PLC 连接成功 {_cfg.IpAddress}:{_cfg.Port}");
                    return true;
                }
                catch (Exception ex)
                {
                    SetConnected(false);
                    // 清理本次尝试的连接，避免 _tcp/_master 残留失效引用（下次 EnsureConnected 必然完整重建）
                    try { _master?.Dispose(); } catch { }
                    _master = null;
                    try { _tcp?.Close(); } catch { }
                    _tcp = null;
                    // 连接失败只记一条日志，避免后台轮询刷屏（每秒几条会撑爆日志文件）
                    if (!_lastFailed)
                    {
                        _lastFailed = true;
                        LogHelper.Warn($"PLC 连接失败 {_cfg.IpAddress}:{_cfg.Port}，原因：{ex.Message}");
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// 带超时的 TCP 连接（无回调式，对齐 AgingTestSystem）：不抛异常，改返回 bool + 原因。
        /// 1) BeginConnect(ip, port, null, null) —— 不注册回调线程；
        /// 2) AsyncWaitHandle.WaitOne(超时) 等待连接结束，超时返回失败；
        /// 3) 【根治 NRE】EndConnect 前检查 tcp.Client == null：若连接期间被并发
        ///    Close（Close 把内部 socket 置 null），绝不能再碰 EndConnect，否则对
        ///    已释放对象调用会抛 NullReferenceException（此前 EndConnect 报 NRE
        ///    正是"WaitOne 返回后、对已被并发关闭的 client 调 EndConnect"竞态路径）。
        /// </summary>
        private static bool TryConnect(TcpClient tcp, string ip, int port,
                                       int timeoutMs, out string error)
        {
            error = null;
            try
            {
                IAsyncResult ar = tcp.BeginConnect(ip, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    error = $"连接 {ip}:{port} 超时（{timeoutMs}ms）";
                    return false;
                }
                // 并发清理已把内部 socket 置 null → 定义为"已被释放"，放弃 EndConnect
                if (tcp.Client == null)
                {
                    error = $"连接 {ip}:{port} 已被并发释放，放弃收尾";
                    return false;
                }
                tcp.EndConnect(ar); // 连接失败时这里抛 SocketException，由 catch 收敛
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
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

        /// <summary>
        /// 清掉失效的连接引用，强制下次 EnsureConnected 完整重建。
        /// 【必须在 lock(_lock) 内调用】所有读写方法已持锁；EnsureConnected 亦可重入，不死锁。
        /// 【为什么必须清（V1.7.2 修复）】Modbus 通讯失败（超时/断流/协议错位）后，TcpClient.Connected
        ///   只是"缓存的状态"、仍可能为 true。若只 SetConnected(false) 不清 _tcp/_master，
        ///   下次 EnsureConnected 会直接复用坏 master → 反复失败且永不重建，现场表现为
        ///   "连上了但一直读不到正确值"。
        /// </summary>
        private void ResetConnection()
        {
            try { _master?.Dispose(); } catch { }
            _master = null;
            try { _tcp?.Close(); } catch { }
            _tcp = null;
        }

        /// <summary>
        /// 读取到位信号寄存器（D 地址）。
        /// 返回 true 表示 PLC 告知"相机运动到位"。读到后应尽快调用 ClearMoveDone() 复位。
        /// </summary>
        public bool ReadMoveDone()
        {
            return SafeRead(_cfg.MoveDoneAddress, out ushort v) && v != 0;
        }

        /// <summary>把到位信号写 0 复位，防止同一信号被重复处理。</summary>
        public void ClearMoveDone()
        {
            SafeWrite(_cfg.MoveDoneAddress, 0);
        }

        /// <summary>通知 PLC 开始工作（触发信号置 1）。</summary>
        public void SetStartSignal(bool on = true) => SafeWrite(_cfg.StartSignalAddress, (ushort)(on ? 1 : 0));

        /// <summary>
        /// 通知 PLC 拍照完成。code：1=成功，2=取像失败，0=复位。
        /// </summary>
        public void SetDone(int code) => SafeWrite(_cfg.DoneSignalAddress, (ushort)code);

        /// <summary>
        /// 把配方号写到 PLC：(RecipeAddress) 起始的连续寄存器。
        /// 采用"ASCII 数字串"方式每寄存器 2 字符写入，PLC 侧按字符串解析，直观好对应。
        /// </summary>
        /// <param name="recipeId">配方编号，如 1</param>
        public bool WriteRecipe(int recipeId)
        {
            string text = recipeId.ToString();
            int len = Math.Max(1, (int)_cfg.RecipeLen);
            ushort[] regs = new ushort[len]; // 先填空格，再写 ASCII

            for (int i = 0; i < len; i++)
            {
                int start = i * 2;
                byte hi = 0x20, lo = 0x20; // 空格
                if (start < text.Length) hi = (byte)text[start];
                if (start + 1 < text.Length) lo = (byte)text[start + 1];
                regs[i] = (ushort)((hi << 8) | lo); // 高字节在前
            }
            return SafeWriteMulti(_cfg.RecipeAddress, regs);
        }

        /// <summary>
        /// 上报检测计数：总数 / OK / NG 三个寄存器。
        /// 【V1.8.3 修复】逐个写并收集成功与否——此前三连写不校验返回值，任一个失败都静默
        /// （现场台账会悄悄少记数）；现在任一失败都会记一条 Warn，便于现场发现并排查。
        /// </summary>
        public void ReportCounts(int total, int ok, int ng)
        {
            bool tOk = SafeWrite(_cfg.TotalCountAddress, (ushort)total);
            bool oOk = SafeWrite(_cfg.OkCountAddress, (ushort)ok);
            bool nOk = SafeWrite(_cfg.NgCountAddress, (ushort)ng);
            if (!(tOk && oOk && nOk))
                LogHelper.Warn($"计数上报未全部成功：总数={tOk} OK={oOk} NG={nOk}（PLC 通讯不稳定或寄存器越界）");
        }

        // ──────────────── 通用 D 地址读写（V1.12.0，功能测试窗体用）────────────────
        // 功能测试窗体（DevTestForm）要"读/写任意 D 寄存器"验证 PLC 逻辑，
        // 因此把内部 SafeRead/SafeWrite 以公开方法形式暴露，地址由调用方指定。
        // 直接复用本服务已建立的连接（EnsureConnected），不额外建连。

        /// <summary>通用读：读取指定 D 地址的单个保持寄存器。返回 true 表示通讯成功。</summary>
        public bool ReadRegister(ushort dAddress, out ushort value) => SafeRead(dAddress, out value);

        /// <summary>通用写：写入指定 D 地址的单个保持寄存器。返回 true 表示通讯成功。</summary>
        public bool WriteRegister(ushort dAddress, ushort value) => SafeWrite(dAddress, value);

        private bool SafeRead(ushort address, out ushort value)
        {
            value = 0;
            try
            {
                if (!EnsureConnected()) return false;
                lock (_lock)
                {
                    value = _master.ReadHoldingRegisters(_cfg.UnitId, address, 1)[0];
                    return true;
                }
            }
            catch (Exception ex)
            {
                SetConnected(false);
                ResetConnection(); // 连接已不可信：清引用，下次 EnsureConnected 强制重建
                LogHelper.Warn($"读 PLC 寄存器 D{address} 失败：{ex.Message}");
                return false;
            }
        }

        private bool SafeWrite(ushort address, ushort value)
        {
            try
            {
                if (!EnsureConnected()) return false;
                lock (_lock)
                    _master.WriteSingleRegister(_cfg.UnitId, address, value);
                return true;
            }
            catch (Exception ex)
            {
                SetConnected(false);
                ResetConnection(); // 连接已不可信：清引用，下次 EnsureConnected 强制重建
                LogHelper.Warn($"写 PLC 寄存器 D{address} 失败：{ex.Message}");
                return false;
            }
        }

        private bool SafeWriteMulti(ushort address, ushort[] values)
        {
            try
            {
                if (!EnsureConnected()) return false;
                lock (_lock)
                    _master.WriteMultipleRegisters(_cfg.UnitId, address, values);
                return true;
            }
            catch (Exception ex)
            {
                SetConnected(false);
                ResetConnection();
                LogHelper.Warn($"批量写 PLC 寄存器 D{address} 失败：{ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _disposed = true; // 先置标志：后台重连/轮询下一次迭代立即放弃，不再碰 _tcp/_master
            // 用"限时抢锁"而非 lock：后台 EnsureConnected 可能正持锁做 TryConnect（最多等
            // 一个 TimeoutMs），但绝不让自己（UI 关窗线程）无限期等下去。
            // 拿不到锁时依旧执行"锁外强断网"兜底：_tcp.Close() 会让持锁线程的
            // BeginConnect 立刻结束（WaitOne 返回，TryConnect 内部 tcp.Client==null 或
            // EndConnect 抛 SocketException 均被其 catch 收敛），随后它自会释放锁退出。
            if (Monitor.TryEnter(_lock, TimeSpan.FromMilliseconds(300)))
            {
                try
                {
                    try { _master?.Dispose(); } catch { }
                    _master = null;
                    try { _tcp?.Close(); } catch { }
                    _tcp = null;
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
            else
            {
                LogHelper.Warn("PLC Dispose 未能拿到锁（后台连接任务繁忙），改走锁外强断网");
                try { _tcp?.Close(); } catch { }
                try { _master?.Dispose(); } catch { }
            }
        }
    }
}