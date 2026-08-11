---
name: "上位机通讯封装"
description: "封装上位机（工控HMI）与PLC、智能相机、串口仪表、Modbus设备等的通讯架构与代码范式：连接管理（手动超时/锁串行化/读写失败即断连标记）、后台心跳、节流静默自动重连、断连边沿提示、后台轮询与UI解耦、指令协议与判定解析、配置化连接参数。当用户要求开发或维护设备通讯、解决连接卡死或EndConnect崩溃或断线不重连或界面冻结等问题、或为多设备上位机设计通讯层时，使用本范式。"
---

# 上位机设备通讯封装范式

> 本技能沉淀自现场验证过的两个上位机项目（CommandCenter、AgingTestSystem）的成熟做法。
> 适用：Windows 上位机（.NET/WinForms 最常见，语言无关，范式通用）连接 PLC、相机、仪表、Modbus 设备、串口设备。
> 接手「换项目做同类通讯工作」时，直接按本范式照做，不必再口述需求。

## 一、何时使用本范式

- 上位机要连 PLC / 相机 / 串口仪表，需要**稳定、能自愈、不卡界面**的通讯层；
- 已知问题：连接不可达 IP 卡死、`EndConnect` 抛 NullReferenceException 崩溃、断线不能自动恢复、断开时界面无感知；
- 需要把「通讯」与「业务编排」「界面刷新」解耦，便于换界面框架复用。

## 二、分层架构（三件事必须分开）

```
┌─────────────────────────────────────────────┐
│ UI 层（MainForm 等）：只订阅事件刷新界面      │
│   - ConnectionChanged → 指示灯绿/红           │
│   - DataUpdated / ErrorRaised → 刷新数据/提示 │
└─────────────────────────────────────────────┘
┌─────────────────────────────────────────────┐
│ 编排层（Coordinator / DeviceManager）        │
│   - 业务循环（到位→触发→等数据→上报）         │
│   - 后台轮询 = 心跳，自动重连编排             │
└─────────────────────────────────────────────┘
┌─────────────────────────────────────────────┐
│ 服务层（每台设备的独立 Service）              │
│   - 连接（带超时/锁/失败即标记断开）           │
│   - 协议指令收发 + 判定/数据解析              │
│   - 只抛事件，不碰任何控件                   │
└─────────────────────────────────────────────┘
```

铁律：**UI 线程禁止做网络 IO**（读/写/连接全部放后台线程或 Task.Run），否则不可达 IP 会把整个界面冻住。
## 三、连接层范式（服务层：每台设备一个 Service，如 PlcService / CameraService）

### 3.1 连接必须带手动超时 + 无回调式（关键，根治 EndConnect 崩溃）

绝对不要用同步 `TcpClient.Connect(ip, port)`：对不可达 IP 会阻塞几十秒，把调用线程（尤其 UI 线程）整段冻死。
正确做法是 BeginConnect + WaitOne 手动超时，且【不注册回调线程、由主线程 EndConnect】：

```csharp
/// <summary>带超时连接（不抛异常，返回 bool + 原因）。
/// 根治 EndConnect NRE：全链路只有主线程接触 TcpClient，并在 EndConnect 前检查
/// tcp.Client 是否已被并发 Close（Close 会把内部 socket 置 null），是则放弃收尾。</summary>
private static bool TryConnect(TcpClient tcp, string ip, int port, int timeoutMs, out string error)
{
    error = null;
    try
    {
        IAsyncResult ar = tcp.BeginConnect(ip, port, null, null); // 无回调线程！
        if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
        {
            error = "连接超时";
            return false;
        }
        if (tcp.Client == null)   // 并发清理已置空内部 socket → 绝不能再碰 EndConnect
        {
            error = "连接已被并发释放，放弃收尾";
            return false;
        }
        tcp.EndConnect(ar);       // 连接失败时这里抛 SocketException，由 catch 收敛
        return true;
    }
    catch (Exception ex) { error = ex.Message; return false; }
}
```

【两个必须】① 不能把 EndConnect 放 BeginConnect 回调线程里：超时 Close/重建后回调线程
拿到已释放的 TcpClient 去 EndConnect 会抛 NRE；② 所有清理（MarkDisconnected/Dispose）必须
收进连接锁 `lock(_lock)` 并设 `_disposed` 标志，否则并发清理会在别人 WaitOne 期间 Close
socket，诱发上面 `tcp.Client == null` 那条路径——连失败都不再是 NRE，只会安全失败重连。

### 3.2 EnsureConnected：幂等 + 锁串行化 + 失败清理 + 日志去重

```csharp
private readonly object _lock = new object(); // 连接管理锁

/// <summary>确保已连接。返回 bool 表示连接可用，内部不抛异常。</summary>
public bool EnsureConnected()
{
    lock (_lock) // 必须串行化：轮询线程与 UI 关窗线程并发 Close/重建会互相踩引用
    {
        try
        {
            if (_tcp != null && _tcp.Connected) return true; // 已连直接返回（幂等）

            _tcp?.Close();
            _tcp = new TcpClient();
            _tcp.ReceiveTimeout = _cfg.TimeoutMs;
            _tcp.SendTimeout = _cfg.TimeoutMs;
            ConnectWithTimeout(_tcp, _cfg.IpAddress, _cfg.Port, _cfg.TimeoutMs);
            SetConnected(true);
            Log("连接成功");
            return true;
        }
        catch (Exception ex)
        {
            SetConnected(false);
            _tcp?.Close(); _tcp = null;  // 清理失效引用，下次必然完整重建
            if (!_lastFailed) { _lastFailed = true; Log("连接失败"); } // 日志只记一次，防刷屏
            return false;
        }
    }
}
```

### 3.3 读写失败 = 断连（主动识别断线的前提）

连接状态不能靠 `TcpClient.Connected` 这个缓存值自欺：它不反映真实对端存活。
正确语义：**任何实际读写抛连接层异常（SocketException / IOException / TimeoutException）就立即标记断开**，
否则 UI 会「网络断了还一直显示已连接」：

```csharp
private void MarkDisconnectedOnFailure(Exception ex)
{
    if (ex is SocketException || ex is System.IO.IOException || ex is TimeoutException)
        SetConnected(false); // IsConnected=false → UI 变红 → 编排层触发自动重连
}
```

`SetConnected` 必须带边沿检测（状态没变不发事件），避免每秒刷事件：

```csharp
public event EventHandler<bool> ConnectionChanged;
public bool IsConnected { get; private set; }
private void SetConnected(bool value)
{
    if (IsConnected != value)
    {
        IsConnected = value;
        ConnectionChanged?.Invoke(this, value); // 只在“连上/断开”边沿触发一次
    }
}
```

### 3.4 Dispose：限时抢锁 + 锁外强断网（关窗绝不能被锁拖死）

⚠ 后台连接/轮询任务在锁内做 `TryConnect`（每次要等一个超时）时，若轮询 200ms 就来一轮，
锁会被**连续占满**，此时 `Dispose` 里裸 `lock(_lock)` 可能**永远拿不到锁** → UI 关窗线程卡死在
FormClosing → 窗口不销毁、进程退出不了（实测卡 >14s 就是这个根因）。三个动作保证关窗不被拖死：

1. 先置 `_disposed = true`（后台下一次迭代见到它就放弃，不再重建连接）；
2. `Monitor.TryEnter(_lock, 300ms)` 限时抢锁，绝不无限等待；
3. 拿不到锁就"锁外强断网"兜底：`_tcp.Close()` 会让持锁任务的 `BeginConnect` 立刻结束
   （`WaitOne` 返回后走 `TryConnect` 的 `tcp.Client == null` / `EndConnect` 抛异常两条路之一，
   其 `catch` 自会收敛并释放锁）。

```csharp
public void Dispose()
{
    _disposed = true;
    if (Monitor.TryEnter(_lock, TimeSpan.FromMilliseconds(300)))
    {
        try { try { _master?.Dispose(); } catch { } _tcp?.Close(); }
        finally { Monitor.Exit(_lock); }
    }
    else
    {
        Log("Dispose 未拿到锁（后台连接任务繁忙），改走锁外强断网");
        try { _tcp?.Close(); } catch { }
    }
}
```

## 四、心跳 + 自动重连范式（编排层：ConnectionMonitor / DeviceManager）

一个独立类统管所有设备的连接健康，不要塞进业务协调器（业务循环暂停/关窗时连接也要自愈）。

三个原则（互相矛盾的诉求的统一解法）：
1. **心跳 = 后台轮询**：2s 一次轻量探测（或直接靠业务轮询兜底），断连 1~3s 内被感知；
2. **日志只记边沿**：连上/断开各提示一次（带设备名），中间失败静默，不打扰操作员；
3. **后台静默持续重连**：不停“重试几次就放弃”，而是按节流（3~5s）一直重试，设备插回后自动连回。

```csharp
public class ConnectionMonitor : IDisposable
{
    private readonly PlcService _plc;
    private readonly CameraService _camera;
    private const int HeartbeatMs = 2000;        // 心跳周期
    private const int ReconnectThrottleMs = 5000; // 重连节流
    private DateTime _lastCameraAttempt = DateTime.MinValue;
    private readonly System.Threading.Timer _timer; // 后台线程，绝不碰 UI

    public ConnectionMonitor(PlcService plc, CameraService camera)
    {
        _plc = plc; _camera = camera;
        _plc.ConnectionChanged += OnPlcChanged;
        _camera.ConnectionChanged += OnCameraChanged;
        _timer = new System.Threading.Timer(Tick, null, Timeout.Infinite, Timeout.Infinite);
    }
    public void Start() => _timer.Change(0, HeartbeatMs);

    private void Tick(object state)
    {
        if (_camera.IsConnected) _camera.CheckConnection(); // 心跳：socket 层探测
        else if ((DateTime.Now - _lastCameraAttempt).TotalMilliseconds >= ReconnectThrottleMs)
        {
            _lastCameraAttempt = DateTime.Now;
            System.Threading.Tasks.Task.Run(() => _camera.EnsureConnected()); // 重连放后台，不阻塞周期
        }
        // PLC 同理：未连接则节流后台重连；已连接无需动作（业务轮询本身即心跳）
    }

    private void OnPlcChanged(object sender, bool ok)
        => Log(ok ? "PLC 已连接" : "PLC 通讯中断，后台将持续自动重连…");
    private void OnCameraChanged(object sender, bool ok)
        => Log(ok ? "相机已连接" : "相机通讯中断，后台将持续自动重连…");

    public void Dispose()
    {
        _timer.Dispose();
        _plc.ConnectionChanged -= OnPlcChanged;
        _camera.ConnectionChanged -= OnCameraChanged;
    }
}
```

【socket 级心跳探测（不打扰业务通讯）】只有已连接时才调：
对端正常关闭（FIN）时 `Poll(0, SelectRead) && Available==0` 可探出；拔网线这类无声断连
靠 Poll 探不出，最终以业务读写失败标记断开兜底。

## 五、业务编排与 UI 解耦

### 5.1 轮询用 System.Threading.Timer（后台线程），绝不用 Forms.Timer
`Forms.Timer` 在 UI 线程回调，里面做网络 IO 不可达 IP 时界面整个冻住。这是「点按钮半天才响应」的经典根因。

### 5.2 事件跨线程回 UI：BeginInvoke + IsDisposed 保护
后台线程抛事件 → UI 订阅方必须切回 UI 线程再刷控件：

```csharp
private void OnStatusChanged(string text)
{
    if (IsDisposed) return;
    if (InvokeRequired) { BeginInvoke(new Action<string>(OnStatusChanged), text); return; }
    lblStatus.Text = text;
}
```

### 5.3 一个事件可能被多个后台路径触发 → 双收尾保护
例如「等图超时函数」与「FTP 新图事件」可能同时收尾一次检测，必须只认第一次：

```csharp
private int _finished; // 0=待收尾 1=已收尾
if (Interlocked.Exchange(ref _finished, 1) != 0) return; // 已收尾过，直接放弃
try { /* 真正收尾 */ }
finally { Interlocked.Exchange(ref _finished, 0); /* 复位供下次使用 */ }
```
忙碌/串行标志统一用 volatile int + Interlocked，避免多线程读到脏值。

### 5.4 后台 Timer 释放竞态：_disposed 标志 + SafeChange

关窗 Dispose（UI 线程）与后台 Tick（线程池）并发时，`timer.Change()` 可能撞上已 Dispose 的
Timer 抛 `ObjectDisposedException`。统一防护：① 编排/服务类带 volatile `_disposed`，Dispose 最
先置 `_disposed=true` 再释放；② 每个调 `_timer.Change` 的地方都走 `SafeChange` 包装（catch 掉
ObjectDisposedException 安静忽略）——不要在 Dispose 里与后台回调做"零窗口同步"，那不可能做到：

```csharp
private volatile bool _disposed;
private readonly System.Threading.Timer _positionTimer; // 后台轮询

private void SafeChange(System.Threading.Timer t, int due, int period)
{
    try { t.Change(due, period); }
    catch (ObjectDisposedException) { } // 已释放：后台回调就此终止，无需再调度
}

// Tick 第一行都要查：
private void Tick(object state)
{
    if (_disposed) return; // 关窗已发生，直接退出本轮回调
    ...
    SafeChange(_positionTimer, 200, 200);
}

public void Dispose()
{
    _disposed = true; // 先置标志，再释放 Timer
    _positionTimer?.Dispose();
}
```

## 六、协议封装与判定解析范式

- **指令/寄存器地址全部配置化**：Knife 不写死数值，进配置节点（如 `ip/port/readAddress/triggerCommand`），
  现场只改 JSON 不重编译。字符串型枚举（停止位 "1"/"15"/"2"、校验位枚举名 None/Odd）存字符串，读写两端大小写兼容。
- **ASCII 指令收发**：指令正文 + 行结束符（CR/LF）由配置决定；响应按字节拼行、遇 CR/LF 截断，
  读超时用 `stream.ReadTimeout` 兜底，防止对方不回帧时无限阻塞。
- **判定解析规则化 + 保守失败**：解析逻辑集中在一个方法（如 ParseResult），明确每位含义与 OK 判定；
  任何异常/超时/格式错误都视为「通讯失败」而非「NG 结果」，把两者严格区分（失败走异常上报，NG 才进业务统计）。
- **日志恰到好处**：成功/失败各记一条 + 关键数据；失败在上层做去重（`_lastFailed`），这既是排障依据也不刷屏。

## 七、完成后自检清单（交付前逐条核对）

1. 所有网络 IO（连接/读写）都在后台线程或 Task.Run，UI 线程零网络调用；
2. 连接带手动超时（BeginConnect+WaitOne），无回调式 EndConnect，无裸 `TcpClient.Connect`；
3. 服务层所有连接/断开/读写被同一把锁串行化，Dispose 与轮询并发安全；
4. 读写失败即 `SetConnected(false)`，状态变化事件带边沿检测；
5. 每个设备有独立心跳 + 节流静默自动重连（监控类单独存在，业务编译 Mandarbox 不停它也自愈）；
6. UI 订阅事件全部 BeginInvoke 切线程 + IsDisposed 保护；
7. 可能并发收尾/并发的资源临界区全部用 Interlocked/锁保护；
8. 构建通过 + 冒烟测试（启动进程、等待数秒确认存活、再关闭）；日志确认边沿只记一次、不刷屏；
9. Dispose 不允许无限等锁：先 `_disposed=true`，再限时抢锁（TryEnterLock），拿不到就锁外强断网，保证关窗不被后台连接任务拖死；
10. 后台轮询 Timer 释放加 `_disposed` 标志 + `SafeChange` 包装，杜绝 Tick 撞上已 Dispose 的 Timer 抛 ObjectDisposedException；
11. 通讯相关改动同步更新通讯文档与 CHANGELOG（含寄存器/指令表与版本号）。

## 八、来源与参考实现

本技能提炼自两个现场跑过的 Windows 上位机（.NET Framework 4.7.2 / WinForms / C# 7.3）：
- CommandCenter（相机+PLC 命令中心）：`Services/KeyenceIV4Camera.cs`、`Services/PlcService.cs`、`Services/ConnectionMonitor.cs`、`Services/ProductionCoordinator.cs`
- AgingTestSystem（老化测试）：`Services/ModbusTcpIoController.cs`、`Services/DeviceManager.cs`
遇到对照需求时打开这些文件看完整注释，比本技能更细。范式语言无关：换语言（Python/Java/C++）只改语法，结构与原则不变。