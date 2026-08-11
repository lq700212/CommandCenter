using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// 窗口与存图点位配置对话框：把"每个显示窗口的存图点位"做成【可视化格子矩阵】来改。
    ///
    /// ┌───────────────────────────────────────────────────────────────┐
    /// │ 窗口与存图点位配置                                              │
    /// │ 每个格子=一个窗口，上:固定编号  下:存图点位                      │
    /// │ ┌───────┬───────┬───────┬───────┐                            │
    /// │ │ 窗口1 │ 窗口2 │ 窗口3 │ 窗口4 │   ← 与主界面矩阵布局一致      │
    /// │ │ 点位1 │ 点位2 │ 点位3 │ 点位4 │                              │
    /// │ ├───────┼───────┼───────┼───────┤                            │
    /// │ │ 窗口5 │ 窗口6 │  ...  │  ...  │                              │
    /// │ └───────┴───────┴───────┴───────┘                            │
    /// │ [btnEditPoint 编辑点位] [btnSwap 交换位置] [btnReset 恢复默认]  │
    /// │                                            [btnOk] [btnCancel] │
    /// └───────────────────────────────────────────────────────────────┘
    ///
    /// 【为什么这么做】
    ///   现场要求"存图点位默认=窗口编号、但可自定义"，且最好可视化：
    ///   - 默认：1 号窗口存图名 = 1.png、2 号 = 2.png…（点位=窗口编号）；
    ///   - 自定义：点中某个格子 → "编辑点位" → 输入任意正整数，例如 1 号窗口存图名改成 2.png；
    ///   - 窗口位置可调：点"交换位置"后依次点两个格子，它们的点位互换（等价于两个窗口内容互换），
    ///     而窗口编号固定跟随格子位置——不管谁被换到第一格，它永远是 1 号。
    ///   所有改动先在 _map（本地副本）上完成，点"确定"才写回 DisplayConfig.WindowStationMap（同一实例）。
    /// </summary>
    public partial class WindowPointForm : Form
    {
        /// <summary>编辑副本：确定时整体写回目标列表（同实例，保证设置窗体保存时拿到最新值）</summary>
        private readonly List<int> _map;

        /// <summary>确定时写回的目标列表（DisplayConfig.WindowStationMap 的引用）</summary>
        private readonly List<int> _target;

        private readonly int _rows;   // 矩阵行数（与主界面一致）
        private readonly int _cols;   // 矩阵列数

        /// <summary>格子按钮矩阵（行×列），Tag 存格子序号（0 起）</summary>
        private Button[,] _cells;

        /// <summary>当前选中的格子序号（-1 = 未选中）；用于"编辑点位"。</summary>
        private int _selectedIdx = -1;

        /// <summary>是否处于"交换位置"模式（点完两个格子自动互换）。</summary>
        private bool _swapping;

        /// <summary>交换模式里已选中的第一个格子序号（-1 = 还没选第一个）。</summary>
        private int _swapA = -1;

        public WindowPointForm(List<int> targetMap, int rows, int cols)
        {
            _target = targetMap;
            _rows = Math.Max(1, rows);
            _cols = Math.Max(1, cols);

            // 复制一份映射作为编辑副本：点确定才回写，避免"改了又取消"污染配置
            _map = new List<int>(targetMap);
            // 长度兜底：调用方已对齐（ConfigStore.EnsureStationMap），这里再保一层，防止越界
            int total = _rows * _cols;
            while (_map.Count < total) _map.Add(_map.Count + 1);
            if (_map.Count > total) _map.RemoveRange(total, _map.Count - total);

            InitializeComponent();      // 先解析设计器里的静态控件
            BuildMatrix();              // 按 Rows×Cols 动态生成格子按钮
            WireEvents();               // 挂按钮/格子交互
            RefreshCells();             // 首次填充"编号 + 点位"文字
        }

        /// <summary>
        /// 动态生成窗口矩阵：与主界面 TableLayoutPanel 一样按百分比等分，
        /// 每个格子是一个 Button（Tag 存序号），上面显示两行文字：固定编号 + 存图点位。
        /// </summary>
        private void BuildMatrix()
        {
            var grid = tblMatrix;
            grid.Controls.Clear();
            grid.ColumnCount = _cols;
            grid.RowCount = _rows;
            grid.ColumnStyles.Clear();
            grid.RowStyles.Clear();
            for (int c = 0; c < _cols; c++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _cols));
            for (int r = 0; r < _rows; r++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _rows));

            _cells = new Button[_rows, _cols];
            int idx = 0;
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    int cur = idx; // 闭包锁定当前序号，避免循环变量被所有事件共享
                    var b = new Button
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(4),
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Microsoft YaHei", 9.5F, FontStyle.Bold),
                        Tag = cur
                    };
                    b.Click += (s, e) => OnCellClick(cur);
                    _cells[r, c] = b;
                    grid.Controls.Add(b, c, r);
                    idx++;
                }
            }
        }

        /// <summary>
        /// 挂底部按钮事件：编辑点位 / 交换位置 / 恢复默认 / 确定。
        /// （取消按钮的 DialogResult 已在设计器里设好，无需挂线。）
        /// </summary>
        private void WireEvents()
        {
            btnEditPoint.Click += (s, e) => EditSelectedPoint();
            btnSwap.Click += (s, e) => ToggleSwapMode();
            btnReset.Click += (s, e) => ResetAll();
            btnOk.Click += (s, e) => OnOk();
        }

        /// <summary>
        /// 格子点击。普通模式：选中/取消选中（供"编辑点位"定位）；
        /// 交换模式：第一次点选第一格，第二次点选第二格并立即互换两个窗口的点位。
        /// </summary>
        private void OnCellClick(int idx)
        {
            if (_swapping)
            {
                if (_swapA < 0)
                {
                    _swapA = idx;                       // 先记第一格
                }
                else if (idx == _swapA)
                {
                    _swapA = -1;                        // 再点同一格 = 取消第一选
                }
                else
                {
                    SwapCells(_swapA, idx);             // 点另一格 → 立即互换并退出交换模式
                    _swapA = -1;
                    _swapping = false;
                    lblHint.Text = HintDefault;
                }
                RefreshCells();
                return;
            }

            _selectedIdx = (_selectedIdx == idx) ? -1 : idx;
            RefreshCells();
        }

        /// <summary>互换两个窗口的存图点位（即"调整窗口位置"，编号固定跟随格子）。</summary>
        private void SwapCells(int a, int b)
        {
            int t = _map[a];
            _map[a] = _map[b];
            _map[b] = t;
        }

        /// <summary>常驻提示文案（Designer 里的默认 Text 也保持一致）。</summary>
        private const string HintDefault =
            "每个格子 = 主界面一个显示窗口。上方是【固定编号】；下方是它的【存图点位】。\r\n" +
            "单击格子选中，点\"编辑点位\"改存图号；点\"交换位置\"可把两个窗口的内容互换（编号固定）。";

        /// <summary>进入/退出"交换位置"模式：模式开启时点两个格子自动互换。</summary>
        private void ToggleSwapMode()
        {
            _swapping = !_swapping;
            _selectedIdx = -1;    // 普通选中态清除，避免与交换选中混淆
            _swapA = -1;
            lblHint.Text = _swapping
                ? "交换位置：请依次单击要互换的两个窗口格子（再点同一个格子可取消第一选）。"
                : HintDefault;
            RefreshCells();
        }

        /// <summary>给当前选中的格子改存图点位（弹出输入框）。</summary>
        private void EditSelectedPoint()
        {
            if (_swapping) return;                 // 交换模式中禁止编辑，避免操作打架
            if (_selectedIdx < 0)
            {
                MessageBox.Show("请先单击选中要编辑的窗口格子（格子会高亮）。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string input = ShowInputBox(
                $"编辑窗口 {_selectedIdx + 1} 的存图点位",
                $"该窗口的图将存成\"{_map[_selectedIdx]}\"（点位号.png）。\r\n请输入新的存图点位（正整数，例如 2）：",
                _map[_selectedIdx].ToString());
            if (input == null) return;             // 用户点了取消

            if (!int.TryParse(input.Trim(), out int n) || n < 1 || n > 9999)
            {
                MessageBox.Show("请输入 1~9999 的整数点位号。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _map[_selectedIdx] = n;
            RefreshCells();
        }

        /// <summary>恢复默认：所有窗口点位 = 窗口编号（1、2、3…）。</summary>
        private void ResetAll()
        {
            if (MessageBox.Show("恢复默认后，每个窗口的存图点位 = 窗口编号（1、2、3…）。\r\n确定恢复吗？",
                "恢复默认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            for (int i = 0; i < _map.Count; i++) _map[i] = i + 1;
            _selectedIdx = -1;
            _swapA = -1;
            _swapping = false;
            lblHint.Text = HintDefault;
            RefreshCells();
        }

        /// <summary>
        /// 刷新所有格子的显示：编号（固定）+ 点位（当前映射值）；
        /// 选中/待交换的格子用浅黄高亮，让现场一眼看到当前操作对象。
        /// </summary>
        private void RefreshCells()
        {
            for (int i = 0; i < _rows * _cols; i++)
            {
                int r = i / _cols, c = i % _cols;
                var b = _cells[r, c];
                b.Text = $"窗口 {i + 1}\r\n点位 {_map[i]}";
                b.BackColor = (i == _selectedIdx || i == _swapA)
                    ? Color.FromArgb(255, 224, 130)
                    : SystemColors.Control;
                b.UseVisualStyleBackColor = (i != _selectedIdx && i != _swapA);
            }
        }

        /// <summary>确定：把编辑副本整体写回目标列表（同一实例，设置窗体保存时自动带上），再关闭。</summary>
        private void OnOk()
        {
            _target.Clear();
            _target.AddRange(_map);
            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// 简易输入框（项目没有现成 InputBox）：标题 + 提示 + 文本框 + 确定/取消。
        /// 返回输入文本；点取消返回 null。
        /// </summary>
        private static string ShowInputBox(string title, string prompt, string initValue)
        {
            using (var f = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(380, 158),
                Font = new Font("Microsoft YaHei", 9.5F)
            })
            {
                var lbl = new Label { Text = prompt, Location = new Point(16, 14), Size = new Size(348, 60) };
                var txt = new TextBox { Text = initValue, Location = new Point(16, 80), Size = new Size(348, 24) };
                var btnOk = new Button
                {
                    Text = "确定",
                    DialogResult = DialogResult.OK,
                    Location = new Point(196, 116),
                    Size = new Size(80, 30)
                };
                var btnCancel = new Button
                {
                    Text = "取消",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(284, 116),
                    Size = new Size(80, 30)
                };
                f.Controls.Add(lbl);
                f.Controls.Add(txt);
                f.Controls.Add(btnOk);
                f.Controls.Add(btnCancel);
                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;

                if (f.ShowDialog() != DialogResult.OK) return null;
                return txt.Text;
            }
        }
    }
}
