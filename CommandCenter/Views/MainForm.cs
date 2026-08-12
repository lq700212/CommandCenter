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
    /// 命令中心主窗体。
    /// 【界面布局】
    /// ┌───────────────────────────────────────────────────────────────────┐
    /// │ 产品型号:[1]产品A▾ | 序列号:[框] | 总数:0 | [OK] | [NG] | [系统设置] │
    /// │                                                        ●PLC ●相机1 ●相机2 │
    /// ├───────────────────────────────────────────────────────────────────┤
    /// │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                  │
    /// │  │ W1   │ │ W2   │ │ W3   │ │ W4   │ │ W5   │                   │
    /// │  │ [OK] │ │ [NG] │ │ [OK] │ │ ...  │ │ [NG] │                   │
    /// │  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘                  │
    /// │  （Rows × Columns 个窗口，逐次环形刷新）                            │
    /// ├───────────────────────────────────────────────────────────────────┤
    /// │ 状态:等待PLC到位…（左下角；配方下发成功时变绿显示"配方切换完成"）          │
    /// └───────────────────────────────────────────────────────────────────┘
    /// 标题栏：左起信息字段（按配置开关）→ 配方下拉框（显示+切换合一）→ 系统设置按钮 → 连接指示灯；
    ///   - OK/NG 计数默认"实心彩色色块 + 白字"高亮（绿底=OK、红底=NG），关闭
    ///     DisplayConfig.TitleOkNgHighlight 则回退普通彩色文字；
    /// 底部栏：仅状态文本，固定在左下角。
    /// 职责：只做界面呈现 + 事件绑定，业务编排在 ProductionCoordinator。
    /// 静态布局控件（标题栏字段/配方下拉框/设置按钮/PLC灯/状态栏/窗口矩阵容器）在
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
        private RecipeManager _recipes;
        private ProductionCoordinator _coordinator;
        private ConnectionMonitor _monitor;
        private List<IScanner> _scanners = new List<IScanner>();   // 扫码枪列表（多台各一个实例，V1.8.1 起支持多台；串口/基恩士 TCP 无协议按各自 Mode 二选一）
        private Label[] _lblCamStatuses;            // 每个相机一个连接指示灯（按相机下标对齐）
        private bool _recipeComboInit;    // 组合框程序内初始化/刷新时防误触 SelectedIndexChanged
        private int _recipeSwitchVer;     // 配方下发任务的版本号：只让"最新一次切换"的结果更新状态条（丢弃过期提示）
        private CameraDisplayControl[] _windows;

        // 统计
        private int _total, _ok, _ng;

        public MainForm()
        {
            InitializeComponent();   // 先解析设计器里的静态控件（否则后续代码引用会拿到 null）

            _config = ConfigStore.Load();
            _recipes = new RecipeManager();
            _recipes.Load();

            BuildServices();         // PLC/多相机/图像/协调器 就绪（相机灯数量依赖 _cameras）
            InitTitleBarRuntime();   // 按配置补全标题栏：文案/可见性/配方填充/动态相机灯/紧凑重排
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
            LogHelper.Info($"BuildServices：共创建 {_cameras.Count} 台相机：{string.Join(" / ", _cameras.ConvertAll(x => x.IpLabel))}");

            _imageStore = new ImageStore(_config.Image);
            _coordinator = new ProductionCoordinator(_plc, _cameras, cams, _imageStore, _config.Display,
                _config.Display.WindowStationMap);

            // 连接健康监控：后台心跳 + 断连自动重连 + 边沿日志（不影响任何 UI 刷新）
            _monitor = new ConnectionMonitor(_plc, _cameras);

            // 扫码枪（V1.8.1 起支持多台）：每台按各自的 ScanConfig.Mode 选实现——
            // "Tcp"=基恩士 SR 以太网无协议，其余按串口兜底。扫码枪断连自愈由实现类内部完成，
            // 不占 ConnectionMonitor。列表为空则不留任何扫码枪（序列号走手动输入/模拟）。
            _scanners = new List<IScanner>();
            foreach (var sc in _config.Scanners ?? new List<ScanConfig>())
                _scanners.Add(BuildScanner(sc));
        }

        /// <summary>
        /// 按配置创建一台扫码枪实例。
        /// "Tcp" → ScannerTcpService（基恩士 SR 系列 TCP/IP 无协议，上位机作客户端收条码行）；
        /// 其余 → ScannerService（串口 RS-232）。两者实现同一 IScanner 接口。
        /// </summary>
        private static IScanner BuildScanner(ScanConfig scan)
        {
            if (scan != null
                && !string.IsNullOrWhiteSpace(scan.Mode)
                && scan.Mode.Trim().Equals("Tcp", StringComparison.OrdinalIgnoreCase))
            {
                return new ScannerTcpService(scan);
            }
            return new ScannerService(scan ?? new ScanConfig());
        }

        /// <summary>
        /// 标题栏"运行时"初始化（仅构造调用一次）：
        ///   ① 字段/可见性/OK-NG 色块/相机灯 → InitTitleBarFields（可重入，热更时再调）；
        ///   ② 配方下拉框填充（只一次）；
        ///   ③ 设置按钮事件挂线（只一次）。
        /// 设计器负责"控件长什么样"，此处负责"数据与动态部分"。
        /// </summary>
        private void InitTitleBarRuntime()
        {
            // ① 标题栏字段 + 动态相机灯（构造与"设置保存热更"都会调用，可重入）
            InitTitleBarFields();

            // ② 配方下拉框：填充配方项并选中当前（期间屏蔽事件，防初始化误触发切换）
            InitRecipeCombo();

            // ③ 设置按钮事件（设计器只做外观，交互在这里挂线，只挂一次）
            btnSettings.Click += (s, e) => OpenSettings();
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
            // 序列号：标题"序列号:"在显示框外（lblSerialTitle），框内只放值；
            // 有值显示值，没有则框内留空（不写"待扫码"），标题+框整体由开关控制显隐
            lblSerialTitle.Text = "序列号:";
            lblSerial.Text = _coordinator.LatestSerialNumber;

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
        /// 为什么抽出来：RelayoutTitleBar 在空间不足时会临时隐藏低价值字段（.Visible=false），
        /// 窗口变大/热更后重排需要"先恢复配置应显示的字段再压缩"，共用同一份配置判定避免两处漂移。
        /// cmbRecipe 默认始终显示（暂无独立开关，保持既有行为）。
        /// </summary>
        private void ApplyConfigVisibility()
        {
            lblProductPrefix.Visible = _config.Display.ShowProductModel;
            cmbRecipe.Visible = true; // 配方下拉框暂不设独立开关（V1.9.9 保持既有行为）
            lblSerialTitle.Visible = _config.Display.ShowSerialNumber;
            lblSerial.Visible = _config.Display.ShowSerialNumber;
            lblTotal.Visible = _config.Display.ShowTotalCount;
            lblOk.Visible = _config.Display.ShowOkCount;
            lblNg.Visible = _config.Display.ShowNgCount;
            // 系统设置按钮显隐（V1.8.4）：按配置隐藏后标题栏自动紧凑重排，隐藏期间配置只读
            btnSettings.Visible = _config.Display.ShowSettingsButton;
        }

        /// <summary>
        /// 重建标题栏每台相机的连接指示灯（构造与热更都会调用）。
        /// 先移除旧的（热更后相机台数可能变化，必须整套重建），再按当前台数正序 Add：
        /// Dock.Right 布局是"先 Add 的靠左、后 Add 的靠右"，正序循环得到
        /// 相机1..相机N 依次排在 PLC 灯右侧（V1.7.1 起：相机1 在相机2 左边，相机3 继续往右排）。
        /// lblCamPlaceholder 是设计器视觉提示，隐藏后 Dock 空间让给循环生成的真灯。
        /// </summary>
        private void BuildCameraStatusLights()
        {
            if (_lblCamStatuses != null)
                foreach (var lbl in _lblCamStatuses)
                    if (lbl != null) pnlTitleBar.Controls.Remove(lbl);

            lblCamPlaceholder.Visible = false;
            _lblCamStatuses = new Label[_cameras.Count];
            for (int i = 0; i < _cameras.Count; i++)
            {
                var lbl = new Label
                {
                    Dock = DockStyle.Right,
                    Width = 96,
                    TextAlign = ContentAlignment.MiddleRight,
                    Text = $"● 相机{i + 1}",
                    ForeColor = Color.FromArgb(150, 150, 150),
                    Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold)
                };
                pnlTitleBar.Controls.Add(lbl);
                _lblCamStatuses[i] = lbl;
            }
        }

        /// <summary>
        /// 窗体首次显示完成（自动缩放已应用）：执行标题栏紧凑重排。
        /// 若在构造函数里重排，AutoScaleMode.Font 会在后续布局中按设计器基准缩放
        /// 控件（覆盖我们赋的 Location），表现为"字段仍停在设计器写死坐标"。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RelayoutTitleBar();
        }

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
        /// 最大 X 限制为 标题栏宽 - 右内边距 - rightDockWidth；空间不足时按优先级
        /// （hidePriority：产品型号→配方→序列号→计数→分隔线）逐个隐藏低价值字段
        /// 再重排，保证"系统设置"按钮始终可见、不被灯盖住。
        /// 注意：这里的隐藏只是运行时"放不下才让位"，不改 ShowXxx 配置；
        /// 热更（InitTitleBarFields）会按配置值重新设置可见性再调用本方法。
        /// </summary>
        private void RelayoutTitleBar()
        {
            const int barHeight = 48; // 标题栏固定高度（见 Designer 的 pnlTitleBar.Size）

            // 先恢复配置可见性：上次重排可能因空间不足临时隐藏了低价值字段，
            // 窗口拉大/热更后必须把"配置说该显示"的字段先亮回来，再按当前空间压缩。
            ApplyConfigVisibility();

            // 排布顺序固定：产品前缀 → 配方下拉框 → 序列号标题 → 序列号框 → | → 总数 → OK → NG → | → 系统设置按钮
            Control[] seq = { lblProductPrefix, cmbRecipe, lblSerialTitle, lblSerial, lblSep1,
                              lblTotal, lblOk, lblNg, lblSep2, btnSettings };

            // 字段"让位"优先级（低→高）：空间不足时从低往高隐藏，系统设置按钮永远保留。
            // 为什么产品/配方先让位：它们只是上下文提示，丢了不影响操作；计数、按钮是刚需。
            Control[] hidePriority = { lblProductPrefix, cmbRecipe, lblSerialTitle, lblSerial,
                                       lblSep1, lblTotal, lblOk, lblNg, lblSep2, btnSettings };

            // 右侧 Dock 区（PLC 灯 + 全部相机灯）占用的总宽：Dock.Right 控件从右往左叠，
            // 每个灯之间留 6px 视觉间距（灯间距是内在间距，宽幅估算 ±几像素不影响正确性）。
            int rightDockWidth = 0;
            foreach (Control c in pnlTitleBar.Controls)
                if (c.Dock == DockStyle.Right && c.Visible)
                    rightDockWidth += c.Width + 6;
            // 左侧字段可用的最大 X（标题栏宽 - 右内边距 - 右侧灯区宽）。
            int maxX = pnlTitleBar.ClientSize.Width - 12 - rightDockWidth;

            while (true)
            {
                int x = 12; // 与设计器 Padding(12,0,12,0) 左内边距保持一致
                bool fits = true;
                foreach (var c in seq)
                {
                    if (!c.Visible) continue;
                    int w = 0;
                    if (c is Button)      w = c.Width + 12;
                    else if (c is ComboBox) w = c.Width + 12;
                    else if (c == lblSerial) w = c.Width + 18;        // 固定宽度显示框
                    else                  w = ((Label)c).PreferredWidth + 18;
                    if (x + w > maxX) { fits = false; break; }          // 放不下→本次排布报废
                    int y = (barHeight - c.Height) / 2;                 // 垂直居中
                    c.Location = new Point(x, y);
                    x += w;
                }
                if (fits) return; // 全部可见字段都放下，完成

                // 放不下：隐藏下一个"最可让位"的可见字段后重排（按钮最后，永不主动隐藏它）
                var toHide = hidePriority.FirstOrDefault(h => h.Visible);
                if (toHide == null) return; // 已无可让位字段（极端情况），按当前可见性排到哪算哪
                toHide.Visible = false;
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

        /// <summary>
        /// 填充"配方显示+切换合一"下拉框：控件显示当前配方名，用户点击弹出下拉列表，
        /// 选择一项即触发配方切换（选中项会回显到控件）。
        /// DropDownStyle=DropDownList 保证只能从清单里选，不能乱输。
        /// 【防误触】程序初始化填充/刷新时置 _recipeComboInit=true，屏蔽 SelectedIndexChanged，
        /// 只有"用户真实选择"才进入 SwitchRecipe。
        /// </summary>
        private void InitRecipeCombo()
        {
            // 填充配方项并选中当前配方（期间屏蔽事件，防止初始化就误触发切换）
            _recipeComboInit = true;
            try
            {
                foreach (var r in _recipes.Recipes)
                    cmbRecipe.Items.Add($"[{r.Id}] {r.Name}");
                // Current 初始为 null（还没切过配方）时，默认选中第一条作为当前配方，
                // 让下拉框"一开始就显示当前配方"而不是空白；
                var cur = _recipes.Current ?? _recipes.Recipes.FirstOrDefault();
                if (cur != null)
                {
                    _recipes.SwitchTo(cur.Id); // 把默认配方落为 Current（无订阅者，事件无副作用）
                    cmbRecipe.SelectedItem = $"[{cur.Id}] {cur.Name}";
                }
            }
            finally
            {
                _recipeComboInit = false;
            }

            cmbRecipe.SelectedIndexChanged += (s, e) =>
            {
                if (_recipeComboInit) return;                          // 程序初始化/刷新，非用户操作
                string item = cmbRecipe.SelectedItem as string;
                if (item == null) return;
                var recipe = _recipes.Recipes.FirstOrDefault(r => item == $"[{r.Id}] {r.Name}");
                if (recipe != null) SwitchRecipe(recipe);             // 用户选中 → 执行切换
            };
        }

        /// <summary>
        /// 执行配方切换（上位机侧流程）：
        ///   ① 本地立即记录当前配方（UI 线程，秒回，下拉框马上锁住用户选择）；
        ///   ② 【不卡 UI】把配方号通过 PLC 下发改到后台线程执行——Modbus TCP 写可能因
        ///      PLC 不可达/超时要等 2s，绝不能堵住界面线程（AGENTS 铁律"UI 线程禁做网络 IO"）；
        ///   ③ 只有"成功发到 PLC"才在左下角状态输出绿色"配方切换完成"；失败输出红色提示。
        ///   连续快速切换时用版本号 _recipeSwitchVer 丢弃过期任务的提示，避免旧结果覆盖新结果。
        /// 颜色遵循现场习惯：OK=绿、NG=红（与标题栏 OK/NG 计数一致）。
        /// </summary>
        private void SwitchRecipe(RecipeConfig recipe)
        {
            _recipes.SwitchTo(recipe.Id);                        // ① 本地切换（同步，足够快）
            int ver = ++_recipeSwitchVer;                        // 本次下发任务的代号

            Task.Run(() =>
            {
                // ② 后台线程写 PLC（WriteRecipe 内部自带锁与超时，多任务并发也安全）
                bool sentOk = _plc.WriteRecipe(recipe.Id);

                // ③ 回 UI 线程更新状态条；若期间又切换了别的新配方，则丢弃本次过期提示
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    if (ver != _recipeSwitchVer) return;         // 已有更新的切换，本次结果作废
                    if (sentOk)
                    {
                        lblStatus.ForeColor = Color.FromArgb(46, 158, 107); // 成功 → 绿
                        lblStatus.Text = $"配方切换完成: {recipe.Name}";
                        LogHelper.Info($"配方切换完成并已成功下发 PLC：[{recipe.Id}] {recipe.Name}");
                    }
                    else
                    {
                        lblStatus.ForeColor = Color.FromArgb(229, 72, 77);  // 失败 → 红
                        lblStatus.Text = $"配方下发 PLC 失败: {recipe.Name}（请检查 PLC 通讯）";
                        LogHelper.Warn($"配方切换但 PLC 下发失败：[{recipe.Id}] {recipe.Name}");
                    }
                }));
            });
        }

        /// <summary>
        /// 显示窗口矩阵：按配置 Rows×Columns 在【设计器容器 gridCameraWindows】里动态重建。
        /// 设计器只负责"容器长什么样"（Dock=Fill 铺满主区、淡蓝白底），具体行列数量与
        /// 每格的 CameraDisplayControl 全部以这里为准重建，保证改 Rows/Columns 配置即生效。
        /// </summary>
        private void BuildWindowGrid()
        {
            int rows = Math.Max(1, _config.Display.Rows);
            int cols = Math.Max(1, _config.Display.Columns);

            // 重置容器：先释放旧窗口（热更时旧窗口 PictureBox 持有图片句柄，必须 Dispose 防泄漏），
            // 再清掉设计器默认的 1×1 行列与可能残留的子控件。
            // 注意：释放必须针对"上一轮"的 _windows 数组，所以要在 _windows 重建之前做。
            var grid = gridCameraWindows;
            if (_windows != null)
                foreach (var w in _windows)
                    try { w?.Dispose(); } catch { }
            _windows = null;
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

            // 新建数组放到填充循环之前：顺序必须是"先释放旧数组→再建新数组→再填充"，
            // 否则循环里 _windows[idx] = w 会对 null 解引用抛 NullReferenceException。
            _windows = new CameraDisplayControl[rows * cols];

            int idx = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var w = new CameraDisplayControl
                    {
                        Margin = new Padding(3),
                        Dock = DockStyle.Fill
                    };
                    w.SetWindowIndex(idx + 1);
                    // 主界面窗口不再显示存图点位标识（点位只通过设置界面 WindowPointForm 查询比对）；
                    // 点位归属由 ProductionCoordinator 按 WindowStationMap 映射计算，窗口编号即拍照顺序。
                    _windows[idx] = w;
                    grid.Controls.Add(w, c, r);
                    idx++;
                }
            }
            // Dock 布局按 z-order 自底向上处理，Fill 最后处理才会给 Top/Bottom 让位。
            // 此刻标题栏/底部栏都已存在，把矩阵放在 z-order 最顶（最后布局），
            // 否则 Top 的标题栏会叠加覆盖矩阵第一排（"第一排窗口显示不全"的根源）。
            grid.BringToFront();
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
            _coordinator.InspectionFinished += OnInspectionFinished;
            _coordinator.StateChanged += OnStateChanged;
            _coordinator.ErrorRaised += msg => LogHelper.Warn("界面收到错误：" + msg);

            // 扫码枪（V1.8.1 多台）：每台扫到的条码都更新当前产品序列号（进 {SN} 目录与标题栏）
            foreach (var sc in _scanners)
            {
                sc.SerialNumberScanned += OnSerialScanned;
                sc.Open(); // 串口打开失败 / TCP 连不上都不影响主流程，后台持续重连
            }

            // 连接状态指示灯：PLC 与每台相机 断连时 UI 实时变红，重连成功回绿
            _plc.ConnectionChanged += (s, c) => UpdateDeviceStatus(lblPlcStatus, c);
            for (int i = 0; i < _cameras.Count; i++)
            {
                int idx = i; // 闭包锁定下标，避免循环变量被所有事件共享
                _cameras[i].ConnectionChanged += (s, c) => UpdateDeviceStatus(_lblCamStatuses[idx], c);
            }

            _monitor?.Start();
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
            if (lblSerial != null) lblSerial.Text = code;
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
        /// 一次检测完成：在界面线程刷新对应窗口图片+OK/NG徽标，并更新统计。
        /// 事件可能从工作线程抛出，统一 Invoke 回界面线程。
        /// </summary>
        private void OnInspectionFinished(WindowData data, int windowIndex)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<WindowData, int>(OnInspectionFinished), data, windowIndex);
                return;
            }

            _total++;
            if (data.IsOk) _ok++; else _ng++;
            RefreshTitle();

            // 刷新目标显示窗口（1..N 环形）
            if (windowIndex >= 1 && windowIndex <= _windows.Length)
            {
                var w = _windows[windowIndex - 1];
                var img = !string.IsNullOrEmpty(data.ImagePath) && File.Exists(data.ImagePath)
                    ? ProductionCoordinator.LoadImageSafe(data.ImagePath)
                    : null;
                w.SetImage(img);
                w.SetOkNgStatus(data.IsOk);
            }
        }

        /// <summary>
        /// 流程状态文本刷新（工作线程抛出，需回 UI 线程）。
        /// 每次流程状态更新都恢复默认深蓝灰色文字——配方切换成功的"绿色提示"只在切换成功
        /// 的瞬间显示，随后流程推进（如"等待PLC到位"）会恢复常规颜色。
        /// </summary>
        private void OnStateChanged(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(OnStateChanged), text); return; }
            lblStatus.ForeColor = Color.FromArgb(52, 73, 94); // 恢复默认深蓝灰
            lblStatus.Text = "状态: " + text;
        }

        /// <summary>刷新标题栏统计与产品型号（配方由下拉框自持显示，无需在这里刷）。</summary>
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

            // ③ 用新配置重建服务层（BuildServices 内部全部读取 _config 的最新值）
            BuildServices();
            _coordinator.LatestSerialNumber = serial;

            // ④ 重建界面与重新订阅：标题栏（相机灯/色块）→ 窗口矩阵 → 运行时事件 → 启动流程
            InitTitleBarFields();
            BuildWindowGrid();
            SubscribeRuntimeEvents();
            _coordinator.Start();
            RelayoutTitleBar();
            RefreshTitle();

            LogHelper.Info("配置已保存并热生效（服务层已按新配置重建）");
        }

        /// <summary>
        /// 打开系统设置：保存后写盘并热生效（V1.6.0 起免重启）。
        /// 【V1.9.0 管理员登录】每次点击先校验管理员账号（SecurityConfig.AdminEnabled=true 时，
        /// 弹 LoginForm 登录，只有验证通过才放行打开设置窗体），
        /// 防止现场操作员随意改 IP/寄存器/存图/点位等关键配置。
        /// </summary>
        private void OpenSettings()
        {
            // 管理员登录校验（V1.9.0）：启用时每次点都要求登录，无"记住登录状态"。
            // 传整个 _config：LoginForm 里不仅能登录，还能修改管理员密码（改后直接写盘）。
            if (_config.Security.AdminEnabled)
            {
                using (var login = new LoginForm(_config))
                {
                    if (login.ShowDialog(this) != DialogResult.OK)
                        return; // 取消/连续失败：不进系统设置
                }
            }

            using (var dlg = new SettingsForm(_config))
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