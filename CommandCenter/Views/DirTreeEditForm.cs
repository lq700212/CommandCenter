using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Services;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 图片存储目录结构可视化配置对话框：把"存图目录怎么建、文件怎么命名"做成逐级编辑的界面。
    ///
    /// ┌───────────────────────────────────────────────────────────────┐
    /// │ 图片存储目录结构配置                                           │
    /// │ 根目录: [txtSaveRootDir           ] [btnBrowse 浏览]           │
    /// │ ── 目录层级（从上到下逐级建目录）───────────────────────────    │
    /// │ ┌──────────────────────────────────────────────────────────┐  │
    /// │ │ lstLevels（ListBox）                                     │  │
    /// │ │   1. {年月日}   ← 每个层级可以是固定名字或占位符规则       │  │
    /// │ │   2. {SN}                                                │  │
    /// │ │   3. {OKNG}                                              │  │
    /// │ └──────────────────────────────────────────────────────────┘  │
    /// │ 当前层级名字/规则: [txtLevelName                   ]          │
    /// │ 插入占位符: [cmbPlaceholder ▼] [btnInsertPh 插入]             │
    /// │ [btnAddLevel 添加] [btnInsertLevel 插入上方] [btnInsertBelow 插入下方] │
    /// │ [btnDeleteLevel 删除] [btnUp ↑] [btnDown ↓]                   │
    /// │ ── 文件名规则 ────────────────────────────────────────────    │
    /// │ 文件名: [txtFileNameTpl                          ]            │
    /// │        （点位号默认进文件名，如 {点位} → 1.png）               │
    /// │ 存图保留天数: [nudKeepDays]      [chkTimestampSuffix 时间戳后缀]│
    /// │ ── 预览 ────────────────────────────────────────────────     │
    /// │ lblPreview（实时显示 OK/NG 两条完整路径）                      │
    /// │                                           [btnOk] [btnCancel] │
    /// └───────────────────────────────────────────────────────────────┘
    ///
    /// 【设计说明】
    ///   - 目录层级是【线性列表】，从上到下逐级创建目录，现场要求即
    ///     "根目录 / 年月日 / SN号 / OK|NG"（OK/NG 靠 {OKNG} 占位符自动分支）；
    ///     点位号默认进文件名（{点位}），不单独建目录。
    ///   - 每级既能写固定名字（如 "OK"），也能写生成规则（含占位符，如 {年月日}）。
    ///   - 改动直接写回传入的 ImageConfig（同一实例），确定后由设置窗体统一保存。
    ///   - 占位符插入目标：默认/选中层级时插入到"当前层级名字"框；用户点过"文件名规则"框后才插文件名。
    ///   - 占位符/按钮说明不占界面（原常驻标签 lblNote 已删）：悬停输入框/按钮/标题显示 ToolTip 气泡，
    ///     悬停 0.5 秒出现、停留 8 秒消失（Windows 标准参数，见 DirTreeEditForm.Designer.cs 的 tip）。
    /// </summary>
    public partial class DirTreeEditForm : Form
    {
        private readonly ImageConfig _cfg;

    /// <summary>当前正在编辑的文本框（目录层级名 or 文件名），用于占位符插入定位。</summary>
    private TextBox _activeEdit;

    /// <summary>防抖定时器：文本输入变化时延迟刷新预览树，避免每次击键都重建 UI 造成卡顿。</summary>
    private readonly Timer _previewDebounce;

    public DirTreeEditForm(ImageConfig cfg)
    {
        _cfg = cfg;
        InitializeComponent();          // 先解析设计器里的控件

        // 防抖：文本类变化统一走定时器，300ms 内不再变化才真正刷新预览树
        _previewDebounce = new Timer { Interval = 300 };
        _previewDebounce.Tick += (s, e) =>
        {
            _previewDebounce.Stop();
            RefreshPreview();
        };

        LoadFromConfig();               // 把当前配置值填进各控件
        WireEvents();                   // 挂交互事件

        // 默认选中第一个层级：用户没点任何层级时，占位符也直接插入到第一层，而不是空着无从下手
        if (lstLevels.Items.Count > 0)
            lstLevels.SelectedIndex = 0;   // 会触发 SelectedIndexChanged → txtLevelName 同步 + 插入目标锁定层级名

        RefreshPreview();               // 初始预览
        ApplyLanguage();                // V2.15.0 国际化：按当前语言初始化文本
    }

        /// <summary>
        /// 占位符"显示层本地化"（V2.15.12）：统一走 PlaceholderLocalizer（Utils/PlaceholderLocalizer.cs）。
        /// 核心契约：配置存储与 ImageStore.RenderTemplate 渲染【始终用中文占位符】（{年月日}/{点位}…），
        /// 英文界面只是"给人看"时翻译成 {Date}/{Station}…，绝不把英文占位符写进配置文件——
        /// 否则 RenderTemplate 不识别、归档路径会变成字面 "{Date}" 目录（脏配置）。SettingsForm 文件名框同用此类。
        /// </summary>
        private void LoadFromConfig()
        {
            txtSaveRootDir.Text = _cfg.SaveRootDir;

            // 层级列表：直接用 SubDirs（模型默认已带三层，首次打开即所见即所得）；
            // 英文界面下显示时把中文占位符翻成英文（保存时还原，见 OnOk）
            var levels = _cfg.SubDirs ?? new List<string>();
            foreach (var lvl in levels)
                lstLevels.Items.Add(PlaceholderLocalizer.ToDisplay(lvl));

            txtFileNameTpl.Text = PlaceholderLocalizer.ToDisplay(_cfg.FileNameTemplate);

            // V2.14.12：时间戳后缀开关与存图保留天数（KeepDays，0 = 不自动清理）
            chkTimestampSuffix.Checked = _cfg.FileTimestampSuffix;
            nudKeepDays.Value = _cfg.KeepDays;

                        // 时间戳后缀勾选框是 AutoSize（宽度由字体渲染决定），右缘要与文件名模板框右缘对齐——
            // AutoSize 宽度只有运行时才知道，故在载入时按 txtFileNameTpl 右缘反向校正 Left。
            // （仅中文界面；英文界面的勾选框位置由 ApplyLayoutForLanguage 另行设置，见 ApplyLanguage）
            chkTimestampSuffix.Left = txtFileNameTpl.Right - chkTimestampSuffix.Width;
        }

        /// <summary>挂事件：选择层级、编辑名字、占位符插入、增删移、预览刷新。</summary>
        private void WireEvents()
        {
            // 默认插入目标 = 当前层级名字框（用户主要就在编辑这个，不必先点它）
            _activeEdit = txtLevelName;

            // 选中层级 → 名字编辑框同步显示该层内容，同时把占位符插入目标锁定到层级名
            lstLevels.SelectedIndexChanged += (s, e) =>
            {
                _activeEdit = txtLevelName;
                if (lstLevels.SelectedIndex >= 0)
                    txtLevelName.Text = lstLevels.SelectedItem.ToString();
            };

            // 只有用户主动点了文件名框，插入目标才切到文件名规则
            txtLevelName.Enter += (s, e) => _activeEdit = txtLevelName;
            txtFileNameTpl.Enter += (s, e) => _activeEdit = txtFileNameTpl;

            // 插入占位符：把下拉选中的占位符插到当前编辑框光标位置
            // （当前编辑框 = 最后交互的输入框；默认/选中层级时都是"层级名"，点过文件名框则是"文件名"）
            btnInsertPh.Click += (s, e) =>
            {
                if (_activeEdit == null) _activeEdit = txtLevelName;   // 兜底：绝不会让插入没去处
                string ph = cmbPlaceholder.SelectedItem?.ToString();
                if (string.IsNullOrWhiteSpace(ph)) { MessageBox.Show(I18n.T("请先在下拉框选择要插入的占位符。", "Select a placeholder from the drop-down first."), I18n.T("提示", "Notice")); return; }
                int pos = _activeEdit.SelectionStart;
                _activeEdit.Text = _activeEdit.Text.Insert(pos, ph);
                _activeEdit.SelectionStart = pos + ph.Length;   // 光标移到插入内容之后
                _activeEdit.Focus();
                if (_activeEdit == txtLevelName) UpdateSelectedLevel();
                SchedulePreview();
            };

            // 名字编辑框内容变化 → 实时同步回当前选中的层级项，并防抖刷新预览
            txtLevelName.TextChanged += (s, e) => { UpdateSelectedLevel(); SchedulePreview(); };
            txtFileNameTpl.TextChanged += (s, e) => SchedulePreview();
            txtSaveRootDir.TextChanged += (s, e) => SchedulePreview();

            // V2.14.12：时间戳后缀勾选状态变化 → 刷新预览（文件名是否带时间戳可见即所得）；
            // 保留天数不参与预览（那是后台清理用的），无需刷新。
            chkTimestampSuffix.CheckedChanged += (s, e) => SchedulePreview();

            btnBrowse.Click += (s, e) => PickRootDir();
            btnAddLevel.Click += (s, e) => AddLevel("");
            btnInsertLevel.Click += (s, e) => AddLevelInsert(true);
            btnInsertBelow.Click += (s, e) => AddLevelInsert(false);
            btnDeleteLevel.Click += (s, e) => DeleteLevel();
            btnUp.Click += (s, e) => MoveLevel(-1);
            btnDown.Click += (s, e) => MoveLevel(1);
            lstLevels.DoubleClick += (s, e) => txtLevelName.Focus();  // 双击进入编辑

            btnOk.Click += (s, e) => OnOk();
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
        }

        /// <summary>把当前编辑框内容写回 ListBox 当前选中项（作为该级目录的名字/规则）。</summary>
        private void UpdateSelectedLevel()
        {
            int idx = lstLevels.SelectedIndex;
            if (idx < 0) return;
            lstLevels.Items[idx] = txtLevelName.Text;
        }

        /// <summary>
        /// 防抖调度：文本输入每变化一次就重置定时器，连续输入只在停下 300ms 后刷一次预览树。
        /// 这样打字时不会每键都重建 TreeView，界面保持流畅。
        /// </summary>
        private void SchedulePreview()
        {
            _previewDebounce.Stop();
            _previewDebounce.Start();
        }

        /// <summary>追加一级目录（默认给 {SN}，现场按需改）。若列表空则给 {年月日} 起头。</summary>
        private void AddLevel(string defaultValue)
        {
            // 默认值先经 ToDisplay：英文界面下 {年月日} 以 {Date} 形式入列表，与人眼看到的一致
            string dflt = string.IsNullOrEmpty(defaultValue)
                ? PlaceholderLocalizer.ToDisplay(lstLevels.Items.Count == 0 ? "{年月日}" : "{SN}")
                : defaultValue;
            int idx = lstLevels.Items.Add(dflt);
            lstLevels.SelectedIndex = idx;
            txtLevelName.Text = dflt;
        }

        /// <summary>
        /// 在选中层级的上方或下方插入一级（默认给 {SN}，现场按需改）。
        /// 现场常需要把"年月日"放在最上面、或把"OK/NG"插在某层后面，插入位置比"先删再加"更直观。
        /// </summary>
        /// <param name="above">true=插入到选中层级上方；false=插入到选中层级下方。</param>
        private void AddLevelInsert(bool above)
        {
            // 没选中任何层级时：插入上方就放最顶部，插入下方就追到末尾，避免无去处
            int idx = lstLevels.SelectedIndex;
            int insertAt = idx < 0
                ? (above ? 0 : lstLevels.Items.Count)
                : (above ? idx : idx + 1);
            lstLevels.Items.Insert(insertAt, PlaceholderLocalizer.ToDisplay("{SN}"));
            lstLevels.SelectedIndex = insertAt;
            txtLevelName.Text = PlaceholderLocalizer.ToDisplay("{SN}");
        }

        /// <summary>删除选中的层级；删空则补一级默认（避免保存出空结构）。</summary>
        private void DeleteLevel()
        {
            int idx = lstLevels.SelectedIndex;
            if (idx < 0) { MessageBox.Show(I18n.T("请先选中要删除的层级。", "Select a level to delete first."), I18n.T("提示", "Notice")); return; }
            lstLevels.Items.RemoveAt(idx);
            if (lstLevels.Items.Count == 0) AddLevel("");
            else lstLevels.SelectedIndex = Math.Min(idx, lstLevels.Items.Count - 1);
            RefreshPreview();
        }

        /// <summary>上移/下移选中的层级，调整目录顺序（顺序即建目录顺序）。</summary>
        private void MoveLevel(int delta)
        {
            int idx = lstLevels.SelectedIndex;
            int target = idx + delta;
            if (idx < 0 || target < 0 || target >= lstLevels.Items.Count) return;
            object tmp = lstLevels.Items[idx];
            lstLevels.Items[idx] = lstLevels.Items[target];
            lstLevels.Items[target] = tmp;
            lstLevels.SelectedIndex = target;
        }

        /// <summary>选择保存根目录的文件夹。</summary>
        private void PickRootDir()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (Directory.Exists(txtSaveRootDir.Text)) fbd.SelectedPath = txtSaveRootDir.Text;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtSaveRootDir.Text = fbd.SelectedPath;
                    RefreshPreview();
                }
            }
        }

        /// <summary>
        /// 实时预览：把当前层级规则用【示例数据】（今天日期、SN-0001、点位1）渲染成目录树，
        /// 显示在 TreeView 里——每一级是一个文件夹节点，末尾是图片文件名，
        /// 让现场一眼看到将来实际落盘的目录结构长什么样。
        /// {OKNG} 会展开成 OK、NG 两个并列分支，各自挂完整的子树。
        /// </summary>
        private void RefreshPreview()
        {
            // 预览渲染走 ImageStore.RenderTemplate，它【只认中文占位符】；
            // 而界面列表/文件名框显示的是英文占位符（英文界面），必须先还原成中文再渲染，否则预览会
            // 把 {Date} 当字面目录名、树里出现 "{Date}" 而非实际日期目录。
            var levels = lstLevels.Items.Cast<string>().Select(PlaceholderLocalizer.ToStorage).ToList();
            string fileRule = PlaceholderLocalizer.ToStorage(txtFileNameTpl.Text.Trim());
            string root = txtSaveRootDir.Text.Trim();
            string sn = "SN-0001";          // 示例序列号
            int station = 1;                // 示例点位号
            DateTime now = DateTime.Now;

            tvPreview.BeginUpdate();        // 重建期间暂停重绘，避免闪烁/卡顿
            try
            {
                tvPreview.Nodes.Clear();

                // 根节点 = 保存根目录（展开后是文件夹）
                TreeNode rootNode = tvPreview.Nodes.Add(root);
                rootNode.Expand();

                // 递归构建：每级目录一个节点，{OKNG} 产生两个并列分支
                // 示例相机名随语言：英文界面预览树里 {相机} 渲染成 "Upper Camera"，不出现中文
                string camName = I18n.Language == "en-US" ? "Upper Camera" : "上相机";
                BuildPreviewBranch(rootNode, levels, 0, fileRule, now, sn, station, camName);

                tvPreview.ExpandAll();      // 全部展开，让结构一目了然
            }
            finally
            {
                tvPreview.EndUpdate();
            }
        }

        /// <summary>
        /// 递归为预览树构建目录层级：从 levelIndex 开始逐级生成目录节点，
        /// 到最后一级之后再挂"图片文件"叶子节点。
        /// 遇到包含 {OKNG} 的层会生成 OK/NG 两个并列目录，各自带完整子树。
        /// </summary>
        private void BuildPreviewBranch(TreeNode parent, List<string> levels, int levelIndex,
                                        string fileRule, DateTime now, string sn, int station, string camName)
        {
            if (levelIndex >= levels.Count)
            {
                // 目录层级已到底：追加图片文件叶子（按文件名规则渲染，默认 {点位}.png）
                string fname = ImageStore.RenderTemplate(fileRule, now, sn, true, station, camName);
                if (string.IsNullOrWhiteSpace(fname))
                    fname = "IMG_" + now.ToString("yyyyMMdd_HHmmss_fff") + "_1";
                // V2.14.12：勾选"时间戳后缀"时预览也追加 _时间戳，与真实归档命名一致
                // （真实归档 = 相机源文件名 + "_" + 时间戳，见 ImageStore.SaveImageFilePair 注释）
                else if (chkTimestampSuffix.Checked)
                    fname = fname + "_" + now.ToString("yyyyMMdd_HHmmss_fff");
                parent.Nodes.Add(fname + ".png");
                return;
            }

            // 渲染本层目录名：OK/NG 各渲染一次。若结果相同（本层不含 {OKNG}）则只建一个节点；
            // 不同（含 {OKNG}）则建两个并列节点，各自递归展开完整子树。
            string okName = ImageStore.RenderTemplate(levels[levelIndex], now, sn, true, station, camName);
            string ngName = ImageStore.RenderTemplate(levels[levelIndex], now, sn, false, station, camName);

            if (okName == ngName)
            {
                // 普通层：单分支继续
                TreeNode child = parent.Nodes.Add(string.IsNullOrWhiteSpace(okName) ? "(空)" : okName);
                BuildPreviewBranch(child, levels, levelIndex + 1, fileRule, now, sn, station, camName);
            }
            else
            {
                // 分支层（{OKNG}）：OK、NG 并列两个目录，各自带完整子树
                TreeNode okNode = parent.Nodes.Add(okName);
                BuildPreviewBranch(okNode, levels, levelIndex + 1, fileRule, now, sn, station, camName);
                TreeNode ngNode = parent.Nodes.Add(ngName);
                BuildPreviewBranch(ngNode, levels, levelIndex + 1, fileRule, now, sn, station, camName);
            }
        }

        /// <summary>确定：把编辑结果写回 ImageConfig（同一实例）。</summary>
        private void OnOk()
        {
            // V2.14.13 加固【禁止完整路径当一层】：层级名必须是"一层目录的名字/规则"，
            // 不能含路径分隔符（\ 或 /）。历史上有人把整条路径模板（如 E:\Images\{年月日}...）粘贴进来，
            // 保存后归档路径出现"一层套一层"的超长嵌套目录（实测 4 层 年月日\SN\相机\NG）。
            // 在保存前统一拦截：任何一个层级含分隔符就中止保存并提示，从根上杜绝脏配置再产生。
            foreach (var item in lstLevels.Items)
            {
                string s = item?.ToString() ?? "";
                if (s.IndexOf('\\') >= 0 || s.IndexOf('/') >= 0)
                {
                    MessageBox.Show(I18n.T(
                        $"目录层级「{s}」里含路径分隔符（\\ 或 /）。\r\n\r\n" +
                        "每个层级只能是【一层目录】的名字或规则（如 {年月日}、{SN}、OK、NG）。\r\n" +
                        "需要多级目录请用【添加/插入层级】分开成多行，不要一次粘贴整条路径。\r\n" +
                        "本次修改未保存，请先修正后再点确定。",
                        $"Directory level \"{s}\" contains a path separator (\\ or /).\r\n\r\n" +
                        "Each level must be a single directory name or rule (e.g. {年月日}, {SN}, OK, NG).\r\n" +
                        "For multiple levels use Add/Insert Level to create separate rows, do not paste a whole path.\r\n" +
                        "This change was not saved. Fix it first, then click OK."),
                        I18n.T("目录层级格式错误", "Invalid Directory Level"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;   // 中止保存，对话框保持打开
                }
            }

            // 收集层级：清掉空白项，避免存出空目录层级；删空则保留默认 {年月日} 兜底。
            // 存进配置前必须把英文界面显示的英文占位符还原成中文（RenderTemplate 只认中文）。
            var levels = lstLevels.Items.Cast<string>()
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => PlaceholderLocalizer.ToStorage(s.Trim()))
                .ToList();
            if (levels.Count == 0) levels.Add("{年月日}");

            _cfg.SaveRootDir = txtSaveRootDir.Text.Trim();
            _cfg.SubDirs = levels;
            _cfg.FileNameTemplate = PlaceholderLocalizer.ToStorage(txtFileNameTpl.Text.Trim());

            // V2.14.12：时间戳后缀开关 + 存图保留天数（后台定期清理用，0 = 不自动清理）
            _cfg.FileTimestampSuffix = chkTimestampSuffix.Checked;
            _cfg.KeepDays = (int)nudKeepDays.Value;

            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// V2.15.0 国际化：按当前语言刷新本窗体全部界面文字（标签/按钮/悬停气泡/标题）。
        /// 在构造函数末尾调用（模态对话框打开瞬间按当前语言初始化；模态期间语言不会变化）。
        /// 占位符下拉/层级列表/文件名框的占位符内容 V2.15.12 起【随语言显示】（英文界面翻成
        /// {Date}/{Station}…，保存时还原中文，见 PlaceholderLocalizer）；预览树渲染结果仍是真实归档目录名。
        /// </summary>
        private void ApplyLanguage()
        {
            this.Text = I18n.T("图片存储目录结构配置", "Image Directory Structure");
            lblRootDir.Text = I18n.T("根目录:", "Root Dir:");
            btnBrowse.Text = I18n.T("浏览...", "Browse...");
            lblLevels.Text = I18n.T("目录层级列表（从上到下逐级目录）:", "Directory Levels (top to bottom):");
            lblLevelName.Text = I18n.T("当前层级名称/规则:", "Current Level Name/Rule:");
            lblPh.Text = I18n.T("插入占位符:", "Insert Placeholder:");
            btnInsertPh.Text = I18n.T("插入", "Insert");
            btnAddLevel.Text = I18n.T("添加层级", "Add Level");
            btnInsertLevel.Text = I18n.T("插入到上方", "Insert Above");
            btnInsertBelow.Text = I18n.T("插入到下方", "Insert Below");
            btnDeleteLevel.Text = I18n.T("删除选中", "Delete Selected");
            btnUp.Text = I18n.T("上 移", "Up");
            btnDown.Text = I18n.T("下 移", "Down");
            lblFileRule.Text = I18n.T("文件名规则:", "File Name Rule:");
            lblKeepDays.Text = I18n.T("存图保留天数:", "Keep Days:");
            chkTimestampSuffix.Text = I18n.T("时间戳后缀", "Timestamp Suffix");
            gbPreview.Text = I18n.T("实时预览（按 OK 保存 / SN-0001 / 点位1）", "Live Preview (OK / SN-0001 / point 1)");
            btnOk.Text = I18n.T("确定", "OK");
            btnCancel.Text = I18n.T("取消", "Cancel");

            // 悬停气泡（Designer 里的静态中文提示，运行时按语言刷新）
            tip.SetToolTip(txtSaveRootDir, I18n.T(
                "图片保存的根目录（绝对路径）。\r\n点\"浏览...\"可直接选文件夹；\r\n实际子目录按下方\"目录层级\"逐级创建。",
                "Root directory for saved images (absolute path).\r\nUse \"Browse...\" to pick a folder.\r\nSub-directories are created level by level as listed below."));
            tip.SetToolTip(btnBrowse, I18n.T("选择图片保存根目录的文件夹。", "Pick the root folder for saved images."));
            tip.SetToolTip(lblLevels, I18n.T(
                "存图目录从根目录起按此列表逐级创建。\r\n每级可以写固定名字（如 OK），也可以是生成规则（如 {年月日}）。\r\n顺序即建目录顺序：从上到下。",
                "Save sub-directories are created top-to-bottom from the root.\r\nEach level can be a fixed name (e.g. OK) or a rule (e.g. {Date})."));
            tip.SetToolTip(lstLevels, I18n.T(
                "目录层级列表（从上到下逐级建目录）。\r\n双击一项可直接进入编辑；\r\n支持占位符：{年月日}整个日期目录、{SN}序列号、{OKNG}→OK/NG 两个分支目录、{点位}点位号。",
                "Directory level list (created top to bottom).\r\nDouble-click an item to edit it.\r\nPlaceholders: {Date} date dir, {SN} serial, {OKNG}→OK/NG branches, {Station} point number."));
            tip.SetToolTip(txtLevelName, I18n.T(
                "当前选中层级的名字/规则，直接改文字就同步到左侧列表。\r\n支持占位符：{年月日}整个日期目录、{SN}序列号、{OKNG}→OK/NG 两个分支目录、{点位}点位号。",
                "Name/rule of the selected level; editing it updates the list at once.\r\nPlaceholders: {Date} date dir, {SN} serial, {OKNG}→OK/NG branches, {Station} point number."));
            tip.SetToolTip(cmbPlaceholder, I18n.T(
                "选中的占位符会插入到当前正在编辑的框里（目录层级名或文件名）。\r\n{年月日}→如 2026.08.20  {SN}→序列号  {OKNG}→OK 或 NG 目录  {点位}→存图点位号  {时间}→毫秒时间戳",
                "The selected placeholder is inserted into the box being edited (level name or file name).\r\n{Date}→e.g. 2026.08.20  {SN}→serial  {OKNG}→OK or NG dir  {Station}→point number  {Time}→ms timestamp"));
            tip.SetToolTip(btnInsertPh, I18n.T(
                "把下拉框选中的占位符插到当前编辑框的光标位置，\r\n插入后光标自动移到其后。",
                "Insert the selected placeholder at the cursor position of the current edit box."));
            tip.SetToolTip(btnAddLevel, I18n.T(
                "在列表末尾追加一级目录（默认给 {SN}，现场按需改）。",
                "Append a level at the end of the list (default {SN})."));
            tip.SetToolTip(btnInsertLevel, I18n.T(
                "在选中层级的上方插入一级（默认 {SN}）；未选中则插到最顶部。",
                "Insert a level above the selected one (default {SN}); top if none selected."));
            tip.SetToolTip(btnInsertBelow, I18n.T(
                "在选中层级的下方插入一级（默认 {SN}）；未选中则插到末尾。\r\n现场常需要把\"OK/NG\"插到某层后面，用它不用先删再加。",
                "Insert a level below the selected one (default {SN}); end if none selected."));
            tip.SetToolTip(btnDeleteLevel, I18n.T(
                "删除选中的层级；删空会自动保留至少一级默认，避免存出空结构。",
                "Delete the selected level; at least one default level is kept to avoid an empty structure."));
            tip.SetToolTip(btnUp, I18n.T("上移选中的层级，调整目录顺序（顺序即建目录顺序）。", "Move the selected level up (order = creation order)."));
            tip.SetToolTip(btnDown, I18n.T("下移选中的层级，调整目录顺序（顺序即建目录顺序）。", "Move the selected level down (order = creation order)."));
            tip.SetToolTip(txtFileNameTpl, I18n.T(
                "图片文件名规则（默认 {点位}，如 1.png）。\r\n占位符：{点位}点位号、{SN}序列号、{OKNG}→OK 或 NG、{年}/{月}/{日}日期、{时间}毫秒时间戳；\r\n其余文字原样保留。",
                "Image file name rule (default {Station}, e.g. 1.png).\r\nPlaceholders: {Station} point, {SN} serial, {OKNG} OK/NG, {Year}/{Month}/{Day} date, {Time} ms timestamp; other text kept as-is."));
            tip.SetToolTip(lblKeepDays, I18n.T(
                "存图目录只保留最近 N 天，更早的由后台定期清理删除（默认 30 天）。\r\n0 = 不自动清理。\r\n清理只动\"保存根目录\"下的过期日期目录，不影响相机 FTP 取图目录。",
                "Keep saved images for the last N days; older ones are cleaned up by a background task (default 30).\r\n0 = no auto cleanup.\r\nOnly the save root's expired date dirs are cleaned, never the camera FTP dirs."));
            tip.SetToolTip(nudKeepDays, I18n.T(
                "存图目录只保留最近 N 天，更早的由后台定期清理删除（默认 30 天）。\r\n0 = 不自动清理。",
                "Keep saved images for the last N days (default 30). 0 = no auto cleanup."));
            tip.SetToolTip(chkTimestampSuffix, I18n.T(
                "勾选后，存图文件名追加时间戳后缀（如 0084_20260814_164022_461.jpeg），\r\n防止同点位重复拍照/重复触发时覆盖旧图（默认开启）。\r\n取消勾选则保持相机源文件名原样（如 0084.jpeg）。",
                "When checked, a timestamp suffix is appended to saved file names (e.g. 0084_20260814_164022_461.jpeg)\r\nto prevent overwriting old images on repeated triggers (on by default).\r\nUncheck to keep the camera source file name as-is."));
            tip.SetToolTip(gbPreview, I18n.T(
                "实时预览：按当前规则用示例数据（今天日期 / SN-0001 / 点位1）\r\n渲染出将来落盘的完整目录树，OK 和 NG 各展示一棵。",
                "Live preview: renders the future directory tree with sample data (today / SN-0001 / point 1),\r\nshowing an OK tree and an NG tree."));
            tip.SetToolTip(tvPreview, I18n.T(
                "实时预览：按当前规则用示例数据（今天日期 / SN-0001 / 点位1）\r\n渲染出将来落盘的完整目录树，OK 和 NG 各展示一棵。",
                "Live preview: renders the future directory tree with sample data (today / SN-0001 / point 1),\r\nshowing an OK tree and an NG tree."));

            // 占位符下拉项按语言重建（V2.15.12）：英文界面显示英文占位符 {Date}/{Station}…，
            // 插入到文本框的也是英文显示值（保存时由 OnOk/PlaceholderLocalizer.ToStorage 统一还原成中文，
            // 绝不让英文占位符进配置/渲染链路）；中文界面维持设计器原文。下拉不挂选择事件，重建无副作用。
            cmbPlaceholder.Items.Clear();
            if (I18n.Language == "en-US")
                cmbPlaceholder.Items.AddRange(new object[] { "{Date}", "{Year}", "{Month}", "{Day}", "{SN}", "{OKNG}", "{Station}", "{Camera}", "{Time}" });
            else
                cmbPlaceholder.Items.AddRange(new object[] { "{年月日}", "{年}", "{月}", "{日}", "{SN}", "{OKNG}", "{点位}", "{相机}", "{时间}" });
            cmbPlaceholder.SelectedIndex = 0;

            ApplyLayoutForLanguage();   // V2.15.7：布局按语言区分布局（仅英文界面调整坐标）
        }

        /// <summary>
        /// V2.15.7 布局按语言区分：Designer 静态坐标是【中文原版式】（标签短，无需让位），
        /// 英文界面各标签变长（实测 `Current Level Name/Rule:` 173px、`File Name Rule:` 112px），
        /// 原输入框左缘离标签太近导致标题不完整/重叠，且 `Timestamp Suffix` 勾选框贴窗体右缘显局促。
        /// 故仅当 `I18n.Language == "en-US"` 时动态右移/收窄各控件（右缘统一保持 600）；
        /// 中文界面【完全不动】，保持设计器原版式（防止现场中文布局被误改）。
        /// 该方法在 ApplyLanguage 末尾调用（模态对话框打开瞬间按当前语言初始化一次）。
        /// </summary>
        private void ApplyLayoutForLanguage()
        {
            if (I18n.Language != "en-US")
            {
                // 中文界面：保持设计器原版式，不做任何调整（含勾选框左缘，LoadFromConfig 已对齐右缘）
                return;
            }

            // 层级名输入框：标签区 20~210 完整容纳英文标签（右缘 ≈199），右缘 600 不变
            txtLevelName.Left = 210;
            txtLevelName.Width = 390;

            // 占位符下拉与"插入"按钮：跟随层级名输入框左缘对齐（210），按钮保持相对间距 10px
            cmbPlaceholder.Left = 210;
            btnInsertPh.Left = 340;

            // 文件名规则输入框：标签区 20~150 完整容纳英文粗体标签（右缘 ≈138），右缘 600 不变
            txtFileNameTpl.Left = 150;
            txtFileNameTpl.Width = 450;

            // 保留天数一行：标签左缘与文件名框左缘对齐（150），数字框紧贴英文标签右缘（≈233）
            lblKeepDays.Left = 150;
            nudKeepDays.Left = 250;

            // chkTimestampSuffix（V2.15.7 定稿）：英文界面右缘与 txtFileNameTpl 右缘对齐（600），
            // 与中文 LoadFromConfig 的右缘对齐逻辑同一视觉基准。此处显式重设——
            // txtFileNameTpl 刚被上文改成左缘 150、宽 450，Right 仍是 600，
            // 用 Right - AutoSize 宽度 反向校正 Left，保证两者右侧对齐、不重叠。
            chkTimestampSuffix.Left = txtFileNameTpl.Right - chkTimestampSuffix.Width;
        }
    }
}
