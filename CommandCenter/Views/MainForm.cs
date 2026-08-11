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
    /// │                                                        ●PLC ●相机2 ●相机1 │
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
            if (cams.Count == 0) cams.Add(new CameraConfig()); // 空配置兜底一台，保证流程能跑
            foreach (var c in cams)
                _cameras.Add(new KeyenceIV4Camera(c));

            _imageStore = new ImageStore(_config.Image);
            _coordinator = new ProductionCoordinator(_plc, _cameras, cams, _imageStore, _config.Display,
                _config.Display.WindowStationMap);

            // 连接健康监控：后台心跳 + 断连自动重连 + 边沿日志（不影响任何 UI 刷新）
            _monitor = new ConnectionMonitor(_plc, _cameras);
        }

        /// <summary>
        /// 标题栏"运行时"初始化：把设计器里做好的静态控件按配置补全成最终形态。
        /// 设计器负责"控件长什么样"，此处负责"数据与动态部分"：
        ///   ① 产品型号前缀文案（ProductModelPrefix）与各信息字段的可见性（ShowXxx 开关）；
        ///   ② 配方下拉框填充（cmbRecipe 显示当前配方、可点切换，见 InitRecipeCombo）；
        ///   ③ 每台相机一个连接指示灯（_lblCamStatuses，按相机下标对齐）——相机台数运行时才知道，
        ///      所以这类"动态控件"不进设计器，在这里循环生成，Dock.Right 排在 PLC 灯右侧；
        ///   ④ 最后按"哪些字段可见"做一次紧凑重排（RelayoutTitleBar），隐藏字段不占位。
        /// </summary>
        private void InitTitleBarRuntime()
        {
            // ① 产品型号 = 配方（V1.1.2 现场业务对应）：前缀文案走配置，开关控制整段显示
            lblProductPrefix.Text = _config.Display.ProductModelPrefix + ":";
            lblProductPrefix.Visible = _config.Display.ShowProductModel;
            // 序列号：标题"序列号:"在显示框外（lblSerialTitle），框内只放值；
            // 有值显示值，没有则框内留空（不写"待扫码"），标题+框整体由开关控制显隐
            lblSerialTitle.Text = "序列号:";
            lblSerialTitle.Visible = _config.Display.ShowSerialNumber;
            lblSerial.Text = _coordinator.LatestSerialNumber;
            lblSerial.Visible = _config.Display.ShowSerialNumber;
            lblTotal.Visible = _config.Display.ShowTotalCount;
            lblOk.Visible = _config.Display.ShowOkCount;
            lblNg.Visible = _config.Display.ShowNgCount;

            // 标题栏 OK/NG 计数高亮（V1.5.0 现场反馈"彩色数字不够醒目"）：
            // 默认把 OK/NG 做成"实心彩色色块 + 白字"（绿底=OK、红底=NG，配色走 DisplayConfig），
            // 关闭 TitleOkNgHighlight 配置时回退为普通彩色文字。
            if (_config.Display.TitleOkNgHighlight)
            {
                StyleCountBadge(lblOk, _config.Display.OkColor);
                StyleCountBadge(lblNg, _config.Display.NgColor);
            }

            // ② 配方下拉框：填充配方项并选中当前（期间屏蔽事件，防初始化误触发切换）
            InitRecipeCombo();

            // 设置按钮事件（设计器只做外观，交互在这里挂线）
            btnSettings.Click += (s, e) => OpenSettings();

            // ③ 动态相机连接指示灯：先 Add 的 Dock.Right 靠左，后 Add 的靠右。
            //    设计器里已 Add 了 PLC 灯（最左侧），这里从"台数-1"倒着 Add，
            //    得到 相机N..相机1 顺序排在 PLC 灯右侧，与历史实测布局一致。
            //    先隐藏设计器里的占位灯（lblCamPlaceholder）：它只是设计器视觉提示，
            //    不参与实际布局，隐藏后 Dock 空间让给下面循环生成的真灯。
            lblCamPlaceholder.Visible = false;
            _lblCamStatuses = new Label[_cameras.Count];
            for (int i = _cameras.Count - 1; i >= 0; i--)
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

            // ④ 紧凑重排延迟到 OnShown 执行：窗体首次显示时的 AutoScale 会自动缩放/还原
            //    设计器基准坐标，会覆盖构造阶段的赋值；OnShown 时缩放已完成，此时重排才真正生效
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
        /// </summary>
        private void RelayoutTitleBar()
        {
            const int barHeight = 48; // 标题栏固定高度（见 Designer 的 pnlTitleBar.Size）

            // 排布顺序固定：产品前缀 → 配方下拉框 → 序列号标题 → 序列号框 → | → 总数 → OK → NG → | → 系统设置按钮
            Control[] seq = { lblProductPrefix, cmbRecipe, lblSerialTitle, lblSerial, lblSep1,
                              lblTotal, lblOk, lblNg, lblSep2, btnSettings };
            int x = 12; // 与设计器 Padding(12,0,12,0) 左内边距保持一致
            foreach (var c in seq)
            {
                if (!c.Visible) continue;
                int y = (barHeight - c.Height) / 2; // 垂直居中（各控件高度不同：按钮30/下拉27/标签19/显示框24）
                if (c is Button)    { c.Location = new Point(x, y); x += c.Width + 12; }
                else if (c is ComboBox) { c.Location = new Point(x, y); x += c.Width + 12; }
                else if (c == lblSerial) { c.Location = new Point(x, y); x += c.Width + 18; } // 固定宽度显示框
                else                { c.Location = new Point(x, y); x += ((Label)c).PreferredWidth + 18; }
            }
        }

        /// <summary>
        /// 把标题栏计数标签做成"实心彩色色块 + 白色加粗字"（现场要求 OK/NG 高亮醒目）。
        /// BackColor 用配置色（绿=OK、红=NG），ForeColor 白色，字号 11F→12F，
        /// 四周留 padding 让色块饱满；AutoSize 保持 true，色块宽度随数字自动伸缩，
        /// RelayoutTitleBar 的 PreferredWidth 布局照常工作、垂直居中公式不变。
        /// </summary>
        /// <param name="lbl">要样式化的标题栏计数标签（lblOk / lblNg）</param>
        /// <param name="color">色块底色（DisplayConfig.OkColor / NgColor）</param>
        private void StyleCountBadge(Label lbl, Color color)
        {
            lbl.BackColor = color;
            lbl.ForeColor = Color.White;
            lbl.Font = new Font("微软雅黑", 12F, FontStyle.Bold);
            lbl.Padding = new Padding(6, 2, 6, 2);
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
            _windows = new CameraDisplayControl[rows * cols];

            // 重置容器：清掉设计器默认的 1×1 行列与可能残留的子控件
            var grid = gridCameraWindows;
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

        /// <summary>订阅业务事件：检测完成 / 状态变化 / 异常提醒 / 设备连接状态。</summary>
        private void SubscribeEvents()
        {
            _coordinator.InspectionFinished += OnInspectionFinished;
            _coordinator.StateChanged += OnStateChanged;
            _coordinator.ErrorRaised += msg => LogHelper.Warn("界面收到错误：" + msg);

            // 连接状态指示灯：PLC 与每台相机 断连时 UI 实时变红，重连成功回绿
            _plc.ConnectionChanged += (s, c) => UpdateDeviceStatus(lblPlcStatus, c);
            for (int i = 0; i < _cameras.Count; i++)
            {
                int idx = i; // 闭包锁定下标，避免循环变量被所有事件共享
                _cameras[i].ConnectionChanged += (s, c) => UpdateDeviceStatus(_lblCamStatuses[idx], c);
            }

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
                foreach (var cam in _cameras)
                {
                    try { cam?.Dispose(); }
                    catch (Exception ex) { LogHelper.Warn("关闭：相机释放异常 " + ex.Message); }
                }
                LogHelper.Info("程序关闭，服务已释放");
            };

            _monitor?.Start();
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

        /// <summary>打开系统设置：保存后提示重启生效。</summary>
        private void OpenSettings()
        {
            using (var dlg = new SettingsForm(_config))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                ConfigStore.Save(_config);
                MessageBox.Show("配置已保存，重启程序后生效。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}