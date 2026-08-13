using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 窗口/点位与相机程序配置对话框：它同时管两个映射（V1.12.25 起同页混排）：
    ///   ① 【窗口 → 存图点位】可视化格子矩阵（原功能）：每个格子=一个显示窗口，改"该窗口的图存成几号点位"；
    ///   ② 【点位 → 相机程序号】每台相机各自一张表（V1.12.25 新增），**V2.8 起再按产品型号分表**：
    ///      同一台相机的程序库会随产品型号变化（如"上相机"型号 U171 用 P000~P012、U172 用 P013~P028），
    ///      所以型号下拉选"默认"编辑旧 StationPrograms 表、选某型号编辑该相机在该型号下的映射表
    ///      （ModelStationPrograms）。触发时按"当前产品型号→点位"切到对应程序（见
    ///      ProductionCoordinator.TriggerOneCamera 与 ResolveProgramForStation）。
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
///     │ ┌ [grpProgram 相机程序映射]──────────────────────────────────┐ │
    ///     │ │ 相机: [cmbCamera▾]  型号: [cmbModel▾]  查"相机+型号"切程序     │ │
    ///     │ │ ┌────────────┬──────────────┐                              │ │
    ///     │ │ │ 点位(下拉)  │ 相机程序(下拉) │ ← dgvPrograms 下拉选择        │ │
    ///     │ │ ├────────────┼──────────────┤      "新增映射"加一行          │ │
    ///     │ │ │  3         │   P2         │                              │ │
    ///     │ │ └────────────┴──────────────┘                              │ │
    ///     │ │ [btnAddProg 新增映射] [btnDelProg 删除选中行] 下区提示:点位从下拉选、  │ │
    ///     │ │   程序号=相机程序库(0~127,与窗口数无关)；型号=产品型号(U171…)  │ │
    ///     │ └───────────────────────────────────────────────────────────┘ │
    /// │ [btnEditPoint 编辑点位][btnSwap 交换位置][btnReset 恢复默认][btnDisable 禁用/启用]│
    /// │                                            [btnOk] [btnCancel]     │
    /// └───────────────────────────────────────────────────────────────────┘
    ///
    /// 【为什么这么做】
    ///   - 现场存图点位默认=窗口编号、可自定义（原逻辑），可视化格子最直观；
    ///   - 相机程序映射是"同页混排"新增区：因为点位和相机程序是强关联的（一次到的件、谁拍、拍时切哪个
    ///     程序），放同一对话框里一起配，避免到处找。也顺带满足"一个入口管完窗口/点位/相机程序"。
    ///   - 相机下拉只影响【哪个相机的表被编辑】，不影响上面的窗口↔点位矩阵。
    ///   【自适应模式（V2.12.0）】构造传入 autoFit=true 时：矩阵行列/窗口数 = 当前型号 + 各相机
    ///     点位表自动铺排（AutoFitLayout，前上相机后下相机），格子下方显示"相机名·点位号"；
    ///     存图点位=全局窗口编号（windowIndex），故【编辑点位/交换位置/恢复默认】锁定置灰，
    ///     仅保留"禁用/启用"与下方相机程序映射区；确定时不再写回 WindowStationMap（自适应不依赖它）。
    ///   【禁用窗口/点位（V1.12.28）】右键点击格子、或选中后点"禁用/启用"按钮切换某窗口的启停：
    ///   禁用的格子显示灰底"已禁用"；生效后主界面该窗口不显示（矩阵紧凑重排）、PLC 拍照请求写到
    ///   该点位时上位机不触发相机、不显示、不存图、不计数，直接把结果写成 3（跳过）让 PLC 走下一工位。
    ///   V2.10.1：禁用状态【跟随点位】——"交换位置"时禁用状态随点位一起搬到另一格（现场语义是
    ///   "禁用的是这个点位"，不是"禁用的是这个格子"）。
    ///   所有改动先落在内存编辑副本上，点"确定"才写回 DisplayConfig.WindowStationMap 与
    ///   WindowEnabled、各 CameraConfig.StationPrograms（同一实例引用，保证设置窗体保存时拿到最新值）。
    /// </summary>
    public partial class WindowPointForm : Form
    {
        /// <summary>编辑副本：确定时整体写回目标列表（同实例，保证设置窗体保存时拿到最新值）</summary>
        private readonly List<int> _map;

        /// <summary>确定时写回的目标列表（DisplayConfig.WindowStationMap 的引用）</summary>
        private readonly List<int> _target;

        /// <summary>编辑副本（V1.12.28 窗口禁用）：与 _map 同下标表示"该窗口是否启用"，确定时整体写回。</summary>
        private readonly List<bool> _enabled;

        /// <summary>确定时写回的启用列表目标（DisplayConfig.WindowEnabled 的引用）</summary>
        private readonly List<bool> _enabledTarget;

        /// <summary>相机配置列表（V1.12.25，主配置引用，确定时把各自映射写回）</summary>
        private readonly List<CameraConfig> _cameras;

        /// <summary>每台相机的"点位→程序号"编辑副本（V2.8 起按型号分表，见 BuildProgramGrid）：
        /// 外层下标与 _cameras 对齐；内层 Dictionary 的 key=产品型号名（""=默认/不区分型号），
        /// value=该型号下的点位→程序号表。</summary>
        private readonly List<Dictionary<string, List<StationProgramItem>>> _programEdits;

        /// <summary>全局产品型号候选列表（构造传入，AppConfig.ProductModels，界面型号下拉候选）。</summary>
        private readonly List<string> _productModels;

        /// <summary>当前程序映射区正在编辑的型号（""="默认（不区分型号）"，对应 StationPrograms）。</summary>
        private string _programModel = "";

        private readonly int _rows;   // 矩阵行数（与主界面一致）
        private readonly int _cols;   // 矩阵列数

        /// <summary>是否自适应模式（V2.12.0）：窗口矩阵按"相机+型号点位表"自动铺排、
        /// 手动点位编辑（编辑/交换/恢复）锁定，仅保留禁用/启用与相机程序映射。</summary>
        private readonly bool _autoFit;

        /// <summary>当前产品型号（V2.12.0，用于按型号查各相机点位表铺排窗口与格子上标注点位）。</summary>
        private readonly string _productModel;

        /// <summary>窗口总数：非自适应=rows×cols；自适应=各相机当前型号点位表条目数之和（≥1，
        /// 布局上 matrix 的格子数=rows×cols≥窗口总数，超出部分留空）。</summary>
        private readonly int _windowCount;

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

public WindowPointForm(List<int> targetMap, int rows, int cols, List<CameraConfig> cameras,
            List<bool> enabledTarget, List<string> productModels, bool autoFit, string productModel)
        {
            _target = targetMap;
            _cameras = cameras ?? new List<CameraConfig>();
            _productModels = productModels ?? new List<string>();
            _autoFit = autoFit;
            _productModel = productModel ?? "";

            // 自适应（V2.12.0）：窗口行列/数量由"当前型号 + 各相机点位表"自动铺排，忽略手填行列；
            // 非自适应保持传入手动行列（与主界面 Rows×Columns 一致）。
            if (_autoFit)
            {
                var layout = DisplayConfig.AutoFitLayout(_cameras, _productModel);
                _rows = layout.rows;
                _cols = layout.cols;
                _windowCount = layout.windowCount;
            }
            else
            {
                _rows = Math.Max(1, rows);
                _cols = Math.Max(1, cols);
                _windowCount = _rows * _cols;
            }

            // 复制一份窗口映射作为编辑副本：点确定才写回，避免"改了又取消"污染配置。
            // 空安全：targetMap 被配置手改成 null 时按空表兜底（自适应下本映射本就不参与存图）。
            _map = new List<int>(targetMap ?? new List<int>());
            // 长度兜底：调用方已对齐（ConfigStore.EnsureStationMap），这里再保一层，防止越界。
            // 自适应下 _map 不参与存图（存图点位=全局窗口编号），仅作格子上坐标对齐参考。
            int total = _windowCount;
            while (_map.Count < total) _map.Add(_map.Count + 1);
            if (_map.Count > total) _map.RemoveRange(total, _map.Count - total);

            // 复制一份"窗口是否启用"编辑副本（V1.12.28）：确定时写回 enabledTarget
            _enabledTarget = enabledTarget ?? new List<bool>();
            _enabled = new List<bool>(_enabledTarget);
            while (_enabled.Count < total) _enabled.Add(true);
            if (_enabled.Count > total) _enabled.RemoveRange(total, _enabled.Count - total);

            // 每台相机复制一份"点位→程序号"编辑副本（V2.8 起按型号分表）：
            //  key "" = 默认表（旧 StationPrograms）；其余 key = ProductModels 里的型号，
            //  对应 ModelStationPrograms 里同名型号表。改的是副本，点确定才写回原配置。
            _programEdits = new List<Dictionary<string, List<StationProgramItem>>>();
            foreach (var cam in _cameras)
            {
                var dict = new Dictionary<string, List<StationProgramItem>>(StringComparer.OrdinalIgnoreCase);
                // 默认表（无型号/旧兼容）
                dict[""] = CloneTable(cam.StationPrograms);
                // 各型号表（ModelStationPrograms）
                if (cam.ModelStationPrograms != null)
                {
                    foreach (var m in cam.ModelStationPrograms)
                    {
                        if (m == null || string.IsNullOrWhiteSpace(m.ModelName)) continue;
                        if (!dict.ContainsKey(m.ModelName))
                            dict[m.ModelName] = CloneTable(m.Programs);
                    }
                }
                _programEdits.Add(dict);
            }

            InitializeComponent();      // 先解析设计器里的静态控件
            BuildMatrix();              // 按 Rows×Cols 动态生成窗口格子按钮
            BuildProgramGrid();         // 初始化相机程序映射区：下拉 + 表格列
            WireEvents();               // 挂按钮/格子交互
            RefreshCells();             // 首次填充"编号 + 点位"文字
        }

        /// <summary>
        /// 动态生成窗口矩阵：与主界面 TableLayoutPanel 一样按百分比等分，
        /// 每个格子是一个 Button（Tag 存序号），上面显示两行文字：固定编号 + 点位/相机标注。
        /// 只生成 _windowCount 个格子（自适应下窗口总数=相机点位和，布局网格 rows×cols 中
        /// 尾部多出的空格子不生成、保持空白）。
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
            for (int idx = 0; idx < _windowCount; idx++)
            {
                int r = idx / _cols, c = idx % _cols;
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
                // V1.12.28 右键切换禁用/启用：右键不参与左键选中/交换逻辑，直接翻转该窗口启用状态
                b.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) ToggleWindowDisabled(cur); };
                _cells[r, c] = b;
                grid.Controls.Add(b, c, r);
            }
        }

        /// <summary>
        /// 初始化相机程序映射区（V1.12.25；V2.8 加型号维度）：
        ///   - 相机下拉列出每台相机（显示名称/IP）；
        ///   - 型号下拉列出"默认（不区分型号）" + 全局产品型号（AppConfig.ProductModels）；
        ///   - DataGridView 两列：点位 / 相机程序号（V1.12.26 起下拉选择，不必手输）；
        ///   - 选中某台相机 + 某型号时把该组合的编辑副本灌进表格。
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

            // 型号下拉（V2.8）：候选 = "默认（不区分型号）" + 全局产品型号列表（AppConfig.ProductModels）。
            // 选"默认"编辑的是旧 StationPrograms 默认表；选某型号编辑该相机在该型号下的映射表
            // （ModelStationPrograms）。当前型号 _programModel="" 表示默认表。
            cmbModel.Items.Clear();
            cmbModel.Items.Add(DefaultModelText);
            foreach (var m in _productModels)
                if (!string.IsNullOrWhiteSpace(m)) cmbModel.Items.Add(m);
            // V2.12.0 自适应：默认选中"当前产品型号"的映射表（自适应窗口/存图均按型号查表，
            // 型号表才是现场要配的主表）；非自适应保持默认"默认（不区分型号）"表。
            // 注意：此处仍在 WireEvents 之前设置 SelectedIndex，不会触发 SelectedIndexChanged，
            // _programModel 需手动同步赋值。
            _programModel = "";
            int modelSel = -1;
            if (_autoFit && !string.IsNullOrWhiteSpace(_productModel))
                modelSel = cmbModel.Items.IndexOf(_productModel);
            if (modelSel > 0)
            {
                _programModel = _productModel;
                cmbModel.SelectedIndex = modelSel;
            }
            else
            {
                cmbModel.SelectedIndex = 0;   // 默认表（兜底，含型号无对应型号表时）
            }

            // 点位下拉候选：以【窗口映射的点位】为准（数量=窗口数；点位默认=窗口编号，改也只是互换或个别调整）。
            // 为什么不再加"所有相机已配点位"当候选（V1.12.26 澄清）：点位数量应能被窗口数量确定，
            // 混入异常点位会让下拉多出不存在的点位号。此处仅兜底追加"已配但窗口里没有"的存量点位
            // （老数据），保证下拉里已配置的行仍能显示/重选，正常情况集合就等于窗口映射点位。
            RefillStationColumn();

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

        /// <summary>型号下拉"默认"项文案（不区分型号 = 查相机默认表 StationPrograms）。</summary>
        private const string DefaultModelText = "默认（不区分型号）";

        /// <summary>重建"点位列"下拉候选（V2.10.1）：候选 = 窗口映射里的点位 ∪ 各相机已配点位。
        /// 构造时调用一次；【矩阵里"编辑点位"输入新点位号后也必须调用】——旧代码只在构造时构建
        /// 一次，用户把窗口点位改成候选外的号后，程序映射区点位列永远选不到它，那个点位就配不上
        /// 程序（运行时查不出映射、不切程序，可能拍到错误的对应程序）。</summary>
        private void RefillStationColumn()
        {
            var set = new SortedSet<int>();
            foreach (var s in _map) if (s >= 1) set.Add(s);
            foreach (var cam in _cameras)
                if (cam.StationPrograms != null)
                    foreach (var it in cam.StationPrograms)
                        if (it.StationNo >= 1) set.Add(it.StationNo);
            colStation.Items.Clear();
            foreach (var s in set) colStation.Items.Add(s);
        }

        /// <summary>当前"相机+型号"组合的编辑槽位表；型号槽不存在时自动建空表（首次切过去即可编辑）。</summary>
        private List<StationProgramItem> _slot()
        {
            var dict = _programEdits[_programCamIdx];
            if (!dict.TryGetValue(_programModel, out var list))
            {
                list = new List<StationProgramItem>();
                dict[_programModel] = list;   // 记住：点确定时要把这个型号的表写回配置
            }
            return list;
        }

        /// <summary>复制一张"点位→程序号"表（编辑副本用，避免直接改到配置对象）。</summary>
        private static List<StationProgramItem> CloneTable(List<StationProgramItem> src)
        {
            var copy = new List<StationProgramItem>();
            if (src != null)
            {
                foreach (var x in src)
                    if (x != null)
                        copy.Add(new StationProgramItem { StationNo = x.StationNo, ProgramNo = x.ProgramNo });
            }
            return copy;
        }

        /// <summary>重新把当前"相机+型号"组合的编辑副本灌入表格（切换相机/型号/增删行后调用）。
        /// 下拉列填值：点位/程序号都直接放 int；程序号 -1 用"不切换"文案（与下拉选项一致）。</summary>
        private void ReloadProgramGrid()
        {
            if (_programCamIdx < 0 || _programCamIdx >= _programEdits.Count) return;
            dgvPrograms.Rows.Clear();
            foreach (var item in _slot())
            {
                object prog = item.ProgramNo < 0 ? "不切换" : (object)item.ProgramNo;
                dgvPrograms.Rows.Add(item.StationNo, prog);
            }
        }

        /// <summary>把表格当前内容回存到正在编辑的"相机+型号"编辑副本（切换相机/型号/确定前调用）。
        /// 下拉列取值：点位/程序号选中值是 int（或"不切换"）；未选/留空按原语义处理。
        /// 点位非法→跳过该行（该相机不拍这个点位）；程序号选"不切换"/空→-1（不切换）。</summary>
        private void FlushProgramGrid()
        {
            if (_programCamIdx < 0 || _programCamIdx >= _programEdits.Count) return;
            var list = _slot();
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
            btnDisable.Click += (s, e) => ToggleSelectedDisabled();
            btnOk.Click += (s, e) => OnOk();

            // V2.12.0 自适应：点位由相机点位表决定，手动"编辑点位/交换位置/恢复默认"锁定置灰，
            // 仅保留"禁用/启用"窗口与相机程序映射区——避免用户把点位改成与相机表不一致（改了也不生效）。
            if (_autoFit)
            {
                btnEditPoint.Enabled = false;
                btnSwap.Enabled = false;
                btnReset.Enabled = false;
                lblHint.Text = HintAutoFit;
            }

            cmbCamera.SelectedIndexChanged += (s, e) =>
            {
                // 切换相机：先把"旧相机+当前型号"的表格内容留存在副本里，再切到新相机的映射
                FlushProgramGrid();
                _programCamIdx = cmbCamera.SelectedIndex;
                ReloadProgramGrid();
            };
            cmbModel.SelectedIndexChanged += (s, e) =>
            {
                // 切换型号：先把"当前相机+旧型号"的表格内容留存在副本里，再切到新型号映射
                FlushProgramGrid();
                _programModel = cmbModel.SelectedIndex <= 0 ? "" : (cmbModel.SelectedItem?.ToString() ?? "");
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

        /// <summary>互换两个窗口的存图点位（即"调整窗口位置"，编号固定跟随格子）。
        /// V2.10.1：禁用状态【跟随点位】一起交换——现场语义是"禁用的是这个点位，不是这个格子"，
        /// 交换后该点位搬到的窗口保持禁用、原窗口恢复为对方反过来的启用状态。</summary>
        private void SwapCells(int a, int b)
        {
            int t = _map[a];
            _map[a] = _map[b];
            _map[b] = t;
            if (a >= 0 && a < _enabled.Count && b >= 0 && b < _enabled.Count)
            {
                bool te = _enabled[a];
                _enabled[a] = _enabled[b];
                _enabled[b] = te;
            }
        }

        /// <summary>常驻提示文案（Designer 里的默认 Text 也保持一致）。</summary>
        private const string HintDefault =
            "每个格子 = 主界面一个显示窗口。上方是【固定编号】；下方是它的【存图点位】。\r\n" +
            "单击格子选中，点\"编辑点位\"改存图号；点\"交换位置\"互换两个窗口的内容（编号固定）。\r\n" +
            "【右键格子】或选中后点\"禁用/启用\"：停用某窗口/点位（主界面不显示、PLC 拍到此点位直接跳过）。";

        /// <summary>自适应模式的常驻提示文案（V2.12.0，与 HintDefault 互斥使用）。</summary>
        private const string HintAutoFit =
            "【自适应模式】窗口矩阵由\"当前型号 + 各相机点位表\"自动铺排（前上相机、后下相机），\r\n" +
            "格子上方=窗口编号、下方=归属相机·相机点位号；存图点位=全局窗口编号，不可手改。\r\n" +
            "【右键格子】或选中后点\"禁用/启用\"可停用某窗口（主界面不显示、PLC 拍到此点位直接跳过）。\r\n" +
            "下方\"相机程序映射\"区照常可配：相机+型号 → 点位 → 相机程序号。";

        /// <summary>进入/退出"交换位置"模式：模式开启时点两个格子自动互换。</summary>
        private void ToggleSwapMode()
        {
            if (_autoFit) return;          // V2.12.0 自适应：点位不可改，禁止交换（按钮已置灰，双保险）
            _swapping = !_swapping;
            _selectedIdx = -1;    // 普通选中态清除，避免与交换选中混淆
            _swapA = -1;
            lblHint.Text = _swapping
                ? "交换位置：请依次单击要互换的两个窗口格子（再点同一个格子可取消第一选）。"
                : HintDefault;
            RefreshCells();
        }

        /// <summary>
        /// 切换"选中的格子"的启用状态（V1.12.28，"禁用/启用"按钮）。
        /// 无选中时提示；交换模式中禁止（避免与换位操作打架）。
        /// </summary>
        private void ToggleSelectedDisabled()
        {
            if (_swapping) return;
            if (_selectedIdx < 0)
            {
                MessageBox.Show("请先单击选中要禁用/启用的窗口格子（格子会高亮）。\r\n也可直接【右键点击】格子切换。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ToggleWindowDisabled(_selectedIdx);
        }

        /// <summary>翻转某个窗口的启用状态（右键点击格子 / 禁用按钮共用），并刷新显示。</summary>
        private void ToggleWindowDisabled(int idx)
        {
            if (idx < 0 || idx >= _enabled.Count) return;
            _enabled[idx] = !_enabled[idx];
            LogHelper.Info($"窗口 {idx + 1} 已{( _enabled[idx] ? "启用" : "禁用")}（点确定后生效）");
            RefreshCells();
        }

        /// <summary>给当前选中的格子改存图点位（弹出输入框）。</summary>
        private void EditSelectedPoint()
        {
            if (_autoFit) return;          // V2.12.0 自适应：点位由相机表决定，禁止手改（按钮已置灰）
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
            // V2.10.1：新点位并入程序映射区点位列候选（否则该点位在下拉里选不到、配不上程序）
            RefillStationColumn();
            RefreshCells();
        }

        /// <summary>恢复默认：所有窗口启用 + 每个窗口的存图点位 = 窗口编号（1、2、3…）。</summary>
        private void ResetAll()
        {
            if (_autoFit) return;          // V2.12.0 自适应：点位不可改，禁止恢复默认（按钮已置灰）
            if (MessageBox.Show("恢复默认后，每个窗口的存图点位 = 窗口编号（1、2、3…），且所有窗口恢复启用。\r\n确定恢复吗？",
                "恢复默认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            for (int i = 0; i < _map.Count; i++) _map[i] = i + 1;
            for (int i = 0; i < _enabled.Count; i++) _enabled[i] = true; // V1.12.28：一并恢复全部启用
            _selectedIdx = -1;
            _swapA = -1;
            _swapping = false;
            lblHint.Text = HintDefault;
            RefreshCells();
        }

        /// <summary>
        /// 刷新所有格子的显示：编号（固定）+ 点位/相机标注；
        /// 自适应（V2.12.0）格子下方显示"相机名·点位号"（点位由相机表决定、存图用全局窗口编号）；
        /// 非自适应显示"存图点位"（_map 值，可自定义）。
        /// 选中/待交换的格子用浅黄高亮；【禁用的格子（V1.12.28）灰底 + "已禁用"】
        /// 红点标注（禁用后主界面不显示该窗口、PLC 拍到此点位直接跳过）。
        /// </summary>
        private void RefreshCells()
        {
            for (int i = 0; i < _windowCount; i++)
            {
                int r = i / _cols, c = i % _cols;
                var b = _cells[r, c];
                bool disabled = i >= _enabled.Count || !_enabled[i];
                b.Text = disabled
                    ? $"窗口 {i + 1}\r\n已禁用"
                    : (_autoFit ? $"窗口 {i + 1}\r\n{ResolveWindowSource(i + 1)}"
                                : $"窗口 {i + 1}\r\n点位 {_map[i]}");
                if (disabled)
                {
                    // 禁用：灰底 + 灰字，醒目区分于普通格子
                    b.BackColor = Color.FromArgb(222, 222, 222);
                    b.ForeColor = Color.FromArgb(150, 150, 150);
                    b.FlatStyle = FlatStyle.Flat;
                }
                else
                {
                    b.ForeColor = Color.Black;
                    b.BackColor = (i == _selectedIdx || i == _swapA)
                        ? Color.FromArgb(255, 224, 130)
                        : SystemColors.Control;
                    b.UseVisualStyleBackColor = (i != _selectedIdx && i != _swapA);
                }
            }
        }

        /// <summary>
        /// 自适应下解析"窗口 w(1 起) → 相机名·点位号"显示文案（V2.12.0）：
        /// 用 AutoFitCameraStarts（各相机起始窗口）找出窗口落在哪台相机的点位表区间，
        /// 再取该表中对应条目的点位号（StationNo）拼成"相机名·点位N"（如上相机·点位3）。
        /// 解析失败（相机表被删/越界）兜底显示窗口编号，只影响展示、不影响配置。
        /// </summary>
        private string ResolveWindowSource(int w)
        {
            var starts = DisplayConfig.AutoFitCameraStarts(_cameras, _productModel);
            for (int i = 0; i < _cameras.Count && i < starts.Count; i++)
            {
                if (_cameras[i] == null) continue;   // 空安全：配置被手改成 null 元素时跳过，不崩
                var table = _cameras[i].ProgramsFor(_productModel);
                if (table == null || table.Count == 0) continue;
                if (w >= starts[i] && w < starts[i] + table.Count)
                {
                    var it = table[w - starts[i]];
                    string camName = string.IsNullOrWhiteSpace(_cameras[i].Name)
                        ? $"相机{i + 1}" : _cameras[i].Name;
                    return $"{camName}·点位{(it == null ? w : it.StationNo)}";
                }
            }
            return $"窗口{w}";
        }

        /// <summary>
        /// 确定：把编辑副本整体写回目标（窗口映射 + 各相机点位→程序号映射，含按型号分表），再关闭。
        /// 写回规则（V2.8）：
        ///   - 型号下拉选的"默认"槽位 → CameraConfig.StationPrograms（默认表，不区分型号）；
        ///   - 其余型号槽位 → CameraConfig.ModelStationPrograms（按型号名合并：已有同名表更新
        ///     Programs，没有的追加；没编辑过的型号表原样保留不丢）。
        /// 两处都是"同实例引用写回"，设置窗体点保存时自动带上最新值。
        /// </summary>
        private void OnOk()
        {
            FlushProgramGrid();                                   // 先把当前表格内容落回编辑副本
            // V2.12.0 自适应：存图点位=全局窗口编号（windowIndex），不依赖 WindowStationMap，
            // 故不把编辑副本写回 _target（写入也是无用值，反而污染配置）；窗口禁用状态照常写回。
            if (!_autoFit)
            {
                _target.Clear();
                _target.AddRange(_map);
            }
            // V1.12.28：把"窗口是否启用"编辑副本整体写回（同实例引用，设置窗体保存时自动落盘）
            _enabledTarget.Clear();
            _enabledTarget.AddRange(_enabled);

            // 被删空、本次【不写回】的型号槽（V2.10.1）：逐个收集，结尾弹窗提示用户，
            // 避免"删光映射行却发现没生效、也没提示"。
            var emptySlots = new List<string>();

            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                var dict = _programEdits[i];
                // 默认表（型号下拉选"默认"编辑的那张）→ StationPrograms
                dict.TryGetValue("", out var defList);
                cam.StationPrograms = defList ?? new List<StationProgramItem>();
                // 型号表 → ModelStationPrograms：按型号名合并，未编辑的型号表不碰
                var dest = cam.ModelStationPrograms ?? new List<ModelStationPrograms>();
                foreach (var kv in dict)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;          // ""=默认表，已写 StationPrograms
                    if (kv.Value == null || kv.Value.Count == 0)
                    {
                        // V2.10.1 空表【沿用该型号既有映射、不写空表】：防止用户删光映射行把配置
                        // 误删掉。但"删了没生效"需要明示，否则现场以为清掉了其实还在按旧表切程序。
                        emptySlots.Add($"相机「{(string.IsNullOrWhiteSpace(cam.Name) ? "相机" + (i + 1) : cam.Name)}」型号「{kv.Key}」");
                        continue;
                    }
                    var m = dest.FirstOrDefault(x =>
                        string.Equals(x?.ModelName, kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (m == null)
                    {
                        m = new ModelStationPrograms { ModelName = kv.Key, Programs = new List<StationProgramItem>() };
                        dest.Add(m);
                    }
                    m.Programs = kv.Value;
                }
                cam.ModelStationPrograms = dest;
            }

            if (emptySlots.Count > 0)
            {
                MessageBox.Show(
                    "以下【相机+型号】的程序映射表已清空，本次【保留该型号原有映射】、不写入空表：\r\n" +
                    string.Join("\r\n", emptySlots) +
                    "\r\n\r\n解释：型号表为空时运行时仍会按该型号既有的 programStationPrograms 配置切程序。" +
                    "如确实要让整张型号表失效，请直接编辑 appconfig.json 的 modelStationPrograms 删掉对应型号节点；" +
                    "只删部分点位则直接在表里删掉那几行即可。",
                    "映射表为空", MessageBoxButtons.OK, MessageBoxIcon.Information);
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