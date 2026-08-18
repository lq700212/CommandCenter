using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Services;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 开发者模式窗体（原名 DevTestForm，V2.15.10 更名；V1.12.0 起，仅开发者账号 dev 可进）：
    /// PLC/相机/扫码枪通讯链路手动验证工具 + 账号管理（防错机制：现场管理员忘密码时，
    /// 开发者可在此重置任意账号密码，见下方"账号管理"区说明）。
    ///
    /// 【背景】PLC 业务逻辑（到位→触发→等图→上报）还没写完时，需要先单独验证
    /// "相机↔上位机""PLC↔上位机""扫码枪↔上位机"几条链路是否通。此窗体只做
    /// 【手动触发/读写/看收码】，不涉及业务编排，专供现场联调与排障。
    ///
    /// 【界面布局】
    /// ┌────────────────────────────────────────────────────────────────┐
    /// │ ▓ 开发者模式                                                     │
    /// ├────────────────────────────────────────────────────────────────┤
    /// │【账号管理】[dgvAccounts 表格]：账号 | 角色 | 启用 | 密码(掩码)    │
    /// │   选中账号:[lblAccAccount] 新密码:[txtNewPwd] 确认:[txtNewPwd2]  │
    /// │   [btnChangePwd 修改密码]（重置该账号密码并写盘；防管理员忘密码  │
    /// │   锁死——开发者可进本页帮现场用户重设新密码，详见"账号管理"说明） │
    /// ├────────────────────────────────────────────────────────────────┤
    /// │【相机】 相机:[cmbCamera▾] 状态:[lblCamState]  [picTestShot 预览]│
    /// │   [btnTrigger 仅触发T1] [btnTriggerRead 触发+判定T2(取图存图)] │
    /// │   结果:[lblCamResult]（OK=绿 / NG=红 / 失败=灰）              │
    /// │   [btnReadProgramNo 读当前程序号][lblCurrentProgram]          │
    /// │   [btnSwProg1 切换程序→P001] [btnSwProg2 切换程序→P002]       │
    /// │   右侧:[lblTestImagePath 最近存图路径]（T2 后自动取图闪图存图）│
    /// ├────────────────────────────────────────────────────────────────┤
    /// │【扫码枪】扫码枪:[cmbScanner▾] 状态:[lblScannerState]            │
    /// │   [btnScannerTrigger 发送触发指令][btnShowScannerFail 显示异常弹窗]│
    /// │   最近读到条码:[lblScannerCode 大字]                            │
    /// │   提示:把条码放到扫码枪下读取，读到会实时显示（与主窗体共用连接）  │
    /// ├────────────────────────────────────────────────────────────────┤
    /// │【PLC】  状态:[lblPlcState]                                      │
    /// │  偏移:[txtOffset]提示:实际D地址=输入地址+偏移量(默认0按D地址)   │
    /// │  读地址测试:[txtReadAddr] [btnReadReg 读] →读到的值[txtReadVal] │
    /// │  写地址测试:[txtWriteAddr] [txtWriteVal] [btnWriteReg 写]       │
    /// │  （V2.12.3 默认地址=DataStore 索引：读=2 第1台相机/3 第2台相机请求、写=5/6 结果；      │
    /// │   PLC 协议号=索引+40000，填 2 就是 D2，零换算；第3台起每台相机地址在相机表填）  │
    /// │  请求:[btnReadScanReq 读扫码请求] [btnReadCamReq 读相机请求]    │
    /// │        值:[lblMoveVal]                                          │
    /// │  型号:[btnWriteModel 写产品型号] [txtModel] (→40007=序号+40008~40012=型号) │
    /// │  结果:[btnResScan0 复位0] [btnResScan1 OK1] [btnResScan2 NG2]   │
    /// │  相机:[btnResCamUp 相机OK1] [btnResCamDown 相机NG2]            │
    /// │       [btnResCamReset 相机复位0]（写全部相机通道）               │
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
    /// 【安全】本窗体只能由开发者账号登录进入（MainForm.OpenSettings 按角色分流）。
    ///   本窗体除【账号管理】区外【不产生任何配置改动】（只做通讯验证，联调误操作不
    ///   污染现场配置）；【账号管理】是唯一例外——开发者可用它重置管理员/开发者密码
    ///   （改的是 SecurityConfig 的密码哈希 + ConfigStore.Save 写盘），防"管理员现场
    ///   改了密码后忘记、把自己锁在外面"的兜底通道。
    /// 【账号管理（V2.15.10 防错机制）】现场管理员在登录框"修改密码"面板改过密码后
    ///   忘记新密码时：开发者用 dev 账号登录本页 → 选中对应账号 → 输入新密码两次 →
    ///   点【修改密码】即重置该账号密码并写盘，用户下次登录用新密码即可。
    ///   安全约定（与全局一致）：表格只显示密码掩码（●●●●●●），绝不放明文；
    ///   新密码至少 6 位、两次输入一致；改完清掉该角色的"记住密码"记录（防旧记录回填
    ///   导致登录失败）；密码只存 SHA-256 哈希。账号数据源 = MainForm 传入的 _config.Security。
    /// </summary>
    public partial class DeveloperModeForm : Form
    {
        private readonly PlcService _plc;                    // 主窗体传入的 PLC 服务（复用其连接）
        private readonly List<KeyenceIV4Camera> _cameras;    // 主窗体传入的相机服务列表（复用其连接）
        private readonly List<IScanner> _scanners;           // 主窗体传入的扫码枪服务列表（复用其连接）
        private readonly List<ScanConfig> _scannerConfigs;   // 扫码枪配置列表（表头标签用，与 _scanners 下标对应）
        private readonly ImageStore _imageStore;             // 主窗体传入的图像存储服务（V1.12.24 取图存图测试复用，不新建）
        private readonly List<CameraConfig> _cameraConfigs;  // 相机配置列表（取每台 FTP 取图目录用，与 _cameras 下标对应）
        private readonly string _serialSnapshot;             // 打开测试窗体时的当前产品序列号快照（存图 {SN} 目录用）
        private readonly AppConfig _config;                  // 整个配置引用（V2.15.10 账号管理：改密码改 SecurityConfig 哈希并 ConfigStore.Save 写盘）
        private volatile bool _busy;                         // 防止连点/并发触发（跨线程读）

        // T2 触发后等待相机 FTP 推图的最长等待时间（V1.12.27）：
        // 相机拍完照到把 jpeg/iv4p 推到 FTP 取图目录有网络/存储延迟，触发成功返回时图未必已到。
        // 主流程靠 FileSystemWatcher 事件等图到达（OnFtpFileArrived），测试窗体没有事件机制，
        // 改为"触发后轮询扫描取图目录"，最多等这么久还见不到新图才报失败，防止"触发成功却没图"。
        private const int FtpWaitAfterTriggerMs = 5000;

        /// <summary>本窗体对话框标题（V2.15.0 双语，MessageBox 统一用；V2.15.10 更名"开发者模式"）。</summary>
        private string Title => I18n.T("开发者模式", "Developer Mode");

        public DeveloperModeForm(PlcService plc, List<KeyenceIV4Camera> cameras,
            List<IScanner> scanners, List<ScanConfig> scannerConfigs,
            ImageStore imageStore, List<CameraConfig> cameraConfigs, string serialSnapshot,
            AppConfig config)
        {
            _plc = plc;
            _cameras = cameras ?? new List<KeyenceIV4Camera>();
            _scanners = scanners ?? new List<IScanner>();
            _scannerConfigs = scannerConfigs ?? new List<ScanConfig>();
            _imageStore = imageStore;
            _cameraConfigs = cameraConfigs ?? new List<CameraConfig>();
            _serialSnapshot = serialSnapshot ?? "";
            _config = config;
            InitializeComponent();

            // 填充相机下拉框：每台一行"相机N IP:端口"（V1.12.22 起带名称：上相机/下相机；
            // V2.13.4 无名称时优先用 CameraId 真编号、其次行序，与设置页第一列一致）
            for (int i = 0; i < _cameras.Count; i++)
            {
                string name = _cameras[i].DisplayName;
                if (string.IsNullOrWhiteSpace(name))
                {
                    int camId = (i >= 0 && i < _cameraConfigs.Count && _cameraConfigs[i] != null)
                        ? _cameraConfigs[i].CameraId : 0;
                    name = (camId > 0 ? I18n.T($"相机{camId}", $"Cam{camId}") : I18n.T($"相机{i + 1}", $"Cam{i + 1}")) + "  " + _cameras[i].IpLabel;
                }
                else
                {
                    name = name + "  " + _cameras[i].IpLabel;
                }
                cmbCamera.Items.Add(name);
            }
            if (cmbCamera.Items.Count > 0) cmbCamera.SelectedIndex = 0;

            // 填充扫码枪下拉框：TCP 显示 IP:端口，串口显示 COM口号+波特率
            for (int i = 0; i < _scanners.Count; i++)
                cmbScanner.Items.Add(ScannerLabel(i));
            if (cmbScanner.Items.Count > 0) cmbScanner.SelectedIndex = 0;

            RefreshStates(); // 初始刷新 PLC/相机/扫码枪连接状态
            WireEvents();    // 订阅连接状态变化事件 + 扫码枪收码事件，实时刷新
            LoadAccounts();  // V2.15.10 账号管理：把 SecurityConfig 里的账号填入表格（含掩码密码）
            AppendLog("开发者模式窗体已打开，复用主窗体已有连接。");
            AppendLog($"PLC={_plc?.IpLabel ?? "null"}，相机数={_cameras.Count}，扫码枪数={_scanners.Count}");
            ApplyLanguage(); // V2.15.0 国际化：按当前语言初始化全部界面文本
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
                    return I18n.T($"扫码枪{index + 1}", $"Scanner{index + 1}") + "  " + sc.IpAddress + ":" + sc.Port;
                return I18n.T($"扫码枪{index + 1}", $"Scanner{index + 1}") + "  " + sc.PortName + "  " + sc.BaudRate;
            }
            return I18n.T($"扫码枪{index + 1}", $"Scanner{index + 1}");
        }

        // ────────────── 事件与通用工具 ──────────────

        /// <summary>
        /// 订阅 PLC/相机的连接状态变化事件（状态灯跟随主窗体连接情况实时变色），
        /// 及扫码枪的收码事件（Scope：测试窗体收到码就显示到界面与日志）。
        /// </summary>
        private void WireEvents()
        {
            if (_plc != null)
            {
                _plc.ConnectionChanged += (s, v) => SafeInvoke(() => RefreshStates());
                // V1.12.11：从站模式下还要看"主站是否真的连入"，订阅主站连入事件实时刷新状态
                _plc.MasterConnectionChanged += (s, v) => SafeInvoke(() => RefreshStates());
            }
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
            // V2.15.4 测试入口：弹"扫码枪异常提醒"对话框看样式/交互（纯 UI，不碰通讯）
            btnShowScannerFail.Click += BtnShowScannerFail_Click;
            // V2.15.10 账号管理：选中表格行 → 底部"选中账号"标签实时跟随显示
            dgvAccounts.SelectionChanged += (s, e) => UpdateSelectedAccountLabel();
        }

        // ────────────── 账号管理（V2.15.10 防错机制）──────────────

        /// <summary>
        /// 把 SecurityConfig 里的全部账号（管理员 admin + 开发者 dev）填入账号管理表格。
        /// 密码列只显示掩码"●●●●●●"（空哈希显示"未设置"），【绝不放明文/哈希】——
        /// 项目红线"配置不存明文、界面不显示明文"，改密码走下方输入框 + HashPassword。
        /// 行 Tag 存角色键（"admin"/"dev"），改密码时据此定位到 SecurityConfig 对应哈希字段。
        /// </summary>
        private void LoadAccounts()
        {
            dgvAccounts.Rows.Clear();
            var sec = _config?.Security;
            if (sec == null)
            {
                lblAccAccount.Text = I18n.T("（未提供配置）", "(no config)");
                return;
            }
            AddAccountRow(sec.AdminUser, I18n.T("管理员", "Admin"), sec.AdminEnabled, sec.AdminPasswordHash, "admin");
            AddAccountRow(sec.DevUser, I18n.T("开发者", "Developer"), sec.DevEnabled, sec.DevPasswordHash, "dev");
        }

        /// <summary>往账号表格加一行（空用户名兜底显示"?"防手改配置 null 崩溃）。</summary>
        private void AddAccountRow(string user, string role, bool enabled, string pwdHash, string tag)
        {
            int idx = dgvAccounts.Rows.Add(
                string.IsNullOrWhiteSpace(user) ? "?" : user,
                role,
                I18n.T(enabled ? "启用" : "停用", enabled ? "Enabled" : "Disabled"),
                string.IsNullOrEmpty(pwdHash) ? I18n.T("未设置", "Not set") : "●●●●●●");
            dgvAccounts.Rows[idx].Tag = tag;
        }

        /// <summary>表格选中行变化时，把"选中账号"标签刷新成当前选中账号（无选中显示占位）。</summary>
        private void UpdateSelectedAccountLabel()
        {
            if (dgvAccounts.SelectedRows.Count > 0
                && dgvAccounts.SelectedRows[0].Cells["colAccUser"].Value != null)
                lblAccAccount.Text = dgvAccounts.SelectedRows[0].Cells["colAccUser"].Value.ToString();
            else
                lblAccAccount.Text = I18n.T("（未选中）", "(not selected)");
        }

        /// <summary>
        /// 修改选中账号的密码（V2.15.10 防错机制）：开发者在此重置管理员/开发者密码。
        /// 场景：现场管理员在登录框"修改密码"面板改过密码后忘记新密码、把自己锁在外面，
        /// 开发者用 dev 账号进本页帮他重设——校验"新密码≥6位 + 两次一致"后把对应账号的
        /// SHA-256 哈希写进 SecurityConfig 并 ConfigStore.Save 落盘，用户下次登录用新密码。
        /// 与全局安全约定一致：只存哈希、不清明文；改完清掉该角色"记住密码"记录（旧记录
        /// 里是旧密码，留着会导致下次自动回填失败），并保持双账号记住记录互斥。
        /// </summary>
        private void BtnChangePwd_Click(object sender, EventArgs e)
        {
            var sec = _config?.Security;
            if (sec == null)
            {
                MessageBox.Show(I18n.T("未提供配置对象，无法修改密码。", "No config available; cannot change password."),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dgvAccounts.SelectedRows.Count == 0)
            {
                MessageBox.Show(I18n.T("请先在表格中选中要修改密码的账号。", "Select an account in the table first."),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string tag = dgvAccounts.SelectedRows[0].Tag as string;
            string user = dgvAccounts.SelectedRows[0].Cells["colAccUser"].Value?.ToString() ?? "";
            if (tag != "admin" && tag != "dev")
            {
                MessageBox.Show(I18n.T("该账号不支持在此修改密码。", "This account cannot be changed here."),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newPwd = txtNewPwd.Text;
            if (newPwd.Length < 6)
            {
                MessageBox.Show(I18n.T("新密码至少 6 位，请重新输入。", "New password must be at least 6 characters."),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPwd.Clear();
                txtNewPwd.Focus();
                return;
            }
            if (newPwd != txtNewPwd2.Text)
            {
                MessageBox.Show(I18n.T("两次输入的新密码不一致，请重新输入。", "The two new passwords do not match."),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPwd2.Clear();
                txtNewPwd2.Focus();
                return;
            }

            // 只落 SHA-256 哈希（与 LoginForm 改密码同一规则），不存明文
            if (tag == "admin")
                sec.AdminPasswordHash = SecurityUtil.HashPassword(newPwd);
            else
                sec.DevPasswordHash = SecurityUtil.HashPassword(newPwd);

            // 改完密码清掉双角色"记住密码"记录：本角色旧记录里是旧密码（回填会登录失败），
            // 对方角色也要清（保持"一次只记最近登录角色"的互斥约定，见 SecurityUtil 注释）
            SecurityUtil.ClearRememberedLogin(false);
            SecurityUtil.ClearRememberedLogin(true);

            try
            {
                ConfigStore.Save(_config);
                LogHelper.Info($"开发者模式：账号「{user}」密码已修改并写盘");
                MessageBox.Show(I18n.T($"账号「{user}」密码修改成功，下次登录请使用新密码。",
                    $"Password for \"{user}\" changed. Use the new password next login."),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtNewPwd.Clear();
                txtNewPwd2.Clear();
                LoadAccounts();                    // 刷新表格（掩码/启用文本按最新配置）
                lblAccAccount.Text = user;         // 刷新后恢复选中账号显示
                foreach (DataGridViewRow r in dgvAccounts.Rows)
                    if ((r.Tag as string) == tag) { r.Selected = true; break; } // 保持选中刚改的行
            }
            catch (Exception ex)
            {
                LogHelper.Error("保存账号密码失败：" + ex.Message);
                MessageBox.Show(I18n.T(
                    "密码已修改但写盘失败（" + ex.Message + "），本次运行生效，重启后可能恢复旧密码。",
                    "Password changed but saving failed (" + ex.Message + "). Effective for this run; it may revert after restart."),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
        /// 显示"扫码枪异常提醒"对话框（V2.15.4 开发者测试入口）：点击直接弹 ScannerFailForm，
        /// 供联调/验收其外观与交互。传示例失败文本（可直观看到 lblFailText 失败文本行）与主窗体
        /// 传入的扫码枪列表（打开期间若真读到一条真码，本窗会自动以"稍后处理"语义关闭，
        /// 顺带验证 V2.14.48 行为）。本方法只 new + ShowDialog，不触发扫码枪、不做任何通讯，
        /// 可反复点击；返回后把用户选择写进操作日志。
        /// </summary>
        private void BtnShowScannerFail_Click(object sender, EventArgs e)
        {
            using (var dlg = new ScannerFailForm("ERROR", _scanners))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AppendLog(I18n.T("扫码枪异常弹窗：【人工补录】", "Scanner error dialog: [Manual Input]"));
                else
                    AppendLog(I18n.T("扫码枪异常弹窗：【稍后处理】/自动关闭", "Scanner error dialog: [Later] / auto closed"));
            }
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
                MessageBox.Show(I18n.T("请先在列表选择一台扫码枪。", "Please select a scanner from the list first."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            btnReadProgramNo.Enabled = !busy;
            btnSwProg1.Enabled = !busy;
            btnSwProg2.Enabled = !busy;
            btnReadScanReq.Enabled = !busy;
            btnReadCamReq.Enabled = !busy;
            btnWriteModel.Enabled = !busy;
            btnResScan0.Enabled = !busy;
            btnResScan1.Enabled = !busy;
            btnResScan2.Enabled = !busy;
            btnResCamUp.Enabled = !busy;
            btnResCamDown.Enabled = !busy;
            btnResCamReset.Enabled = !busy;
            btnReadReg.Enabled = !busy;
            btnWriteReg.Enabled = !busy;
            btnScannerTrigger.Enabled = !busy;
        }

        /// <summary>刷新 PLC/相机/扫码枪连接状态标签（绿=已连接/已打开，红=断连）。
        /// PLC 为从站模式（V1.12.11），显示三态：主站已连入(绿) / 监听就绪等待主站(橙) / 监听失败(红)。</summary>
        private void RefreshStates()
        {
            if (_plc != null)
            {
                lblPlcState.Text = _plc.HasMasterConnected ? I18n.T("● 主站已连入", "● Master connected")
                    : _plc.IsConnected ? I18n.T("● 监听就绪（等待主站）", "● Listening (waiting master)")
                    : I18n.T("○ 监听失败", "○ Listen failed");
                lblPlcState.ForeColor = _plc.HasMasterConnected ? Color.Green
                    : _plc.IsConnected ? Color.Orange
                    : Color.Red;
            }
            else
            {
                lblPlcState.Text = I18n.T("无 PLC 服务", "No PLC service");
                lblPlcState.ForeColor = Color.Gray;
            }

            var cam = SelectedCamera();
            lblCamState.Text = cam != null
                ? (cam.IsConnected ? I18n.T("● 已连接", "● Connected") : I18n.T("○ 断连", "○ Disconnected"))
                : I18n.T("无相机", "No camera");
            lblCamState.ForeColor = cam != null && cam.IsConnected ? Color.Green : Color.Red;

            var scanner = SelectedScanner();
            lblScannerState.Text = scanner != null
                ? (scanner.IsOpen ? I18n.T("● 已连接", "● Connected") : I18n.T("○ 断连", "○ Disconnected"))
                : I18n.T("无扫码枪", "No scanner");
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
            if (cam == null) { MessageBox.Show(I18n.T("请先在相机列表选择一台相机。", "Please select a camera from the camera list first."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            SetBusy(true);
            AppendLog($"→ 相机 {cam.IpLabel} 触发拍照（T1）…");
            Task.Run(() =>
            {
                bool ok = cam.SendTrigger();
                SafeInvoke(() =>
                {
                    lblCamResult.Text = ok ? I18n.T("T1 触发成功：已收到相机回显", "T1 triggered: camera echoed OK")
                        : I18n.T("T1 触发失败：无回显", "T1 trigger failed: no echo");
                    lblCamResult.ForeColor = ok ? Color.Green : Color.Gray;
                    AppendLog(ok ? "← T1 触发成功" : "← T1 触发失败（相机未回显）");
                    FinishOp();
                });
            });
        }

        /// <summary>
        /// 触发＋读判定（T2）+ 取图存图（V1.12.24）：相机拍照并回传判定结果后，
        /// 上位机再去该相机的 FTP 取图目录拿【修改时间最新】的 jpeg（+iv4p 如有），
        /// 在界面右侧 picTestShot 闪图，并按主窗体相同的归档规则保存到
        /// ImageConfig.SaveRootDir 下（点位固定用 1——测试场景未指定点位）。
        /// 【V1.12.27 时序修复】T2 触发成功后不能立即扫目录：相机推图到 FTP 有延迟，
        /// 立即扫可能取到旧图或空目录。现在触发后轮询等待最多 5 秒，认"修改时间不早
        /// 于触发时刻"的新图，超时才报失败。
        /// 复用主窗体传入的 _imageStore 实例与相机连接，不新建任何连接、不改配置。
        /// 所有网络/文件 IO 在后台线程执行，完成后 SafeInvoke 回 UI 更新。
        /// </summary>
        private void BtnTriggerRead_Click(object sender, EventArgs e)
        {
            var cam = SelectedCamera();
            if (cam == null) { MessageBox.Show(I18n.T("请先在相机列表选择一台相机。", "Please select a camera from the camera list first."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            int camIndex = cmbCamera.SelectedIndex; // 取图目录按相机下标对应配置

            SetBusy(true);
            AppendLog($"→ 相机 {cam.IpLabel} 触发+读判定（T2）…");
            Task.Run(() =>
            {
                var r = cam.TriggerAndRead();
                // 触发成功 → 去该相机 FTP 取图目录拿最新图并归档（点位固定 1，测试用）
                string jpeg = null, iv4p = null, archived = null;
                string fetchError = null;
                if (r.Succeeded)
                {
                    // V1.12.27 时序修复：T2 触发成功只代表"相机已拍照并回判定"，图推到 FTP
                    // 取图目录还有延迟（网络/存储），立即扫目录可能扫不到本次新图。
                    // 主流程靠 FileSystemWatcher 事件等图到达，测试窗体没有事件机制，
                    // 改为"记录触发时刻 → 轮询扫目录"：直到出现"修改时间不早于触发时刻"
                    // 的 jpeg（视为本次新图）或等待超时；超时后仍有旧图残留则取最新对兜底。
                    DateTime triggerUtc = DateTime.UtcNow;
                    var stopwatch = Stopwatch.StartNew();
                    var pair = new ImageStore.LatestPairResult();
                    while (stopwatch.ElapsedMilliseconds < FtpWaitAfterTriggerMs)
                    {
                        var candidate = ResolveLatestFtpPair(camIndex);
                        // 找到本次触发后的新图 → 直接收下（jpeg 的时间戳晚于触发时刻）
                        if (!string.IsNullOrEmpty(candidate.JpegPath)
                            && IsNewerThanTrigger(candidate.JpegPath, triggerUtc))
                        {
                            pair = candidate;
                            break;
                        }
                        // 还没等到新图：等 200ms 再扫（等相机 FTP 上传完成；后台线程不阻塞 UI）
                        Thread.Sleep(200);
                    }
                    // 兜底：等待超时仍未见"新图"，但目录里可能有旧图残留——取最新一对，
                    // 由下方按实际结果提示（有图会照常归档，无图则报"没推到图"）。
                    if (string.IsNullOrEmpty(pair.JpegPath))
                        pair = ResolveLatestFtpPair(camIndex);

                    jpeg = pair.JpegPath;
                    iv4p = pair.IvpPath;
                    if (string.IsNullOrEmpty(jpeg))
                        fetchError = I18n.T(
                            "FTP 取图目录里没有 jpeg 图片（相机已触发但未推图，请检查相机 FTP 配置/网络）",
                            "No jpeg found in the FTP image dir (camera triggered but did not upload; check camera FTP config/network)");
                    else if (_imageStore != null)
                    {
                        // V2.12.1：存图文件名 {点位}=1，目录按相机名 {相机} 层隔离（与主流程同规则），
                        // 相机名取配置 Name，空则优先 CameraId 真编号、其次"相机N"（V2.13.4）。
                        string camName = (camIndex >= 0 && camIndex < _cameraConfigs.Count
                            && !string.IsNullOrWhiteSpace(_cameraConfigs[camIndex].Name))
                            ? _cameraConfigs[camIndex].Name.Trim()
                            : ((camIndex >= 0 && camIndex < _cameraConfigs.Count
                                && _cameraConfigs[camIndex].CameraId > 0)
                                ? I18n.T($"相机{_cameraConfigs[camIndex].CameraId}", $"Cam{_cameraConfigs[camIndex].CameraId}")
                                : I18n.T($"相机{camIndex + 1}", $"Cam{camIndex + 1}"));
                        archived = _imageStore.SaveImageFilePair(jpeg, iv4p, 1, r.IsOk, _serialSnapshot, camName);
                        if (archived != null)
                        {
                        // V1.12.25：归档成功后才删 FTP 源图（删早了会把图弄丢），与主流程"处理即删"一致。
                        // 删除在后台线程执行（UI 禁 IO），方法内部吞异常，删除失败不影响本次测试。
                        ImageStore.DeleteSourceFile(jpeg, $"功能测试 {camName}");
                        ImageStore.DeleteSourceFile(iv4p, $"功能测试 {camName}");
                        }
                    }
                    else
                        fetchError = I18n.T("未提供主窗体 ImageStore（无法存图）", "No ImageStore provided (cannot save image)");
                }
                SafeInvoke(() =>
                {
                    if (r.Succeeded)
                    {
                        lblCamResult.Text = r.IsOk
                            ? I18n.T($"T2 判定：OK（{r.ResultText}）", $"T2 result: OK ({r.ResultText})")
                            : I18n.T($"T2 判定：NG（{r.ResultText}）", $"T2 result: NG ({r.ResultText})");
                        lblCamResult.ForeColor = r.IsOk ? Color.Green : Color.Red;
                        AppendLog($"← T2 判定 {(r.IsOk ? "OK" : "NG")}：{r.ResultText}"
                            + (string.IsNullOrEmpty(r.Detail) ? "" : "　" + r.Detail));
                        if (archived != null)
                        {
                            // 闪图 + 显示存档路径（主窗体保存目录下，点位 1）
                            bool shown = ShowTestImage(archived);
                            lblTestImagePath.Text = shown
                                ? I18n.T("最近存图：" + archived, "Last saved: " + archived)
                                : I18n.T("存图成功但预览加载失败：" + archived, "Saved but preview failed: " + archived);
                            lblTestImagePath.ForeColor = shown ? Color.FromArgb(46, 158, 107) : Color.Red;
                            AppendLog($"→ 已取图并存档（点位1）：{archived}"
                                + (string.IsNullOrEmpty(iv4p) ? "（无 iv4p）" : "")
                                + "，已删除 FTP 源图"
                                + (shown ? "" : "（预览图加载失败，文件可能被占用）"));
                        }
                        else
                        {
                            lblTestImagePath.Text = I18n.T("取图失败：" + (fetchError ?? "未知原因"), "Fetch failed: " + (fetchError ?? "unknown"));
                            lblTestImagePath.ForeColor = Color.Red;
                            AppendLog("← 取图失败：" + (fetchError ?? "（无图可存）"));
                        }
                    }
                    else
                    {
                        lblCamResult.Text = I18n.T("T2 失败：" + r.Detail, "T2 failed: " + r.Detail);
                        lblCamResult.ForeColor = Color.Gray;
                        AppendLog("← T2 失败：" + r.Detail);
                    }
                    FinishOp();
                });
            });
        }

        /// <summary>
        /// 取该相机 FTP 取图目录里"修改时间最新"的一对文件（V1.12.24）。
        /// 目录优先用相机配置 FtpUploadDir，为空回退全局 Image.FtpRootDir；
        /// 找不到则返回空结果（调用方自行提示）。
        /// </summary>
        private ImageStore.LatestPairResult ResolveLatestFtpPair(int cameraIndex)
        {
            if (_imageStore == null) return new ImageStore.LatestPairResult();
            if (_cameraConfigs == null || cameraIndex < 0 || cameraIndex >= _cameraConfigs.Count)
                return new ImageStore.LatestPairResult();
            string dir = _cameraConfigs[cameraIndex].FtpUploadDir;
            if (string.IsNullOrWhiteSpace(dir)) dir = _imageStore.DefaultFtpDir;
            return _imageStore.FindLatestPair(dir);
        }

        /// <summary>在右侧预览框显示一张图片（闪图）。返回是否显示成功；
        /// 图片加载失败（文件被占用/损坏）时返回 false，调用方据此提示，不再静默留白。</summary>
        private bool ShowTestImage(string path)
        {
            var img = ProductionCoordinator.LoadImageSafe(path);
            var old = picTestShot.Image;
            picTestShot.Image = null;
            old?.Dispose();
            picTestShot.Image = img;
            return img != null;
        }

        /// <summary>
        /// 判断文件是否是"本次 T2 触发之后新推的图"（V1.12.27）：
        /// 修改时间（UTC）不早于触发时刻即视为新图。文件读取失败视为"不是新图"（保守，
        /// 防把正在写入/被占用打不开的半成品图当成新图）。容差 1 秒防相机/上位机时钟微差。
        /// </summary>
        private static bool IsNewerThanTrigger(string path, DateTime triggerUtc)
        {
            try
            {
                return File.GetLastWriteTimeUtc(path) >= triggerUtc.AddSeconds(-1);
            }
            catch
            {
                return false;
            }
        }

        // ────────────── 相机程序切换（V1.12.19，基恩士侧仍在调试，供前期验证）──────────────
        // 背景：现场"一台相机拍多个点位"，每个点位对应相机里一个程序（P000/P001/P002…）。
        //   这几个按钮直接复用 KeyenceIV4Camera.SwitchProgram（PW,nnn）做切换验证；
        //   "读当前程序号"按钮复用 ReadProgramNo（PR 指令）读回当前程序号，用来确认
        //   切换是否真正生效（相机侧程序号以 P 开头三位，如 P001 → 程序号 1）。
        // 注：按钮只验证"切换程序"链路，不触发拍照；要连拍一起验证先切程序再点 T2。

        /// <summary>读当前程序号（PR）：显示 P000/P001/P002…，用于确认 PW 切换是否生效。</summary>
        private void BtnReadProgramNo_Click(object sender, EventArgs e)
        {
            var cam = SelectedCamera();
            if (cam == null) { MessageBox.Show(I18n.T("请先在相机列表选择一台相机。", "Please select a camera from the camera list first."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            SetBusy(true);
            AppendLog($"→ 相机 {cam.IpLabel} 读取当前程序号（PR）…");
            Task.Run(() =>
            {
                int no = cam.ReadProgramNo();
                SafeInvoke(() =>
                {
                    if (no >= 0)
                    {
                        lblCurrentProgram.Text = I18n.T("当前程序：", "Current program: ") + $"P{no:D3}";   // P000/P001/P002…
                        lblCurrentProgram.ForeColor = Color.Green;
                        lblCamResult.Text = I18n.T($"当前程序号 P{no:D3}（读回成功）", $"Current program P{no:D3} (read back OK)");
                        lblCamResult.ForeColor = Color.Green;
                        AppendLog($"← 当前程序号 P{no:D3}");
                    }
                    else
                    {
                        lblCurrentProgram.Text = I18n.T("当前程序：读取失败", "Current program: read failed");
                        lblCurrentProgram.ForeColor = Color.Red;
                        lblCamResult.Text = I18n.T("PR 读取失败（未连接/无响应）", "PR read failed (not connected / no response)");
                        lblCamResult.ForeColor = Color.Gray;
                        AppendLog("← 读取当前程序号失败（未连接或通讯异常）");
                    }
                    FinishOp();
                });
            });
        }

        /// <summary>切换到相机程序 P001（发 PW,001）。成功则顺带读回确认。</summary>
        private void BtnSwProg1_Click(object sender, EventArgs e)
        {
            SwitchCameraProgram(1, "P001");
        }

        /// <summary>切换到相机程序 P002（发 PW,002）。成功则顺带读回确认。</summary>
        private void BtnSwProg2_Click(object sender, EventArgs e)
        {
            SwitchCameraProgram(2, "P002");
        }

        /// <summary>通用"切相机程序"：发 PW,nnn 并读回当前程序号做确认（V1.12.19）。
        /// 后台线程执行，完成后 SafeInvoke 回 UI 显示结果。</summary>
        /// <param name="programNo">目标程序号（0~127，越界自动夹取）</param>
        /// <param name="display">界面显示名（如 "P001"），仅用于日志/结果文案</param>
        private void SwitchCameraProgram(int programNo, string display)
        {
            var cam = SelectedCamera();
            if (cam == null) { MessageBox.Show(I18n.T("请先在相机列表选择一台相机。", "Please select a camera from the camera list first."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            SetBusy(true);
            AppendLog($"→ 相机 {cam.IpLabel} 切换程序 → {display}（PW,{programNo:D3}）…");
            Task.Run(() =>
            {
                bool ok = cam.SwitchProgram(programNo);
                if (ok)
                {
                    // 切成功再读回确认（基恩士侧可能还需几毫秒，直接读；失败不阻断主结果）
                    int no = cam.ReadProgramNo();
                    SafeInvoke(() =>
                    {
                        lblCurrentProgram.Text = no >= 0 ? I18n.T("当前程序：", "Current program: ") + $"P{no:D3}" : I18n.T("当前程序：P???", "Current program: P???");
                        lblCurrentProgram.ForeColor = ok ? Color.Green : Color.Red;
                        lblCamResult.Text = ok
                            ? I18n.T($"已切到 {display}（PW,{programNo:D3} 成功）", $"Switched to {display} (PW,{programNo:D3} OK)")
                            : I18n.T($"切换 {display} 失败", $"Switch {display} failed");
                        lblCamResult.ForeColor = ok ? Color.Green : Color.Red;
                        AppendLog($"← 切换 {display} 成功" + (no >= 0 ? $"，读回当前程序 P{no:D3}" : "（读回超时，可点'读当前程序号'确认）"));
                        FinishOp();
                    });
                }
                else
                {
                    SafeInvoke(() =>
                    {
                        lblCurrentProgram.Text = I18n.T("当前程序：切换失败", "Current program: switch failed");
                        lblCurrentProgram.ForeColor = Color.Red;
                        lblCamResult.Text = I18n.T($"切换 {display} 失败（PW 无响应/相机报错）", $"Switch {display} failed (PW no response / camera error)");
                        lblCamResult.ForeColor = Color.Red;
                        AppendLog($"← 切换 {display} 失败（未连接或相机返回 ER）");
                        FinishOp();
                    });
                }
            });
        }

        // ────────────── PLC 操作（全部后台线程；V1.12.11 起从站模式，V2.7 三拍握手）────────────────
        // 【角色反转】PLC(汇川)做主站、上位机做从站。下列 _plc 调用底层已改为读写上位机自己
        //   DataStore 寄存器区（不连远端 PLC）：读请求（协议 40001~40003=索引 1~3）=读 PLC 写入
        //   自己区的值；写结果/型号（协议 40004~40011=索引 4~11）=写自己区供 PLC 主站来读。
        //   功能测试这里验证"从站数据存储读写正常 + PLC 主站能读到/写入"（三拍握手：请求→结果→复位）。

        /// <summary>读扫码请求（V2.7，读 40001）：显示 PLC 主站是否请求扫码（1=请求，0=无）。</summary>
        private void BtnReadScanReq_Click(object sender, EventArgs e)
        {
            if (!EnsurePlc()) return;
            SetBusy(true);
            AppendLog("→ 读扫码请求（40001）…");
            Task.Run(() =>
            {
                bool ok = _plc.ReadScanRequest(out bool requested);
                SafeInvoke(() =>
                {
                    lblMoveVal.Text = ok ? (requested ? I18n.T("扫码请求=1", "Scan request=1") : I18n.T("扫码请求=0", "Scan request=0")) : I18n.T("读取失败", "Read failed");
                    lblMoveVal.ForeColor = ok ? (requested ? Color.Green : Color.Gray) : Color.Red;
                    AppendLog(ok ? $"← 扫码请求 = {(requested ? 1 : 0)}" : "← 读扫码请求失败");
                    FinishOp();
                });
            });
        }

        /// <summary>读相机请求（V2.12.6 起每台相机一路通道）：遍历相机表读各相机请求寄存器，显示各自点位（0=无请求）。</summary>
        private void BtnReadCamReq_Click(object sender, EventArgs e)
        {
            if (!EnsurePlc()) return;
            if (_cameraConfigs.Count == 0)
            {
                MessageBox.Show(I18n.T("当前没有相机配置，无法读取相机请求。", "No camera configuration, cannot read camera requests."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetBusy(true);
            int n = _cameraConfigs.Count;
            AppendLog($"→ 读相机请求（{n} 台相机）…");
            Task.Run(() =>
            {
                var labels = new List<string>();
                bool anyOk = false;
                bool allOk = true;
                for (int i = 0; i < n; i++)
                {
                    bool ok = _plc.ReadCameraRequest(_cameraConfigs[i], out int station);
                    if (ok) anyOk = true; else allOk = false;
                    string name = string.IsNullOrWhiteSpace(_cameraConfigs[i]?.Name)
                        ? ((_cameraConfigs[i] != null && _cameraConfigs[i].CameraId > 0)
                            ? I18n.T($"相机{_cameraConfigs[i].CameraId}", $"Cam{_cameraConfigs[i].CameraId}")
                            : I18n.T($"相机{i + 1}", $"Cam{i + 1}"))
                        : _cameraConfigs[i].Name.Trim();
                    labels.Add($"{name}={station}");
                }
                string joined = string.Join("  ", labels);
                bool anyActive = false;
                foreach (var s in labels)
                    if (!s.EndsWith("=0")) { anyActive = true; break; }
                SafeInvoke(() =>
                {
                    if (allOk)
                    {
                        lblMoveVal.Text = joined;
                        lblMoveVal.ForeColor = anyActive ? Color.Green : Color.Gray;
                    }
                    else
                    {
                        lblMoveVal.Text = I18n.T("读取失败", "Read failed");
                        lblMoveVal.ForeColor = Color.Red;
                    }
                    AppendLog(allOk && anyOk ? $"← 相机请求：{joined}" : "← 读相机请求失败");
                    FinishOp();
                });
            });
        }

        /// <summary>写产品型号（V2.14.13，写 40007=型号序号 + 40008~40012=型号 ASCII）：把 txtModel
        /// 内容（≤10 字符）写入型号区供 PLC 主站读取；序号按 plc.modelIndexes 映射自动带出（默认
        /// Z121=1、U171=2，型号没配序号写 0）。</summary>
        private void BtnWriteModel_Click(object sender, EventArgs e)
        {
            if (!EnsurePlc()) return;
            SetBusy(true);
            string model = txtModel.Text.Trim();
            AppendLog($"→ 写产品型号 [{model}]（40007=序号 + 40008~40012=型号 ASCII）…");
            Task.Run(() =>
            {
                bool ok = _plc.WriteProductModel(model);
                SafeInvoke(() =>
                {
                    AppendLog(ok ? "← 型号与序号已写入" : "← 型号写入失败（从站未就绪）");
                    FinishOp();
                });
            });
        }

        /// <summary>写扫码结果（V2.7，写 40004）：0=复位 / 1=OK / 2=NG。三个按钮共用 WriteScanRes。</summary>
        private void BtnResScan0_Click(object sender, EventArgs e) => WriteScanRes(0);
        private void BtnResScan1_Click(object sender, EventArgs e) => WriteScanRes(1);
        private void BtnResScan2_Click(object sender, EventArgs e) => WriteScanRes(2);

        /// <summary>写扫码结果公共流程。</summary>
        private void WriteScanRes(int code)
        {
            if (!EnsurePlc()) return;
            SetBusy(true);
            AppendLog($"→ 写扫码结果 = {code}（40004）…");
            Task.Run(() =>
            {
                _plc.WriteScanResult(code);
                SafeInvoke(() =>
                {
                    AppendLog($"← 已写扫码结果 {code}（{(code == 0 ? "复位" : code == 1 ? "OK" : "NG")}）");
                    FinishOp();
                });
            });
        }

        /// <summary>写相机结果 = 1（OK，V2.12.6 起写所有相机通道的结果寄存器）。</summary>
        private void BtnResCamUp_Click(object sender, EventArgs e) => WriteCamRes(1);

        /// <summary>写相机结果 = 2（NG）。</summary>
        private void BtnResCamDown_Click(object sender, EventArgs e) => WriteCamRes(2);

        /// <summary>相机结果复位 = 0（所有相机通道）。</summary>
        private void BtnResCamReset_Click(object sender, EventArgs e) => WriteCamRes(0);

        /// <summary>写相机结果公共流程（V2.12.6 起多相机）：遍历相机表，每台相机写各自结果通道。
        /// code：0=复位 / 1=OK / 2=NG。地址来自相机配置 PlcResultAddress（V2.13.4 起显式配置，
        /// 0=未配置则跳过该台）。</summary>
        private void WriteCamRes(int code)
        {
            if (!EnsurePlc()) return;
            if (_cameraConfigs.Count == 0)
            {
                MessageBox.Show(I18n.T("当前没有相机配置，无法写相机结果。", "No camera configuration, cannot write camera results."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetBusy(true);
            int n = _cameraConfigs.Count;
            AppendLog($"→ 写相机结果 = {code}（{n} 台相机）…");
            Task.Run(() =>
            {
                for (int i = 0; i < n; i++)
                    _plc.WriteCameraResult(_cameraConfigs[i], code);
                SafeInvoke(() =>
                {
                    AppendLog($"← 已写相机结果 {code}（{(code == 0 ? "复位" : code == 1 ? "OK" : "NG")}，全部 {n} 台）");
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
                MessageBox.Show(I18n.T("协议偏移量需为 0~65535 的整数。", "Offset must be an integer 0~65535."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(I18n.T("D 地址需为 0~65535 的整数。", "D address must be an integer 0~65535."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            long combined = (long)addr + offset; // 用 long 防 int 溢出
            if (combined < 0 || combined > 65535)
            {
                MessageBox.Show(I18n.T($"实际地址（{addr} + {offset}）超出 0~65535 范围。", $"Actual address ({addr} + {offset}) is out of range 0~65535."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    txtReadVal.Text = ok ? value.ToString() : I18n.T("通讯失败", "Comm failed");
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
                MessageBox.Show(I18n.T("写值需为 0~65535 的整数。", "Write value must be an integer 0~65535."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(I18n.T("未提供 PLC 服务实例。", "No PLC service instance available."), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// V2.15.0 国际化：按当前语言刷新本窗体全部静态界面文字。
        /// 在构造函数末尾调用（模态对话框打开瞬间按当前语言初始化；模态期间语言不会变化）。
        /// 状态标签/结果标签由运行时方法（RefreshStates/各按钮回调）随时覆盖，这里只管初始文案。
        /// 日志框（txtLog）内容保持中文（日志保留中文约定，供工程师排查）。
        /// </summary>
        private void ApplyLanguage()
        {
            this.Text = I18n.T("开发者模式", "Developer Mode");
            // ───── 账号管理区（V2.15.10）─────
            grpAccount.Text = I18n.T("账号管理", "Account Management");
            lblAccTip.Text = I18n.T("选中账号:", "Selected account:");
            lblAccAccount.Text = I18n.T("（未选中）", "(not selected)");
            lblNewPwd.Text = I18n.T("新密码:", "New password:");
            lblNewPwd2.Text = I18n.T("确认密码:", "Confirm:");
            btnChangePwd.Text = I18n.T("修改密码", "Change Password");
            colAccUser.HeaderText = I18n.T("账号", "Account");
            colAccRole.HeaderText = I18n.T("角色", "Role");
            colAccEnabled.HeaderText = I18n.T("启用", "Enabled");
            colAccPwd.HeaderText = I18n.T("密码", "Password");
            grpCamera.Text = I18n.T("相机测试", "Camera Test");
            btnTrigger.Text = I18n.T("仅触发拍照 T1", "Trigger T1");
            btnTriggerRead.Text = I18n.T("触发+判定T2（取图存图）", "Trigger+Result T2");
            lblCamResult.Text = I18n.T("（尚未操作相机）", "(Not operated yet)");
            btnReadProgramNo.Text = I18n.T("读当前程序号", "Read Program No.");
            lblCurrentProgram.Text = I18n.T("当前程序：?", "Current program: ?");
            btnSwProg1.Text = I18n.T("切换程序 → P001", "Switch → P001");
            btnSwProg2.Text = I18n.T("切换程序 → P002", "Switch → P002");
            lblTestImagePath.Text = I18n.T("（最近未测试存图）", "(No test image yet)");
            grpScanner.Text = I18n.T("扫码枪测试", "Scanner Test");
            lblScannerCode.Text = I18n.T("（尚未读到条码）", "(No code received yet)");
            lblScannerHint.Text = I18n.T(
                "把条码放到扫码枪下读，读到会实时显示（共用连接）",
                "Place a code under the scanner; it shows here in real time (shared connection)");
            btnScannerTrigger.Text = I18n.T("发送触发指令", "Send Trigger");
            btnShowScannerFail.Text = I18n.T("显示扫码枪异常弹窗", "Scanner Error Dialog");
            grpPlc.Text = I18n.T("PLC 测试", "PLC Test");
            lblOffset.Text = I18n.T("协议偏移量:", "Offset:");
            lblOffsetTip.Text = I18n.T(
                "实际D地址 = 输入地址 + 偏移量（默认0按D地址读写）",
                "Actual D address = input + offset (0 = read/write by D address)");
            lblReadAddrTip.Text = I18n.T("读地址测试:", "Read addr test:");
            btnReadReg.Text = I18n.T("读 取", "Read");
            lblReadValTip.Text = I18n.T("→ 读到的值:", "→ Value:");
            lblWriteValTip.Text = I18n.T("写地址测试:", "Write addr test:");
            btnWriteReg.Text = I18n.T("写 入", "Write");
            btnReadScanReq.Text = I18n.T("读扫码请求", "Read Scan Req");
            btnReadCamReq.Text = I18n.T("读相机请求", "Read Cam Req");
            btnWriteModel.Text = I18n.T("写产品型号", "Write Model");
            btnResScan0.Text = I18n.T("扫码结果 = 0", "Scan Res = 0");
            btnResScan1.Text = I18n.T("扫码OK = 1", "Scan OK = 1");
            btnResScan2.Text = I18n.T("扫码NG = 2", "Scan NG = 2");
            btnResCamUp.Text = I18n.T("相机OK = 1", "Cam OK = 1");
            btnResCamDown.Text = I18n.T("相机NG = 2", "Cam NG = 2");
            btnResCamReset.Text = I18n.T("相机复位 = 0", "Cam Reset = 0");
            grpLog.Text = I18n.T("操作日志", "Operation Log");
            RefreshStates(); // 状态标签按当前语言刷新（初始文案也随语言走）
        }
    }
}