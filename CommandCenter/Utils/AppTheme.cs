using System;
using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Utils
{
    /// <summary>
    /// 界面深色/浅色主题管理器（V2.16.2 新增）。
    ///
    /// 【定位】与 <see cref="I18n"/> 对等的全局外观开关：内存里只存当前是深还是浅，
    /// 切换即触发 <see cref="ThemeChanged"/>，常驻窗体（MainForm）订阅后全量重刷；
    /// 模态对话框（ShowDialog）在打开瞬间调一次 <see cref="ApplyTo"/> 按当前主题上色即可
    /// （模态期间切主题入口在主界面、点不到，所以不需要订阅事件，和 I18n 同理）。
    ///
    /// 【持久化】当前主题同时存进 AppConfig.Theme（"Light"/"Dark"，默认浅色），切换即写盘，
    /// 重启后保持；json 被手改成非法值时 <see cref="Normalize"/> 回落浅色，绝不崩。
    ///
    /// 【语义色保留红线】OK=绿、NG=红、PLC黄/绿/红、主按钮蓝底白字、横幅白字都是"业务语义"，
    /// 换主题绝不能把它们洗成普通文字色——ApplyOne 里凡是命中语义色的一律跳过（只换底板/普通文字）。
    /// 新增语义色时把它的 RGB 加进 IsSemanticColor，否则切主题会误改状态色。
    ///
    /// 【为什么不用系统主题/第三方换肤库】项目离线编译（libs 直引、不走 NuGet），
    /// WinForms 原生对深色支持很弱（Button/ComboBox 下拉仍系统绘制），自写递归上色最直接可靠；
    /// 配色常量收敛在这里一处，改深色 shades 只动本文件。
    /// </summary>
    public static class AppTheme
    {
        /// <summary>浅色主题标识（存盘值，大小写不敏感）。</summary>
        public const string Light = "Light";

        /// <summary>深色主题标识（存盘值，大小写不敏感）。</summary>
        public const string Dark = "Dark";

        private static string _theme = Light;

        /// <summary>
        /// 当前主题（"Light" 浅色 / "Dark" 深色），赋值即切换并触发 ThemeChanged。
        /// 非法值一律回落浅色（配置被手改脏也不崩，旧配置缺字段默认浅色、与历史外观一致）。
        /// 只应在 UI 线程赋值（主界面主题按钮），订阅者同步收到通知。
        /// </summary>
        public static string Theme
        {
            get { return _theme; }
            set
            {
                string normalized = Normalize(value);
                if (normalized == _theme) return;
                _theme = normalized;
                EventHandler handler = ThemeChanged;
                if (handler != null)
                {
                    try { handler(null, EventArgs.Empty); }
                    catch { /* 单个订阅者异常不阻断其他订阅者（与 I18n 同策略） */ }
                }
            }
        }

        /// <summary>当前是否为深色（浅色返回 false）。界面代码按它选分支文本/配色。</summary>
        public static bool IsDark => string.Equals(_theme, Dark, StringComparison.OrdinalIgnoreCase);

        /// <summary>主题切换事件：MainForm 等常驻窗体订阅后全量重刷界面配色。</summary>
        public static event EventHandler ThemeChanged;

        /// <summary>
        /// 把任意输入归一成规范主题值："Dark"（大小写不敏感）→ 深色，其余一切（含 null/空/非法）→ 浅色。
        /// ConfigStore.ApplyDefaults 与本属性 setter 统一走这里，json 手改脏值不会让界面崩。
        /// </summary>
        public static string Normalize(string theme)
        {
            if (string.Equals(theme, Dark, StringComparison.OrdinalIgnoreCase)) return Dark;
            return Light;
        }

        // ────────────── 配色常量 ──────────────
        // 浅色 = 历史外观像素级保留（MainForm.Designer 里的 240,245,250 系），老用户无感；
        // 深色 = VS 深色系（37,37,38 底 + 220 文字），保证文字可读、语义色仍醒目。

        /// <summary>窗体/矩阵主背景（浅色=历史淡蓝 240,245,250；深色=37,37,38）。</summary>
        public static Color Background => IsDark ? Color.FromArgb(37, 37, 38) : Color.FromArgb(240, 245, 250);

        /// <summary>标题栏/状态栏/对话框横幅容器底（浅色=历史 235,242,250；深色=45,45,48）。</summary>
        public static Color TitleBar => IsDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(235, 242, 250);

        /// <summary>普通面板/内容面板底（浅色=白；深色=51,51,55）。对话框白面板、设置页内容区走这里。</summary>
        public static Color Surface => IsDark ? Color.FromArgb(51, 51, 55) : Color.White;

        /// <summary>普通文字主色（浅色=历史深蓝灰 52,73,94；深色=220,220,220）。Label/CheckBox 文案走这里。</summary>
        public static Color TextPrimary => IsDark ? Color.FromArgb(220, 220, 220) : Color.FromArgb(52, 73, 94);

        /// <summary>次要文字色（浅色=100,100,100 灰；深色=170,170,170）。提示/小字走这里。</summary>
        public static Color TextSecondary => IsDark ? Color.FromArgb(170, 170, 170) : Color.FromArgb(100, 100, 100);

        /// <summary>输入框底（TextBox/ComboBox/NumericUpDown，浅色=白；深色=62,62,68）。</summary>
        public static Color InputBackground => IsDark ? Color.FromArgb(62, 62, 68) : Color.White;

        /// <summary>输入框文字色（与 TextPrimary 同值，单独命名方便以后分开调）。</summary>
        public static Color InputText => TextPrimary;

        /// <summary>次按钮底（非主按钮，浅色=历史 236,240,245；深色=62,62,68）。主按钮蓝底两主题共用、不走这里。</summary>
        public static Color SecondaryButtonBackground => IsDark ? Color.FromArgb(62, 62, 68) : Color.FromArgb(236, 240, 245);

        /// <summary>主按钮蓝（品牌色，两主题共用、绝不随主题变）：52,152,219，白字。</summary>
        public static Color PrimaryBlue => Color.FromArgb(52, 152, 219);

        /// <summary>显示窗口空态卡底（CameraDisplayControl，浅色=历史 220,231,243；深色=62,62,66）。</summary>
        public static Color CardBackground => IsDark ? Color.FromArgb(62, 62, 66) : Color.FromArgb(220, 231, 243);

        /// <summary>表格网格线色（浅色=200,210,225；深色=68,68,72）。</summary>
        public static Color Border => IsDark ? Color.FromArgb(68, 68, 72) : Color.FromArgb(200, 210, 225);

        /// <summary>
        /// 是否语义色（命中即保留、不随主题改）：
        /// OK绿 46,158,107 / NG红 229,72,77 / PLC黄 240,173,78 / 主按钮蓝 52,152,219 /
        /// 代码里直接用的 Green/Red/Gray（DeveloperModeForm 状态灯）/ White（蓝底白字）。
        /// 白字保留是关键——横幅/主按钮的白字若被改成深色字会直接看不见。
        /// </summary>
        public static bool IsSemanticColor(Color c)
        {
            if (c.ToArgb() == Color.FromArgb(46, 158, 107).ToArgb()) return true;   // OK绿
            if (c.ToArgb() == Color.FromArgb(229, 72, 77).ToArgb()) return true;    // NG红
            if (c.ToArgb() == Color.FromArgb(240, 173, 78).ToArgb()) return true;   // PLC黄
            if (c.ToArgb() == Color.FromArgb(52, 152, 219).ToArgb()) return true;   // 主按钮蓝
            if (c.ToArgb() == Color.Green.ToArgb()) return true;
            if (c.ToArgb() == Color.Red.ToArgb()) return true;
            if (c.ToArgb() == Color.Gray.ToArgb()) return true;
            if (c.ToArgb() == Color.White.ToArgb()) return true;                    // 蓝底白字
            return false;
        }

        /// <summary>
        /// 对一棵控件树全量上色（递归，父先子后）。
        /// 空/已释放控件直接返回；在 UI 线程调用（构造末尾、主题切换事件、热更重建后）。
        /// 图片区（PictureBox）与 OK/NG 自绘徽标（OkNgBadge 在 CameraDisplayControl 内部、不挂树上）跳过。
        /// </summary>
        public static void ApplyTo(Control root)
        {
            if (root == null || root.IsDisposed) return;
            try { ApplyOne(root); }
            catch { /* 单个控件上色失败不阻断整树（极端自定义控件容错） */ }
            Control[] children;
            try { children = new Control[root.Controls.Count]; root.Controls.CopyTo(children, 0); }
            catch { return; }
            foreach (var child in children)
            {
                if (child == null) continue;
                ApplyTo(child);
            }
        }

        /// <summary>
        /// 单个控件上色（ApplyTo 的每节点逻辑，抽出方便单测/复用）。
        /// 原则：只动"底板/普通文字/输入框"，语义色（状态/品牌）一律保留。
        /// </summary>
        public static void ApplyOne(Control c)
        {
            if (c == null || c.IsDisposed) return;

            // 图片区不碰（检测画面本身，主题只换框不换图）。
            if (c is PictureBox) return;
            // OkNgBadge 是自绘语义徽标（在 CameraDisplayControl 内部），全名匹配避免引用 Controls 程序集。
            if (c.GetType().FullName == "CommandCenter.Controls.OkNgBadge") return;

            if (c is Form)
            {
                c.BackColor = Background;
                c.ForeColor = TextPrimary;
                return;
            }

            if (c is DataGridView)
            {
                ApplyGridTheme((DataGridView)c);
                return;
            }

            if (c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel
                || c is SplitContainer || c is TabControl || c is TabPage || c is GroupBox)
            {
                // 品牌蓝横幅（LoginForm/SerialInputForm/ScannerFailForm/ModelIndexEditForm 的
                // pnlHeader，蓝底白字）是品牌语义、两主题共用，绝不能洗成灰——先判语义保留。
                if (c.BackColor.ToArgb() == PrimaryBlue.ToArgb())
                {
                    c.ForeColor = Color.White;
                    return;
                }
                // 标题栏/状态栏（Dock=Top/Bottom 且直接挂在 Form 下）用 TitleBar 色，
                // 其余内容面板用 Surface 色——MainForm 标题栏/状态栏自动区分，无需按名字特判。
                bool isBar = (c.Dock == DockStyle.Top || c.Dock == DockStyle.Bottom)
                    && c.Parent is Form;
                c.BackColor = isBar ? TitleBar : Surface;
                c.ForeColor = TextPrimary;
                return;
            }

            if (c is Button)
            {
                var btn = (Button)c;
                // 主按钮（蓝底）两主题共用品牌蓝，只保证白字；次按钮才跟随主题换底/字。
                if (btn.BackColor.ToArgb() == PrimaryBlue.ToArgb())
                {
                    btn.ForeColor = Color.White;
                }
                else
                {
                    btn.BackColor = SecondaryButtonBackground;
                    if (!IsSemanticColor(btn.ForeColor))
                        btn.ForeColor = TextPrimary;
                }
                return;
            }

            if (c is TextBox || c is MaskedTextBox || c is RichTextBox
                || c is ComboBox || c is NumericUpDown || c is ListBox || c is CheckedListBox
                || c is TreeView)
            {
                c.BackColor = InputBackground;
                c.ForeColor = InputText;
                return;
            }

            if (c is CheckBox || c is RadioButton)
            {
                // 勾选框/单选背景透明跟随父容器，只换文字色（语义色如红字提示则保留）。
                if (!IsSemanticColor(c.ForeColor))
                    c.ForeColor = TextPrimary;
                return;
            }

            if (c is Label)
            {
                var lbl = (Label)c;
                // 带边框的 Label 是当输入框用的（MainForm lblSerial 只读框）：按输入框配色。
                if (lbl.BorderStyle != BorderStyle.None)
                {
                    lbl.BackColor = InputBackground;
                    lbl.ForeColor = InputText;
                    return;
                }
                // 实心语义色块（标题栏 OK/NG 计数徽标：绿/红底+白字）整体保留。
                if (IsSemanticColor(lbl.BackColor) && lbl.BackColor.ToArgb() != Color.Transparent.ToArgb())
                    return;
                // 普通标签只换文字色（语义状态色保留），背景保持透明跟随父容器。
                if (!IsSemanticColor(lbl.ForeColor))
                    lbl.ForeColor = TextPrimary;
                return;
            }

            // 未知/自定义控件保守处理：只换非语义前景色，背景不动（防误伤自绘控件）。
            try
            {
                if (!IsSemanticColor(c.ForeColor))
                    c.ForeColor = TextPrimary;
            }
            catch { }
        }

        /// <summary>
        /// DataGridView 全套深色适配（设置页/点位窗/账号表格共用）。
        /// 选中行保持系统高亮蓝（V2.15.22 CellPainting 强制蓝与它一致，深色下仍可读，故不改选中色）。
        /// </summary>
        public static void ApplyGridTheme(DataGridView grid)
        {
            if (grid == null || grid.IsDisposed) return;
            grid.BackgroundColor = Surface;
            grid.GridColor = Border;
            grid.BorderStyle = BorderStyle.FixedSingle;
            try { grid.EnableHeadersVisualStyles = false; }
            catch { }
            try
            {
                var header = grid.ColumnHeadersDefaultCellStyle;
                header.BackColor = TitleBar;
                header.ForeColor = TextPrimary;
                header.SelectionBackColor = TitleBar;
                header.SelectionForeColor = TextPrimary;
                var rowHeader = grid.RowHeadersDefaultCellStyle;
                rowHeader.BackColor = TitleBar;
                rowHeader.ForeColor = TextPrimary;
                var cell = grid.DefaultCellStyle;
                cell.BackColor = InputBackground;
                cell.ForeColor = InputText;
            }
            catch { /* 样式句柄极端情况容错，不阻断 */ }
        }
    }
}
