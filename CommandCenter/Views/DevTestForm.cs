using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Services;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 功能测试窗体（V1.12.0，仅开发者账号 dev 可进）：PLC/相机/扫码枪通讯链路手动验证工具。
    ///
    /// 【背景】PLC 业务逻辑（到位→触发→等图→上报）还没写完时，需要先单独验证
    /// "相机↔上位机""PLC↔上位机""扫码枪↔上位机"几条链路是否通。此窗体只做
    /// 【手动触发/读写/看收码】，不涉及业务编排，专供现场联调与排障。
    ///
    /// 【界面布局】
    /// ┌────────────────────────────────────────────────────────────────┐
    /// │ ▓ 功能测试（开发者）                                            │
    /// ├────────────────────────────────────────────────────────────────┤
    /// │【相机】 相机:[cmbCamera▾] 状态:[lblCamState]                    │
    /// │   [btnTrigger 仅触发T1] [btnTriggerRead 触发+判定T2]            │
    /// │   结果:[lblCamResult]（OK=绿 / NG=红 / 失败=灰）                │
    /// ├────────────────────────────────────────────────────────────────┤
    /// │【扫码枪】扫码枪:[cmbScanner▾] 状态:[lblScannerState]            │
    /// │   [btnScannerTrigger 发送触发指令]                               │
    /// │   最近读到条码:[lblScannerCode 大字]                            │
    /// │   提示:把条码放到扫码枪下读取，读到会实时显示（与主窗体共用连接）  │
    /// ├────────────────────────────────────────────────────────────────┤
    /// │【PLC】  状态:[lblPlcState]                                      │
    /// │  偏移:[txtOffset]提示:实际D地址=输入地址+偏移量(默认0按D地址)   │
    /// │  读地址测试:[txtReadAddr] [btnReadReg 读] →读到的值[txtReadVal] │
    /// │  写地址测试:[txtWriteAddr] [txtWriteVal] [btnWriteReg 写]       │
    /// │  到位:[btnReadMoveDone 读] 值[lblMoveVal] [btnClearMoveDone 清] │
    /// │  触发:[btnStartOn ON] [btnStartOff OFF]                         │
    /// │  完成:[btnDone0 复位0] [btnDone1 成功1] [btnDone2 失败2]         │
    /// │  配方:[txtRecipe] [btnWriteRecipe 下发配方]                     │
    /// ├────────────────────────────────────────────────────────────────┤
    /// │【日志】 [txtLog 多行只读滚动]                                    │
    /// └────────────────────────────────────────────────────────────────┘
    ///
    /// 【连接复用（关键）】本窗体【不新建任何 TcpClient/连接/串口】，
    /// 直接使用 MainForm 传入的 _plc / _cameras / _scanners 服务实例：
    ///   - 它们内部 EnsureConnected()/后台重连会缓存、复用主窗体同一连接；
    ///   - 扫码枪为"设备主动推码"模式：主窗体已 Open 并持续监听，此处只订阅
    ///     SerialNumberScanned 事件展示收到的条码，不重复 Open/不新建连接；
    ///   - 连接健康监控（ConnectionMonitor）仍由主窗体统一管，本窗体只读写不接管；
    ///   - 关窗体时也不 Dispose 这些服务（它们属于主窗体，由主窗体统一释放）。
    ///
    /// 【线程（红线）】所有网络 IO（触发/读写寄存器）一律丢后台线程（Task.Run），
    /// 完成后用 SafeInvoke 回到 UI 线程更新控件，绝不在 UI 线程同步读写。
    /// 扫码枪事件本身在工作线程触发，响应也统一用 SafeInvoke 回 UI。
    ///
    /// 【安全】本窗体只能由开发者账号登录进入（MainForm.OpenSettings 按角色分流），
    /// 进入后不提供任何配置修改能力，避免联调时误改现场配置。
    /// </summary>
    public partial class DevTestForm : Form
    {
        private readonly PlcService _plc;                    // 主窗体传入的 PLC 服务（复用其连接）
        private readonly List<KeyenceIV4Camera> _cameras;    // 主窗体传入的相机服务列表（复用其连接）
        private readonly List<IScanner> _scanners;           // 主窗体传入的扫码枪服务列表（复用其连接）
        private readonly List<ScanConfig> _scannerConfigs;   // 扫码枪配置列表（表头标签用，与 _scanners 下标对应）
        private volatile bool _busy;                         // 防止连点/并发触发（跨线程读）

        public DevTestForm(PlcService plc, List<KeyenceIV4Camera> cameras,
            List<IScanner> scanners, List<ScanConfig> scannerConfigs)
        {
            _plc = plc;
            _cameras = cameras ?? new List<KeyenceIV4Camera>();
            _scanners = scanners ?? new List<IScanner>();
            _scannerConfigs = scannerConfigs ?? new List<ScanConfig>();
            InitializeComponent();

            // 填充相机下拉框：每台一行"相机N IP:端口"（与主窗体标题栏命名一致）
            for (int i = 0; i < _cameras.Count; i++)
                cmbCamera.Items.Add($"相机{i + 1}  {_cameras[i].IpLabel}");
            if (cmbCamera.Items.Count > 0) cmbCamera.SelectedIndex = 0;

            // 填充扫码枪下拉框：TCP 显示 IP:端口，串口显示 COM口号+波特率
            for (int i = 0; i < _scanners.Count; i++)
                cmbScanner.Items.Add(ScannerLabel(i));
            if (cmbScanner.Items.Count > 0) cmbScanner.SelectedIndex = 0;

            RefreshStates(); // 初始刷新 PLC/相机/扫码枪连接状态
            WireEvents();    // 订阅连接状态变化事件 + 扫码枪收码事件，实时刷新
            AppendLog("功能测试窗体已打开，复用主窗体已有连接。");
            AppendLog($"PLC={_plc?.IpLabel ?? "null"}，相机数={_cameras.Count}，扫码枪数={_scanners.Count}");
        }

        /// <summary>扫码枪在测试窗体下拉框里的显示名：TCP 显示 IP:端口，串口显示 COM口号+波特率。</summary>
        private string ScannerLabel(int index)
        {
            // 优先用配置信息生成可读标签；取不到就用"扫码枪N+序号"
            if (index < _scannerConfigs.Count && _scannerConfigs[index] != null)
            {
                var sc = _scannerConfigs[index];
                // 空安全比较：Mode 为 null/空时按串口标签显示（与 BuildScanner 行为一致），防配置手改 null 崩溃
                if (sc.Mode?.Trim().Equals("Tcp", StringComparison.OrdinalIgnoreCase) == true)
                    return $"扫码枪{index + 1}  {sc.IpAddress}:{sc.Port}";
                return $"扫码枪{index + 1}  {sc.PortName}  {sc.BaudRate}";
            }
            return $"扫码枪{index + 1}";
        }

        // ────────────── 事件与通用工具 ──────────────

        /// <summary>
        /// 订阅 PLC/相机的连接状态变化事件（状态灯跟随主窗体连接情况实时变色），
        /// 及扫码枪的收码事件（Scope：测试窗体收到码就显示到界面与日志）。
        /// </summary>
        private void WireEvents()
        {
            if (_plc != null) _plc.ConnectionChanged += (s, v) => SafeInvoke(() => RefreshStates());
            foreach (var cam in _cameras)
                cam.ConnectionChanged += (s, v) => SafeInvoke(() => RefreshStates());

            // 扫码枪"设备主动推码"：订阅收码事件实时展示（主窗体业务订阅不受影响，各自独立）
            foreach (var sc in _scanners)
                sc.SerialNumberScanned += OnScannerCode;

            // 扫码枪连接状态（V1.12.5）：IScanner 新增 ConnectionChanged，状态灯随真实
            // 连接实时变色。此前扫码枪没有连接事件，状态灯只在打开窗体时刷新一次、永远
            // 停"断连"——即使后台已自动连上（如调试助手占用端口、关掉后自动连回），界面
            // 也一直显示断连，给用户"连不上"的错觉。订阅后连上转绿、断开转红即时可见。
            foreach (var sc in _scanners)
                sc.ConnectionChanged += (s, v) => SafeInvoke(() => RefreshStates());

            // 发送触发指令按钮：基恩士 SR 连上后需发 LON 才读码；扫码枪突然不读时可手动重发
            btnScannerTrigger.Click += BtnScannerTrigger_Click;
        }

        /// <summary>扫码枪收到条码（工作线程触发）：把内容显示到界面大字区与日志。</summary>
        private void OnScannerCode(object sender, string code)
        {
            SafeInvoke(() =>
            {
                lblScannerCode.Text = code ?? "";
                AppendLog($"扫码枪读到条码：{code}");
            });
        }

        /// <summary>
        /// 发送触发指令（V1.12.0）：基恩士 SR 无协议模式下，连接成功后上位机需发一条
        /// 触发指令（默认 LON）扫码枪才进入读码状态。连接成功时已自动发送过（见
        /// ScannerTcpService.TryConnect），此按钮用于扫码枪停止读码时手动重发一次。
        /// 网络写入走后台线程（红线），完成后 SafeInvoke 回 UI 刷新状态。
        /// </summary>
        private void BtnScannerTrigger_Click(object sender, EventArgs e)
        {
            var scanner = SelectedScanner();
            if (scanner == null)
            {
                MessageBox.Show("请先在列表选择一台扫码枪。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            AppendLog("→ 发送扫码枪触发指令 …");
            Task.Run(() =>
            {
                bool ok = scanner.SendTrigger();
                SafeInvoke(() =>
                {
                    AppendLog(ok ? "← 触发指令已发送" : "← 触发指令发送失败（未连接或通讯异常）");
                    FinishOp();
                });
            });
        }

        /// <summary>跨线程安全更新 UI：若当前在 UI 线程直接执行，否则丢给 UI 线程队列。</summary>
        private void SafeInvoke(Action action)
        {
            if (IsDisposed || Disposing) return; // 窗体已关：放弃
            if (InvokeRequired)
            {
                try { BeginInvoke(action); }
                catch (InvalidOperationException) { } // 句柄已销毁时的竞态，忽略
            }
            else action();
        }

        /// <summary>
        /// 把一段文本追加到日志框（带时间戳），任何线程可调（内部 SafeInvoke 回到 UI 线程）。
        /// </summary>
        private void AppendLog(string text)
        {
            SafeInvoke(() =>
            {
                string line = $"[{DateTime.Now:HH:mm:ss}] {text}";
                txtLog.AppendText(line + Environment.NewLine);
                // 始终滚到底部：最新日志可见
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
            });
        }

        /// <summary>
        /// 忙碌开关：_busy=true 时禁止再触发新操作（防连点并发读写同一连接）；
        /// 传入 false 才恢复。所有后台操作结束后必须调用 SetBusy(false)。
        /// </summary>
        private void SetBusy(bool busy)
        {
            if (_busy == busy) return;
            _busy = busy;
            // 忙碌期间把"会发起网络操作"的按钮全部禁用，操作完成恢复
            btnTrigger.Enabled = !busy;
            btnTriggerRead.Enabled = !busy;
            btnReadMoveDone.Enabled = !busy;
            btnClearMoveDone.Enabled = !busy;
            btnStartOn.Enabled = !busy;
            btnStartOff.Enabled = !busy;
            btnDone0.Enabled = !busy;
            btnDone1.Enabled = !busy;
            btnDone2.Enabled = !busy;
            btnWriteRecipe.Enabled = !busy;
            btnReadReg.Enabled = !busy;
            btnWriteReg.Enabled = !busy;
            btnScannerTrigger.Enabled = !busy;
        }

        /// <summary>刷新 PLC/相机/扫码枪连接状态标签（绿=已连接/已打开，红=断连）。</summary>
        private void RefreshStates()
        {
            lblPlcState.Text = _plc != null
                ? (_plc.IsConnected ? "● 已连接" : "○ 断连")
                : "无 PLC 服务";
            lblPlcState.ForeColor = _plc != null && _plc.IsConnected ? Color.Green : Color.Red;

            var cam = SelectedCamera();
            lblCamState.Text = cam != null
                ? (cam.IsConnected ? "● 已连接" : "○ 断连")
                : "无相机";
            lblCamState.ForeColor = cam != null && cam.IsConnected ? Color.Green : Color.Red;

            var scanner = SelectedScanner();
            lblScannerState.Text = scanner != null
                ? (scanner.IsOpen ? "● 已连接" : "○ 断连")
                : "无扫码枪";
            lblScannerState.ForeColor = scanner != null && scanner.IsOpen ? Color.Green : Color.Red;
        }

        /// <summary>当前下拉框选中的相机实例；无选中/列表为空返回 null。</summary>
        private KeyenceIV4Camera SelectedCamera()
        {
            int idx = cmbCamera.SelectedIndex;
            if (idx < 0 || idx >= _cameras.Count) return null;
            return _cameras[idx];
        }

        /// <summary>当前下拉框选中的扫码枪实例；无选中/列表为空返回 null。</summary>
        private IScanner SelectedScanner()
        {
            int idx = cmbScanner.SelectedIndex;
            if (idx < 0 || idx >= _scanners.Count) return null;
            return _scanners[idx];
        }

        /// <summary>把操作结果写入日志并刷新连接状态（后台线程回调 UI 时统一收尾）。</summary>
        private void FinishOp()
        {
            RefreshStates();
            SetBusy(false);
        }

        // ────────────── 相机操作（全部后台线程） ──────────────

        /// <summary>仅触发拍照（T1）：相机收到指令拍一张，不做判定读取。返回是否收到相机回显。</summary>
        private void BtnTrigger_Click(object sender, EventArgs e)
        {
            var cam = SelectedCamera();
            if (cam == null) { MessageBox.Show("请先在相机列表选择一台相机。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            SetBusy(true);
            AppendLog($"→ 相机 {cam.IpLabel} 触发拍照（T1）…");
            Task.Run(() =>
            {
                bool ok = cam.SendTrigger();
                SafeInvoke(() =>
                {
                    lblCamResult.Text = ok ? "T1 触发成功：已收到相机回显" : "T1 触发失败：无回显";
                    lblCamResult.ForeColor = ok ? Color.Green : Color.Gray;
                    AppendLog(ok ? "← T1 触发成功" : "← T1 触发失败（相机未回显）");
                    FinishOp();
                });
            });
        }

        /// <summary>触发＋读判定（T2）：相机拍照并回传判定结果，一次完成。</summary>
        private void BtnTriggerRead_Click(object sender, EventArgs e)
        {
            var cam = SelectedCamera();
            if (cam == null) { MessageBox.Show("请先在相机列表选择一台相机。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            SetBusy(true);
            AppendLog($"→ 相机 {cam.IpLabel} 触发+读判定（T2）…");
            Task.Run(() =>
            {
                var r = cam.TriggerAndRead();
                SafeInvoke(() =>
                {
                    if (r.Succeeded)
                    {
                        lblCamResult.Text = r.IsOk
                            ? $"T2 判定：OK（{r.ResultText}）"
                            : $"T2 判定：NG（{r.ResultText}）";
                        lblCamResult.ForeColor = r.IsOk ? Color.Green : Color.Red;
                        AppendLog($"← T2 判定 {(r.IsOk ? "OK" : "NG")}：{r.ResultText}"
                            + (string.IsNullOrEmpty(r.Detail) ? "" : "　" + r.Detail));
                    }
                    else
                    {
                        lblCamResult.Text = "T2 失败：" + r.Detail;
                        lblCamResult.ForeColor = Color.Gray;
                        AppendLog("← T2 失败：" + r.Detail);
                    }
                    FinishOp();
                });
            });
        }

        // ────────────── PLC 操作（全部后台线程；V1.12.11 起从站模式）────────────────
        // 【角色反转】PLC(汇川)做主站、上位机做从站。下列 _plc 调用底层已改为读写上位机自己
        //   DataStore 寄存器区（不连远端 PLC）：读 D100 到位=读 PLC 写入自己区的值；写寄存器=
        //   写自己区供 PLC 主站来读。功能测试这里验证"从站数据存储读写正常 + PLC 主站能读到/写入"。

        /// <summary>读到位信号（ReadMoveDone）：返回 true 表示到位寄存器≠0（PLC 主站写入 1）。</summary>
        private void BtnReadMoveDone_Click(object sender, EventArgs e)
        {
            if (!EnsurePlc()) return;
            SetBusy(true);
            AppendLog("→ 读到位信号 …");
            Task.Run(() =>
            {
                bool done = _plc.ReadMoveDone();
                SafeInvoke(() =>
                {
                    lblMoveVal.Text = done ? "1（已到位）" : "0（未到位）";
                    lblMoveVal.ForeColor = done ? Color.Green : Color.Gray;
                    AppendLog("← 到位信号 = " + (done ? "1" : "0"));
                    FinishOp();
                });
            });
        }

        /// <summary>清到位信号（写 0 复位），防止同一信号被重复处理。</summary>
        private void BtnClearMoveDone_Click(object sender, EventArgs e)
        {
            if (!EnsurePlc()) return;
            SetBusy(true);
            AppendLog("→ 清到位信号（写 0）…");
            Task.Run(() =>
            {
                _plc.ClearMoveDone();
                SafeInvoke(() =>
                {
                    lblMoveVal.Text = "0（已复位）";
                    lblMoveVal.ForeColor = Color.Gray;
                    AppendLog("← 已发送清到位信号");
                    FinishOp();
                });
            });
        }

        /// <summary>触发信号置 1（通知 PLC 开始工作） / 置 0。</summary>
        private void BtnStartOn_Click(object sender, EventArgs e) => WriteStartSignal(true);
        private void BtnStartOff_Click(object sender, EventArgs e) => WriteStartSignal(false);

        /// <summary>写触发信号公共流程：置 1/置 0 共用一个后台线程入口。</summary>
        private void WriteStartSignal(bool on)
        {
            if (!EnsurePlc()) return;
            SetBusy(true);
            AppendLog($"→ 写触发信号 = {(on ? 1 : 0)} …");
            Task.Run(() =>
            {
                _plc.SetStartSignal(on);
                SafeInvoke(() =>
                {
                    AppendLog($"← 已发送触发信号 {(on ? "1" : "0")}");
                    FinishOp();
                });
            });
        }

        /// <summary>写完成信号：1=成功，2=取像失败，0=复位。</summary>
        private void BtnDone1_Click(object sender, EventArgs e) => WriteDone(1);
        private void BtnDone2_Click(object sender, EventArgs e) => WriteDone(2);
        private void BtnDone0_Click(object sender, EventArgs e) => WriteDone(0);

        /// <summary>写完成信号公共流程。</summary>
        private void WriteDone(int code)
        {
            if (!EnsurePlc()) return;
            SetBusy(true);
            AppendLog($"→ 写完成信号 = {code} …");
            Task.Run(() =>
            {
                _plc.SetDone(code);
                SafeInvoke(() =>
                {
                    AppendLog($"← 已发送完成信号 {code}（{(code == 1 ? "成功" : code == 2 ? "取像失败" : "复位")}）");
                    FinishOp();
                });
            });
        }

        /// <summary>下发配方号（WriteRecipe，ASCII 数字串写入连续寄存器）。</summary>
        private void BtnWriteRecipe_Click(object sender, EventArgs e)
        {
            if (!EnsurePlc()) return;
            int recipeId;
            if (!int.TryParse(txtRecipe.Text.Trim(), out recipeId))
            {
                MessageBox.Show("配方号需为整数。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetBusy(true);
            AppendLog($"→ 下发配方号 {recipeId} …");
            Task.Run(() =>
            {
                bool ok = _plc.WriteRecipe(recipeId);
                SafeInvoke(() =>
                {
                    AppendLog(ok ? "← 配方下发成功" : "← 配方下发失败（PLC 通讯异常）");
                    FinishOp();
                });
            });
        }

        /// <summary>
        /// 解析协议偏移量（txtOffset）：返回 0~65535 的合法值；非法输入弹提示并返回 false。
        /// 实际读写地址 = 界面输入地址 + 偏移量（用于某些协议地址与 D 地址不一致的换算）。
        /// </summary>
        private bool TryParseOffset(out int offset)
        {
            offset = 0;
            string text = txtOffset.Text.Trim();
            if (string.IsNullOrEmpty(text)) return true; // 空=0，允许
            if (!int.TryParse(text, out offset) || offset < 0 || offset > 65535)
            {
                MessageBox.Show("协议偏移量需为 0~65535 的整数。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 把界面输入地址（D 地址）+ 协议偏移量换算为实际读写地址。
        /// 地址越界（>65535）弹提示返回 false。
        /// </summary>
        private bool TryResolveAddress(string input, out ushort actualAddress)
        {
            actualAddress = 0;
            int offset;
            if (!TryParseOffset(out offset)) return false;

            int addr;
            if (!int.TryParse(input.Trim(), out addr) || addr < 0)
            {
                MessageBox.Show("D 地址需为 0~65535 的整数。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            long combined = (long)addr + offset; // 用 long 防 int 溢出
            if (combined < 0 || combined > 65535)
            {
                MessageBox.Show($"实际地址（{addr} + {offset}）超出 0~65535 范围。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            actualAddress = (ushort)combined;
            return true;
        }

        /// <summary>通用读任意 D 地址寄存器（读地址 + 协议偏移量 → 实际地址，ReadRegister）。</summary>
        private void BtnReadReg_Click(object sender, EventArgs e)
        {
            if (!EnsurePlc()) return;
            ushort actual;
            if (!TryResolveAddress(txtReadAddr.Text, out actual)) return;

            SetBusy(true);
            AppendLog($"→ 读 D{txtReadAddr.Text.Trim()}（+偏移={txtOffset.Text.Trim()}=实际D{actual}）…");
            Task.Run(() =>
            {
                ushort value;
                bool ok = _plc.ReadRegister(actual, out value);
                SafeInvoke(() =>
                {
                    txtReadVal.Text = ok ? value.ToString() : "通讯失败";
                    AppendLog(ok ? $"← D{actual} = {value}" : $"← 读 D{actual} 失败");
                    FinishOp();
                });
            });
        }

        /// <summary>通用写任意 D 地址寄存器（写地址 + 协议偏移量 → 实际地址，WriteRegister）。</summary>
        private void BtnWriteReg_Click(object sender, EventArgs e)
        {
            if (!EnsurePlc()) return;
            ushort actual;
            if (!TryResolveAddress(txtWriteAddr.Text, out actual)) return;
            ushort value;
            if (!ushort.TryParse(txtWriteVal.Text.Trim(), out value))
            {
                MessageBox.Show("写值需为 0~65535 的整数。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            AppendLog($"→ 写 D{txtWriteAddr.Text.Trim()}（+偏移={txtOffset.Text.Trim()}=实际D{actual}）= {value} …");
            Task.Run(() =>
            {
                bool ok = _plc.WriteRegister(actual, value);
                SafeInvoke(() =>
                {
                    AppendLog(ok ? $"← 已写 D{actual} = {value}" : $"← 写 D{actual} 失败");
                    FinishOp();
                });
            });
        }

        /// <summary>PLC 服务存在性检查：为 null 时提示并返回 false。</summary>
        private bool EnsurePlc()
        {
            if (_plc == null)
            {
                MessageBox.Show("未提供 PLC 服务实例。", "功能测试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }
    }
}