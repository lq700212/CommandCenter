using System;
using System.IO.Ports;
using System.Text;
using CommandCenter.Models;

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

        /// <summary>设备是否已连接/已打开</summary>
        bool IsOpen { get; }

        /// <summary>启动（打开串口 / 发起 TCP 连接与后台读取）。返回 false 表示启动失败
        /// （串口打不开等），不影响主流程（可手动输入序列号）；TCP 实现立即返回 true（连接在后台）。</summary>
        bool Open();
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

        /// <summary>扫到一条完整条码的事件（参数为条码文本，在工作线程触发，UI 需 Invoke）</summary>
        public event EventHandler<string> SerialNumberScanned;

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
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("扫码枪打开失败：" + ex.Message);
                return false;
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
                                SerialNumberScanned?.Invoke(this, line);
                        }
                    }
                    else
                    {
                        _buffer.Append(c);
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

        public void Dispose()
        {
            if (_port != null)
            {
                if (_port.IsOpen) _port.Close();
                _port.Dispose();
            }
        }
    }
}