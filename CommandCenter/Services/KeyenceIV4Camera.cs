using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Services
{
    /// <summary>
    /// 相机通讯服务：基恩士 IV4-500CA，走 "TCP/IP 无协议通信"（×2 连接）。
    ///
    /// 【对接方式】
    ///   基恩士 IV4 内置以太网支持：EtherNet/IP、PROFINET CC-B、TCP/IP 无协议通信（最多 2 路）。
    ///   上位机作为 TCP 客户端连接相机的 CommandPort，发送 ASCII 控制指令，相机回 ASCII 响应帧。
    ///   指令以 CR(0x0D) 终止，响应以 CR 结尾的一/多行。
    ///
    /// 【IV4 指令表（见 docs/通讯接入.md，以《IV4 通信、连接指南》为准）】
    ///   T1[CR]              触发拍摄；响应 T1[CR]（回显）
    ///   RT[CR]              读取判定结果；响应 RT, 工具结果(标准)[CR]
    ///                       或 RT, 工具结果(详细)[CR]
    ///   T2[CR]              触发＋读取判定结果；响应同 RT
    ///   BR,m[CR]            读取最新图像（V1.7.0，24bit 位图）；响应：
    ///                       BR, nnnnnnnnnn, ddddddd, 图像数据
    ///   工具结果(标准) = 8 位字符，每位一个工具：'0'=OK、'1'=NG、'4'=未进行、'-'=该工具未启用
    ///
    /// 【本服务提供的入口】
    ///   - TriggerAndRead()：发 T2，一次完成"触发+读判定"，返回 OK/NG（主流程用）；
    ///   - SendTrigger()：  发 T1，仅触发（场景：判定由其他途径/PLC 侧给）；
    ///   - ReadImage()：    发 BR,m，读最新图像字节（Tcp 取图模式用，见 CameraConfig.ImageSource）；
    ///   判定解析规则配置于 CameraConfig.OkChar，遇到 '4'/'-'/未知一律保守判 NG。
    ///
    /// 【线程】每次动作独立短连接，避免占用相机 2 连接上限；方法自带超时，绝不在 UI 线程调用。
    /// </summary>
    public class KeyenceIV4Camera : IDisposable
    {
        private readonly CameraConfig _cfg;
        private TcpClient _tcp;
        private NetworkStream _stream;

        /// <summary>
        /// 连接管理锁：把"检查/重建/关闭连接"串行化。
        /// 【为什么必须加锁】T2/触发等操作会走后台线程，而 UI 关窗的 Dispose 也可能同时进来；
        /// 若两个线程并发走到 EnsureConnected，一个 Close/重建 _tcp 时另一个会拿到将要被释放
        /// 的旧引用去 EndConnect → 正是此前 `tcp.EndConnect(result)` 抛 NullReferenceException 的根因。
        /// C# lock 可重入，读写在别处再套锁不冲突。
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>已释放标记：Dispose 后任何后台重连动作立即放弃（volatile 跨线程可见）</summary>
        private volatile bool _disposed;

        /// <summary>连接状态变化事件</summary>
        public event EventHandler<bool> ConnectionChanged;

        /// <summary>当前是否已连接</summary>
        public bool IsConnected { get; private set; }

        public KeyenceIV4Camera(CameraConfig cfg) => _cfg = cfg;

        /// <summary>日志/界面区分用标签：IP:端口（多相机时能分清断开的是哪台）</summary>
        public string IpLabel => $"{_cfg.IpAddress}:{_cfg.CommandPort}";

        private bool _lastFailed; // 上一次连接是否失败（日志降噪）

        /// <summary>
        /// 触发＋读取判定结果（T2）。
        /// 返回 TriggerReadOutcome：Succeeded=true 表示通讯成功并拿到判定；
        /// IsOk=true 表示判 OK（全部判定位为合格位）。
        /// </summary>
        public TriggerReadOutcome TriggerAndRead()
        {
            try
            {
                if (!EnsureConnected())
                    return TriggerReadOutcome.Fail("相机连接失败");

                string raw = SendCommandAndReadLine(_cfg.TriggerAndReadCommand, _cfg.ResponseTimeoutMs);
                return ParseResult(raw);
            }
            catch (Exception ex)
            {
                MarkDisconnected();
                LogHelper.Error("T2 触发+读判定异常", ex);
                return TriggerReadOutcome.Fail("异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 仅触发拍摄（T1）。返回 true 表示已发出并收到相机回显。
        /// 用于 ReadResultFromCamera=false 的退化模式（判定不详，FTP 图到即记 OK）。
        /// </summary>
        public bool SendTrigger()
        {
            try
            {
                if (!EnsureConnected()) return false;
                string raw = SendCommandAndReadLine(_cfg.TriggerCommand, _cfg.TimeoutMs);
                return raw != null;
            }
            catch (Exception ex)
            {
                MarkDisconnected();
                LogHelper.Error("T1 触发异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 发送一行 ASCII 指令（自动补 CR 结尾），并读取一条"以 CR/LF 结尾"的响应行。
        /// 返回去掉行尾符的正文；无响应/超时返回 null。
        /// </summary>
        private string SendCommandAndReadLine(string command, int readTimeoutMs)
        {
            if (_stream == null)
                throw new InvalidOperationException("网络流未就绪");

            byte[] sendBuf = Encoding.ASCII.GetBytes(command.Trim() + "\r");
            _stream.Write(sendBuf, 0, sendBuf.Length);
            _stream.Flush();
            LogHelper.Info($"已发送相机指令：{command.Trim()}");

            try { _stream.ReadTimeout = readTimeoutMs; } catch { }

            // 逐字节拼一行，遇 CR/LF 停止；每次 Read 到期由 ReadTimeout 抛异常兜底
            var sb = new StringBuilder();
            var one = new byte[1];
            while (sb.Length < 1024)
            {
                int n;
                try { n = _stream.Read(one, 0, 1); }
                catch { break; }
                if (n <= 0) break;
                char c = (char)one[0];
                if (c == '\r' || c == '\n' || c == '\0') break;
                sb.Append(c);
            }
            string line = sb.ToString();
            if (line.Length > 0)
                LogHelper.Info("相机响应：" + line);
            return line.Length > 0 ? line : null;
        }

        /// <summary>
        /// 读取相机最新图像（V1.7.0，Tcp 取图模式）。指令：BR,m[CR]；响应：
        ///   BR,nnnnnnnnnn,ddddddd,&lt;图像数据&gt;
        ///     nnnnnnnnnn = 图像数据字节数（10 位十进制，前导零）
        ///     ddddddd    = 图像属性（7 位十进制，含义以《IV4 通信、连接指南》为准，此处仅透出供现场对照）
        ///     逗号后紧跟的二进制即图像数据（24bit 位图，期望是完整 BMP 文件：BM 头 + 像素）。
        ///
        /// 【为什么用状态机逐字节解析响应头，而不是按"行"读】
        ///   图像数据是二进制，可能包含任意字节值（含 0x0D/0x0A，恰好会骗过"读到换行就停"的逻辑）；
        ///   必须先精确读完 ASCII 头部（BR,长度,属性,），再按长度字段精确读 N 字节，才不丢不截。
        ///
        /// 【连接复用】本方法与 TriggerAndRead 同走 EnsureConnected 的短连接缓存：同一次流程里
        ///   T2（触发+判定）紧接 BR（取图）会用同一条 TCP 连接，避免多占相机 2 路连接上限。
        ///
        /// 【耗时说明】一张 24bit BMP 通常数百 KB~几 MB，读取是同步的（会占用调用线程），
        ///   因此绝不能在 UI 线程调用；主流程在后台线程串行触发+取图，可接受。
        /// </summary>
        public ReadImageOutcome ReadImage()
        {
            try
            {
                if (!EnsureConnected())
                    return ReadImageOutcome.Fail("相机连接失败");

                // 拼指令：ReadImageCommand 默认 "BR"，ReadImageMode 默认 "1" → "BR,1"，末尾补 CR
                string cmd = (_cfg.ReadImageCommand ?? "BR").Trim() + "," + (_cfg.ReadImageMode ?? "1");
                byte[] sendBuf = Encoding.ASCII.GetBytes(cmd + "\r");
                _stream.Write(sendBuf, 0, sendBuf.Length);
                _stream.Flush();
                LogHelper.Info($"已发送相机指令：{cmd}");

                try { _stream.ReadTimeout = _cfg.ResponseTimeoutMs; } catch { }

                // ── 阶段0：响应前缀 "BR,"（容忍前缀前夹带的 CR/LF 空行） ──
                var prefix = new char[3];
                int pos = 0;
                while (pos < 3)
                {
                    int b = _stream.ReadByte();
                    if (b < 0) return ReadImageOutcome.Fail("读取响应前缀超时/连接断开");
                    if (b == '\r' || b == '\n') continue; // 跳过空行（此阶段没有图像数据，不会误吞）
                    prefix[pos++] = (char)b;
                }
                if (prefix[0] != 'B' || prefix[1] != 'R' || prefix[2] != ',')
                    return ReadImageOutcome.Fail($"响应前缀异常：\"{new string(prefix)}\"（期望 BR,）");

                // ── 阶段1：长度字段 nnnnnnnnnn（数字读到逗号为止，兼容前导零/变长） ──
                long size = 0;
                int digitCount = 0;
                while (true)
                {
                    int b = _stream.ReadByte();
                    if (b < 0) return ReadImageOutcome.Fail("读取图像长度字段超时/连接断开");
                    if (b == ',') break;
                    if (b < '0' || b > '9')
                        return ReadImageOutcome.Fail($"长度字段含非数字字符：{(char)b}");
                    size = size * 10 + (b - '0');
                    digitCount++;
                }
                // 防御：长度必须 >0 且不超过 64MB（IV4 视场图不会超过，防异常响应把内存吃爆）
                if (digitCount == 0 || size <= 0 || size > 64L * 1024 * 1024)
                    return ReadImageOutcome.Fail($"图像长度非法：{size}");

                // ── 阶段2：属性字段 ddddddd（数字读到逗号为止） ──
                // 属性含义以手册为准，这里只读出来放进结果，供日志/现场对照，不参与后续逻辑。
                long attr = 0;
                while (true)
                {
                    int b = _stream.ReadByte();
                    if (b < 0) return ReadImageOutcome.Fail("读取属性字段超时/连接断开");
                    if (b == ',') break;
                    if (b < '0' || b > '9')
                        return ReadImageOutcome.Fail($"属性字段含非数字字符：{(char)b}");
                    attr = attr * 10 + (b - '0');
                }

                // ── 阶段3：精确读取 size 字节图像数据 ──
                // 分块读（8KB/块）避免逐字节低效；单次 Read 的最长等待由 ReadTimeout 兜底，
                // 只要相机持续发数据就不会超时；中途断流（n<=0）判失败并记已收字节数便于排查。
                var data = new byte[size];
                int offset = 0;
                var chunk = new byte[8192];
                while (offset < size)
                {
                    int need = (int)Math.Min(chunk.Length, size - offset);
                    int n = _stream.Read(chunk, 0, need);
                    if (n <= 0)
                        return ReadImageOutcome.Fail($"图像数据读取不完整（已收 {offset}/{size} 字节）");
                    Array.Copy(chunk, 0, data, offset, n);
                    offset += n;
                }

                // BMP 完整性轻校验：完整位图文件应以 'B''M' 开头。
                // 若现场实测发现不以 BM 开头（可能是相机按其他格式/裸像素回传），
                // 需在此按实测格式调整（例如补 BMP 文件头），归档端 SaveImageBytes 才能解码。
                if (size < 2)
                {
                    // 数据不足 2 字节：既没法看文件头，也几乎不可能是有效图，记日志（仍作为成功返回，让归档端判成败）
                    LogHelper.Warn($"相机 BR 返回数据过短：{size}B（期望完整 BMP，可能响应头解析偏移）");
                }
                else
                {
                    if (data[0] != (byte)'B' || data[1] != (byte)'M')
                        LogHelper.Warn($"相机 BR 取回数据不以 BMP 文件头(BM) 开头，可能需按现场格式调整：大小={size}");
                    LogHelper.Info($"相机 BR 取图成功：大小={size}B 属性={attr} 首2字节=0x{data[0]:X2}0x{data[1]:X2}");
                }
                return ReadImageOutcome.Ok(size, attr, data);
            }
            catch (Exception ex)
            {
                MarkDisconnected();
                LogHelper.Error("BR 读取图像异常", ex);
                return ReadImageOutcome.Fail("异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 解析 T2/RT 的响应为判定结果。
        /// 期望形如 "RT,00010200"（标准）或 "RT,0001,12.3,..."（详细，取逗号前 8 位）。
        /// </summary>
        private TriggerReadOutcome ParseResult(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return TriggerReadOutcome.Fail("相机无响应");

            string line = raw.Trim();
            if (line.Length < 3 || !line.Substring(0, 3).Equals("RT,",
                StringComparison.OrdinalIgnoreCase))
                return TriggerReadOutcome.Fail("响应格式异常：" + line);

            string payload = line.Substring(3).Trim();
            string flags = payload.Split(',')[0]; // 标准：整段即 8 位；详细：取首个逗号前字段

            // 逐位判定：全部为合格位才算 OK，任一其他字符（含'1'/'4'/'-'/未知）一律保守 NG
            var badChars = new List<char>();
            char okChar = string.IsNullOrEmpty(_cfg.OkChar) ? '0' : _cfg.OkChar[0];
            bool isOk = true;
            foreach (char c in flags)
            {
                if (c == okChar) continue;
                isOk = false;
                if (!badChars.Contains(c)) badChars.Add(c);
            }

            return isOk
                ? TriggerReadOutcome.OkResult(flags, raw)
                : TriggerReadOutcome.NgResult(flags, raw, "非合格位: " + new string(badChars.ToArray()));
        }

        /// <summary>建立到相机的 TCP 连接。返回 true 表示连接可用。</summary>
        public bool EnsureConnected()
        {
            if (_disposed) return false; // 已释放：后台心跳/重连直接放弃
            // 整体串行化：杜绝并发 Close/重建 _tcp 时对旧引用 EndConnect 造成空引用
            lock (_lock)
            {
                try
                {
                    if (_tcp != null && _tcp.Connected && _stream != null)
                        return true;

                    _tcp?.Close();
                    _tcp = new TcpClient();
                    _tcp.ReceiveTimeout = _cfg.TimeoutMs;
                    _tcp.SendTimeout = _cfg.TimeoutMs;
                    string err;
                    if (!TryConnect(_tcp, _cfg.IpAddress, _cfg.CommandPort, _cfg.TimeoutMs, out err))
                        throw new Exception(err);
                    _stream = _tcp.GetStream();
                    _lastFailed = false;
                    SetConnected(true);
                    LogHelper.Info($"相机连接成功 {_cfg.IpAddress}:{_cfg.CommandPort}");
                    return true;
                }
                catch (Exception ex)
                {
                    SetConnected(false);
                    // 清理本次失败的连接，避免残留失效引用（下次 EnsureConnected 完整重建）
                    try { _stream?.Dispose(); } catch { }
                    _stream = null;
                    try { _tcp?.Close(); } catch { }
                    _tcp = null;
                    if (!_lastFailed)
                    {
                        _lastFailed = true;
                        LogHelper.Warn($"相机连接失败 {_cfg.IpAddress}:{_cfg.CommandPort}，原因：{ex.Message}");
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// 带超时的 TCP 连接（无回调式，对齐 AgingTestSystem）：不抛异常，改返回 bool + 原因。
        /// BeginConnect 不注册回调线程，用 AsyncWaitHandle.WaitOne 等待连接结束；
        /// 【根治 NRE 的两个关键】
        /// ① 无回调线程 → 全链路只有主线程接触 TcpClient；
        /// ② EndConnect 前检查 tcp.Client == null：若连接期间被并发清理（Close 会把内部
        ///    socket 置 null），此时绝不能再碰 EndConnect，否则对已释放对象调用会抛
        ///    NullReferenceException（此前 EndConnect 报 NRE 正是这条竞态路径）。
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

        /// <summary>
        /// 心跳检查（供连接监控器周期调用，不打断拍摄流程）：
        /// 纯 socket 级探测，确认 TCP 连接还"活着"。若发现对端已关闭/连接失效，
        /// 立即标记断开并触发 ConnectionChanged，让 UI 状态同步变红、监控器随后自动重连。
        /// 【局限】拔网线等"无声断连"靠 Poll 无法立即感知，真正断连仍以触发时的读写异常为准；
        ///    该检查主要捕获"对端主动关闭连接/FIN"这类可探测断连。
        /// </summary>
        public bool CheckConnection()
        {
            lock (_lock)
            {
                if (_tcp == null || _stream == null) return false;
                try
                {
                    if (!_tcp.Connected) return false;
                    // Poll(0, SelectRead) 有可读数据 + Available==0 → 对端已发 FIN/关闭
                    if (_tcp.Client.Poll(0, SelectMode.SelectRead) && _tcp.Client.Available == 0)
                    {
                        MarkDisconnected();
                        return false;
                    }
                    return true;
                }
                catch
                {
                    MarkDisconnected();
                    return false;
                }
            }
        }

        /// <summary>标记断开并清理连接（幂等；仅在状态变化时触发一次 ConnectionChanged）。
        /// 【必须在锁内】否则与 EnsureConnected 的重建并发时，会在对方 BeginConnect 的
        /// WaitOne 期间把 socket Close，诱发 EndConnect 的 NRE 竞态。</summary>
        private void MarkDisconnected()
        {
            lock (_lock)
            {
                _lastFailed = true; // 已有失败记录，重连期间的失败日志自动静默
                SetConnected(false); // 内部判断状态未变则不重复发事件（边沿检测）
                try { _stream?.Dispose(); } catch { }
                _stream = null;
                try { _tcp?.Close(); } catch { }
                _tcp = null;
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

        public void Dispose()
        {
            _disposed = true; // 先置标志：后台重连下一步立即放弃，不再碰 _tcp/_stream
            // 同 PlcService：限时抢锁。后台 EnsureConnected 重连任务可能正持锁，但
            // UI 关窗线程绝不能无限期等锁；拿不到锁就"锁外强断网"兜底：
            // _tcp.Close() 会让持锁任务的 BeginConnect 立刻结束（WaitOne 返回后
            // TryConnect 内 tcp.Client==null / EndConnect 抛异常均被其 catch 收敛）。
            if (Monitor.TryEnter(_lock, TimeSpan.FromMilliseconds(300)))
            {
                try
                {
                    try { _stream?.Dispose(); } catch { }
                    _stream = null;
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
                LogHelper.Warn("相机 Dispose 未能拿到锁（后台重连任务繁忙），改走锁外强断网");
                try { _tcp?.Close(); } catch { }
                try { _stream?.Dispose(); } catch { }
            }
        }
    }

    /// <summary>
    /// 一次"触发+读判定"的结果载体。
    /// Succeeded=false 表示通讯/指令失败；true 时 IsOk 为判定结论。
    /// </summary>
    public class TriggerReadOutcome
    {
        /// <summary>是否成功取到判定（避免误把失败当 NG 判定）</summary>
        public bool Succeeded { get; private set; }

        /// <summary>判定结论：true=OK，false=NG（或字段异常保守 NG）</summary>
        public bool IsOk { get; private set; }

        /// <summary>8 位标准判定文本（如 "00000000"），供现场对照</summary>
        public string ResultText { get; private set; }

        /// <summary>相机原始响应行</summary>
        public string Raw { get; private set; }

        /// <summary>失败原因/非合格位说明</summary>
        public string Detail { get; private set; }

        public static TriggerReadOutcome OkResult(string resultText, string raw) =>
            new TriggerReadOutcome { Succeeded = true, IsOk = true, ResultText = resultText, Raw = raw };

        public static TriggerReadOutcome NgResult(string resultText, string raw, string detail) =>
            new TriggerReadOutcome { Succeeded = true, IsOk = false, ResultText = resultText, Raw = raw, Detail = detail };

        public static TriggerReadOutcome Fail(string detail) =>
            new TriggerReadOutcome { Succeeded = false, Detail = detail };
    }

    /// <summary>
    /// 一次"读取图像"的结果载体（V1.7.0，BR 指令）。
    /// Succeeded=false 表示通讯/指令失败；true 时 ImageData 为读回的图像字节。
    /// </summary>
    public class ReadImageOutcome
    {
        /// <summary>是否成功取回图像</summary>
        public bool Succeeded { get; private set; }

        /// <summary>
        /// 图像原始字节。期望是完整 24bit BMP 文件（以 'BM' 开头，可直接 Image.FromStream 解码）；
        /// 若现场实测相机返回的是无文件头的裸像素，需在 ReadImage/SaveImageBytes 侧按实测补头。
        /// </summary>
        public byte[] ImageData { get; private set; }

        /// <summary>响应头长度字段 nnnnnnnnnn（图像数据字节数）</summary>
        public long DataSize { get; private set; }

        /// <summary>响应头属性字段 ddddddd（含义以《IV4 通信、连接指南》为准，此处仅透出供日志对照）</summary>
        public long DataAttr { get; private set; }

        /// <summary>失败原因（通讯失败/响应格式异常/数据不完整等）</summary>
        public string Detail { get; private set; }

        public static ReadImageOutcome Ok(long size, long attr, byte[] data) =>
            new ReadImageOutcome { Succeeded = true, ImageData = data, DataSize = size, DataAttr = attr };

        public static ReadImageOutcome Fail(string detail) =>
            new ReadImageOutcome { Detail = detail };
    }
}