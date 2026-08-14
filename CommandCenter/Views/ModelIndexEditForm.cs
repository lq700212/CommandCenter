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
    ///   - 表格末尾 * 新行可加行、Delete 键删选中行；单元格直接编辑；
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
            LoadFromConfig();
        }

        /// <summary>把当前映射填充进表格（前几行 = 已有型号与序号）。</summary>
        private void LoadFromConfig()
        {
            grid.Rows.Clear();
            foreach (var item in _edits)
                AddRow(item.ModelName, item.ModelIndex);
        }

        /// <summary>向表格追加一行（供 LoadFromConfig 与确定回填复用）。</summary>
        private void AddRow(string modelName, int modelIndex)
        {
            grid.Rows.Add(modelIndex, modelName);
        }

        /// <summary>
        /// 确定按钮：收集表格 → 校验 → 整体写回目标列表并关闭。
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

                object idxObj = row.Cells[0]?.Value;
                object nameObj = row.Cells[1]?.Value;
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
