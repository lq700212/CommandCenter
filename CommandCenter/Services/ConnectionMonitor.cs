using System;
using System.Collections.Generic;
using System.Threading;
using CommandCenter.Utils;

namespace CommandCenter.Services
{
    /// <summary>
    /// 连接健康监控器（设计对齐 AgingTestSystem 的成熟方案，V1.0.3）。
    ///
    /// 【为什么单独建这个类，而不是塞进 Coordinator】
    /// Coordinator 只负责"检测流程"；连接健康是跨流程的系统级事务——程序暂停、关窗、
    /// 设备中途插拔/重启时都要保持自愈能力。独立监控器让"后台静默重连"职责单一，
    /// 玩法与 AgingTestSystem 的 DeviceManager.TryReconnectIo/TryReconnectBarometer 一致。
    ///
    /// 【三个原则（对照 AgingTestSystem V1.16.2 心跳机制）】
    /// 1) 心跳 = 后台轮询：相机每 2s socket 级探测；PLC 的 200ms 位信号轮询本身就是心跳，
    ///    这里只负责未连接时的自动重连兜底。
    /// 2) 日志只记边沿：连上/断开各提示一次（带设备名），连续失败的中间过程静默，
    ///    不刷日志、不打扰操作员（服务的 _lastFailed / SetConnected 已在源头去重）。
    /// 3) 后台静默持续重连：不是为了"重试几次就放弃"，而是按节流（5s）一直重试，
    ///    设备插上/恢复后几秒内自动连回，全程不影响 UI 刷新。
    ///
    /// 【线程安全】
    /// - 心跳周期在 System.Threading.Timer 后台线程执行（绝不碰 UI 线程）；
    /// - 重连动作放 Task.Run 后台：TCP 连不上最坏耗一个 TimeoutMs，绝不阻塞主链路与本周期；
    /// - 对 PLC/相机的调用，服务层已自带锁 + 日志降噪 + 状态边沿事件，本项目安全。
    /// </summary>
    public class ConnectionMonitor : IDisposable
    {
        private readonly PlcService _plc;
        private readonly List<KeyenceIV4Camera> _cameras; // 多台相机各自心跳/重连（V1.1.0）

        /// <summary>心跳周期（毫秒）：相机 socket 探测 + 未连接节流重连的决策节奏</summary>
        private const int HeartbeatMs = 2000;

        /// <summary>对已断开设备的重连节流间隔（毫秒）：避免高频无效连接占资源</summary>
        private const int ReconnectThrottleMs = 5000;

        private readonly System.Threading.Timer _timer;

        /// <summary>相机/PLC 上次发起重连的时间（节流用，后台线程读写，无需锁——最坏损失一次重连）</summary>
        private readonly DateTime[] _lastCameraAttempt;
        private DateTime _lastPlcAttempt = DateTime.MinValue;

        private volatile bool _disposed;

        public ConnectionMonitor(PlcService plc, List<KeyenceIV4Camera> cameras)
        {
            _plc = plc;
            _cameras = cameras;
            _lastCameraAttempt = new DateTime[Math.Max(1, cameras.Count)];

            // 订阅 PLC 与各相机的连接状态边沿事件（SetConnected 只在状态变化时触发一次）
            _plc.ConnectionChanged += OnPlcConnectionChanged;
            foreach (var cam in _cameras)
                cam.ConnectionChanged += OnCameraConnectionChanged;

            // 心跳定时器：先不跑（Infinite），Start() 里点火
            _timer = new System.Threading.Timer(Tick, null,
                System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }

        /// <summary>启动心跳与自动重连（紧跟在服务创建后调用）。</summary>
        public void Start()
        {
            _timer.Change(0, HeartbeatMs); // 立即首轮，之后每 2s
        }

        /// <summary>
        /// 心跳周期主循环（后台线程）。
        /// - 相机已连接 → 做 socket 级心跳探测（断连即时事件上报，UI 同步变红）；
        /// - 相机/PLC 未连接 → 按节流在后台静默重连，永不停歇，直到连上。
        /// </summary>
        private void Tick(object state)
        {
            if (_disposed) return;

            // ===== 每台相机各自心跳/重连 =====
            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam.IsConnected)
                {
                    // 心跳：纯 socket 探测，不打扰拍摄；探测发现对端已关连接会由服务自动标记断开
                    cam.CheckConnection();
                }
                else if ((DateTime.Now - _lastCameraAttempt[i]).TotalMilliseconds >= ReconnectThrottleMs)
                {
                    _lastCameraAttempt[i] = DateTime.Now;
                    int idx = i; // 闭包锁副本
                    System.Threading.Tasks.Task.Run(() => _cameras[idx].EnsureConnected()); // 后台重连，不阻塞本周期
                }
            }

            // ===== PLC（V1.12.11 起从站模式）=====
            // 已就绪无须处理：Coordinator 每 200ms 轮询"到位信号"（读自己 DataStore）本身就是心跳；
            // 从站监听未就绪则按节流后台重启监听（EnsureConnected 启动监听，端口占用/异常恢复后自动就绪）。
            if (!_plc.IsConnected &&
                (DateTime.Now - _lastPlcAttempt).TotalMilliseconds >= ReconnectThrottleMs)
            {
                _lastPlcAttempt = DateTime.Now;
                System.Threading.Tasks.Task.Run(() => _plc.EnsureConnected());
            }
        }

        // ===== 边沿日志：事件只在"状态变化"时触发，刚好对应一次明确的断连/恢复 =====

        private void OnPlcConnectionChanged(object sender, bool connected)
        {
            if (connected)
                LogHelper.Info("PLC 已连接，通讯恢复正常");
            else
                LogHelper.Warn("PLC 通讯中断，后台将持续自动重连…");
        }

        private void OnCameraConnectionChanged(object sender, bool connected)
        {
            // sender 具体是哪台，日志带 IP 方便区分（多相机 V1.1.0）
            var cam = sender as KeyenceIV4Camera;
            string who = cam != null ? cam.IpLabel : "相机";
            if (connected)
                LogHelper.Info($"{who} 已连接，通讯恢复正常");
            else
                LogHelper.Warn($"{who} 通讯中断，后台将持续自动重连…");
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            _timer.Dispose();
            _plc.ConnectionChanged -= OnPlcConnectionChanged;
            foreach (var cam in _cameras)
                cam.ConnectionChanged -= OnCameraConnectionChanged;
        }
    }
}