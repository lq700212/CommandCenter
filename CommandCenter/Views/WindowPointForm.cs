using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CommandCenter.Models;

namespace CommandCenter.Views
{
    /// <summary>
    /// 窗口/点位与相机程序配置对话框：它同时管两个映射（V1.12.25 起同页混排）：
    ///   ① 【窗口 → 存图点位】可视化格子矩阵（原功能）：每个格子=一个显示窗口，改"该窗口的图存成几号点位"；
    ///   ② 【点位 → 相机程序号】每台相机各自一张表（新增）：现场"28 个窗口点位由两台相机分工拍摄"，
    ///      各相机的程序库互相独立，所以必须"每相机一张表"——表里配了哪些点位就是这台相机负责拍哪些
    ///      点位，触发时按本轮点位切到对应相机程序（见 ProductionCoordinator.TriggerOneCamera）。
    ///
    /// ┌───────────────────────────────────────────────────────────────────┐
    /// │ 窗口/点位与相机程序配置                                              │
    /// │ [lblHint 操作说明]                                                   │
    /// │ ┌──────────────────────────────┐                                   │
    /// │ │ 窗口↔点位矩阵（格子：上=固定编号 下=存图点位）                     │
    /// │ │ ┌──────┬──────┬──────┬──────┐                                    │
    /// │ │ │窗口1 │窗口2 │窗口3 │窗口4 │   ← 与主界面矩阵布局一致             │
    /// │ │ │点位1 │点位2 │点位3 │点位4 │                                    │
    /// │ │ └──────┴──────┴──────┴──────┘                                    │
    /// │ └──────────────────────────────┘                                   │
///     │ ┌ [grpProgram 相机程序映射]──────────────────────────────────────┐ │
    /// │ │ 相机: [cmbCamera▾]  提示:表里配了哪些点位=这台相机负责拍哪些      │ │
    /// │ │ ┌────────────┬──────────────┐                                  │ │
    /// │ │ │ 点位(下拉)  │ 相机程序(下拉) │ ← dgvPrograms 下拉选择          │ │
    /// │ │ ├────────────┼──────────────┤      "新增映射"加一行             │ │
    /// │ │ │  3         │   P2         │                                  │ │
    /// │ │ └────────────┴──────────────┘                                  │ │
    /// │ │ [btnAddProg 新增映射] [btnDelProg 删除选中行] 下区提示:点位数=窗口数、   │ │
    /// │ │   程序号=相机程序库(与窗口数无关,现场动态选)                      │ │
    /// │ └────────────────────────────────────────────────────────────────┘ │
    /// │ [btnEditPoint 编辑点位][btnSwap 交换位置][btnReset 恢复默认]         │
    /// │                                            [btnOk] [btnCancel]     │
    /// └───────────────────────────────────────────────────────────────────┘
    ///
    /// 【为什么这么做】
    ///   - 现场存图点位默认=窗口编号、可自定义（原逻辑），可视化格子最直观；
    ///   - 相机程序映射是"同页混排"新增区：因为点位和相机程序是强关联的（一次到的件、谁拍、拍时切哪个
    ///     程序），放同一对话框里一起配，避免到处找。也顺带满足"一个入口管完窗口/点位/相机程序"。
    ///   - 相机下拉只影响【哪个相机的表被编辑】，不影响上面的窗口↔点位矩阵。
    ///   所有改动先落在内存编辑副本上，点"确定"才写回 DisplayConfig.WindowStationMap 与各
    ///   CameraConfig.StationPrograms（同一实例引用，保证设置窗体保存时拿到最新值）。
    /// </summary>
    public partial class WindowPointForm : Form
    {
        /// <summary>编辑副本：确定时整体写回目标列表（同实例，保证设置窗体保存时拿到最新值）</summary>
        private readonly List<int> _map;

        /// <summary>确定时写回的目标列表（DisplayConfig.WindowStationMap 的引用）</summary>
        private readonly List<int> _target;

        /// <summary>相机配置列表（V1.12.25，主配置引用，确定时把各自映射写回）</summary>
        private readonly List<CameraConfig> _cameras;

        /// <summary>每台相机的"点位→程序号"编辑副本（下标与 _cameras 对齐；ShowInputBox 前从配置复制）</summary>
        private readonly List<List<StationProgramItem>> _programEdits;

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

        /// <summary>当前相机映射区正在编辑哪台相机（cmbCamera 下标，-1=还没选）</summary>
        private int _programCamIdx = -1;

        public WindowPointForm(List<int> targetMap, int rows, int cols, List<CameraConfig> cameras)
        {
            _target = targetMap;
            _rows = Math.Max(1, rows);
            _cols = Math.Max(1, cols);
            _cameras = cameras ?? new List<CameraConfig>();

            // 复制一份窗口映射作为编辑副本：点确定才回写，避免"改了又取消"污染配置
            _map = new List<int>(targetMap);
            // 长度兜底：调用方已对齐（ConfigStore.EnsureStationMap），这里再保一层，防止越界
            int total = _rows * _cols;
            while (_map.Count < total) _map.Add(_map.Count + 1);
            if (_map.Count > total) _map.RemoveRange(total, _map.Count - total);

            // 每台相机复制一份"点位→程序号"编辑副本（同实例引用，点确定写回原处）
            _programEdits = new List<List<StationProgramItem>>();
            foreach (var cam in _cameras)
                _programEdits.Add((cam.StationPrograms ?? new List<StationProgramItem>())
                    .Select(x => new StationProgramItem { StationNo = x.StationNo, ProgramNo = x.ProgramNo })
                    .ToList());

            InitializeComponent();      // 先解析设计器里的静态控件
            BuildMatrix();              // 按 Rows×Cols 动态生成窗口格子按钮
            BuildProgramGrid();         // 初始化相机程序映射区：下拉 + 表格列
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
                        Font = new Font("Microsoft YaHei", 8F, FontStyle.Bold),
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
        /// 初始化相机程序映射区（V1.12.25）：
        ///   - 相机下拉列出每台相机（显示名称/IP）；
        ///   - DataGridView 两列：点位 / 相机程序号（V1.12.26 起下拉选择，不必手输）；
        ///   - 选中某台相机时把该相机的编辑副本灌进表格。
        /// 【下拉可选项·V1.12.26 澄清】点位列＝窗口映射里的点位（数量=窗口数，点位默认=窗口编号、
        ///   调整也只是互换/个别改号）；程序号列＝相机侧程序库（"不切换"+0~127，程序数量和编号
        ///   由相机实际装的程序决定、与窗口数量无关，现场动态选）。
        /// </summary>
        private void BuildProgramGrid()
        {
            cmbCamera.Items.Clear();
            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                string name = string.IsNullOrWhiteSpace(cam.Name) ? $"相机{i + 1}" : cam.Name;
                cmbCamera.Items.Add($"{name}  {cam.IpAddress}");
            }

            // 点位下拉候选：以【窗口映射的点位】为准（数量=窗口数；点位默认=窗口编号，改也只是互换或个别调整）。
            // 为什么不再加"所有相机已配点位"当候选（V1.12.26 澄清）：点位数量应能被窗口数量确定，
            // 混入异常点位会让下拉多出不存在的点位号。此处仅兜底追加"已配但窗口里没有"的存量点位
            // （老数据），保证下拉里已配置的行仍能显示/重选，正常情况集合就等于窗口映射点位。
            var stationSet = new SortedSet<int>();
            foreach (var s in _map) if (s >= 1) stationSet.Add(s);
            foreach (var cam in _cameras)
                if (cam.StationPrograms != null)
                    foreach (var it in cam.StationPrograms)
                        if (it.StationNo >= 1) stationSet.Add(it.StationNo);
            colStation.Items.Clear();
            foreach (var s in stationSet) colStation.Items.Add(s);

            // 程序号下拉候选："不切换"（-1，保持相机当前程序，等价于该点位未配映射）+ 0~127。
            // 注意：程序号数量和具体编号是【相机侧程序库】定的，与窗口数量无关——相机装了几个程序、
            // 编号是多少（可跳过不连续），现场就在这 0~127 全集里动态选，配几行就是几个程序。
            // 0 也是合法程序号（相机 P000），必须能选到；"不切换"解析为 -1。
            colProgram.Items.Clear();
            colProgram.Items.Add("不切换");
            for (int p = 0; p <= 127; p++) colProgram.Items.Add(p);

            if (_cameras.Count > 0)
            {
                cmbCamera.SelectedIndex = 0;   // 注意：此处在 WireEvents 之前，不会触发 SelectedIndexChanged，
                _programCamIdx = 0;            // 必须显式 ReloadProgramGrid，否则首次打开表格是空的（看不到已有映射）
                ReloadProgramGrid();
            }
            if (_cameras.Count == 0)
            {
                dgvPrograms.Enabled = false;
                btnAddProg.Enabled = false;
                btnDelProg.Enabled = false;
            }
        }

        /// <summary>重新把当前选中相机的编辑副本灌入表格（切换相机 / 增删行后调用）。
        /// 下拉列填值：点位/程序号都直接放 int；程序号 -1 用"不切换"文案（与下拉选项一致）。</summary>
        private void ReloadProgramGrid()
        {
            if (_programCamIdx < 0 || _programCamIdx >= _programEdits.Count) return;
            dgvPrograms.Rows.Clear();
            foreach (var item in _programEdits[_programCamIdx])
            {
                object prog = item.ProgramNo < 0 ? "不切换" : (object)item.ProgramNo;
                dgvPrograms.Rows.Add(item.StationNo, prog);
            }
        }

        /// <summary>把表格当前内容回存到正在编辑的相机编辑副本（切换相机 / 确定前调用）。
        /// 下拉列取值：点位/程序号选中值是 int（或"不切换"）；未选/留空按原语义处理。
        /// 点位非法→跳过该行（该相机不拍这个点位）；程序号选"不切换"/空→-1（不切换）。</summary>
        private void FlushProgramGrid()
        {
            if (_programCamIdx < 0 || _programCamIdx >= _programEdits.Count) return;
            var list = _programEdits[_programCamIdx];
            list.Clear();
            foreach (DataGridViewRow row in dgvPrograms.Rows)
            {
                // 点位列：选中值是 int，未选是 null；转字符串解析，非法即跳过（不拍这个点位）
                int station;
                string stText = row.Cells[0].Value == null ? "" : Convert.ToString(row.Cells[0].Value).Trim();
                if (!int.TryParse(stText, out station) || station < 1 || station > 9999)
                    continue;
                // 程序号列："不切换"或空/非法 → -1（不切换）；int 则直接用
                int program = -1;
                string progText = row.Cells[1].Value == null ? "" : Convert.ToString(row.Cells[1].Value).Trim();
                if ("不切换".Equals(progText, StringComparison.OrdinalIgnoreCase) || progText.Length == 0) program = -1;
                else if (int.TryParse(progText, out program) && (program < 0 || program > 127)) program = -1;
                list.Add(new StationProgramItem { StationNo = station, ProgramNo = program });
            }
            // 去重：同一台相机不允许同点位重复（后者覆盖前者，避免映射表里乱）
            var dedup = new Dictionary<int, int>();
            foreach (var item in list) dedup[item.StationNo] = item.ProgramNo;
            list.Clear();
            foreach (var kv in dedup) list.Add(new StationProgramItem { StationNo = kv.Key, ProgramNo = kv.Value });
            list.Sort((a, b) => a.StationNo.CompareTo(b.StationNo));
        }

        /// <summary>
        /// 挂底部按钮 + 相机映射区事件。窗口↔点位区：
        /// 编辑点位 / 交换位置 / 恢复默认 / 确定；相机映射区：切换相机、新增/删除映射行。
        /// （取消按钮的 DialogResult 已在设计器里设好，无需挂线。）
        /// </summary>
        private void WireEvents()
        {
            btnEditPoint.Click += (s, e) => EditSelectedPoint();
            btnSwap.Click += (s, e) => ToggleSwapMode();
            btnReset.Click += (s, e) => ResetAll();
            btnOk.Click += (s, e) => OnOk();

            cmbCamera.SelectedIndexChanged += (s, e) =>
            {
                // 切换相机：先把上一台的表格内容保留下来，再把新相机的表灌进来
                FlushProgramGrid();
                _programCamIdx = cmbCamera.SelectedIndex;
                ReloadProgramGrid();
            };
            btnAddProg.Click += (s, e) =>
            {
                FlushProgramGrid();                       // 确保已有行先落副本（否则立刻被清）
                int idx = dgvPrograms.Rows.Add(null, null); // 新增一行空映射，两列都是下拉，等用户选点位/程序号
                dgvPrograms.CurrentCell = dgvPrograms.Rows[idx].Cells[0];
                dgvPrograms.BeginEdit(true);
            };
            btnDelProg.Click += (s, e) =>
            {
                if (dgvPrograms.CurrentRow == null)
                {
                    MessageBox.Show("请先单击选中要删除的映射行。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                dgvPrograms.Rows.Remove(dgvPrograms.CurrentRow);
            };
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

        /// <summary>
        /// 确定：把编辑副本整体写回目标（窗口映射 + 各相机点位→程序号映射），再关闭。
        /// 两处都是"同实例引用写回"，设置窗体点保存时自动带上最新值。
        /// </summary>
        private void OnOk()
        {
            FlushProgramGrid();                                   // 先把当前表格内容落回编辑副本
            _target.Clear();
            _target.AddRange(_map);

            for (int i = 0; i < _cameras.Count; i++)
            {
                var dest = _cameras[i].StationPrograms;
                if (dest == null) dest = _cameras[i].StationPrograms = new List<StationProgramItem>();
                dest.Clear();
                dest.AddRange(_programEdits[i]);
            }
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