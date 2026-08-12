using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Services
{
    /// <summary>
    /// 扫码枪服务（以太网 TCP/IP 无协议版，V1.8.0）：基恩士 SR 系列扫码枪。
    ///
    /// 【对接方式（基恩士 SR 系列无协议通讯，以《SR 系列通信指南》为准）】
    ///   扫码枪在无协议模式下作为 TCP 服务器监听端口，上位机作为 TCP 客户端连入；
    ///   但基恩士 SR 无协议模式并不是"连上就回数据"：多数机型需上位机先发一条
    ///   **打开激光/开始读取**的指令（本现场实测为 `LON`，帧尾 CRLF 结束），扫码枪
    ///   才会进入读码状态，之后每读到一条条码就主动推送一行文本（通常 + CR/LF）。
    ///   因此本服务在**每次连接成功后自动发送触发指令**（ScanConfig.TriggerCommand，
    ///   默认 "LON"）；断线自动重连后也会再次发送，保证扫码枪始终处于可读状态。
    ///
    /// 【线程模型】本类自持一个后台线程做"连接 + 阻塞读流"：
    ///   - Open() 只启动线程，立即返回，绝不在 UI 线程做网络 IO；
    ///   - 连接用 BeginConnect + WaitOne 强制超时（防不可达 IP 卡线程，对齐项目铁律）；
    ///   - 断连后按节流（3s）静默自动重连，连上后恢复收码，全程不打扰主流程；
    ///   - 收码在专用读线程，抛 SerialNumberScanned 事件，UI 订阅方自行 Invoke。
    ///
    /// 【为什么阻塞读 + Close 打断】读线程阻塞在 NetworkStream.Read 上等条码，不设 ReadTimeout
    ///   （设了会导致每 500ms 周期性误判断线）；Dispose/断流时 Close socket 会让 Read 立即返回
    ///   0 或抛异常，线程自然退出或进入重连分支。
    /// </summary>
    public class ScannerTcpService : IScanner
    {
        private readonly ScanConfig _cfg;
        private readonly object _lock = new object();
        private TcpClient _tcp;
        private NetworkStream _stream;
        private Thread _thread;
        private DateTime _lastAttempt = DateTime.MinValue;
        private readonly StringBuilder _line = new StringBuilder();
        private bool _connectedEver;   // 是否成功连上过（重连失败日志降噪）
        private volatile bool _disposed;
        private bool _connected;        // 当前连接状态缓存，用于 ConnectionChanged 边沿检测（状态没变不发事件）

        /// <summary>重连节流间隔（毫秒）</summary>
        private const int ReconnectMs = 3000;

        /// <summary>TCP 连接超时（毫秒）</summary>
        private const int ConnectTimeoutMs = 2000;

        /// <summary>单条条码最大长度（防御异常数据撑爆内存）</summary>
        private const int MaxLineLen = 512;

        /// <summary>扫到一条完整条码的事件（参数为条码文本，工作线程触发，UI 需 Invoke）</summary>
        public event EventHandler<string> SerialNumberScanned;

        /// <summary>
        /// 连接状态变化事件（V1.12.5）：连接成功触发 true、断线触发 false（边沿检测，状态没变不发）。
        /// 工作线程触发，UI 订阅方需自行 Invoke。功能测试窗体的扫码枪状态灯靠它实时刷新——
        /// 此前 IScanner 没有此事件，状态灯只初始刷新一次永远停"断连"，用户会误以为连不上，
        /// 实际已连上（例：调试助手占用端口时连不上，关掉后程序自动连上，界面上却仍显示断连）。
        /// </summary>
        public event EventHandler<bool> ConnectionChanged;

        public ScannerTcpService(ScanConfig cfg) => _cfg = cfg;

        /// <summary>是否已连接（供界面/日志判断，非主要状态来源）</summary>
        public bool IsOpen
        {
            get
            {
                lock (_lock)
                {
                    try { return _tcp != null && _tcp.Connected; }
                    catch { return false; }
                }
            }
        }

        /// <summary>启动后台连接与读取线程。幂等：重复调用不叠加线程。
        /// 立即返回 true（实际连接在后台线程异步进行，失败自动重连，不阻塞调用方）。
        /// 【V1.8.3 修复】未启用（Enabled=false）的扫码枪直接返回 false 不启线程，
        ///   与串口实现 ScannerService.Open 的 `if (!_cfg.Enabled) return false;` 行为对齐，
        ///   否则被禁用但仍留在配置里的 TCP 扫码枪也会白白起一个后台连接线程。</summary>
        public bool Open()
        {
            if (_disposed || _thread != null) return false;
            if (!_cfg.Enabled) return false; // 未启用：不起后台线程，行为对齐串口实现
            _thread = new Thread(Worker) { IsBackground = true, Name = "ScannerTcp" };
            _thread.Start();
            LogHelper.Info($"扫码枪(TCP)启动：{_cfg.IpAddress}:{_cfg.Port}");
            return true;
        }

        /// <summary>
        /// 后台主循环：已连接→阻塞读流收条码；未连接→按节流重连。
        /// 断流（Read 返回 0/异常）后进入重连分支，直到 Dispose 或连上。
        /// </summary>
        private void Worker()
        {
            while (!_disposed)
            {
                NetworkStream stream;
                lock (_lock)
                {
                    // 已连接且流可用：拿去读
                    if (_tcp != null && _tcp.Connected && _stream != null)
                    {
                        stream = _stream;
                    }
                    else if ((DateTime.Now - _lastAttempt).TotalMilliseconds >= ReconnectMs)
                    {
                        // 未连接且过了节流期：尝试连接（最多阻塞 ConnectTimeoutMs）
                        _lastAttempt = DateTime.Now;
                        stream = TryConnect();
                    }
                    else
                    {
                        stream = null; // 节流期内：歇一下再试
                    }
                }

                if (stream == null)
                {
                    Thread.Sleep(200);
                    continue;
                }

                // 阻塞读流：直到断流/超时/Dispose。返回后下一轮循环自动重连。
                ReadLoop(stream);
            }
        }

        /// <summary>
        /// 连接状态变化（边沿检测）：仅状态真正改变时触发一次 ConnectionChanged（对齐 PLC/相机）。
        /// 线程安全说明：可能由 Worker 线程（连接成功/断流）与 UI 线程（Dispose）调用，
        /// 事件订阅方负责 Invoke；b 为最新的连接状态。
        /// </summary>
        private void SetConnected(bool value)
        {
            if (_connected != value)
            {
                _connected = value;
                ConnectionChanged?.Invoke(this, value);
            }
        }

        /// <summary>
        /// 尝试建立 TCP 连接（在 Worker 线程内调用，最多阻塞 ConnectTimeoutMs）。
        /// 成功返回流并缓存 _tcp/_stream；失败返回 null（日志降噪：只记首次失败）。
        /// </summary>
        private NetworkStream TryConnect()
        {
            TcpClient tcp = null;
            try
            {
                tcp = new TcpClient();
                // BeginConnect + WaitOne 强制超时：对不可达 IP 最多等 2s，不卡调用线程
                IAsyncResult ar = tcp.BeginConnect(_cfg.IpAddress, _cfg.Port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
                {
                    // V1.12.6：连接失败也通知订阅方（对齐 PLC/相机"连接失败→状态灯变红"）。
                    // 边沿检测：_connected 已是 false 时不重复发事件，无日志循环负担。
                    SetConnected(false);
                    try { tcp.Close(); } catch { }
                    return null;
                }
                // 若连接期间被并发 Close（内部 socket 置 null），放弃 EndConnect 防空引用
                if (tcp.Client == null)
                {
                    // V1.12.6：同超时分支，失败即通知（对齐 PLC/相机），边沿检测防重复事件。
                    SetConnected(false);
                    try { tcp.Close(); } catch { }
                    return null;
                }
                tcp.EndConnect(ar);
                tcp.NoDelay = true;
                var stream = tcp.GetStream();
                _tcp = tcp;
                _stream = stream;
                _connectedEver = true;
                LogHelper.Info($"扫码枪(TCP)已连接 {_cfg.IpAddress}:{_cfg.Port}");
                // V1.12.5：通知订阅方（功能测试窗体状态灯）连接已成功。放日志后、发触发指令前，
                // 保证状态第一时间变绿；触发指令发送失败会走断流分支自动重连，状态灯随后转红。
                SetConnected(true);
                // V1.12.0：连上即发触发指令（基恩士 SR 的 LON 打开激光），否则扫码枪不读码。
                // 断开重连后也会再次发送（每次连上走一遍此分支），保证始终处于可读状态。
                SendTrigger();
                return stream;
            }
            catch
            {
                try { tcp?.Close(); } catch { }
                _tcp = null;
                _stream = null;
// 连接失败只记一条日志（重连期间静默，避免每 3s 刷一行）
                    if (!_connectedEver)
                        LogHelper.Warn($"扫码枪(TCP)连接失败 {_cfg.IpAddress}:{_cfg.Port}（后台持续重连）");
                    // V1.12.6：失败也通知订阅方（对齐 PLC/相机"连接失败→状态灯变红"）。
                    // 边沿检测：状态没变（已是 false）不发重复事件，重连期间无事件风暴。
                    SetConnected(false);
                    return null;
            }
        }

        /// <summary>
        /// 阻塞读流并按行切分条码。Read 在此阻塞等数据；断流返回 0、异常或 Dispose 时退出。
        /// 行结束符兼容 CR / LF / CRLF（对齐串口实现）；行首多余的换行符不产生空条码。
        /// </summary>
        private void ReadLoop(NetworkStream stream)
        {
            var one = new byte[1];
            try
            {
                while (!_disposed)
                {
                    int n = stream.Read(one, 0, 1);
                    if (n <= 0) break;                       // 对端关闭：断线
                    char c = (char)one[0];
                    if (c == '\r' || c == '\n')
                    {
                        // 一行结束 = 一条条码（CR/LF/CRLF 都算，行首多余换行不产生空条码）
                        if (_line.Length > 0)
                        {
                            string code = _line.ToString().Trim();
                            _line.Clear();
                            if (code.Length > 0)
                            {
                                LogHelper.Info("扫码枪收到条码：" + code);
                                SerialNumberScanned?.Invoke(this, code);
                            }
                        }
                    }
                    else
                    {
                        _line.Append(c);
                        if (_line.Length > MaxLineLen) _line.Clear(); // 防御异常长数据
                    }
                }
            }
            catch { } // 断流/Dispose 引发的异常：统一走下方清理
            finally
            {
                MarkDown();
            }
        }

        /// <summary>清空失效连接引用（锁内幂等），下一轮 Worker 循环自动重连。
        /// 【V1.12.5 补充】断流时顺便：
        ///   ① 清空半条条码缓存 _line——否则"条码读到一半断线"，残留的半截会与重连后
        ///      新收到的条码拼接成脏数据（现实中一条码 40B 内一次读完概率高，但小概率
        ///      断点在中间就会污染下一条码，属于低级但确实存在的边界 bug）；
        ///   ② 从"已连接"首次变"断开"时打一条边沿日志（不是每次重连都刷日志），
        ///      并触发 ConnectionChanged(false)，让功能测试窗体状态灯实时转红。
        /// </summary>
        private void MarkDown()
        {
            bool wasConnected = _connected;
            lock (_lock)
            {
                try { _stream?.Dispose(); } catch { }
                _stream = null;
                try { _tcp?.Close(); } catch { }
                _tcp = null;
            }
            _line.Clear(); // 半条码缓存清空（_line 只在读线程用，无需加锁）
            // 边沿日志 + 事件：仅当"之前处于已连接"才提示断线（从未连上则不刷日志，连接失败日志已在 TryConnect 降噪）
            if (wasConnected)
            {
                LogHelper.Warn("扫码枪(TCP)连接断开，3s 节流后自动重连");
                SetConnected(false);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            // 【V1.8.3 修复】对齐 PlcService/相机的"限时抢锁 + 锁外强断网"约定：
            //   Worker 线程持锁做 TryConnect 时最多阻塞 ConnectTimeoutMs(2s)，若用无超时
            //   lock(_lock)，不可达 IP 会拖住 UI 关窗线程最多 2s。这里限时 300ms 抢锁，
            //   拿不到锁就锁外 Close socket——Close 会让持锁线程的 BeginConnect 立刻结束
            //   （WaitOne 返回后 tcp.Client==null 或 EndConnect 抛异常，均被其 catch 收敛），
            //   随后它自会释放锁退出，本方法也不会被阻塞。
            if (Monitor.TryEnter(_lock, TimeSpan.FromMilliseconds(300)))
            {
                try
                {
                    CloseConn();
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
            else
            {
                LogHelper.Warn("扫码枪(TCP) Dispose 未能拿到锁（后台连接任务繁忙），改走锁外强断网");
                try { _stream?.Dispose(); } catch { }
                try { _tcp?.Close(); } catch { }  // Close 让读线程的 Read 立即返回/抛异常退出
            }
            // 等读线程退出（短超时，不阻塞关窗）
            var t = _thread;
            if (t != null && t != Thread.CurrentThread)
            {
                try { if (!t.Join(500)) { } } catch { }
            }
            SetConnected(false); // 关闭即断开：通知订阅方状态复位（幂等，已 false 则不发事件）
            LogHelper.Info("扫码枪(TCP)已释放");
        }

        /// <summary>锁内清理连接引用（幂等）。Dispose 的锁内分支专用，避免重复代码。</summary>
        private void CloseConn()
        {
            try { _stream?.Dispose(); } catch { }
            _stream = null;
            try { _tcp?.Close(); } catch { }
            _tcp = null;
        }

        /// <summary>
        /// 发送触发指令（V1.12.0，实现 IScanner.SendTrigger）：
        /// 向扫码枪写配置的 TriggerCommand（默认 "LON"）并补 "\r\n" 帧结束符。
        /// 连接成功后会自动发送一次（见 TryConnect），本方法供界面手动重发（如扫码枪停止读码时点一下）。
        /// 触发指令配置为空则不发送。返回 true 表示指令已发出（不代表扫码枪执行成功）。
        /// </summary>
        public bool SendTrigger()
        {
            if (_disposed || string.IsNullOrEmpty(_cfg.TriggerCommand)) return true;
            NetworkStream stream;
            lock (_lock)
            {
                stream = _stream;
            }
            if (stream == null)
            {
                LogHelper.Warn("扫码枪(TCP)触发指令发送跳过：尚未连接");
                return false;
            }
            try
            {
                string cmd = _cfg.TriggerCommand.Trim() + "\r\n";
                byte[] data = Encoding.ASCII.GetBytes(cmd);
                stream.Write(data, 0, data.Length);
                stream.Flush();
                LogHelper.Info($"扫码枪(TCP)触发指令已发送：{cmd.Trim()}");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Warn($"扫码枪(TCP)触发指令发送失败：{ex.Message}");
                // 发送失败多半连接已断，交给读循环退出后自动重连（重连后会自动重发触发指令）
                return false;
            }
        }
    }
}
