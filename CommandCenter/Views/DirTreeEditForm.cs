using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Services;

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
    }

        /// <summary>把配置填进界面：根目录、目录层级列表、文件名规则。</summary>
        private void LoadFromConfig()
        {
            txtSaveRootDir.Text = _cfg.SaveRootDir;

            // 层级列表：直接用 SubDirs（模型默认已带三层，首次打开即所见即所得）
            var levels = _cfg.SubDirs ?? new List<string>();
            foreach (var lvl in levels)
                lstLevels.Items.Add(lvl);

            txtFileNameTpl.Text = _cfg.FileNameTemplate;
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
                if (string.IsNullOrWhiteSpace(ph)) { MessageBox.Show("请先在下拉框选择要插入的占位符。", "提示"); return; }
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
            string dflt = string.IsNullOrEmpty(defaultValue)
                ? (lstLevels.Items.Count == 0 ? "{年月日}" : "{SN}")
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
            lstLevels.Items.Insert(insertAt, "{SN}");
            lstLevels.SelectedIndex = insertAt;
            txtLevelName.Text = "{SN}";
        }

        /// <summary>删除选中的层级；删空则补一级默认（避免保存出空结构）。</summary>
        private void DeleteLevel()
        {
            int idx = lstLevels.SelectedIndex;
            if (idx < 0) { MessageBox.Show("请先选中要删除的层级。", "提示"); return; }
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
            var levels = lstLevels.Items.Cast<string>().ToList();
            string fileRule = txtFileNameTpl.Text.Trim();
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
                BuildPreviewBranch(rootNode, levels, 0, fileRule, now, sn, station);

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
                                        string fileRule, DateTime now, string sn, int station)
        {
            if (levelIndex >= levels.Count)
            {
                // 目录层级已到底：追加图片文件叶子（按文件名规则渲染，默认 {点位}.png）
                string fname = ImageStore.RenderTemplate(fileRule, now, sn, true, station);
                if (string.IsNullOrWhiteSpace(fname))
                    fname = "IMG_" + now.ToString("yyyyMMdd_HHmmss_fff") + "_1.png";
                parent.Nodes.Add(fname + ".png");
                return;
            }

            // 渲染本层目录名：OK/NG 各渲染一次。若结果相同（本层不含 {OKNG}）则只建一个节点；
            // 不同（含 {OKNG}）则建两个并列节点，各自递归展开完整子树。
            string okName = ImageStore.RenderTemplate(levels[levelIndex], now, sn, true, station);
            string ngName = ImageStore.RenderTemplate(levels[levelIndex], now, sn, false, station);

            if (okName == ngName)
            {
                // 普通层：单分支继续
                TreeNode child = parent.Nodes.Add(string.IsNullOrWhiteSpace(okName) ? "(空)" : okName);
                BuildPreviewBranch(child, levels, levelIndex + 1, fileRule, now, sn, station);
            }
            else
            {
                // 分支层（{OKNG}）：OK、NG 并列两个目录，各自带完整子树
                TreeNode okNode = parent.Nodes.Add(okName);
                BuildPreviewBranch(okNode, levels, levelIndex + 1, fileRule, now, sn, station);
                TreeNode ngNode = parent.Nodes.Add(ngName);
                BuildPreviewBranch(ngNode, levels, levelIndex + 1, fileRule, now, sn, station);
            }
        }

        /// <summary>确定：把编辑结果写回 ImageConfig（同一实例）。</summary>
        private void OnOk()
        {
            // 收集层级：清掉空白项，避免存出空目录层级；删空则保留默认 {年月日} 兜底
            var levels = lstLevels.Items.Cast<string>()
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();
            if (levels.Count == 0) levels.Add("{年月日}");

            _cfg.SaveRootDir = txtSaveRootDir.Text.Trim();
            _cfg.SubDirs = levels;
            _cfg.FileNameTemplate = txtFileNameTpl.Text.Trim();

            DialogResult = DialogResult.OK;
        }
    }
}
