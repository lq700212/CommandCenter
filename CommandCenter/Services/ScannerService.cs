using System;
using System.IO.Ports;
using System.Text;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Services
{
    /// <summary>
    /// 扫码枪统一接口（V1.8.0）：串口（ScannerService）与以太网 TCP/IP 无协议
    /// （ScannerTcpService）两种实现都暴露同一声明，主窗体只依赖接口，按
    /// ScanConfig.Mode 决定实例化哪个，将来换扫码枪实现不影响上层。
    /// </summary>
    public interface IScanner : IDisposable
    {
        /// <summary>扫到一条完整条码的事件（参数为条码文本，在工作线程触发，UI 需 Invoke）</summary>
        event EventHandler<string> SerialNumberScanned;

        /// <summary>
        /// 扫码枪"读码失败/状态错误"通知（V2.14.30）：收码层遇到"错误文本过滤名单"
        /// （ScanConfig.IgnoreScanTexts，如基恩士 SR 读码失败的 ERROR 输出）命中时触发，
        /// 参数=命中的原始文本。协调器订阅后**立即把扫码结果写 2 通知 PLC**（V2.14.30/33：
        /// PLC 拿到 2 会死等人工补录，不必等 ScanWaitMs 超时），
        /// 让 PLC 那拍不用空等。工作线程触发，UI 不需要此事件。
        /// </summary>
        event EventHandler<string> ScanFailed;

        /// <summary>
        /// 连接状态变化事件（V1.12.5）：true=已连接/已打开，false=断开/已关闭。
        /// 边沿触发（状态没变不发事件），在工作线程触发，UI 订阅方需自行 Invoke。
        /// 串口实现只在 Open/Dispose 时触发；TCP 实现每次连接成功/断线时触发。
        /// 功能测试窗体靠它实时刷新扫码枪状态灯（此前 IScanner 没有此事件，状态灯
        /// 只刷新一次、永远停在"断连"，导致"实际连上了界面还显示断连"的误判）。
        /// </summary>
        event EventHandler<bool> ConnectionChanged;

        /// <summary>设备是否已连接/已打开</summary>
        bool IsOpen { get; }

        /// <summary>启动（打开串口 / 发起 TCP 连接与后台读取）。返回 false 表示启动失败
        /// （串口打不开等），不影响主流程（可手动输入序列号）；TCP 实现立即返回 true（连接在后台）。</summary>
        bool Open();

        /// <summary>
        /// 发送触发指令（V1.12.0）：基恩士 SR 系列扫码枪多数需先发一条"打开激光/开始读取"
        /// 指令（如 LON）才开始读码。TCP 实现（ScannerTcpService）每次连接成功后会自动发送
        /// 一次；本方法供界面手动重发（如测试时扫码枪突然不读，可点一下重新触发）。
        /// 串口扫码枪上电即读码，无需触发，串口实现为空操作。返回 true 表示指令已发出。
        /// </summary>
        bool SendTrigger();
    }

    /// <summary>
    /// 扫码枪服务：封装串口扫码枪数据接收。
    ///
    /// 【说明】
    ///   现场扫码枪分"键盘仿真"与"串口"两类。串口类扫码枪扫完会把条码作为一行文本发到串口，末尾带 CR/LF。
    ///   本类监听 DataReceived 事件，按行切分并抛出 SerialNumberScanned 事件。
    ///   未来改用键盘仿真扫码枪时，保持同名事件即可替换实现。
    /// </summary>
    public class ScannerService : IScanner
    {
        private readonly ScanConfig _cfg;
        private SerialPort _port;
        private readonly StringBuilder _buffer = new StringBuilder();
        private bool _open; // 串口打开状态缓存，用于 ConnectionChanged 边沿检测（状态没变不发事件）

        /// <summary>扫到一条完整条码的事件（参数为条码文本，在工作线程触发，UI 需 Invoke）</summary>
        public event EventHandler<string> SerialNumberScanned;

        /// <summary>
        /// 扫码枪"读码失败/状态错误"通知（V2.14.30，实现 IScanner）：收码层命中
        /// ScanConfig.IgnoreScanTexts 名单时触发（见 OnDataReceived），协调器据此立即把扫码结果写 2
        /// 通知 PLC（V2.14.33：PLC 拿到 2 会死等人工补录）。
        /// </summary>
        public event EventHandler<string> ScanFailed;

        /// <summary>连接（串口打开）状态变化事件：Open 成功 true / Dispose 关闭 false（边沿触发）。</summary>
        public event EventHandler<bool> ConnectionChanged;

        /// <summary>串口是否已打开</summary>
        public bool IsOpen => _port != null && _port.IsOpen;

        public ScannerService(ScanConfig cfg) => _cfg = cfg;

        /// <summary>
        /// 打开扫码枪串口。失败返回 false，不影响主流程（可手动输入序列号）。
        /// </summary>
        public bool Open()
        {
            if (!_cfg.Enabled) return false;
            try
            {
                _port = new SerialPort(_cfg.PortName, _cfg.BaudRate, ParityFromName(_cfg.Parity))
                {
                    DataBits = 8,
                    StopBits = StopBitsFromString(_cfg.StopBits),
                    ReadTimeout = 500
                };
                _port.DataReceived += OnDataReceived;
                _port.Open();
                SetConnected(true); // 串口打开成功：通知订阅方状态变"已连接"（幂等，状态没变不发）
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("扫码枪打开失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>串口打开状态变化：仅在状态真正改变时触发一次 ConnectionChanged（对齐 PLC/相机的边沿语义）。</summary>
        private void SetConnected(bool value)
        {
            if (_open != value)
            {
                _open = value;
                ConnectionChanged?.Invoke(this, value);
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string chunk = _port.ReadExisting();
                foreach (char c in chunk)
                {
                    // 遇回车即认为一条条码结束
                    if (c == '\r' || c == '\n')
                    {
                        if (_buffer.Length > 0)
                        {
                            string line = _buffer.ToString().Trim();
                            _buffer.Clear();
                            if (line.Length > 0)
                            {
                                // 【V2.14.30 读码失败文本过滤】基恩士扫码枪读码失败时可能把错误字符串
                                // （如 ERROR）当条码推出；命中 IgnoreScanTexts 名单的行不是真码——
                                // 不抛 SerialNumberScanned（避免污染序列号/存图目录），改抛 ScanFailed
                                // 让协调器立即把扫码结果写 2 通知 PLC（死等补录，详见 StepScanChannel）。
                                // 精确匹配不误伤同前缀真实条码。
                                if (_cfg.IsIgnoredScanText(line))
                                {
                                    LogHelper.Warn($"扫码枪(串口)读码失败/状态文本「{line}」已忽略（命中 IgnoreScanTexts），上报扫码失败信号");
                                    ScanFailed?.Invoke(this, line);
                                }
                                else
                                {
                                    SerialNumberScanned?.Invoke(this, line);
                                }
                            }
                        }
                    }
                    else
                    {
                        _buffer.Append(c);
                        // 【V1.8.3 修复】防御异常/噪声数据撑爆内存：串口没有行分隔符时
                        //   _buffer 会无限增长（对齐 TCP 实现 ScannerTcpService 的 MaxLineLen=512）。
                        if (_buffer.Length > 512) _buffer.Clear();
                    }
                }
            }
            catch
            {
                // 串口异常直接忽略，扫码只是辅助输入
            }
        }

        private static StopBits StopBitsFromString(string s)
        {
            if (s == "2") return StopBits.Two;
            if (s == "15") return StopBits.OnePointFive;
            return StopBits.One;
        }

        private static Parity ParityFromName(string name)
        {
            switch ((name ?? "None").ToLowerInvariant())
            {
                case "odd": return Parity.Odd;
                case "even": return Parity.Even;
                case "mark": return Parity.Mark;
                case "space": return Parity.Space;
                default: return Parity.None;
            }
        }

        /// <summary>触发指令：串口扫码枪上电即读码，无需触发指令，直接返回 true（V1.12.0）。</summary>
        public bool SendTrigger() => true;

        public void Dispose()
        {
            if (_port != null)
            {
                if (_port.IsOpen) _port.Close();
                _port.Dispose();
            }
            SetConnected(false); // 关闭串口：通知订阅方状态变"已关闭"（幂等，已 false 则不发）
        }
    }
}