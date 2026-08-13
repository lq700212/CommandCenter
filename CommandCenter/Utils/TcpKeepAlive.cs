using System;
using System.Net.Sockets;

namespace CommandCenter.Utils
{
    /// <summary>
    /// TCP KeepAlive 配置工具（V2.10.5）。
    ///
    /// 【为什么需要它】
    /// 上位机对 PLC/相机/扫码枪走 TCP 时，连接一旦建立就常驻。对"拔网线 / 对端断电 /
    /// 中间交换机断电"这类【静默断连】（网络层不补发 FIN/RST）：
    /// - 依赖"阻塞 Read 返回 / 对端主动关闭"的检测手段会一直等不到信号；
    /// - TCP 栈默认 keepalive 要 2 小时才触发，现场根本等不起。
    /// 启用本工具设置的短间隔 KeepAlive 后，TCP 栈会在空闲够久时主动向对端发探测包，
    /// 对端失联若干次后把连接判死，之后任何阻塞的 Read / Poll / Connected 都会立即
    /// 反映"连接已死"，业务层的断连检测（MarkDown/MarkDisconnected/IsConnected）随之生效。
    ///
    /// 【参数】空闲 5s 开始探测、探测间隔 5s。对"长时间无业务数据"的连接（相机不拍照、
    /// 扫码枪不扫码、PLC 主站没请求）不会误判——KeepAlive 只探活，不干扰业务数据流；
    /// 判死耗时 = 空闲 5s + 系统默认重试次数（通常 ~数十秒），实时又不激进。
    ///
    /// 【失败处理】SIO_KEEPALIVE_VALS 在个别平台不可用，捕获后静默忽略（仅退化为系统
    /// 默认 keepalive，连接照常使用，功能不受影响）。
    /// </summary>
    public static class TcpKeepAlive
    {
        /// <summary>
        /// 对已建立的 TcpClient 连接启用短间隔 KeepAlive（幂等，可对同一连接重复调用）。
        /// 需在连接成功后调用；连接未建立/已释放时安全返回。
        /// </summary>
        public static void Configure(TcpClient tcp)
        {
            try
            {
                Socket sock = tcp?.Client;
                if (sock == null) return;
                sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                // SIO_KEEPALIVE_VALS：Windows tcp_keepalive 结构 { onoff, keepalivetime,
                //   keepaliveinterval }，各 4 字节小端，单位毫秒。
                byte[] inValue = new byte[12];
                BitConverter.GetBytes((uint)1).CopyTo(inValue, 0);    // onoff = 1 启用
                BitConverter.GetBytes((uint)5000).CopyTo(inValue, 4); // keepalivetime = 5s
                BitConverter.GetBytes((uint)5000).CopyTo(inValue, 8); // keepaliveinterval = 5s
                sock.IOControl(IOControlCode.KeepAliveValues, inValue, null);
            }
            catch
            {
                // 个别平台/环境不支持该 IOControl：静默降级为系统默认 keepalive
            }
        }
    }
}
