using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CommandCenter.Models;

namespace CommandCenter.Views
{
    /// <summary>
    /// 产品型号配置对话框（V2.14.14，取代旧"型号序号"框 nudModelIndex）：
    /// 用表格维护"型号名称 ↔ PLC 序号(40007)"的映射关系（PlcConfig.ModelIndexes）。
    ///
    /// 【为什么用弹窗】
    ///   型号序号与型号名称是一对多的关系（现场默认 Z121=1、U171=2，还可能要加新型号），
    ///   在设置窗体单行摆一个 NumericUpDown 只能改"当前选中型号"的序号，加新型号/批量调整
    ///   很不直观。改为独立弹窗 + 两列表格（序号、型号名称），前几行默认预载当前已有型号与序号，
    ///   可增删行、可改任意行的序号与名称，确定统一写回配置。
    ///
    /// 【编辑模式（编辑副本，确定才写回——参考 WindowPointForm 深拷贝红线）】
    ///   构造时把目标列表（_cfg.Plc.ModelIndexes）**深拷贝**成 _edits 副本填充表格；
    ///   【取消】直接关闭、什么都不做（原配置不受影响）；【确定】OnOk 收集表格到新列表、
    ///   校验通过后整体赋回目标列表。绝不直接编辑目标列表引用，防止取消也生效。
    ///
    /// 【持久化】本窗体只改内存中的 _cfg.Plc.ModelIndexes 引用；真正写盘在设置窗体点
    ///   【保存】（OnSave → ConfigStore.Save → appconfig.json 的 plc.modelIndexes），
    ///   软件重启后 ConfigStore.Load 自动重新加载显示（见 ConfigStore.EnsureModelIndexes）。
    ///
    /// 【交互】
    ///   - 打开即预载当前映射（LoadFromConfig）；
    ///   - 回车 = 【确定】（AcceptButton）、Esc = 【取消】（CancelButton）；
    ///   - 加新型号 = 点【新增】按钮（btnAdd，V2.14.29）：在表格末尾追加一行空行再填写——
    ///     **不开放 DataGridView 自带"* 新行"（AllowUserToAddRows=false）**，避免"编辑一半
    ///     未提交的新行被删除"抛"无法删除未提交的新行"异常（V2.14.29 现场报错），
    ///     也避免现场误点出空行；
    ///   - 删除（V2.14.25；V2.14.29 防错加固）：第一列"选中"勾选框标记待删除行，点右上角
    ///     【删除选中】批量删除（多选）；也可鼠标/方向键选中一行/多行（Ctrl/Shift）后点删除
    ///     或按 Delete 键（单选/多选）。**Delete 键与按钮共用同一套逻辑（V2.14.29 显式接管）**：
    ///     优先删"选中"列勾选的行，无勾选才退当前选中行——勾选多行后按 Delete 同样全部删掉
    ///     （不依赖 DataGridView 内建删除，内建只删 SelectedRows 会漏勾选行）。删除只在编辑
    ///     副本上进行，取消不影响原配置；删除集合一律跳过"新行"（IsNewRow，即便未来有人重新
    ///     打开自动新行也不崩）；正在编辑单元格文本时 Delete 是删字符、不删行；
    ///   - 全部内容（表头 + 单元格）居中显示，方便现场点选；
    ///   - 【确定】校验：型号名称非空、同型号名不重复（忽略大小写）、序号 0~65535；
    ///     不合法弹提示留在窗体，合法才关闭并写回。
    /// 本窗体不做任何通讯 IO，只在 UI 主线程使用。
    /// </summary>
    public partial class ModelIndexEditForm : Form
    {
        /// <summary>目标映射列表（调用方 _cfg.Plc.ModelIndexes 引用；确定时整体赋回）。</summary>
        private readonly List<ModelIndexItem> _target;

        /// <summary>编辑副本（深拷贝目标列表，表格直接绑定本副本；取消不影响原配置）。</summary>
        private readonly List<ModelIndexItem> _edits;

        /// <summary>
        /// 创建产品型号配置对话框。
        /// </summary>
        /// <param name="target">目标映射列表（_cfg.Plc.ModelIndexes）。传 null/空则从空表开始编辑。</param>
        public ModelIndexEditForm(List<ModelIndexItem> target)
        {
            _target = target ?? new List<ModelIndexItem>();

            // 深拷贝目标列表：表格只动副本，确定才写回（见类注释"编辑模式"红线）。
            _edits = _target
                .Where(x => x != null)
                .Select(x => new ModelIndexItem { ModelName = x.ModelName ?? "", ModelIndex = x.ModelIndex })
                .ToList();

            InitializeComponent();

            // 回车=确定 / Esc=取消（AcceptButton/CancelButton 在 Designer 已设）。
            btnOk.Click += (s, e) => OnOk();
            btnCancel.Click += (s, e) => OnCancel();
            btnAdd.Click += (s, e) => BtnAdd_Click();
            btnDelete.Click += (s, e) => BtnDelete_Click();
            // V2.14.29：显式接管 Delete 键删除——与【删除选中】按钮共用同一套"优先勾选行、
            // 无勾选回退选中行"逻辑（见 Grid_KeyDown）。不依赖 DataGridView 内建删除（内建只删
            // SelectedRows、"选中"列勾选的行会漏）。
            grid.KeyDown += Grid_KeyDown;
            LoadFromConfig();
        }

        /// <summary>把当前映射填充进表格（前几行 = 已有型号与序号）。</summary>
        private void LoadFromConfig()
        {
            grid.Rows.Clear();
            foreach (var item in _edits)
                AddRow(item.ModelName, item.ModelIndex);
        }

/// <summary>向表格追加一行（供 LoadFromConfig 与确定回填复用）。
        /// 三列按顺序：选中(默认不勾选)、序号、型号名称。</summary>
        private void AddRow(string modelName, int modelIndex)
        {
            grid.Rows.Add(false, modelIndex, modelName);
        }

        /// <summary>
        /// 新增按钮（V2.14.29）：在表格末尾追加一行空行（序号 0、型号名留空），
        /// 并把光标聚焦到新行的"型号名称"格方便直接输入。
        /// 【为什么不用 DataGridView 自带"* 新行"】自动新行编辑到一半时若被删除会抛
        /// "无法删除未提交的新行"异常（现场报错），且容易误点出空行；
        /// 改为显式按钮加行后行为可控、零异常风险。取消关闭时新增行随之丢弃。
        /// </summary>
        private void BtnAdd_Click()
        {
            AddRow("", 0);
            if (grid.Rows.Count > 0)
            {
                var last = grid.Rows[grid.Rows.Count - 1];
                grid.CurrentCell = last.Cells["colModel"];  // 聚焦型号名格，回车结束编辑后再点确定
            }
        }

        /// <summary>
        /// 删除选中行（V2.14.25，右上角【删除选中】按钮；V2.14.29 与 Delete 键共用 DeleteRows）：
        /// ① 优先删除"选中"列勾选的所有行（勾选=批量标记，多选删除）；
        /// ② 没有勾选行时，删除当前选中的行（鼠标点选/方向键单选，或 Ctrl/Shift 多选）；
        /// ③ 两者都没有 → 弹提示提醒先选择。
        /// 只动编辑副本 _edits 绑定的表格，取消不落盘（与 WindowPointForm 副本式编辑一致）。
        /// </summary>
        private void BtnDelete_Click()
        {
            if (!DeleteRows())
            {
                MessageBox.Show(this, "请先勾选要删除的行（左侧复选框），或直接选中一行/多行后再删除。",
                    "产品型号配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Delete 键删除（V2.14.29 显式接管，不再依赖 DataGridView 内建行为）：
        /// 【为什么显式接管】DataGridView 内建 Delete 只删"当前选中的行"（SelectedRows），
        /// 现场按"选中"列勾选多行再按 Delete 会漏删勾选行；且内建删除是隐式的，一旦删到
        /// "未提交的新行"会抛"无法删除未提交的新行"异常。这里与【删除选中】按钮共用
        /// DeleteRows()（优先勾选行 → 无勾选回退选中行），行为统一可控。
        /// 【边界】正在编辑单元格文本时（grid.IsCurrentCellInEditMode=true），Delete 键是
        /// "删除光标前字符"的编辑键，必须放行给编辑控件处理，不能拿去删行。
        /// </summary>
        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;

            // 编辑单元格中：Delete 是删字符，交给 DataGridView 编辑控件，不删行。
            if (grid.IsCurrentCellInEditMode) return;

            DeleteRows();
            e.Handled = true;   // 已接管删除，阻止内建删除再次执行（防重叠/二次删）。
        }

        /// <summary>
        /// 统一删除："选中"列勾选的行优先，无勾选则退当前选中的行（单选/多选）。
        /// 一律跳过新行（IsNewRow），从后往前删防索引错乱。返回是否删掉至少一行。
        /// 按钮（BtnDelete_Click）与 Delete 键（Grid_KeyDown）共用，确保行为一致。
        /// </summary>
        private bool DeleteRows()
        {
            // ① 收集"选中"列勾选的行（checkbox 值可能是 bool 或 null，null 视作未勾选）。
            //    一律跳过 IsNewRow（未提交的新行 RemoveAt 会抛"无法删除未提交的新行"）。
            var toRemove = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells["colSel"]?.Value is bool selected && selected)
                    toRemove.Add(row);
            }

            // ② 无勾选 → 回退用当前选中行（单选/多选），与勾选互斥避免误删两套集合。
            //    选中集合同样跳过新行（防错兜底）。
            if (toRemove.Count == 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    if (row.IsNewRow) continue;
                    toRemove.Add(row);
                }
            }

            if (toRemove.Count == 0) return false;

            // ③ 按行索引从后往前删，避免删行引起索引/遍历错乱；Distinct 防勾选与选中重叠。
            var indexes = toRemove.Select(r => r.Index).Distinct().OrderByDescending(i => i).ToList();
            foreach (int idx in indexes)
                grid.Rows.RemoveAt(idx);
            return true;
        }

        /// <summary>
        /// 确定按钮：收集表格 → 校验 → 整体写回目标列表并关闭。
        /// 表格三列：colSel(选中，仅删除用不写回) / colIndex(序号) / colModel(型号名)。
        /// 校验规则：① 型号名称不能为空（空白行视为"空行"，跳过不写、不报错）；
        /// ② 同型号名不能重复（忽略大小写，防止 PLC 序号反查歧义）；③ 序号必须在 0~65535。
        /// 校验不通过弹提示并留在窗体（返回 DialogResult.None 撤销默认 OK）。
        /// </summary>
        private void OnOk()
        {
            var result = new List<ModelIndexItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;   // 跳过末尾"* 新行"占位行

                object idxObj = row.Cells["colIndex"]?.Value;
                object nameObj = row.Cells["colModel"]?.Value;
                string name = nameObj?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(name))
                    continue;                 // 型号名空的整行忽略（等同没写这行）

                int idx;
                if (idxObj == null || !int.TryParse(idxObj.ToString(), out idx) || idx < 0 || idx > 65535)
                {
                    MessageBox.Show(this, $"型号 [{name}] 的序号无效，必须是 0~65535 的整数。",
                        "产品型号配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;   // 撤销默认 OK，留在窗体
                    return;
                }
                if (!seen.Add(name))
                {
                    MessageBox.Show(this, $"型号 [{name}] 重复出现，请合并为一行。",
                        "产品型号配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                result.Add(new ModelIndexItem { ModelName = name, ModelIndex = idx });
            }

            // 校验通过：整体赋回目标列表（先清空再加，避免残留旧行）。
            _target.Clear();
            _target.AddRange(result);
            DialogResult = DialogResult.OK;   // 正常关闭（写盘由上层设置窗体【保存】负责）
            Close();
        }

        /// <summary>取消按钮：什么都不做，直接关闭（Designer 已设 DialogResult.Cancel）。</summary>
        private void OnCancel()
        {
            Close();
        }
    }
}
