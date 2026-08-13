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
    ///
    /// 【CR 语义（数据包结束标记，务必理解）】
    ///   基恩士 TCP/IP 无协议通信中，CR（回车符 '\r'，0x0D）是数据包结束标记：
    ///   指令以 CR 结尾发送，相机以 CR（或 CRLF）结尾回帧，双方靠 CR 识别帧边界。
    ///   例：触发指令发 "T1[CR]"，相机回显 "T1[CR]" 作为确认；T2 则回 "RT,xxx[CR]"[判定]。
    ///   程序发指令一律补 "\r"；收帧按 CR/LF 截断（兼容 CR 与 CRLF 两种结尾，见
    ///   SendCommandAndReadLine）。这是 V1.0.1 血泪教训修复的核心：收帧不以 CR 正确切行，
    ///   残留换行符会污染下一次读取导致"隔一次判定失败"。
    ///
    /// 【IV4 指令表（见 docs/通讯接入.md，以《IV4 通信、连接指南》为准）】
    ///   T1[CR]              触发拍摄；响应 T1[CR]（回显确认）
    ///   RT[CR]              读取判定结果；响应 RT, 工具结果(标准)[CR]
    ///                       或 RT, 工具结果(详细)[CR]
    ///   T2[CR]              触发＋读取判定结果；响应同 RT
    ///   BR,m[CR]            读取最新图像（V1.7.0，24bit 位图）；m=压缩率(0=无压缩,1=1/2)；
    ///                       响应 BR, nnnnnnnnnn, ddddddd, 图像数据
    ///                       （nnnnnnnnnn=合计触发编号，ddddddd=图像数据的数据长度；V1.9.1 修正）
    ///   PW,nnn[CR]          切换相机程序（V1.12.18）；nnn=程序编号(000~127，3 位补零)；
    ///                       响应 PW[CR]（成功）或 ER,PW,03[CR]/ER,PW,22[CR]（失败）
    ///   PR[CR]              读取当前程序编号；响应 PR,nnn[CR]
    ///   OF,nn[CR]           切换判定结果输出格式（V1.12.18）；nn=00标准/01详细/02标准主控编号/03详细主控编号；
    ///                       响应 OF[CR]（成功）
    ///   工具结果(标准) = 8 位字符，每位一个工具：'0'=OK、'1'=NG、'4'=未进行、'-'=该工具未启用
    ///
    /// 【本服务提供的入口】
    ///   - TriggerAndRead()：发 T2，一次完成"触发+读判定"，返回 OK/NG（主流程用）；
    ///   - SwitchProgram(n)：发 PW,nnn，先切相机程序再触发（V1.12.18，多程序/多点位现场用）；
    ///   - ReadProgramNo()：发 PR，读当前程序编号（V1.12.18，联调确认程序切换是否生效）；
    ///   - SetOutputFormat()：发 OF,nn，设置判定结果输出格式（V1.12.18，按 CameraConfig.OutputFormat）；
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

        /// <summary>相机名称（上相机/下相机，V1.12.22）：界面/日志显示用，取配置 Name；空则为空串。</summary>
        public string DisplayName => _cfg?.Name ?? "";

        /// <summary>日志/界面区分用标签：IP:端口（多相机时能分清断开的是哪台）</summary>
        public string IpLabel => $"{_cfg.IpAddress}:{_cfg.CommandPort}";

        /// <summary>仅 IP 地址（V1.10.0：标题栏相机下拉列表只显示 IP，不显示端口）</summary>
        public string IpAddressOnly => _cfg.IpAddress;

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
        /// 切换相机程序（V1.12.18，指令 PW,nnn[CR]）。
        /// 现场"一个相机拍多个点位、每个点位用不同程序（不同视觉工具组）"时，触发前必须先
        /// 把相机切到当前点位对应的程序，否则相机仍在跑上一个程序，判定/取图都对应不上。
        /// nnn = 程序编号（000~127，不足 3 位自动补 0，如程序 9 → "PW,009"）。
        /// 成功响应 "PW[CR]"（相机会回显指令名）；失败响应 "ER,PW,xx[CR]"（xx=错误码）。
        /// 本方法只校验响应前缀是否为 "PW"（大小写不敏感）；"ER,PW" 视为切换失败。
        /// </summary>
        /// <param name="programNo">目标程序编号（0~127，越界自动夹到 0~127）</param>
        /// <returns>true=已切到目标程序；false=通讯失败/相机报错</returns>
        public bool SwitchProgram(int programNo)
        {
            try
            {
                if (!EnsureConnected()) return false;
                // 夹到合法区间：IV4 程序编号 000~127
                programNo = Math.Max(0, Math.Min(127, programNo));
                string cmd = "PW," + programNo.ToString("D3");
                string raw = SendCommandAndReadLine(cmd, _cfg.ResponseTimeoutMs);
                if (raw == null) return false;
                if (raw.StartsWith("ER", StringComparison.OrdinalIgnoreCase))
                {
                    LogHelper.Warn($"相机切程序失败 {_cfg.IpAddress}:{_cfg.CommandPort}：{raw}");
                    return false;
                }
                // 成功响应是回显 "PW"（或带后续）；ER 已在上面拦掉，这里按前缀 PW 判断
                bool ok = raw.StartsWith("PW", StringComparison.OrdinalIgnoreCase);
                if (ok)
                    LogHelper.Info($"相机已切换程序 → {cmd}（响应：{raw}）");
                else
                    LogHelper.Warn($"相机切程序响应异常 {_cfg.IpAddress}:{_cfg.CommandPort}：{raw}");
                return ok;
            }
            catch (Exception ex)
            {
                MarkDisconnected();
                LogHelper.Error("相机切换程序异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 读取相机当前程序编号（V1.12.18，指令 PR[CR]，响应 "PR,nnn[CR]"）。
        /// 用于联调时确认 PW 切换是否真正生效；主流程不依赖它（切完程序直接触发）。
        /// </summary>
        /// <returns>成功返回程序编号(0~127)；失败返回 -1</returns>
        public int ReadProgramNo()
        {
            try
            {
                if (!EnsureConnected()) return -1;
                string raw = SendCommandAndReadLine("PR", _cfg.ResponseTimeoutMs);
                if (raw == null) return -1;
                // 期望 "PR,009"；非法前缀返回 -1
                if (!raw.StartsWith("PR", StringComparison.OrdinalIgnoreCase)) return -1;
                string noStr = raw.Substring(2).TrimStart(',', ' ', '\t').Trim();
                int no;
                return int.TryParse(noStr, out no) ? no : -1;
            }
            catch (Exception ex)
            {
                MarkDisconnected();
                LogHelper.Error("相机读取程序编号异常", ex);
                return -1;
            }
        }

        /// <summary>
        /// 设置判定结果输出格式（V1.12.18，指令 OF,nn[CR]，响应 "OF[CR]"）。
        /// 现场若把 T2 响应切成"详细格式"，判定解析仍兼容（ParseResult 取逗号前字段），
        /// 但为对齐《通信指南》，程序主动按 CameraConfig.OutputFormat 发一次 OF 固化格式。
        /// nn 固定 2 位：00标准 / 01详细 / 02标准(主控编号) / 03详细(主控编号)。
        /// 设置后连接断开或断电前一直保持；相机断电后需要重新设置（主流程每次触发前会带发）。
        /// </summary>
        /// <param name="format">2 位格式编号；空/非法则不发送（相机维持当前/默认格式）</param>
        /// <returns>true=发送成功（收到 OF 回显）；false=失败或未发送</returns>
        public bool SetOutputFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format)) return false;
            string nn = format.Trim();
            if (nn.Length != 2 || !char.IsDigit(nn[0]) || !char.IsDigit(nn[1])) return false;
            try
            {
                if (!EnsureConnected()) return false;
                string raw = SendCommandAndReadLine("OF," + nn, _cfg.ResponseTimeoutMs);
                if (raw == null) return false;
                bool ok = raw.StartsWith("OF", StringComparison.OrdinalIgnoreCase);
                if (ok) LogHelper.Info($"相机已设置判定输出格式 → OF,{nn}");
                else LogHelper.Warn($"相机设置输出格式响应异常 {_cfg.IpAddress}:{_cfg.CommandPort}：{raw}");
                return ok;
            }
            catch (Exception ex)
            {
                MarkDisconnected();
                LogHelper.Error("相机设置输出格式异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 仅触发拍摄（T1）。返回 true 表示已发出并收到相机回显。
        /// 相机对 T1 的回帧就是回显 "T1"（以 CR 结尾的确认帧），收到任意非空响应即视为触发成功；
        /// 回显内容无需解析（不像 T2 要读判定）。用于 ReadResultFromCamera=false 的退化模式
        /// （判定不详，FTP 图到即记 OK）。
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
        ///
        /// 【V1.7.2 修复的两个现场级 bug】
        /// ① CRLF 残留：IV4 响应以 CRLF 结尾（见 docs/通讯接入.md "CR 或 CRLF 结束，程序两者兼容"）。
        ///    旧实现读到 '\r' 就停，把 '\n' 留在流里，下一次动作先读到残留 '\n' 判"无响应"，
        ///    表现为"第一次触发正常、第二次判定失败"交替出现。现在行首遇 CR/LF/NUL 一律跳过
        ///    （同时容忍相机响应前发送的空行），残留行尾不会污染下一次读取。
        /// ② 假活连接复用：读超时/断流（Read 抛异常或返回 0）时，TCP 层的 Connected 属性
        ///    仍可能为 true，旧实现不清理连接 → 下一次 EnsureConnected 复用坏流，永远失败且不重连。
        ///    现在超时/断流一律 MarkDisconnected，下次动作强制重建连接。
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

            // 逐字节拼一行；Read 到期由 ReadTimeout 抛异常兜底
            var sb = new StringBuilder();
            var one = new byte[1];
            while (sb.Length < 1024)
            {
                int n;
                try { n = _stream.Read(one, 0, 1); }
                catch { MarkDisconnected(); break; }       // 超时/断流：连接不可信，标记待重建
                if (n <= 0) { MarkDisconnected(); break; } // 对端关闭：同上
                char c = (char)one[0];
                if (sb.Length == 0 && (c == '\r' || c == '\n' || c == '\0'))
                    continue;                              // 前导空行/上一帧残留行尾：跳过
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
        ///     nnnnnnnnnn = 合计触发编号（10 位十进制；计数模式时固定 999999999）
        ///     dddddddd    = 图像数据的数据长度（决定后续要读的字节数）
        ///     逗号后紧跟的二进制即图像数据（24bit 位图，期望是完整 BMP 文件：BM 头 + 像素）
        ///
        /// 【V1.9.1 修正——两个字段含义此前理解反了，务必注意】
        ///   《IV4 通信、连接指南》原文：
        ///     BR,m[CR]          读取图像数据
        ///     响应 BR,nnnnnnnnnn,ddddddd,图像数据
        ///     nnnnnnnnnn = 合计触发编号（固定长度 0~DWORD_MAX-1；通过计数模式时 = 999999999）
        ///     dddddddd   = 图像数据的数据长度
        ///   V1.9.0 之前的实现把"合计触发编号"误当成图像字节数、把"数据长度"当成属性，
        ///   正好颠倒 → 会用触发编号去读图像（长度校验几乎必失败 / 多读少读），
        ///   已在 ReadImage 里按"触发编号→数据长度"正确顺序重新解析。
        ///   触发编号仅作日志/现场对照透出，不参与读取。
        ///
        /// 【为什么用状态机逐字节解析响应头，而不是按"行"读】
        ///   图像数据是二进制，可能包含任意字节值（含 0x0D/0x0A，恰好会骗过"读到换行就停"的逻辑）；
        ///   必须先精确读完 ASCII 头部（BR,触发编号,长度,），再按长度字段精确读 N 字节，才不丢不截。
        ///
        /// 【连接复用】本方法与 TriggerAndRead 同走 EnsureConnected 的短连接缓存：同一次流程里
        ///   T2（触发+判定）紧接 BR（取图）会用同一条 TCP 连接，避免多占相机 2 路连接上限。
        ///
        /// 【无最新图像时的错误】《IV4 通信、连接指南》说明：在没有最新图像的状态下试图读取
        ///   会出错。此时相机响应不是正常 "BR,..." 帧（可能直接断连/回错误码），
        ///   前缀校验不过即判失败，配合断连标记自动走重连，不会误取错数据。
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

                // 拼指令：ReadImageCommand 默认 "BR"，ReadImageMode 默认 "1" → "BR,1"，末尾补 CR。
                // 参数 m = 压缩率：0=无压缩，1=1/2（数据量减半、传输更快，默认取 1）。
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
                    if (b < 0) { MarkDisconnected(); return ReadImageOutcome.Fail("读取响应前缀超时/连接断开（可能相机无最新图像）"); }
                    if (b == '\r' || b == '\n') continue; // 跳过空行（此阶段没有图像数据，不会误吞）
                    prefix[pos++] = (char)b;
                }
                if (prefix[0] != 'B' || prefix[1] != 'R' || prefix[2] != ',')
                    return ReadImageOutcome.Fail($"响应前缀异常：\"{new string(prefix)}\"（期望 BR,，可能无最新图像）");

                // ── 阶段1：合计触发编号 nnnnnnnnnn（数字读到逗号为止，仅透出日志，不用于读取） ──
                long triggerNo = 0;
                int trigDigitCount = 0;
                while (true)
                {
                    int b = _stream.ReadByte();
                    if (b < 0) { MarkDisconnected(); return ReadImageOutcome.Fail("读取合计触发编号超时/连接断开"); }
                    if (b == ',') break;
                    if (b < '0' || b > '9')
                        return ReadImageOutcome.Fail($"合计触发编号含非数字字符：{(char)b}");
                    triggerNo = triggerNo * 10 + (b - '0');
                    trigDigitCount++;
                }
                if (trigDigitCount == 0)
                    return ReadImageOutcome.Fail("合计触发编号为空");

                // ── 阶段2：图像数据的数据长度 ddddddd（数字读到逗号为止） ──
                // 这是真正决定"后面读多少字节"的字段（V1.9.1 修正：此前误用触发编号当长度）。
                long size = 0;
                int digitCount = 0;
                while (true)
                {
                    int b = _stream.ReadByte();
                    if (b < 0) { MarkDisconnected(); return ReadImageOutcome.Fail("读取图像长度字段超时/连接断开"); }
                    if (b == ',') break;
                    if (b < '0' || b > '9')
                        return ReadImageOutcome.Fail($"图像长度字段含非数字字符：{(char)b}");
                    size = size * 10 + (b - '0');
                    digitCount++;
                }
                // 防御：长度必须 >0 且不超过 64MB（IV4 视场图不会超过，防异常响应把内存吃爆）
                if (digitCount == 0 || size <= 0 || size > 64L * 1024 * 1024)
                    return ReadImageOutcome.Fail($"图像长度非法：{size}");

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
                    if (n <= 0) { MarkDisconnected(); return ReadImageOutcome.Fail($"图像数据读取不完整（已收 {offset}/{size} 字节）"); }
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
                    LogHelper.Info($"相机 BR 取图成功：触发编号={triggerNo} 大小={size}B 首2字节=0x{data[0]:X2}0x{data[1]:X2}");
                }
                return ReadImageOutcome.Ok(size, triggerNo, data);
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
        ///
        /// 【V1.7.2 修复】
        /// ① 判定内容为空（"RT," / "RT,,..."）时直接判失败——此前空 flags 会因 foreach 不执行
        ///    而误判 OK，若现场相机未配判定工具会把不良直接放行，后果严重；
        /// ② flags 做 Trim：兼容"RT, 00000000"（逗号后带空格）的响应，避免把空格当非合格位误判 NG。
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
            string flags = payload.Split(',')[0].Trim(); // 标准：整段即 8 位；详细：取首个逗号前字段

            // 无判定内容：通讯成功但没有结论，按失败处理（绝不默认 OK）
            if (flags.Length == 0)
                return TriggerReadOutcome.Fail("判定内容为空：" + line);

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

        /// <summary>响应头第二个字段：图像数据的数据长度 ddddddd（决定读取的字节数）</summary>
        public long DataSize { get; private set; }

        /// <summary>响应头第一个字段：合计触发编号 nnnnnnnnnn（计数模式固定 999999999，仅透出日志对照）</summary>
        public long DataTriggerNo { get; private set; }

        /// <summary>失败原因（通讯失败/响应格式异常/数据不完整等）</summary>
        public string Detail { get; private set; }

        public static ReadImageOutcome Ok(long size, long triggerNo, byte[] data) =>
            new ReadImageOutcome { Succeeded = true, ImageData = data, DataSize = size, DataTriggerNo = triggerNo };

        public static ReadImageOutcome Fail(string detail) =>
            new ReadImageOutcome { Detail = detail };
    }
}