using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommandCenter.Controls;
using CommandCenter.Models;
using CommandCenter.Services;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 控制中心主窗体。
    /// 【界面布局】
    /// ┌───────────────────────────────────────────────────────────────────┐
    /// │ 产品型号:[cmbModel▾] 序列号:[框] | 总数:0 | [OK] | [NG] | [系统设置] │
    /// │                                        ●PLC ●扫码枪 ●上相机 ●下相机 │
    /// │（相机灯/下拉显示配置名称：有名称显名称，无名称回退"相机N"即序号）       │
    /// ├───────────────────────────────────────────────────────────────────┤
    /// │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                  │
    /// │  │ W1   │ │ W2   │ │ W3   │ │ W4   │ │ W5   │                   │
    /// │  │ [OK] │ │ [NG] │ │ [OK] │ │ ...  │ │ [NG] │                   │
    /// │  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘                  │
    /// │  （Rows × Columns 个窗口，逐次环形刷新；外层 pnlWindowScroll      │
    /// │   AutoScroll=true，行多超高时右侧出滚动条、滚轮下滑）              │
    /// ├───────────────────────────────────────────────────────────────────┤
    /// │ 状态:等待PLC主站到位…（左下角；型号切换成功时变绿显示"型号切换完成"）     │
    /// └───────────────────────────────────────────────────────────────────┘
    /// 窗口放大/还原（V1.12.15）：鼠标左键双击任一显示窗口 → 该窗口全屏放大（整屏含任务栏），
    ///   再次双击 / 按 Esc → 还原回窗口矩阵原位置；全屏时画面仍随检测实时刷新（移动的是同一控件）。
    /// 序列号手动输入（V1.12.17 弹窗 / V1.12.19 框内直录）：点击标题栏"序列号"框（txtSerial，TextBox）
    ///   即可出现输入光标直接录入当前产品 SN（无扫码枪/扫码枪未读到时手动补录）；
    ///   Enter 提交 / Esc 还原 / 失焦非空提交（SetManualSerial 与扫码枪收码等效，可推进"等 SN"阶段）。
/// 标题栏：左起信息字段（按配置开关）→ 产品型号下拉（V2.8，可直接切型号）→ 系统设置按钮
/// → 连接指示灯；
    ///   - 连接指示灯从右到左：●相机N..●相机1 → ●扫码枪 → ●PLC（Dock.Right 先 Add 靠左）。
    ///     扫码枪灯显示"扫码枪：已连接/未连接"，绿=已连接、红=未连接（V1.12.6，聚合刷新）；
    ///     PLC 灯三态（V1.12.11 从站模式）：绿=主站已连入 / 黄=监听就绪等待主站 / 红=监听失败
    ///     （悬停 ToolTip 给出状态含义与排查方向，见 UpdatePlcStatus）。
    ///   - OK/NG 计数默认"实心彩色色块 + 白字"高亮（绿底=OK、红底=NG），关闭
    ///     DisplayConfig.TitleOkNgHighlight 则回退普通彩色文字；
    /// 底部栏：仅状态文本，固定在左下角。
    /// 职责：只做界面呈现 + 事件绑定，业务编排在 ProductionCoordinator。
    /// 静态布局控件（标题栏字段/设置按钮/PLC灯/扫码枪灯/状态栏/窗口矩阵容器）在
    /// MainForm.Designer.cs 中由设计器维护；动态控件（相机灯/窗口矩阵内容）在此运行时生成。
    /// 系统设置保存后不重启：ApplyRuntimeConfig 停旧服务层、按新配置全量重建服务与界面
    /// （V1.6.0，连接惰性 + 后台心跳自动按新 IP 重连，等效"断开重连"）。
    /// </summary>
    public partial class MainForm : Form
    {
        private AppConfig _config;
        private PlcService _plc;
        private List<KeyenceIV4Camera> _cameras;   // 多台相机各一个服务实例（V1.1.0）
        private ImageStore _imageStore;
        private ProductionCoordinator _coordinator;
        private ConnectionMonitor _monitor;
        private List<IScanner> _scanners = new List<IScanner>();   // 扫码枪列表（多台各一个实例，V1.8.1 起支持多台；串口/基恩士 TCP 无协议按各自 Mode 二选一）
        private Label[] _lblCamStatuses;            // 每台相机一个连接指示灯（≤2台模式，按相机下标对齐）
        private ComboBox _cmbCamOverview;           // 相机下拉列表（≥3台模式）：下拉查看每台名字+状态圆点
        private Label _lblCamAggregate;             // 相机总连接状态标签（≥3台模式）：全部连接才绿色，否则红色
        private Panel _pnlCamOverview;              // ≥3台模式的容器：把总标签+下拉框装一起，统一垂直居中
        private ToolTip _camTip;                    // 总状态标签的悬停明细提示（列出每台相机连/断）
        private ToolTip _plcTip;                    // PLC 灯悬停提示（说明三态灯当前含义，V1.12.11）
        private bool _modelComboInit;     // 型号下拉程序内初始化/刷新时防误触 SelectedIndexChanged
        private bool _modelComboWired;    // 型号下拉事件是否已挂线（构造与热更都会走 InitModelCombo，只挂一次）

        /// <summary>
        /// 显示窗口集合（V1.12.28 起按"窗口编号"索引，不再用数组下标）：
        /// 禁用的窗口（DisplayConfig.WindowEnabled=false）不在矩阵显示、不建控件，
        /// 因此窗口编号与格子位置不再是 1:1 连续——用字典按编号存取，OnInspectionFinished
        /// 拿到窗口编号即可找到对应控件刷新。键=窗口编号（1 起），值=该窗口的显示控件。
        /// </summary>
        private readonly Dictionary<int, CameraDisplayControl> _windowControls = new Dictionary<int, CameraDisplayControl>();

        // 窗口双击放大/还原全屏（V1.12.15）：双击任一显示窗口 → 全屏显示，再双击/Esc → 还原。
        private Form _fullScreenForm;                  // 全屏承载窗体（无边框、置顶、覆盖全屏）
        private CameraDisplayControl _fullScreenWindow;// 当前被放大的窗口（从 grid 移入全屏窗体）
        private TableLayoutPanelCellPosition? _fullScreenCell; // 该窗口在 grid 里的原单元格（还原时放回）

        // 统计
        private int _total, _ok, _ng;

        public MainForm()
        {
            InitializeComponent();   // 先解析设计器里的静态控件（否则后续代码引用会拿到 null）

            _config = ConfigStore.Load();

            BuildServices();         // PLC/多相机/图像/协调器 就绪（相机灯数量依赖 _cameras）
            InitTitleBarRuntime();   // 按配置补全标题栏：文案/可见性/型号下拉/动态相机灯/紧凑重排
            BuildWindowGrid();       // 窗口矩阵（用设计器的 gridCameraWindows 容器，动态重建行列）
            SubscribeEvents();
            _coordinator.Start();
        }

        /// <summary>组装底层服务（PLC/多相机/图像/协调器）。</summary>
        private void BuildServices()
        {
            _plc = new PlcService(_config.Plc);

            // 多相机：配置列几台就建几个相机服务实例，各自独立连接/触发/存图
            _cameras = new List<KeyenceIV4Camera>();
            var cams = _config.Cameras ?? new List<CameraConfig>();
            // 空配置兜底两台默认相机（V1.9.8：现场相机 IP 已写死，见 CameraConfig.DefaultCameras）。
            // 注意 cams 是 _config.Cameras 的引用，AddRange 修改会直接生效到配置；仅空列表兜底，
            // 不影响"用户在设置里配了几台就用几台"的既有行为。
            if (cams.Count == 0) cams.AddRange(CameraConfig.DefaultCameras());
            foreach (var c in cams)
                _cameras.Add(new KeyenceIV4Camera(c));
            // V2.13.8：把各相机结果寄存器地址注册给 PLC 服务——从站就绪时统一清 0
            // （上电/断电重启后结果寄存器不残留旧值，见 PlcService.ResetResultRegisters）
            _plc.SetCameraResultAddresses(cams);
            LogHelper.Info($"BuildServices：共创建 {_cameras.Count} 台相机：{string.Join(" / ", _cameras.ConvertAll(x => x.IpLabel))}");

            _imageStore = new ImageStore(_config.Image);
            // V2.13.6：为每台相机启动其 FTP 取图目录的 FileSystemWatcher（AddMonitor 幂等去重，
            // 目录不存在自动建）。相机一推图事件就置位协调器里的信号，等图流程立即唤醒（信号加速）。
            // 目录取相机配置 FtpUploadDir，留空回退全局 FtpRootDir（与协调器 FtpDirFor 同规则）；
            // 监听线程只发事件，不做取图/归档（那些在协调器后台 Task 里），不违反"UI 禁 IO"红线。
            for (int ci = 0; ci < cams.Count; ci++)
            {
                string dir = string.IsNullOrWhiteSpace(cams[ci]?.FtpUploadDir)
                    ? _imageStore.DefaultFtpDir
                    : cams[ci].FtpUploadDir.Trim();
                _imageStore.AddMonitor(dir, ci);
            }
            _coordinator = new ProductionCoordinator(_plc, _cameras, cams, _imageStore,
                _config.Display.WindowEnabled, _config.ProductModel, _config.Display.WindowPointMaps);

            // 连接健康监控：后台心跳 + 断连自动重连 + 边沿日志（不影响任何 UI 刷新）
            _monitor = new ConnectionMonitor(_plc, _cameras);

            // 扫码枪（V1.8.1 起支持多台）：每台按各自的 ScanConfig.Mode 选实现——
            // "Tcp"=基恩士 SR 以太网无协议，其余按串口兜底。扫码枪断连自愈由实现类内部完成，
            // 不占 ConnectionMonitor。列表为空则不留任何扫码枪（序列号走手动输入/模拟）。
            _scanners = new List<IScanner>();
            foreach (var sc in _config.Scanners ?? new List<ScanConfig>())
                _scanners.Add(BuildScanner(sc));

            // V1.12.16 两阶段流程：把扫码枪注入协调器，供"扫码到位→触发扫码→等SN"阶段使用
            // （协调器在 BuildServices 里比扫码枪先创建，故用方法注入而非构造参数）。
            _coordinator.AttachScanners(_scanners);
        }

        /// <summary>
        /// 按配置创建一台扫码枪实例。
        /// "Tcp" → ScannerTcpService（基恩士 SR 系列 TCP/IP 无协议，上位机作客户端收条码行）；
        /// 其余 → ScannerService（串口 RS-232）。两者实现同一 IScanner 接口。
        /// </summary>
        private static IScanner BuildScanner(ScanConfig scan)
        {
            // 空安全比较：Mode 为 null/空时一律走串口分支（ScannerService），不按旧配置兜底，
            // 只是防止配置里 mode 被手写成 null 导致 .Trim() 空引用崩溃。
            if (scan.Mode?.Trim().Equals("Tcp", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new ScannerTcpService(scan);
            }
            return new ScannerService(scan);
        }

        /// <summary>
        /// 标题栏"运行时"初始化（仅构造调用一次）：
        ///   ① 字段/可见性/OK-NG 色块/相机灯 → InitTitleBarFields（可重入，热更时再调）；
        ///   ② 产品型号下拉填充（只一次）；
        ///   ③ 设置按钮事件挂线（只一次）。
        /// 设计器负责"控件长什么样"，此处负责"数据与动态部分"。
        /// </summary>
        private void InitTitleBarRuntime()
        {
            // ① 标题栏字段 + 动态相机灯（构造与"设置保存热更"都会调用，可重入）
            InitTitleBarFields();

            // ② 产品型号下拉（V2.8）：填充预置三型号候选并选中当前型号（期间屏蔽事件）。
            //    热更（ApplyRuntimeConfig）也会调用，重新按新配置候选刷新。
            InitModelCombo();

            // ③ 设置按钮事件（设计器只做外观，交互在这里挂线，只挂一次）
            btnSettings.Click += (s, e) => OpenSettings();

            // ④ 序列号框点击直录（V1.12.19）：txtSerial 是 TextBox，点击即有输入光标，
            //    无需弹窗即可手工录入当前产品 SN（无扫码枪 / 扫码枪没读到码时）。
            //    Enter 提交 / Esc 还原 / 失焦非空提交，全部在 SetupSerialEditor 里接线一次。
            SetupSerialEditor();
        }

        /// <summary>
        /// 序列号框内直录交互（V1.12.19，替代 V1.12.17 的"双击弹窗"）。
        /// txtSerial 虽然平时看起来像"只读显示框"（白底+单线边框），但它是标准 TextBox，
        /// 鼠标点击即进入编辑态出现输入光标，无需弹窗。交互约定：
        ///   - Enter：trim 后非空则写协调器（SetManualSerial，推进"等 SN"阶段，同扫码枪收码），
        ///     空输入不更新（保留原 SN）；触发后把焦点还给标题栏，避免妨害按键操作；
        ///   - Esc：放弃本次输入，还原为协调器当前 SN 值；
        ///   - 失焦（Leave）：非空按 Enter 同规则提交；空输入还原为上值（防误清空）。
        /// 为什么在失焦也提交：操作员录完习惯点窗口其它区域，若只认 Enter 会丢输入。
        /// 为什么空输入还原：扫码收到的 SN 不应被一次误编辑清掉。
        /// </summary>
        private void SetupSerialEditor()
        {
            txtSerial.KeyUp += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitSerialEdit();          // Enter 提交
                    pnlTitleBar.Focus();         // 焦点还给标题栏，避免连续 Enter 连发
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    RestoreSerialDisplay();      // Esc 还原，丢弃本次输入
                    pnlTitleBar.Focus();
                }
            };
            txtSerial.Leave += (s, e) => CommitSerialEdit();
        }

        /// <summary>把 txtSerial 当前内容按"非空才提交"规则写入协调器，并同步显示（V1.12.19）。</summary>
        private void CommitSerialEdit()
        {
            if (IsDisposed) return;
            string code = txtSerial.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                RestoreSerialDisplay();  // 空输入不更新，还原显示当前 SN
                return;
            }
            _coordinator.SetManualSerial(code);
            txtSerial.Text = code;
            LogHelper.Info($"手动输入序列号：{code}");
        }

        /// <summary>把 txtSerial 恢复为协调器当前 SN 值（Esc/空输入时丢弃编辑）。</summary>
        private void RestoreSerialDisplay()
        {
            if (IsDisposed) return;
            txtSerial.Text = _coordinator.LatestSerialNumber ?? "";
        }

        /// <summary>
        /// 标题栏"字段与动态部分"按配置初始化（构造与"设置保存热更"都会调用，可重入）：
        ///   ① 产品型号前缀文案（ProductModelPrefix）与各信息字段的可见性（ShowXxx 开关）；
        ///   ② 标题栏 OK/NG 计数色块高亮（StyleCountBadge，配色跟随配置）；
        ///   ③ 每台相机一个连接指示灯（_lblCamStatuses，按相机下标对齐）——相机台数运行时才知道，
        ///      所以这类"动态控件"不进设计器，在这里循环生成，Dock.Right 排在 PLC 灯右侧；
        /// 说明：紧凑重排（RelayoutTitleBar）在 OnShown / 热更末尾执行，不在此处。
        /// </summary>
        private void InitTitleBarFields()
        {
            ApplyConfigVisibility();

            // 产品型号前缀文案（V1.1.2 现场业务对应）：前缀文案走配置，开关控制整段显示
            lblProductPrefix.Text = _config.Display.ProductModelPrefix + ":";
            // 序列号：标题"序列号:"在显示框外（lblSerialTitle），框内只放值（txtSerial，点击可直录）；
            // 有值显示值，没有则框内留空（不写"待扫码"），标题+框整体由开关控制显隐
            lblSerialTitle.Text = "序列号:";
            txtSerial.Text = _coordinator.LatestSerialNumber;

            // ② 标题栏 OK/NG 计数高亮（V1.5.0 现场反馈"彩色数字不够醒目"）：
            // 默认把 OK/NG 做成"实心彩色色块 + 白字"（绿底=OK、红底=NG，配色走 DisplayConfig），
            // 关闭 TitleOkNgHighlight 配置时回退为普通彩色文字。
            if (_config.Display.TitleOkNgHighlight)
            {
                StyleCountBadge(lblOk, _config.Display.OkColor);
                StyleCountBadge(lblNg, _config.Display.NgColor);
            }

            // ③ 动态相机连接指示灯：先 Add 的 Dock.Right 靠左，后 Add 的靠右。
            BuildCameraStatusLights();
        }

        /// <summary>
        /// 按配置开关设置标题栏各字段/按钮的可见性（V1.9.9 从 InitTitleBarFields 抽出复用）。
        /// 为什么抽出来：InitTitleBarFields（热更）与 RelayoutTitleBar（重排）都依赖同一份
        /// "哪些字段该显示"的判定，共用此方法避免两处漂移。
        /// </summary>
        private void ApplyConfigVisibility()
        {
            lblProductPrefix.Visible = _config.Display.ShowProductModel;
            cmbModel.Visible = _config.Display.ShowProductModel; // 型号下拉与"产品型号"标签同开关（V2.8）
            lblSerialTitle.Visible = _config.Display.ShowSerialNumber;
            txtSerial.Visible = _config.Display.ShowSerialNumber;
            lblTotal.Visible = _config.Display.ShowTotalCount;
            lblOk.Visible = _config.Display.ShowOkCount;
            lblNg.Visible = _config.Display.ShowNgCount;
            // 系统设置按钮显隐（V1.8.4）：按配置隐藏后标题栏自动紧凑重排，隐藏期间配置只读
            btnSettings.Visible = _config.Display.ShowSettingsButton;
        }

        /// <summary>
        /// 重建标题栏相机连接状态区（构造与热更都会调用）。按相机台数分两种模式（V1.10.0）：
        ///
        /// 【≤2 台】保持既有逻辑：每台相机一个独立指示灯"● 相机N"，直接显示在标题栏，
        /// 绿=已连接、红=断连。先移除旧的（热更后相机台数可能变化，必须整套重建），
        /// 再按当前台数正序 Add：Dock.Right 布局是"先 Add 的靠左、后 Add 的靠右"，
        /// 正序循环得到 相机1..相机N 依次排在 PLC 灯右侧。
        ///
        /// 【≥3 台】聚拢成两个控件（现场相机多时 96px/台 的灯阵会占满标题栏）：
        ///   - _lblCamAggregate（总状态标签）：只有所有相机都连接才绿色，任一断连就红色；
        ///   - _cmbCamOverview（下拉列表）：默认收起只显示一个入口，点开看每台相机
        ///     名字+连接状态（OwnerDraw 自绘圆点，绿=OK、红=断连）。
        /// 两者装进 _pnlCamOverview（Dock.Right）统一垂直居中（ComboBox 直接 Dock.Right
        /// 会被拉满 48px 高、文字偏上，与左侧"● PLC"标签不对齐）；总标签字体与 PLC 标签
        /// 一致（微软雅黑 10F Bold）。RelayoutTitleBar 统计右侧 Dock 区时把容器按整体宽度计入。
        /// lblCamPlaceholder 是设计器视觉提示，隐藏后 Dock 空间让给运行时生成的控件。
        /// </summary>
        private void BuildCameraStatusLights()
        {
            if (_lblCamStatuses != null)
                foreach (var lbl in _lblCamStatuses)
                    if (lbl != null) pnlTitleBar.Controls.Remove(lbl);

            if (_cmbCamOverview != null)
            {
                pnlTitleBar.Controls.Remove(_cmbCamOverview);
                _cmbCamOverview.Dispose();
                _cmbCamOverview = null;
            }
            if (_lblCamAggregate != null)
            {
                pnlTitleBar.Controls.Remove(_lblCamAggregate);
                _lblCamAggregate.Dispose();
                _lblCamAggregate = null;
            }
            if (_pnlCamOverview != null)
            {
                // 容器里的子控件（总标签+下拉框）先移除再释放，避免残留
                _pnlCamOverview.Controls.Clear();
                pnlTitleBar.Controls.Remove(_pnlCamOverview);
                _pnlCamOverview.Dispose();
                _pnlCamOverview = null;
            }

            lblCamPlaceholder.Visible = false;

            if (_cameras.Count <= 2)
            {
                // 小台数模式：每台一个独立指示灯（与历史行为一致）
                _lblCamStatuses = new Label[_cameras.Count];
                for (int i = 0; i < _cameras.Count; i++)
                {
                    var lbl = new Label
                    {
                        Dock = DockStyle.Right,
                        Width = 96,
                        TextAlign = ContentAlignment.MiddleRight,
                        Text = $"● {CamDisplayName(i)}",
                        ForeColor = Color.FromArgb(150, 150, 150),
                        Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold)
                    };
                    pnlTitleBar.Controls.Add(lbl);
                    _lblCamStatuses[i] = lbl;
                }
            }
            else
            {
                // 大台数模式：总状态标签 + 相机下拉列表。
                // 两个控件都装进 _pnlCamOverview（Dock.Right）统一垂直居中：
                // ComboBox 如果直接 Dock.Right 会被容器拉伸到 48px 高、显示文字偏上，
                // 与左侧"● PLC"标签（MiddleRight 垂直居中）不对齐；放容器里手动定位可精确居中。
                // 字体统一用与 PLC 标签一致的"微软雅黑 10F Bold"（见 Designer 的 lblPlcStatus.Font）。
                var camFont = new Font("微软雅黑", 10F, FontStyle.Bold);
                _lblCamAggregate = new Label
                {
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleRight,
                    Text = "● 相机", // V1.10.0：不显示台数，纯状态圆点+相机字样
                    ForeColor = Color.FromArgb(150, 150, 150),
                    Font = camFont
                };
                _cmbCamOverview = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList, // 只能选不能输，防止误改
                    Font = camFont,
                    DrawMode = DrawMode.OwnerDrawFixed,          // 自绘：每项画"状态圆点+相机名"
                    ItemHeight = 24
                };
                _cmbCamOverview.DrawItem += CmbCamOverview_DrawItem;
                for (int i = 0; i < _cameras.Count; i++)
                    _cmbCamOverview.Items.Add(CamOverviewLabel(i)); // 显示文案，画圆点时按下标找状态
                if (_cmbCamOverview.Items.Count > 0) _cmbCamOverview.SelectedIndex = 0;

                // 容器：Dock.Right，宽度=标签+间距+下拉框，其余由 pnlTitleBar 高度决定
                _pnlCamOverview = new Panel
                {
                    Dock = DockStyle.Right,
                    BackColor = pnlTitleBar.BackColor // 与标题栏同色，视觉上"隐形"
                };
                // 先算出下拉框的布局宽度（文本取最长项，_cameras 可能为空则用默认宽）
                int cmbW = 160;
                if (_cmbCamOverview.Items.Count > 0)
                    cmbW = TextRenderer.MeasureText((string)_cmbCamOverview.Items[_cmbCamOverview.Items.Count - 1], camFont).Width + 40;
                int lblW = _lblCamAggregate.PreferredWidth;
                _cmbCamOverview.Width = cmbW;
                _pnlCamOverview.Width = lblW + 8 + cmbW;

                // 垂直居中：标题栏高 48，控件 y = (48 - 控件高)/2
                int barH = pnlTitleBar.ClientSize.Height;
                int lblY = (barH - _lblCamAggregate.PreferredHeight) / 2;
                int cmbY = (barH - _cmbCamOverview.Height) / 2;
                _lblCamAggregate.Location = new Point(0, lblY);
                _cmbCamOverview.Location = new Point(lblW + 8, cmbY);
                _pnlCamOverview.Controls.Add(_lblCamAggregate);
                _pnlCamOverview.Controls.Add(_cmbCamOverview);

                _camTip = _camTip ?? new ToolTip();
                _camTip.SetToolTip(_lblCamAggregate, "相机连接状态明细");

                pnlTitleBar.Controls.Add(_pnlCamOverview);

                RefreshCameraAggregateStatus(); // 初始按当前连接状态上色
            }
        }

        /// <summary>
        /// 第 i 台相机的显示名（V1.12.23）：有配置名称（上相机/下相机/…）用名称，
        /// 无名称回退"相机N"（V2.13.4 起优先 CameraConfig.CameraId 真编号，其次行序 i+1）。
        /// 所有主界面展示相机的文案（相机灯/悬停/下拉）都应走这里，保证"编号"唯一对应。
        /// </summary>
        private string CamDisplayName(int i)
        {
            if (i < 0 || i >= _cameras.Count) return "相机";
            string name = _cameras[i].DisplayName;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            // 无名称时优先用相机真编号（CameraId>0），没有才退回行序 i+1（与设置页第一列一致）
            int camId = 0;
            var cfgList = _config.Cameras;
            if (cfgList != null && i < cfgList.Count && cfgList[i] != null)
                camId = cfgList[i].CameraId;
            return camId > 0 ? $"相机{camId}" : $"相机{i + 1}";
        }

        /// <summary>
        /// 生成下拉列表第 i 台相机的显示文案："上相机  19.87.6.213"（V1.12.22 起带名称）。
        /// 名称来自 CameraConfig.Name（配置缺省为空则退回 "相机N  IP"），状态用圆点表
        /// </summary>
        private string CamOverviewLabel(int i)
        {
            if (i < 0 || i >= _cameras.Count) return "";
            return $"{CamDisplayName(i)}  {_cameras[i].IpAddressOnly}";
        }

        /// <summary>
        /// 相机下拉列表的项绘制（OwnerDraw）：
        /// 每项左边画一个"状态圆点"（绿=已连接、红=断连），圆点右侧画相机名+IP。
        /// 高亮行用系统选中色背景，圆点颜色保持语义不变。
        /// </summary>
        private void CmbCamOverview_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _cameras.Count) return;
            bool connected = _cameras[e.Index].IsConnected;
            Color dotColor = connected ? Color.FromArgb(46, 158, 107)  // 绿=OK
                                       : Color.FromArgb(229, 72, 77);   // 红=断连
            e.DrawBackground();

            // 圆点：垂直居中，半径约 5px
            int dotSize = 10;
            int dotX = e.Bounds.Left + 6;
            int dotY = e.Bounds.Top + (e.Bounds.Height - dotSize) / 2;
            using (var b = new SolidBrush(dotColor))
                e.Graphics.FillEllipse(b, dotX, dotY, dotSize, dotSize);

            // 相机名+IP 文本，用系统前景色（选中行高亮时可读）
            TextRenderer.DrawText(e.Graphics, CamOverviewLabel(e.Index), e.Font,
                new Point(dotX + dotSize + 8, e.Bounds.Top + (e.Bounds.Height - e.Font.Height) / 2),
                e.ForeColor);
            e.DrawFocusRectangle();
        }

        /// <summary>
        /// 刷新相机总连接状态标签（≥3台模式，后台线程事件触发，BeginInvoke 切回 UI 线程）。
        /// 规则：所有相机都 IsConnected → 绿色；只要有一台断连 → 红色。
        /// 同时更新悬停明细文本（每台相机名 + 连/断），并让下拉框重绘当前状态圆点。
        /// </summary>
        private void RefreshCameraAggregateStatus()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshCameraAggregateStatus));
                return;
            }
            if (_lblCamAggregate == null) return;

            bool allOk = _cameras.Count > 0 && _cameras.All(c => c.IsConnected);
            _lblCamAggregate.ForeColor = allOk ? Color.FromArgb(46, 158, 107)   // 全部连接 → 绿
                                               : Color.FromArgb(229, 72, 77);    // 任一断连 → 红
            _lblCamAggregate.Text = "● 相机";

            // 悬停明细：列出每台"名字+状态"，方便现场快速定位是哪台断了（只显示 IP，不带端口）
            var lines = _cameras.Select((c, i) => $"{CamDisplayName(i)} {c.IpAddressOnly}：" + (c.IsConnected ? "已连接" : "断连"));
            if (_camTip != null) _camTip.SetToolTip(_lblCamAggregate, string.Join("\n", lines));

            if (_cmbCamOverview != null) _cmbCamOverview.Invalidate(); // 重绘各下拉项的状态圆点
        }

        /// <summary>
        /// 窗体首次显示完成（自动缩放已应用）：执行标题栏紧凑重排。
        /// 若在构造函数里重排，AutoScaleMode.Font 会在后续布局中按设计器基准缩放
        /// 控件（覆盖我们赋的 Location），表现为"字段仍停在设计器写死坐标"。
        /// 启动全屏由 Designer 的 WindowState=Maximized 保证（保留边框与关闭按钮，
        /// V1.11.1：客户要能正常关软件，不做无边框铺屏）。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 铺满窗体所在屏幕的工作区（保留任务栏），等效全屏。
            // 为什么手动铺满而不是 WindowState.Maximized（V1.11.0 关键）：
            // Maximized 状态会被 Windows 强制切换成"可调整边框"，边缘拖拽缩放照常开放；
            // 而 Normal + FixedSingle 的边框是真正固定、没有可调热区的，配合 WndProc
            // 拦截双保险，按钮缩放、拖拽缩放、最大化窗口边缘缩放全部失效。
            var work = Screen.FromControl(this).WorkingArea;
            Bounds = new Rectangle(work.Location, work.Size);

            RelayoutTitleBar();

            // V2.14：铺满到真实屏幕后"窗口显示区高度"才正确，用真实高度重算一次矩阵的
            // 铺满/滚动形态（构造期 BuildWindowGrid 用的是设计器默认高度，可能偏小）。
            // 重建只动窗口控件（Dispose 旧的、按新行列新建），不影响已 Start 的协调器
            // （刷新窗口改走 _windowControls 字典，重建后字典照常可用）。
            BuildWindowGrid();
        }

        /// <summary>
        /// 拦截鼠标命中测试（WM_NCHITTEST），禁止窗口边缘拖拽缩放（V1.11.0）。
        /// 背景：仅设 FormBorderStyle=FixedSingle + MaximizeBox=false 不够——Windows 10/11
        /// 对"最大化窗口"有系统级特性，即使固定边框，鼠标移到窗口边缘/四角仍会出现
        /// 双箭头光标并允许拖拽调整大小，客户照样能拉出小窗。
        /// 做法：把系统返回的"调整大小热区"（左/右/上/下/四角，HTLEFT..HTBOTTOMRIGHT）
        /// 全部改写为 HTCLIENT（客户区），Windows 就不会进入 resize 拖拽流程；
        /// 最小化/关闭按钮（HTMINBUTTON/HTCLOSE）与标题栏拖动（HTCAPTION）不受影响。
        /// 另拦 WM_MOUSEWHEEL（V2.14）：光标在窗口矩阵滚动宿主内时把滚轮转发给宿主滚动，
        /// 见下方"为什么"注释。
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            // V2.14 滚动宿主滚轮转发：
            // 【为什么】WinForms 的 WM_MOUSEWHEEL 只发给"聚焦控件"，并沿 聚焦控件→父链 冒泡到某个
            // ScrollableControl。若用户还没点击过任何窗口（焦点停在窗体本身）、或焦点落在标题栏的
            // TextBox/ComboBox 上（它们自己消费滚轮、不冒泡），滚轮就不会到达 pnlWindowScroll——
            // 表现为"鼠标在窗口矩阵上滚轮没反应"。这里用消息自带的鼠标屏幕坐标（lParam）判断光标
            // 是否落在滚动宿主内，命中就把消息原样转发给宿主（其 WndProc 按坐标判定后自行滚动）。
            // 光标在标题栏/状态栏等宿主外时保持默认行为，不干扰其它区域。
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg == WM_MOUSEWHEEL &&
                pnlWindowScroll != null && !pnlWindowScroll.IsDisposed &&
                pnlWindowScroll.VerticalScroll.Visible)
            {
                long lp = m.LParam.ToInt64();
                int sx = (short)(lp & 0xFFFF);          // 低16位 X 屏幕坐标（可能为负，转 short）
                int sy = (short)((lp >> 16) & 0xFFFF);  // 高16位 Y 屏幕坐标
                if (pnlWindowScroll.RectangleToScreen(pnlWindowScroll.ClientRectangle)
                        .Contains(sx, sy))
                {
                    SendMessage(pnlWindowScroll.Handle, m.Msg, m.WParam, m.LParam);
                    return;                             // 已交给宿主滚动，主窗体不再处理
                }
            }

            const int WM_NCHITTEST = 0x0084;
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                int hit = m.Result.ToInt32();
                // 这些命中码 = 窗口边缘/四角的调整大小热区，一律当作客户区处理
                if (hit >= 10 && hit <= 17) // HTLEFT(10) HTRIGHT(11) HTTOP(12) HTTOPLEFT(13)
                {                           // HTTOPRIGHT(14) HTBOTTOM(15) HTBOTTOMLEFT(16) HTBOTTOMRIGHT(17)
                    m.Result = new IntPtr(1); // HTCLIENT：当作点击客户区，不进入缩放
                }
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>发送 WM_* 消息给指定窗口（V2.14 滚动转发用；user32 原生 API）。</summary>
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 标题栏紧凑重排：按固定顺序把"可见"的字段从左往右 x 进位摆放，
        /// 隐藏的字段（ShowXxx=false）跳过不占位，避免中间空缺或重叠。
        /// 所有控件垂直居中：标题栏高 48，y = (48 - 控件高度)/2，视觉上全部居中对齐。
        /// 设计器里的坐标只作为"全部可见"时的初始参照，最终以这里算出的为准。
        ///
        /// 【V1.9.9：防止右侧灯区压住字段的根因修复】
        /// 标题栏里有两套互不知情的布局：左侧字段是"绝对坐标从左往右排"，右侧
        /// PLC 灯 + 每台相机灯是"Dock.Right 从右往左排"。原来 RelayoutTitleBar 只算
        /// 自己这一半——相机灯多（每台占 96px，Dock.Right）时右侧总体宽度变大，
        /// 挤占了画面，把"系统设置"按钮等最右侧字段推进灯区并被盖住。
        /// 修复：先统计右侧所有 Dock.Right 控件的总宽 rightDockWidth，把左侧字段的
        /// 最大 X 限制为 标题栏宽 - 右内边距 - rightDockWidth。
        ///
        /// 【V1.10.0：去掉"空间不足隐藏字段让位"逻辑】
        /// 早期版本在放不下时会按 hidePriority（产品型号→序列号→计数→分隔线）逐个
        /// 隐藏低价值字段再重排，保证按钮可见。但相机台数多时会把前边的信息字段直接
        /// 藏掉，现场"字段显示不出来"即由此而来。V1.10.0 相机区已
        /// 聚拢成"总标签+下拉框"固定宽度容器，右侧不再随台数膨胀，无需再隐藏任何字段。
        /// 现在所有可见字段一律完整排布，宽度超出右侧灯区边界时停止排布（不隐藏）。
        /// 注意：这里只负责排布，不改 ShowXxx 配置；字段可见性由 ApplyConfigVisibility 决定。
        /// </summary>
        private void RelayoutTitleBar()
        {
            const int barHeight = 48; // 标题栏固定高度（见 Designer 的 pnlTitleBar.Size）

            // 按配置开关恢复字段可见性（配置说该显示的字段必须显示，不再被压缩隐藏）。
            ApplyConfigVisibility();

            // 排布顺序固定：产品前缀 → 型号下拉(V2.8) → 序列号标题 → 序列号框 → | → 总数 → OK → NG → | → 系统设置按钮
            Control[] seq = { lblProductPrefix, cmbModel, lblSerialTitle, txtSerial, lblSep1,
                              lblTotal, lblOk, lblNg, lblSep2, btnSettings };

            // 右侧 Dock 区（PLC 灯 + 相机聚拢容器）占用的总宽：Dock.Right 控件从右往左叠，
            // 每个控件之间留 6px 视觉间距（间距是内在间距，宽幅估算 ±几像素不影响正确性）。
            int rightDockWidth = 0;
            foreach (Control c in pnlTitleBar.Controls)
                if (c.Dock == DockStyle.Right && c.Visible)
                    rightDockWidth += c.Width + 6;
            // 左侧字段可用的最大 X（标题栏宽 - 右内边距 - 右侧 Dock 区宽）。
            int maxX = pnlTitleBar.ClientSize.Width - 12 - rightDockWidth;

            // 单次从左往右排布：所有可见字段都摆放，放不下（越过右边界）就停，不隐藏任何字段。
            int x = 12; // 与设计器 Padding(12,0,12,0) 左内边距保持一致
            foreach (var c in seq)
            {
                if (!c.Visible) continue;
                int w = 0;
                if (c is Button)         w = c.Width + 12;
                else if (c is ComboBox)  w = c.Width + 12;
                else if (c == txtSerial) w = c.Width + 18;      // 序列号框固定宽度（TextBox），尊重设计宽度
                else if (c is Label)     w = ((Label)c).PreferredWidth + 18;
                if (x + w > maxX) break;                          // 越过右边界：停止排布，不隐藏
                int y = (barHeight - c.Height) / 2;               // 垂直居中
                c.Location = new Point(x, y);
                x += w;
            }
        }

        /// <summary>
        /// 把标题栏计数标签做成"实心彩色色块 + 白色加粗字"（现场要求 OK/NG 高亮醒目）。
        /// BackColor 用配置色（绿=OK、红=NG），ForeColor 白色，字号 11F→12F，
        /// 四周留 padding 让色块饱满；AutoSize 保持 true，色块宽度随数字自动伸缩，
        /// RelayoutTitleBar 的 PreferredWidth 布局照常工作、垂直居中公式不变。
        /// 【V1.9.2】客户反馈色块不够醒目，左右 padding 由 6→14、上下 2→3，整体加宽放大；
        /// 【V1.9.3】客户仍嫌不够醒目，左右 padding 再 14→22、上下 3→5，继续加宽放大。
        /// </summary>
        /// <param name="lbl">要样式化的标题栏计数标签（lblOk / lblNg）</param>
        /// <param name="color">色块底色（DisplayConfig.OkColor / NgColor）</param>
        private void StyleCountBadge(Label lbl, Color color)
        {
            lbl.BackColor = color;
            lbl.ForeColor = Color.White;
            lbl.Font = new Font("微软雅黑", 12F, FontStyle.Bold);
            lbl.Padding = new Padding(22, 5, 22, 5);
            lbl.TextAlign = ContentAlignment.MiddleCenter;
        }

        // ────────────── 产品型号下拉（V2.8）──────────────
        /// <summary>
        /// 填充"产品型号"下拉（V2.8，可重入：构造与设置保存热更都会调用）。
        /// 候选 = 预置三型号（AppConfig.DefaultProductModels）∪ 配置已有型号（_config.ProductModels），
        /// 去重、忽略空白、当前配置型号不在候选时补进去——保证 appconfig 缺 productModels 字段/
        /// 为空时标题栏也直接能下拉选 U171/U172/Z121（现场三型号写死预置，无需依赖配置文件）。
        /// DropDownStyle=DropDownList 只能从清单选（型号只认候选，不乱输）。
        /// 【防误触】填充/选中期间置 _modelComboInit=true，屏蔽 SelectedIndexChanged，
        /// 只有"用户真实选择"才进入 SwitchModel。
        /// 事件只挂线一次（_modelComboWired 标记，热更重复调用只刷新候选、不重复挂事件）。
        /// </summary>
        private void InitModelCombo()
        {
            _modelComboInit = true;
            try
            {
                cmbModel.Items.Clear();
                var candidates = new List<string>();
                // ① 预置三型号优先（用户要求"直接预置"）：即便配置为空也恒可下拉选到
                foreach (var m in AppConfig.DefaultProductModels())
                    if (!string.IsNullOrWhiteSpace(m)) candidates.Add(m);
                // ② 配置里追加的型号（设置页手输保存自动加入的）合进来
                foreach (var m in _config.ProductModels ?? new List<string>())
                    if (!string.IsNullOrWhiteSpace(m) && !candidates.Contains(m)) candidates.Add(m);
                // ③ 当前配置型号（_config.ProductModel）不在候选时补上，防下拉空白
                string cur = (_config.ProductModel ?? "").Trim();
                if (cur.Length > 0 && !candidates.Contains(cur)) candidates.Add(cur);
                foreach (var m in candidates) cmbModel.Items.Add(m);
                if (cur.Length > 0 && cmbModel.Items.Contains(cur)) cmbModel.SelectedItem = cur;
                else if (cmbModel.Items.Count > 0) cmbModel.SelectedIndex = 0;
            }
            finally
            {
                _modelComboInit = false;
            }

            if (_modelComboWired) return;
            _modelComboWired = true;
            cmbModel.SelectedIndexChanged += (s, e) =>
            {
                if (_modelComboInit) return;                       // 程序内初始化/刷新，非用户操作
                string model = cmbModel.SelectedItem?.ToString();
                if (string.IsNullOrWhiteSpace(model)) return;
                if (model == _config.ProductModel) return;         // 选中的就是当前型号，忽略
                SwitchModel(model);
            };
        }

        /// <summary>
        /// 主界面直接切换产品型号（V2.8，操作员生产日常操作）：
        ///   ① 更新配置 _config.ProductModel 并写盘（重启后保持当前型号；写盘失败只告警不阻断）；
        ///   ② 重建协调器——_productModel 是构造时快照，型号决定"点位→相机程序号"查哪张表
        ///      （ModelStationPrograms）与每次扫码写入 PLC 40007~40011 的型号值，换型号必须重建
        ///      coordinator 才生效。只重建协调器：PLC/相机/扫码枪连接参数与型号无关，全部复用，
        ///      比 ApplyRuntimeConfig 全量重建轻量（设备不断连，流程无缝）。
        ///   ③ 保留当前 SN 状态、重挂协调器事件、启动流程，标题栏提示"型号切换完成"（绿字）。
        /// 说明：PLC 型号区（40007~40011）由协调器在每次扫码时写入当前 _productModel，
        /// 这里切换后无需立即下发，下一拍扫码自然带上新型号。
        /// </summary>
        private void SwitchModel(string model)
        {
            _config.ProductModel = model;

            // ① 写盘持久化（try-catch：写盘失败不阻断切换，配置以内存为准）
            try { ConfigStore.Save(_config); }
            catch (Exception ex) { LogHelper.Warn("型号切换：配置写盘失败 " + ex.Message); }

            // ② 重建协调器（复用既有 PLC/相机/扫码枪/图像服务实例）
            string serial = _coordinator?.LatestSerialNumber ?? "";
            try { _coordinator?.Dispose(); }
            catch (Exception ex) { LogHelper.Warn("型号切换：协调器释放异常 " + ex.Message); }

            _coordinator = new ProductionCoordinator(_plc, _cameras,
                _config.Cameras ?? new List<CameraConfig>(), _imageStore,
                _config.Display.WindowEnabled, _config.ProductModel, _config.Display.WindowPointMaps);
            _coordinator.AttachScanners(_scanners);
            _coordinator.LatestSerialNumber = serial;
            SubscribeCoordinatorEvents();
            _coordinator.Start();

            // ③ 窗口矩阵跟随型号重建（V2.12.1）：窗口总数 = 各相机按新型号点位表条目和，
            // 换型号可能增删窗口，必须在标题栏切型号后就地重建矩阵（自适应/非自适应都一样）。
            BuildWindowGrid();

            // ④ 提示 + 日志（成功绿字，遵循现场 OK=绿 习惯）
            lblStatus.ForeColor = Color.FromArgb(46, 158, 107);
            lblStatus.Text = $"型号切换完成: {model}";
            LogHelper.Info($"产品型号切换：{model}（已生效并写盘，PLC 型号区随下次扫码写入）");
        }

        /// <summary>
        /// 显示窗口矩阵：按"当前型号 + 相机点位表"在【设计器容器 gridCameraWindows】里动态重建
        /// （V2.12.1 起自适应/非自适应统一，窗口总数=相机点位表条目和，见 DisplayConfig.ResolveLayout；
        ///  V2.14 起自适应行列形状改为"优先增加列、最后一行缺失最少"，见 ResolveLayout 注释）。
        /// 设计器只负责"容器长什么样"（Dock=Fill 铺满 pnlWindowScroll 滚动宿主、淡蓝白底），
        /// 具体行列数量与每格的 CameraDisplayControl 全部以这里为准重建，保证改行列/切型号/
        /// 保存配置即生效；行数放不下时滚动宿主自动出滚动条（见 ApplyGridScrollLayout）。
        ///
        /// 【V1.12.28 窗口禁用重排】DisplayConfig.WindowEnabled=false 的窗口【不创建控件】：
        /// 从矩阵中"完全移除"该格子，剩余启用窗口按原窗口编号顺序【紧凑排列】（编号保留原值，
        /// 不重新编号），尾部多出的格子留空。窗口编号与格子位置不再一一对应，
        /// 刷新窗口改走 _windowControls[窗口编号] 字典（见 OnInspectionFinished）。
        /// 为什么保留原编号：窗口编号绑定"相机点位表条目"（前上相机后下相机分组）与 WindowEnabled、
        /// StationPrograms（点位→相机程序）等配置，重新编号会打乱既有配置，宁可让格子位置空出。
        /// </summary>
        private void BuildWindowGrid()
        {
            // V2.12.1 统一布局：窗口总数 = 各相机按当前产品型号点位表条目数之和（与是否自适应无关，
            // 自适应/非自适都是"点位由相机表唯一决定，窗口只是按前上后下把点位条目铺排"）；
            // 行列形状：自适应自动算，非自适列用手填、行不足自动补齐（见 DisplayConfig.ResolveLayout）。
            // 当产品型号在各相机点位表里查不到任何点位时 windowCount≥1，矩阵至少保留一个窗口。
            var layout = DisplayConfig.ResolveLayout(
                _config.Cameras,
                _config.ProductModel,
                _config.Display.AutoFit,
                _config.Display.Rows,
                _config.Display.Columns);
            int rows = layout.rows, cols = layout.cols, total = layout.windowCount;
            int gridCells = rows * cols; // 矩阵总格子数（自适应下 ≥ 窗口总数，尾行空余格子留空）

            // 重置容器：先释放旧窗口（热更时旧窗口 PictureBox 持有图片句柄，必须 Dispose 防泄漏），
            // 再清掉设计器默认的 1×1 行列与可能残留的子控件。
            // 注意：释放必须针对"上一轮"的 _windowControls，所以要在重建之前做。
            // 热更前若有窗口正被全屏放大（挂在全屏窗体上），先移回 grid 一并释放，避免孤儿句柄泄漏。
            RestoreFullScreenWindow();
            var grid = gridCameraWindows;
            foreach (var w in _windowControls.Values)
                try { w?.Dispose(); } catch { }
            _windowControls.Clear();
            grid.Controls.Clear();
            grid.ColumnCount = cols;
            grid.RowCount = rows;
            grid.ColumnStyles.Clear();
            grid.RowStyles.Clear();

            // 所有行列按百分比等分 → 每格尺寸严格一致并铺满主区域，改行列数无需调像素
            for (int c = 0; c < cols; c++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
            for (int r = 0; r < rows; r++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

            // V2.14：铺满 vs 滚动。行少 → grid 占满滚动宿主（窗口尽量大、占满整个显示区域）；
            // 行多放不下 → 固定每行最小高度让矩阵变长，由 pnlWindowScroll（AutoScroll）出竖直滚动条。
            ApplyGridScrollLayout(grid, rows);

            // 逐窗口编号创建：禁用的窗口不建控件（从矩阵移除），启用的按顺序紧凑填格子。
            var enabled = _config.Display.WindowEnabled ?? new List<bool>();
            int cellIdx = 0;
            for (int w = 1; w <= total; w++)
            {
                bool on = enabled.Count < w || enabled[w - 1]; // 越界按启用（新窗口默认开）
                if (!on) continue;                             // 禁用窗口：不显示、不占格子
                if (cellIdx >= gridCells) break;               // 格子已填满（理论上不会）
                int r = cellIdx / cols, c = cellIdx % cols;
                cellIdx++;

                var win = new CameraDisplayControl
                {
                    Margin = new Padding(3),
                    Dock = DockStyle.Fill
                };
                win.SetWindowIndex(w); // 显示"窗口编号"（编号=相机点位表条目序号，前上相机后下相机分组）
                // V2.10.4：按配置控制左上角窗口编号显隐（默认显示；关掉画面更干净）
                win.SetWindowIndexVisible(_config.Display.WindowIndexVisible);
                // V2.10.8：按配置控制悬停气泡提示显隐（默认显示；勾掉画面更干净）
                win.SetToolTipVisible(_config.Display.WindowToolTipVisible);
                // V2.10.3：按配置控制右下角 OK/NG 徽标显隐与颜色（默认关；BuildWindowGrid 在
                // 构造与热更都会调用，改配置保存后即时生效）
                win.SetOkNgVisible(_config.Display.WindowOkNgVisible);
                win.SetOkNgColors(_config.Display.OkColor, _config.Display.NgColor);
                // 双击放大/还原（V1.12.15）：每格订阅双击事件，由 OnWindowDoubleClicked 统一处理。
                win.WindowDoubleClicked += OnWindowDoubleClicked;
                _windowControls[w] = win;
                grid.Controls.Add(win, c, r);
            }
            // Dock 布局按 z-order 自底向上处理，Fill 最后处理才会给 Top/Bottom 让位。
            // 此刻标题栏/底部栏都已存在，把矩阵放在 z-order 最顶（最后布局），
            // 否则 Top 的标题栏会叠加覆盖矩阵第一排（"第一排窗口显示不全"的根源）。
            grid.BringToFront();

            // V2.14：矩阵重建后把滚动宿主滚回顶部。否则切型号/热更重建时若上次滚到过中下部，
            // AutoScrollPosition 会残留 → 用户看到新矩阵"从中间开始"，第一排窗口被滚出视口。
            // 铺满模式（无滚动条）时置 0 无害。先 PerformLayout 让滚动范围随新行列算好再归零。
            if (pnlWindowScroll != null)
            {
                pnlWindowScroll.PerformLayout();
                pnlWindowScroll.AutoScrollPosition = new Point(0, 0);
            }
        }

        /// <summary>
        /// 窗口矩阵"每行最小高度"（V2.14，滚动阈值）：当 行数×本高度 超过滚动宿主可视高度时，
        /// 窗口不再被继续压缩，而是切换成"滚动模式"——每行固定本高度、超出部分由外侧滚动条翻看。
        /// 取 160：既保证基恩士画面缩到最小时仍可辨识，又不会因为行数稍多就让窗口塌成一条缝。
        /// </summary>
        private const int MinWindowRowHeight = 160;

        /// <summary>
        /// 切换窗口矩阵的"铺满 / 滚动"两种形态（V2.14）：
        ///   - 铺满（grid.Dock=Fill）：行样式百分比等分、矩阵占满整个窗口显示区——1 个窗口即最大
        ///     尺寸占满，2 个即左右平分，4 个即 2×2 等分，符合"不要多余空白"的自适应预期；
        ///   - 滚动（grid.Dock=Top + 定高）：行数×MinWindowRowHeight 超过宿主可视高度时，
        ///     矩阵按最小行高定高变长，pnlWindowScroll（AutoScroll=true）自动出右侧竖直滚动条，
        ///     鼠标滚轮 / 滑块翻看，标题栏与状态栏不随滚动。
        /// 【为什么以像素高度判定而不看行数】窗体 FixedSingle 全屏铺满（高度≈屏幕工作区），
        /// 用"行数×最小行高 > 可视高"能精确做到"放得下就占满、放不下才滚动"，与分辨率无关。
        /// 构造期宿主高度还没跟上（设计器默认值），先保守铺满；OnShown 铺到实际工作区后
        /// 会再调一次 BuildWindowGrid 重算，保证最终形态基于真实屏幕高度。
        /// </summary>
        private void ApplyGridScrollLayout(TableLayoutPanel grid, int rows)
        {
            var host = pnlWindowScroll;
            if (host == null || host.ClientSize.Height <= 0)
            {
                grid.Dock = DockStyle.Fill;   // 宿主尺寸未知（构造期）→ 保守铺满，OnShown 会重算
                return;
            }
            int minH = rows * MinWindowRowHeight + grid.Padding.Vertical;   // 滚动模式下矩阵应占高度
            bool scroll = minH > host.ClientSize.Height;                    // 放不下才出滚动条
            if (scroll)
            {
                grid.Dock = DockStyle.Top;      // Top：宽随宿主、高用自定值 → 超出后宿主出竖直滚动条
                grid.Height = minH;
                grid.AutoScroll = false;        // grid 自身不滚，滚动交给外层宿主
            }
            else
            {
                grid.Dock = DockStyle.Fill;     // 铺满整个显示区，窗口按行百分比等分占满
            }
        }

        /// <summary>
        /// 双击任一显示窗口的入口（V1.12.15，UI 线程）：
        ///   - 当前无全屏窗口 → 把双击的窗口放大到全屏（EnterFullScreenWindow）；
        ///   - 当前已有全屏窗口 → 先还原（移到 grid），若双击的正是全屏窗口则到此为止（还原完成），
        ///     否则继续把新双击的窗口放大（实现"双击放大、再双击还原"与"双击另一窗口切换"）。
        /// 事件来自 CameraDisplayControl.OnDoubleClick，已在 UI 线程，无需回切。
        /// </summary>
        private void OnWindowDoubleClicked(object sender, EventArgs e)
        {
            var w = sender as CameraDisplayControl;
            if (w == null) return;

            var cur = _fullScreenWindow;
            if (cur != null)
            {
                RestoreFullScreenWindow();          // 先把已有全屏窗口移回 grid 原单元格
                if (ReferenceEquals(w, cur)) return; // 双击的正是全屏窗口 → 仅还原即可
            }
            EnterFullScreenWindow(w);               // 否则把（新）双击的窗口放大到全屏
        }

        /// <summary>
        /// 把指定显示窗口放大到全屏（V1.12.15）：
        /// 用一个无边框、置顶、覆盖整屏（含任务栏）的独立 Form 承载该窗口，Dock=Fill 铺满。
        /// 【为什么用独立窗体而非主窗体内覆盖层】直接搬动 Dock 控件到主窗体覆盖层会与
        ///   标题栏/底部栏的 Dock 布局冲突（Fill 抢占剩余空间次序难控）；独立无边框窗体
        ///   布局最简单，且是 TopLevel 顶层窗口天然盖在一切之上。
        /// 【为什么要移动控件实例而非复制图片】检测完成刷新图片走的是 `_windowControls[窗口编号]`
        ///   SetImage（见 OnInspectionFinished），全屏时若复制图片，主流程刷新不生效、画面停住；
        ///   移动同一实例则全屏窗口里的画面照常随检测实时刷新。
        /// 【还原依据】进入全屏前记录该窗口在 grid 里的原单元格（_fullScreenCell），
        ///   还原时按它 Add 回原位，restore 后布局与放大前完全一致。
        /// </summary>
        private void EnterFullScreenWindow(CameraDisplayControl w)
        {
            if (_fullScreenForm != null || w == null) return; // 异常防护：已在全屏则不做
            LogHelper.Info($"窗口{ w.WindowIndex } 进入全屏");

            // 记录原单元格（column,row），还原时用 GetCellPosition 一致的下标 Add 回原位。
            var pos = gridCameraWindows.GetCellPosition(w);
            _fullScreenCell = new TableLayoutPanelCellPosition(pos.Column, pos.Row);

            // 全屏承载窗体：无边框、覆盖当前屏幕整屏（含任务栏）、置顶、不占任务栏。
            _fullScreenForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                TopMost = true,
                BackColor = Color.Black
            };
            _fullScreenForm.Bounds = Screen.FromControl(this).Bounds; // 覆盖整屏含任务栏
            _fullScreenForm.KeyPreview = true;
            // Esc 兜底还原：无边框窗体没有关闭按钮，除双击外按 Esc 也能退出全屏。
            _fullScreenForm.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) RestoreFullScreenWindow(); };
            _fullScreenForm.Shown += (s, e) => _fullScreenForm.Focus(); // 聚焦到全屏窗体，保证 Esc 能收到

            // 把窗口从 grid 移入全屏窗体：Dock=Fill 保持，正好铺满整屏。
            _fullScreenWindow = w;
            gridCameraWindows.Controls.Remove(w);
            w.Dock = DockStyle.Fill;
            w.Margin = new Padding(0); // 全屏吞掉 grid 的 3px 间距
            _fullScreenForm.Controls.Add(w);
            _fullScreenForm.Show();
        }

        /// <summary>
        /// 全屏还原（V1.12.15，UI 线程）：把挂在全屏窗体的窗口移回 grid 原单元格并释放全屏窗体。
        /// 幂等：无全屏窗口时直接返回（BuildWindowGrid/FormClosing 可在任意时机安全调用）。
        /// </summary>
        private void RestoreFullScreenWindow()
        {
            var form = _fullScreenForm;
            var w = _fullScreenWindow;
            var cell = _fullScreenCell;
            _fullScreenForm = null;
            _fullScreenWindow = null;
            _fullScreenCell = null;

            // 先关掉全屏窗体（把窗口从它身上摘下来，避免 Dispose 时连带销毁窗口）。
            if (form != null)
            {
                form.Controls.Remove(w);
                form.Close();
                form.Dispose();
            }

            // 把窗口放回 grid 原单元格，恢复 Fill + 间距，形态与放大前一致。
            if (w != null && gridCameraWindows != null && !gridCameraWindows.IsDisposed)
            {
                w.Dock = DockStyle.Fill;
                w.Margin = new Padding(3);
                if (cell != null)
                    gridCameraWindows.Controls.Add(w, cell.Value.Column, cell.Value.Row);
                else
                    gridCameraWindows.Controls.Add(w);
                w.BringToFront();
            }
        }

        /// <summary>
        /// 事件订阅总入口（仅构造调用一次）：
        ///   - 运行时业务事件（检测完成/状态变化/异常/设备连接状态）→ SubscribeRuntimeEvents，
        ///     构造与"设置保存热更"都会调用（旧服务已释放，新服务重新订阅，可重入）；
        ///   - 窗体生命周期事件（FormClosing 释放服务）只挂一次。lambda 内引用的是字段
        ///     （_monitor/_coordinator/_plc/_cameras），热更替换服务后自动指向新实例，无需解绑重挂。
        /// </summary>
        private void SubscribeEvents()
        {
            SubscribeRuntimeEvents();

            // 窗口大小变化时重排标题栏（V1.9.9）：相机灯多时右侧 Dock 区很宽，
            // 窗口缩窄会让左侧字段挤进灯区；Resize 时重新按"当前可用宽度"压缩/恢复字段。
            Resize += (s, e) => RelayoutTitleBar();

            FormClosing += (s, e) =>
            {
                // 若正有窗口全屏放大（挂在独立全屏窗体上），先关掉并移回，防止顶级窗体残留导致
                // 进程退出不了（V1.12.15）。
                RestoreFullScreenWindow();

                // 清理服务。任何一步异常都不能中断关窗，否则程序会卡在关闭流程（进程退出不了）。
                // 关窗顺序：先停心跳/编排，再断设备；各服务 Dispose 均已限时抢锁 + 锁外强断网，
                // 这里再做一层兜底 catch，保证即使个别服务释放出问题，窗口也能正常关闭退出。
                try { _monitor?.Dispose(); }
                catch (Exception ex) { LogHelper.Warn("关闭：监控器释放异常 " + ex.Message); }
                try { _coordinator?.Dispose(); }
                catch (Exception ex) { LogHelper.Warn("关闭：协调器释放异常 " + ex.Message); }
                try { _plc?.Dispose(); }
                catch (Exception ex) { LogHelper.Warn("关闭：PLC 释放异常 " + ex.Message); }
                foreach (var sc in _scanners)
                {
                    try { sc?.Dispose(); }
                    catch (Exception ex) { LogHelper.Warn("关闭：扫码枪释放异常 " + ex.Message); }
                }
                foreach (var cam in _cameras)
                {
                    try { cam?.Dispose(); }
                    catch (Exception ex) { LogHelper.Warn("关闭：相机释放异常 " + ex.Message); }
                }
                // V2.13.6：ImageStore 归主窗体所有，关闭时显式释放（协调器不再代关）。
                try { _imageStore?.Dispose(); }
                catch (Exception ex) { LogHelper.Warn("关闭：图像存储释放异常 " + ex.Message); }
                LogHelper.Info("程序关闭，服务已释放");
            };
        }

        /// <summary>
        /// 订阅"运行时"业务事件（构造与热更都会调用）：
        /// 检测完成 / 状态变化 / 异常提醒 / PLC与各相机连接状态指示灯 / 扫码条码。
        /// 旧服务实例在热更时已 Dispose，这里只对当前字段引用的新服务订阅，不会叠加。
        /// </summary>
        private void SubscribeRuntimeEvents()
        {
            // 协调器业务事件（检测完成/状态变化/异常）单独订阅；主界面切型号重建协调器时
            // 只需重挂这三个（见 SwitchModel），不重挂 PLC/相机/扫码枪灯事件（会叠加）。
            SubscribeCoordinatorEvents();

            // 扫码枪（V1.8.1 多台）：每台扫到的条码都更新当前产品序列号（进 {SN} 目录与标题栏）
            foreach (var sc in _scanners)
            {
                sc.SerialNumberScanned += OnSerialScanned;
                sc.Open(); // 串口打开失败 / TCP 连不上都不影响主流程，后台持续重连
            }
            // 扫码枪连接状态灯（V1.12.6）：订阅每台扫码枪的连接状态变化，聚合刷新标题栏右上角
            // "● 扫码枪"圆点灯颜色（样式与 PLC/相机灯一致：绿=已连接、红=未连接；全部启用的
            // 都已连接才绿，任一未连接即红）。事件在工作线程触发，RefreshScannerStatus 内部
            // Invoke 回 UI 线程。
            foreach (var sc in _scanners)
                sc.ConnectionChanged += (s, c) => RefreshScannerStatus();
            RefreshScannerStatus(); // 初始上色（构造/热更后立即按当前连接状态刷新一次）

            // 连接状态指示灯（V1.10.0 双模式）：
            //   ≤2台：每台一个灯，断连变红、重连回绿（UpdateDeviceStatus）；
            //   ≥3台：每台灯不存在（_lblCamStatuses 为 null），改为刷新"总状态标签+下拉圆点"。
            // PLC 灯（V1.12.11 起三态，见 UpdatePlcStatus）：监听就绪 + 主站连入 + 监听失败 三态区分。
            _plc.ConnectionChanged += (s, c) => UpdatePlcStatus();
            _plc.MasterConnectionChanged += (s, c) => UpdatePlcStatus();
            UpdatePlcStatus(); // 初始按当前状态上色（构造/热更后立即反映真实三态）
            for (int i = 0; i < _cameras.Count; i++)
            {
                int idx = i; // 闭包锁定下标，避免循环变量被所有事件共享
                _cameras[i].ConnectionChanged += (s, c) =>
                {
                    if (_lblCamStatuses != null && idx < _lblCamStatuses.Length && _lblCamStatuses[idx] != null)
                        UpdateDeviceStatus(_lblCamStatuses[idx], c);   // ≤2台：更新对应灯
                    RefreshCameraAggregateStatus();                    // ≥3台：刷新聚合（≤2台时内部直接返回）
                };
            }

            _monitor?.Start();
        }

        /// <summary>
        /// 订阅协调器的"运行时"业务事件（构造热更与主界面切型号 SwitchModel 都会调用）：
        /// 检测完成 / 状态变化 / 异常提醒。旧协调器在重建前已 Dispose，这里只对当前字段引用的
        /// 新实例订阅，不会叠加（注意：只挂协调器事件，不挂 PLC/相机/扫码枪灯事件，
        /// 那些事件的主窗订阅只在构造/热更挂一次，重复重挂会叠加出双倍刷新）。
        /// </summary>
        private void SubscribeCoordinatorEvents()
        {
            _coordinator.InspectionFinished += OnInspectionFinished;
            _coordinator.StateChanged += OnStateChanged;
            _coordinator.ErrorRaised += msg => LogHelper.Warn("界面收到错误：" + msg);
        }

        /// <summary>
        /// 扫到一条条码：更新当前产品序列号并刷新标题栏（V1.8.0 接入）。
        /// 事件来自扫码枪工作线程，统一 Invoke 回 UI 线程。
        /// </summary>
        private void OnSerialScanned(object sender, string code)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, string>(OnSerialScanned), sender, code);
                return;
            }
            _coordinator.LatestSerialNumber = code;
            if (txtSerial != null) txtSerial.Text = code;
            LogHelper.Info("当前产品序列号：" + code);
        }

        /// <summary>
        /// 刷新一个设备连接状态标签（后台线程事件触发，BeginInvoke 切回 UI 线程）。
        /// 颜色约定：绿=已连接，红=未连接（与现场 OK/NG 的绿红习惯一致）。
        /// </summary>
        private void UpdateDeviceStatus(Label lbl, bool connected)
        {
            if (IsDisposed || lbl == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<Label, bool>(UpdateDeviceStatus), lbl, connected);
                return;
            }
            lbl.ForeColor = connected ? Color.FromArgb(46, 158, 107) // 绿
                                      : Color.FromArgb(229, 72, 77);  // 红
        }

        /// <summary>
        /// 刷新标题栏"● PLC"连接灯（V1.12.11 起三态，对应从站模式的三种真实状态）：
        ///   红 = 监听失败/未启动（IsConnected=false）：上位机从站 502 都没起来，
        ///        检查端口是否被占用、是否绑定错误、Windows 防火墙是否放行入站；
        ///   黄 = 监听就绪但主站未连入（IsConnected && !HasMasterConnected）：等在等汇川主站来连，
        ///        说明 PLC 侧还没建立 TCP 会话——检查 PLC 主站程序是否运行、连的 IP/端口是否为本机 502；
        ///   绿 = 主站已连入（IsConnected && HasMasterConnected）：PLC 主站已 TCP 连上本机 502，通讯建立。
        /// 颜色与现场 OK/NG 习惯一致：绿=正常、红=故障，新增黄色=中间态（等待主站）。
        /// 同时把状态含义挂到悬停气泡上，现场鼠标放上去即可看到原因与排查方向。
        /// </summary>
        private void UpdatePlcStatus()
        {
            if (IsDisposed || lblPlcStatus == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdatePlcStatus));
                return;
            }

            string tipText;
            if (_plc.IsConnected)
            {
                if (_plc.HasMasterConnected)
                {
                    lblPlcStatus.ForeColor = Color.FromArgb(46, 158, 107);   // 绿：主站已连入
                    tipText = $"PLC 主站已连入（{_plc.IpLabel}），通讯正常";
                }
                else
                {
                    lblPlcStatus.ForeColor = Color.FromArgb(240, 173, 78);   // 黄：监听就绪、等待主站
                    tipText = $"从站监听就绪，等待 PLC 主站连入 {_plc.IpLabel}\n检查：PLC 主站程序是否运行、是否指向本机 502 端口";
                }
            }
            else
            {
                lblPlcStatus.ForeColor = Color.FromArgb(229, 72, 77);        // 红：监听失败
                tipText = $"PLC 从站监听未就绪（{_plc.IpLabel}）\n检查：端口占用 / 绑定 IP / 防火墙放行 502";
            }
            _plcTip = _plcTip ?? new ToolTip();
            _plcTip.SetToolTip(lblPlcStatus, tipText);
        }

        /// <summary>
        /// 刷新标题栏右上角"● 扫码枪"状态灯颜色（V1.12.6，颜色显示逻辑与 PLC/相机灯完全一致）：
        ///   文本固定"● 扫码枪"，只切颜色——已连接=绿(46,158,107)、未连接/断开=红(229,72,77)，
        ///   与 UpdateDeviceStatus（PLC/相机灯统一上色）同色值；
        ///   连接失败同样触发变红（V1.12.6 起 ScannerTcpService.TryConnect 失败也触发
        ///   ConnectionChanged(false)，对齐 PLC/相机"连不上就红"，此前扫码枪一直连不上时灯不变化）。
        /// 【多台聚合规则】对齐相机 ≥3 台的聚合语义：**只要有一台"启用"的扫码枪未连接就变红**，
        /// 全部启用扫码枪都已连接才变绿；禁用（Enabled=false）不参与判定；
        /// **没有任何启用的扫码枪时显示灰色**（同 PLC/相机灯"无设备/未判定"的初始灰），
        /// 不表示故障也不表示断开。
        /// 【数据源】_scanners[i] 与 _config.Scanners[i] 下标一一对应（BuildServices 按配置
        /// 顺序创建实例，绝不跳过），"启用与否"以配置为准——因为 Enabled=false 的实例 Open()
        /// 直接返回 false 不建连，IsOpen 恒 false，若不加过滤会误报红灯。
        /// 【线程】事件来自扫码枪工作线程，统一 BeginInvoke 回 UI 线程。
        /// </summary>
        private void RefreshScannerStatus()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshScannerStatus));
                return;
            }

            var configs = _config.Scanners ?? new List<ScanConfig>();
            bool connected = true; // 先假设全部已连接
            bool anyEnabled = false;
            for (int i = 0; i < _scanners.Count; i++)
            {
                bool enabled = i < configs.Count && configs[i].Enabled;
                if (!enabled) continue; // 禁用的扫码枪不参与聚合判定
                anyEnabled = true;
                if (!_scanners[i].IsOpen) { connected = false; break; } // 任一启用未连接 → 红
            }
            if (!anyEnabled)
            {
                // 没有启用的扫码枪：灰色（同 PLC/相机灯初始灰，表示"无设备/未判定"而非故障）
                lblScannerStatus.ForeColor = Color.FromArgb(150, 150, 150);
                return;
            }

            lblScannerStatus.ForeColor = connected ? Color.FromArgb(46, 158, 107) // 绿=已连接
                                                    : Color.FromArgb(229, 72, 77);  // 红=未连接
        }

        /// <summary>
        /// 一次检测完成：在界面线程刷新对应窗口图片+OK/NG徽标，并更新统计。
        /// 事件可能从工作线程抛出，统一 Invoke 回界面线程。
        ///
        /// 【V2.13.2 显示提速，两层配合，图片"到窗口"不再滞后】
        ///   ① 协调器（ProductionCoordinator.DoCameraShot）在 FTP 模式 jpeg 一到位就**提前从源文件
        ///      加载内存缩略图**塞进 WindowData.PreviewImage 随事件带过来——显示不等"jpeg+iv4p
        ///      归档复制 + 删 FTP 源"全部完成（iv4p 复制可能因文件在写而 400ms×3 重试，是旧链路最大
        ///      的隐性延迟）；UI 收到直接赋值，不做任何磁盘 IO。
        ///   ② 若 PreviewImage 为 null（非 FTP 取图 / 提前加载失败 / 源文件半截）——回退：后台
        ///      Task 读盘+解码+降采样（LoadThumbnailSafe）完成后把小图带回 UI 赋值。解码/缩放开销
        ///      完全移出界面线程（较旧版"UI 线程全尺寸解码"已不卡界面）。
        ///   计数/标题等轻量更新与图片加载分开投递（图片加载不得拖慢写回 PLC 结果的协调器线程）。
        /// </summary>
        private void OnInspectionFinished(WindowData data, int windowIndex)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                // ① 计数更新立刻回 UI（纯状态刷新，即刻反映到标题栏）
                BeginInvoke(new Action<WindowData>(UpdateCountsTitle), data);

                if (data.PreviewImage != null)
                {
                    // 协调器已提前加载好的内存缩略图：直接转交 UI（无磁盘 IO，最快路径）
                    BeginInvoke(new Action<WindowData, int, Image>(ApplyResultImage), data, windowIndex, data.PreviewImage);
                    return;
                }

                // ② 回退路径：图片读盘/解码/降采样放后台 Task，完成后小图回 UI 赋值。
                string path = data.ImagePath;
                Task.Factory.StartNew(() =>
                {
                    Image thumb = (!string.IsNullOrEmpty(path) && File.Exists(path))
                        ? ProductionCoordinator.LoadThumbnailSafe(path)
                        : null;
                    if (IsDisposed)
                    {
                        // 窗体已关：无窗口可显示，立即释放缩略图防 GDI+ 句柄泄漏
                        thumb?.Dispose();
                        return;
                    }
                    try
                    {
                        BeginInvoke(new Action<WindowData, int, Image>(ApplyResultImage), data, windowIndex, thumb);
                    }
                    catch
                    {
                        // 关窗竞态：BeginInvoke 抛异常（句柄已销毁等），原地释放缩略图防泄漏
                        thumb?.Dispose();
                    }
                });
                return;
            }

            // 罕见：直接在 UI 线程调用（测试/harness 直连）：优先用事件带的内存图，否则同步缩略图。
            UpdateCountsTitle(data);
            Image img = data.PreviewImage
                ?? (!string.IsNullOrEmpty(data.ImagePath) && File.Exists(data.ImagePath)
                    ? ProductionCoordinator.LoadThumbnailSafe(data.ImagePath)
                    : null);
            ApplyResultImage(data, windowIndex, img);
        }

        /// <summary>
        /// 检测计数+标题栏刷新（V2.13.2 拆出）：只做轻量 UI 状态更新，供检测完成事件
        /// 的 UI 回调使用（图片刷新见 ApplyResultImage，两者独立分工，计数不依赖图片加载结果）。
        /// </summary>
        private void UpdateCountsTitle(WindowData data)
        {
            if (IsDisposed) return;
            _total++;
            if (data.IsOk) _ok++; else _ng++;
            RefreshTitle();
        }

        /// <summary>
        /// 把后台加载好的缩略图赋给对应窗口（V2.13.2 拆出，UI 线程执行）：
        /// 按窗口编号查字典（禁用的窗口不建控件，事件异常路径直接忽略）；
        /// 窗口已消失（禁用/切型号重建）时不落控件、原地 Dispose 缩略图，防句柄泄漏。
        /// </summary>
        private void ApplyResultImage(WindowData data, int windowIndex, Image thumb)
        {
            if (IsDisposed) { thumb?.Dispose(); return; }
            if (_windowControls.TryGetValue(windowIndex, out var w))
            {
                w.SetImage(thumb);
                w.SetOkNgStatus(data.IsOk);
            }
            else
            {
                thumb?.Dispose(); // 窗口已重建：释放刚加载的缩略图，防句柄泄漏
            }
        }

        /// <summary>
        /// 流程状态文本刷新（工作线程抛出，需回 UI 线程）。
        /// 每次流程状态更新都恢复默认深蓝灰色文字——型号切换成功的"绿色提示"只在切换成功
        /// 的瞬间显示，随后流程推进（如"等待PLC主站到位"）会恢复常规颜色。
        /// </summary>
        private void OnStateChanged(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(OnStateChanged), text); return; }
            lblStatus.ForeColor = Color.FromArgb(52, 73, 94); // 恢复默认深蓝灰
            lblStatus.Text = "状态: " + text;
        }

        /// <summary>刷新标题栏统计与产品型号（型号由下拉框自持显示，无需在这里刷）。</summary>
        private void RefreshTitle()
        {
            lblTotal.Text = "总数: " + _total;
            lblOk.Text = "OK: " + _ok;
            lblNg.Text = "NG: " + _ng;
        }

        /// <summary>
        /// 配置保存后的热生效入口（V1.6.0，免重启）：停掉旧服务层，用新配置全量重建。
        /// 服务连接是惰性的（EnsureConnected 才建连），重建后由后台心跳/到位轮询按新 IP 自动重连，
        /// 等效于"按新配置断开重连"；界面（标题栏字段/相机灯/OK-NG 色块、窗口矩阵）同步按新配置重建。
        ///
        /// 【线程安全】本方法在 UI 线程执行，但只做"停服务/建对象/摆控件"，不发任何网络请求，
        /// 不违反"UI 线程禁网络 IO"铁律；真正的连接动作发生在后台心跳线程。
        /// 各服务 Dispose 均有"限时抢锁 + 锁外强断网"兜底，即使后台连接任务正忙也不会阻塞界面。
        ///
        /// 【为什么全量重建而非局部热更】PLC/相机寄存器、FTP 目录、窗口行列、相机台数等配置
        /// 相互牵连（coordinator 持有相机列表与窗口总数、ImageStore 持有 FTP 监听），局部替换易留
        /// 旧引用；全量重建逻辑简单且不易出错，副作用仅是"保存后设备短暂断连、几秒内自动连回"，
        /// 对现场可接受。
        /// </summary>
        private void ApplyRuntimeConfig()
        {
            // ① 保留下一个流程要用的状态（新 coordinator 实例会重建，这些状态属于主窗体）
            string serial = _coordinator?.LatestSerialNumber ?? "";

            // ② 停旧服务：关窗顺序同 FormClosing，先停心跳/编排，再断设备
            try { _monitor?.Dispose(); }
            catch (Exception ex) { LogHelper.Warn("热更：监控器释放异常 " + ex.Message); }
            try { _coordinator?.Dispose(); }
            catch (Exception ex) { LogHelper.Warn("热更：协调器释放异常 " + ex.Message); }
            try { _plc?.Dispose(); }
            catch (Exception ex) { LogHelper.Warn("热更：PLC 释放异常 " + ex.Message); }
            foreach (var sc in _scanners)
            { try { sc?.Dispose(); } catch (Exception ex) { LogHelper.Warn("热更：扫码枪释放异常 " + ex.Message); } }
            foreach (var cam in _cameras ?? new List<KeyenceIV4Camera>())
            { try { cam?.Dispose(); } catch (Exception ex) { LogHelper.Warn("热更：相机释放异常 " + ex.Message); } }
            // V2.13.6：ImageStore 归主窗体所有（协调器不再代关），热更重建前必须显式释放旧的，
            // 否则旧 FileSystemWatcher 会一直监听旧目录（句柄泄漏 + 事件发给已废弃的信号）。
            try { _imageStore?.Dispose(); }
            catch (Exception ex) { LogHelper.Warn("热更：图像存储释放异常 " + ex.Message); }

            // ③ 用新配置重建服务层（BuildServices 内部全部读取 _config 的最新值）
            BuildServices();
            _coordinator.LatestSerialNumber = serial;

            // ④ 重建界面与重新订阅：标题栏（相机灯/色块）→ 型号下拉候选刷新 → 窗口矩阵 → 运行时事件 → 启动流程
            InitTitleBarFields();
            InitModelCombo();      // 热更后：按新配置型号/候选刷新标题栏下拉
            BuildWindowGrid();
            SubscribeRuntimeEvents();
            _coordinator.Start();
            RelayoutTitleBar();
            RefreshTitle();

            LogHelper.Info("配置已保存并热生效（服务层已按新配置重建）");
        }

        /// <summary>
        /// 打开系统设置：保存后写盘并热生效（V1.6.0 起免重启）。
        /// 【V1.9.0 管理员登录】每次点击先校验账号（SecurityConfig.AdminEnabled=true 时，
        /// 弹 LoginForm 登录，只有验证通过才放行），防止现场操作员随意改关键配置。
        /// 【V1.12.0 双账号分流】LoginForm 校验通过后按角色（login.Role）决定打开哪个界面：
        ///   - LoginRole.Admin → 系统设置窗体 SettingsForm（改配置，原行为）；
        ///   - LoginRole.Developer → 功能测试窗体 DevTestForm（相机/PLC 通讯验证）。
        /// 开发者账号进入功能测试后不写盘、不改配置，且复用主窗体已建好的 PLC/相机连接。
        /// </summary>
        private void OpenSettings()
        {
            // 登录校验（V1.9.0）：启用时每次点都要求登录，无"记住登录状态"。
            // 传整个 _config：LoginForm 里不仅能登录，还能修改管理员密码（改后直接写盘）。
            if (_config.Security.AdminEnabled)
            {
                using (var login = new LoginForm(_config))
                {
                    if (login.ShowDialog(this) != DialogResult.OK)
                        return; // 取消/连续失败：不进任何界面

                    // 开发者账号 → 功能测试窗体（V1.12.0）：复用主窗体已有连接，不新建
                    if (login.Role == LoginRole.Developer)
                    {
                        // 传入 PLC/相机/扫码枪服务实例与相机扫码配置：测试窗体直接复用、不建新连接；
                        // V1.12.24 追加 ImageStore + 相机配置 + 当前 SN 快照，供"T2 取图→闪图→存图"测试
                        using (var test = new DevTestForm(_plc, _cameras, _scanners, _config.Scanners,
                            _imageStore, _config.Cameras, _coordinator?.LatestSerialNumber ?? ""))
                            test.ShowDialog(this);
                        return; // 测试窗体关闭后不触发保存/热更（测试不产生配置改动）
                    }
                    // 其余（Admin）继续走系统设置
                }
            }

            // V2.10.1：把主窗体标题栏型号下拉的"当前选中值"传给设置窗体，保证两个 cmbModel 同步。
            // 不传 _config.ProductModel 的原因：标题栏下拉在配置型号为空时会默认选第一个候选，
            // 但 _config.ProductModel 仍是空，设置页直接读配置会显示空白（见 SettingsForm 的 _titleBarModel）。
            // V2.12.x：配置对话框（SettingsForm→WindowPointForm）里切型号【不实时影响主界面】——
            // 只同步设置页"产品型号"下拉（OnSave 时写 _cfg.ProductModel），用户点【保存】后本方法
            // 下面的 ApplyRuntimeConfig 才刷新标题栏型号下拉 + 窗口矩阵 + 协调器（见 ApplyRuntimeConfig）。
            using (var dlg = new SettingsForm(_config, cmbModel.SelectedItem?.ToString()))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                ConfigStore.Save(_config);
                ApplyRuntimeConfig();   // 保存即生效：停旧服务、按新配置重建服务层与界面
                MessageBox.Show("配置已保存并即时生效。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}