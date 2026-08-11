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
    ///   扫码枪读到条码后把条码文本（通常 + CR/LF 分隔符）主动推送给连接的上位机。
    ///   因此本服务与串口实现行为一致：连上后收文本行，一行 = 一条条码。
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

        /// <summary>重连节流间隔（毫秒）</summary>
        private const int ReconnectMs = 3000;

        /// <summary>TCP 连接超时（毫秒）</summary>
        private const int ConnectTimeoutMs = 2000;

        /// <summary>单条条码最大长度（防御异常数据撑爆内存）</summary>
        private const int MaxLineLen = 512;

        /// <summary>扫到一条完整条码的事件（参数为条码文本，工作线程触发，UI 需 Invoke）</summary>
        public event EventHandler<string> SerialNumberScanned;

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
        /// 立即返回 true（实际连接在后台线程异步进行，失败自动重连，不阻塞调用方）。</summary>
        public bool Open()
        {
            if (_disposed || _thread != null) return false;
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
                    try { tcp.Close(); } catch { }
                    return null;
                }
                // 若连接期间被并发 Close（内部 socket 置 null），放弃 EndConnect 防空引用
                if (tcp.Client == null)
                {
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

        /// <summary>清空失效连接引用（锁内幂等），下一轮 Worker 循环自动重连。</summary>
        private void MarkDown()
        {
            lock (_lock)
            {
                try { _stream?.Dispose(); } catch { }
                _stream = null;
                try { _tcp?.Close(); } catch { }
                _tcp = null;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            lock (_lock)
            {
                try { _stream?.Dispose(); } catch { }
                _stream = null;
                try { _tcp?.Close(); } catch { }  // Close 让读线程的 Read 立即返回/抛异常退出
                _tcp = null;
            }
            // 等读线程退出（短超时，不阻塞关窗）
            var t = _thread;
            if (t != null && t != Thread.CurrentThread)
            {
                try { if (!t.Join(500)) { } } catch { }
            }
            LogHelper.Info("扫码枪(TCP)已释放");
        }
    }
}
