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
    ///   ① 【窗口 ↔ 相机点位】可视化格子矩阵（V2.12.1 起统一模型，V2.13 起支持手动编辑）：
    ///      每个格子=一个显示窗口，默认按"前上相机后下相机、各相机点位表顺序"铺排（点数=
    ///      各相机点位表条目和），格子下方标注"归属相机·点位号"（如上相机·点位3）；
    ///      **V2.13 恢复手动编辑**：编辑点位（从相机点位表已有点位里选）/ 交换位置（同相机内
    ///      互换两窗口）/ 恢复默认（重置出厂铺排 + 全部启用）——自适应/非自适应都可用，
    ///      结果存进 DisplayConfig.WindowPointMaps（按型号分表）。
    ///   ② 【点位 → 相机程序号】每台相机各自一张表（V1.12.25 新增），**V2.8 起再按产品型号分表**：
    ///      同一台相机的程序库会随产品型号变化（如"上相机"型号 U171 用 P000~P012、U172 用 P013~P028），
    ///      所以型号下拉【只列真实产品型号】（V2.12.x 起移除"默认（不区分型号）"项）、默认选中与主界面
    ///      标题栏型号一致的值，选某型号即编辑该相机在该型号下的映射表（ModelStationPrograms）。
    ///      触发时按"当前产品型号→点位"切到对应程序（见
    ///      ProductionCoordinator.ResolveProgramForStation）。
    ///
    /// ┌───────────────────────────────────────────────────────────────────┐
    /// │ 窗口/点位与相机程序配置                                              │
    /// │ [lblHint 操作说明]                                                   │
    /// │ ┌──────────────────────────────┐                                   │
    /// │ │ 窗口↔点位矩阵（格子：上=窗口编号 下=相机·点位，随型号联动）          │
    /// │ │ ┌──────┬──────┬──────┬──────┐                                    │
    /// │ │ │窗口1 │窗口2 │窗口3 │窗口4 │   ← 与主界面矩阵布局一致             │
    /// │ │ │上·点1 │上·点2 │上·点3 │下·点1 │（点位=相机点位表的点位号）       │
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
    ///   - 窗口总数由"相机点位表"唯一决定（各相机按当前型号点位表条目和，上下相机点位号从 1 起
    ///     会重复），默认窗口=点位表条目顺序铺排；V2.13 起在保持该总数前提下允许手动调整
    ///     "哪个窗口对应哪台相机的哪个点位"（现场调整两路内容、给窗口换点位），自适应/非自适应
    ///     只影响矩阵行列形状，不影响点位编辑——两种模式都可编辑、效果一致。
    ///   - 相机程序映射是"同页混排"新增区：因为点位和相机程序是强关联的（一次到的件、谁拍、
    ///     拍时切哪个程序），放同一对话框里一起配，避免到处找。
    ///   - 相机下拉只影响【哪个相机的表被编辑】，不影响上面的窗口↔点位矩阵。
    ///   【统一模型（V2.12.1）+ 独立映射（V2.13）】无论是否勾选"自适应"，窗口总数都 =
    ///     各相机按当前型号点位表条目和（DisplayConfig.ResolveLayout/WindowCountFor）；
    ///     窗口↔点位对应默认=相机点位表顺序铺排，手动编辑后写 DisplayConfig.WindowPointMaps
    ///     （按型号分表，见 DefaultWindowPointMap / ResolveWindowPointMap）。
    ///     主界面切型号、或本窗体"程序映射区"型号下拉切型号时，矩阵都会跟随重建（ApplyMatrixForModel）。
    ///     存图点位 = 相机点位号（文件名 {点位}），靠存图目录的 {相机} 层按相机隔开（见 ImageStore）。
    ///   【禁用窗口/点位（V1.12.28）】右键点击格子、或选中后点"禁用/启用"按钮切换某窗口的启停：
    ///   禁用的格子显示灰底"已禁用"；生效后主界面该窗口不显示（矩阵紧凑重排）、PLC 拍照请求写到
    ///   该点位时上位机不触发相机、不显示、不存图、不计数，直接把结果写成 3（跳过）让 PLC 走下一工位。
    ///   所有改动先落在内存编辑副本上，点"确定"才写回 DisplayConfig.WindowEnabled、
    ///   WindowPointMaps 与各相机 ModelStationPrograms（同一实例引用，保证设置窗体保存时拿到最新值）；
    ///   WindowStationMap 已退役不再写回（见 DisplayConfig.WindowStationMap 注释）。
    /// </summary>
    public partial class WindowPointForm : Form
    {
        /// <summary>历史兼容副本（V2.12.1 起 WindowStationMap 已退役）：仅保留对齐长度 +
        /// 作为点位列下拉的兜底候选，确定时【不写回】目标配置（见 OnOk）。</summary>
        private readonly List<int> _map;

        /// <summary>
        /// 窗口↔点位独立映射的编辑副本（V2.13）：外层 Dictionary 的 key=产品型号，
        /// value=该型号下的"窗口→(相机,点位)"映射（长度=该型号窗口总数，Points[i]=窗口 i+1）。
        /// 默认铺排=前上相机后下相机（DisplayConfig.DefaultWindowPointMap）；编辑点位/交换位置
        /// 只改这个副本，点确定才写回 WindowPointMaps 目标配置。
        /// </summary>
        private readonly Dictionary<string, List<WindowPointItem>> _windowPointEdits
            = new Dictionary<string, List<WindowPointItem>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>确定时写回的"窗口↔点位映射"目标（DisplayConfig.WindowPointMaps 的引用，V2.13）。</summary>
        private readonly List<ModelWindowPointMap> _windowPointMapsTarget;

        /// <summary>是否处于"交换位置"模式（点完两个窗口自动互换，V2.13 恢复；仅同相机内允许）。</summary>
        private bool _swapping;

        /// <summary>交换模式里已选中的第一个窗口序号（-1 = 还没选第一个）。</summary>
        private int _swapA = -1;

        /// <summary>编辑副本（V1.12.28 窗口禁用）：与 _map 同下标表示"该窗口是否启用"，确定时整体写回。</summary>
        private readonly List<bool> _enabled;

        /// <summary>确定时写回的启用列表目标（DisplayConfig.WindowEnabled 的引用）</summary>
        private readonly List<bool> _enabledTarget;

        /// <summary>相机配置列表（V1.12.25，主配置引用，确定时把各自映射写回）</summary>
        private readonly List<CameraConfig> _cameras;

        /// <summary>每台相机的"点位→程序号"编辑副本（V2.8 起按型号分表，见 BuildProgramGrid）：
        /// 外层下标与 _cameras 对齐；内层 Dictionary 的 key=产品型号名（V2.12.x 起恒为真实型号，
        /// 不再有 ""="默认不区分型号"槽——该功能已移除），value=该型号下的点位→程序号表。</summary>
        private readonly List<Dictionary<string, List<StationProgramItem>>> _programEdits;

        /// <summary>全局产品型号候选列表（构造传入，AppConfig.ProductModels，界面型号下拉候选）。</summary>
        private readonly List<string> _productModels;

        /// <summary>当前程序映射区正在编辑的型号（V2.12.x 起恒为真实型号，无"默认"项；
        /// 默认选中与主界面标题栏型号一致，见 BuildProgramGrid）。</summary>
        private string _programModel = "";

        private int _rows;   // 矩阵行数（与主界面一致；切型号会随点位表重算）
        private int _cols;   // 矩阵列数（切型号会随点位表重算）

        /// <summary>是否自适应模式（V2.12.0）：矩阵行列是否自动算（窗口总数两者一致，见 ResolveLayout）。</summary>
        private readonly bool _autoFit;

        /// <summary>当前产品型号（V2.12.0，构建传入：用于初始化矩阵铺排与点位表解析）。</summary>
        private readonly string _productModel;

        /// <summary>当前矩阵正在铺排用的产品型号（V2.12.1）：初始=构建传入当前型号，
        /// 用户在"相机程序映射区"型号下拉切型号时矩阵跟随重建（见 ApplyMatrixForModel）。</summary>
        private string _matrixModel;

        /// <summary>用户手填行列（V2.12.1）：非自适下作为"排列宽度/行数"的形状基准，切型号重建沿用。</summary>
        private readonly int _manualRows;
        private readonly int _manualCols;

        /// <summary>窗口总数（V2.12.1）：各相机按当前矩阵型号点位表条目数之和（≥1，
        /// 布局上 matrix 的格子数=rows×cols≥窗口总数，超出部分留空；切型号会重算）。</summary>
        private int _windowCount;

        /// <summary>格子按钮矩阵（行×列），Tag 存格子序号（0 起）</summary>
        private Button[,] _cells;

        /// <summary>当前选中的格子序号（-1 = 未选中）；用于"禁用/启用"定位。</summary>
        private int _selectedIdx = -1;

        /// <summary>当前相机映射区正在编辑哪台相机（cmbCamera 下标，-1=还没选）</summary>
        private int _programCamIdx = -1;

public WindowPointForm(List<int> targetMap, int rows, int cols, List<CameraConfig> cameras,
            List<bool> enabledTarget, List<string> productModels, bool autoFit, string productModel,
            List<ModelWindowPointMap> windowPointMaps)
        {
            // 注意：targetMap（WindowStationMap）参数仍接收但 V2.12.1 起不再写回（点位由相机点位表
            // 决定），保留参数仅为兼容调用方签名；本窗体实际落盘的只有 WindowEnabled + 相机点位表
            // + V2.13 的窗口↔点位独立映射（windowPointMaps，见 _windowPointMapsTarget）。
            _cameras = cameras ?? new List<CameraConfig>();
            _productModels = productModels ?? new List<string>();
            _autoFit = autoFit;
            _productModel = productModel ?? "";
            _windowPointMapsTarget = windowPointMaps ?? new List<ModelWindowPointMap>();

            // 矩阵当前铺排用的产品型号（V2.12.1）：初始=构建传入的当前运营型号；之后用户在下部
            // "相机程序映射区"型号下拉里切型号时矩阵跟随重建（见 cmbModel.SelectedIndexChanged）。
            _matrixModel = _productModel;
            _manualRows = Math.Max(1, rows);
            _manualCols = Math.Max(1, cols);

            // 统一布局（V2.12.1）：窗口总数 = 各相机按当前型号点位表条目和，自适应/非自适应一致；
            // 自适应行列自动算；非自适列用手填、行不足自动补齐（见 DisplayConfig.ResolveLayout）。
            var layout = DisplayConfig.ResolveLayout(_cameras, _matrixModel, _autoFit, _manualRows, _manualCols);
            _rows = layout.rows;
            _cols = layout.cols;
            _windowCount = layout.windowCount;

            // 复制一份窗口映射作为编辑副本：V2.12.1 起仅作历史兼容保留（点位由相机点位表决定，
            // 运行时/显示/存图均不读取本表，见 DisplayConfig.WindowStationMap 注释），长度照常对齐。
            // 空安全：targetMap 被配置手改成 null 时按空表兜底。
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

            // V2.13：为当前铺排型号 seed 窗口↔点位编辑副本（默认=前上相机后下相机铺排）。
            // 若目标配置里已有该型号的已编辑映射（长度=该型号窗口总数），载入继续编辑——
            // 保证"上次改过的点位/交换"下次打开还能看到、能再改。
            var defMap = DisplayConfig.DefaultWindowPointMap(_cameras, _matrixModel);
            var seed = _windowPointMapsTarget.FirstOrDefault(x => x != null
                && string.Equals(x.ModelName, _matrixModel, StringComparison.OrdinalIgnoreCase));
            if (seed != null && seed.Points != null && seed.Points.Count == defMap.Count)
                _windowPointEdits[_matrixModel] = seed.Points;
            else
                _windowPointEdits[_matrixModel] = defMap;

            // 每台相机复制一份"点位→程序号"编辑副本（V2.8 起按型号分表；V2.12.x 起不再含
            // ""=默认表槽，界面型号下拉不再提供"默认（不区分型号）"项，StationPrograms 旧默认表
            // 仅作无型号回退、确定时不写回不碰它，防止误清空老数据）：
            //  key=真实型号名，对应 ModelStationPrograms 里同名型号表。改的是副本，点确定才写回原配置。
            _programEdits = new List<Dictionary<string, List<StationProgramItem>>>();
            foreach (var cam in _cameras)
            {
                var dict = new Dictionary<string, List<StationProgramItem>>(StringComparer.OrdinalIgnoreCase);
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
            BuildMatrix();              // 按 _rows×_cols 动态生成窗口格子按钮
            BuildProgramGrid();         // 初始化相机程序映射区：下拉 + 表格列
            WireEvents();               // 挂按钮/格子交互
            RefreshCells();             // 首次填充"编号 + 相机·点位"文字
        }

        /// <summary>
        /// 按某产品型号重建窗口矩阵（V2.12.1）：窗口总数/行列随型号点位表变化（U171=上18+下4=22 窗、
        /// U172=上26=26 窗…），切型号必须重建 TableLayoutPanel，否则矩阵跟不上新型号（用户实测的
        /// "切型号后矩阵不刷新"bug 的根治）。步骤：重算布局 → _map/_enabled 重新对齐（保留已有的
        /// 禁用状态，按窗口号前缀截断）→ BuildMatrix 重建格子 → RefillStationColumn（点位列候选
        /// 随矩阵点位更新）→ RefreshCells。
        /// 行列形状：非自适沿用进入对话框时传入手填行列（_manualRows/_manualCols），自适自动算。
        /// </summary>
        private void ApplyMatrixForModel(string model)
        {
            _matrixModel = model ?? "";
            var layout = DisplayConfig.ResolveLayout(_cameras, _matrixModel, _autoFit, _manualRows, _manualCols);
            _rows = layout.rows;
            _cols = layout.cols;
            _windowCount = layout.windowCount;
            while (_map.Count < _windowCount) _map.Add(_map.Count + 1);
            if (_map.Count > _windowCount) _map.RemoveRange(_windowCount, _map.Count - _windowCount);
            while (_enabled.Count < _windowCount) _enabled.Add(true);
            if (_enabled.Count > _windowCount) _enabled.RemoveRange(_windowCount, _enabled.Count - _windowCount);

            // V2.13：切型号后必须为新型号 seed 窗口↔点位编辑副本（长度=新型号窗口总数）。
            // 缺省（首次切到该型号）按默认铺排补，已有该型号编辑表（长度正确）则保留继续编辑，
            // 长度不对（点位表增删）回退默认铺排防越界。
            if (!_windowPointEdits.ContainsKey(_matrixModel))
            {
                var defMap = DisplayConfig.DefaultWindowPointMap(_cameras, _matrixModel);
                var found = _windowPointMapsTarget.FirstOrDefault(x => x != null
                    && string.Equals(x.ModelName, _matrixModel, StringComparison.OrdinalIgnoreCase));
                if (found != null && found.Points != null && found.Points.Count == defMap.Count)
                    _windowPointEdits[_matrixModel] = found.Points;
                else
                    _windowPointEdits[_matrixModel] = defMap;
            }

            _selectedIdx = -1;
            BuildMatrix();
            RefillStationColumn();
            RefreshCells();
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
        /// 初始化相机程序映射区（V1.12.25；V2.8 加型号维度；V2.12.x 移默认项）：
        ///   - 相机下拉列出每台相机（显示名称/IP）；
        ///   - 型号下拉列出全局产品型号 + 当前运营型号（AppConfig.ProductModels ∪ MainForm 标题栏型号），
        ///     默认选中与主界面标题栏下拉一致的值；
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

            // 型号下拉（V2.8；V2.12.x 起移除"默认（不区分型号）"项）：
            //   候选 = 全局产品型号列表（AppConfig.ProductModels）+ 当前运营型号（_productModel，
            //   即主界面标题栏 cmbModel 的选中值，来自 SettingsForm 传入。主界面候选是预置三型号，
            //   与本窗体候选不同源，先把当前型号加进去保证能选中、能编辑到这张表，重复值去重）。
            //   默认选中 = 当前运营型号，与主界面标题栏下拉所见一致（用户诉求）。
            //   选某型号即编辑该相机在该型号下的映射表（ModelStationPrograms）。
            cmbModel.Items.Clear();
            if (!string.IsNullOrWhiteSpace(_productModel))
                cmbModel.Items.Add(_productModel);
            foreach (var m in _productModels)
                if (!string.IsNullOrWhiteSpace(m) && !cmbModel.Items.Contains(m)) cmbModel.Items.Add(m);
            // 当前型号一定在候选里（上面已先加入），找不到只会在 _productModel 为空时发生，兜底选首个。
            int modelSel = string.IsNullOrWhiteSpace(_productModel) ? 0 : cmbModel.Items.IndexOf(_productModel);
            if (modelSel < 0) modelSel = 0;
            // 注意：此处仍在 WireEvents 之前设置 SelectedIndex，不会触发 SelectedIndexChanged，
            // _programModel 需手动同步赋值（= 选中项，恒为真实型号）。
            if (cmbModel.Items.Count > 0)
            {
                cmbModel.SelectedIndex = modelSel;
                _programModel = cmbModel.SelectedItem?.ToString() ?? "";
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

        /// <summary>重建"点位列"下拉候选（V2.12.1）：点位由【相机点位表】唯一决定，候选 = 当前矩阵型号
        /// （_matrixModel）下各相机点位表里的点位号 ∪ 历史 _map 兜底（老配置），保证下拉里已配行仍能
        /// 重选。构造与 ApplyMatrixForModel（切型号）都会调用——型号变了点位集合跟着变。</summary>
        private void RefillStationColumn()
        {
            var set = new SortedSet<int>();
            foreach (var cam in _cameras)
            {
                if (cam == null) continue;
                foreach (var it in cam.ProgramsFor(_matrixModel))
                    if (it != null && it.StationNo >= 1) set.Add(it.StationNo);
            }
            foreach (var s in _map) if (s >= 1) set.Add(s);   // 历史兼容兜底（V2.12.1 起 _map 已退役）
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
            // V2.13 恢复窗口↔点位手动编辑（自适应/非自适应均可用）：编辑点位/交换位置/恢复默认
            btnEditPoint.Click += (s, e) => EditSelectedPoint();
            btnSwap.Click += (s, e) => ToggleSwapMode();
            btnReset.Click += (s, e) => ResetAll();
            btnDisable.Click += (s, e) => ToggleSelectedDisabled();
            btnOk.Click += (s, e) => OnOk();
            lblHint.Text = HintDefault;

            cmbCamera.SelectedIndexChanged += (s, e) =>
            {
                // 切换相机：先把"旧相机+当前型号"的表格内容留存在副本里，再切到新相机的映射
                FlushProgramGrid();
                _programCamIdx = cmbCamera.SelectedIndex;
                ReloadProgramGrid();
            };
            cmbModel.SelectedIndexChanged += (s, e) =>
            {
                // 切换型号：先把"当前相机+旧型号"的表格内容留存在副本里，再切到新型号映射。
                // V2.12.x 起候选全为真实型号（无"默认"项），_programModel 恒 = 选中型号。
                FlushProgramGrid();
                _programModel = cmbModel.SelectedItem?.ToString() ?? "";
                ReloadProgramGrid();
                // V2.12.1：型号决定窗口矩阵（总数/行列/相机标注点位都按型号点位表），程序映射区
                // 切型号时矩阵必须跟随重建——否则矩阵还停留在旧型号布局（用户实测 bug）。
                string matrixModel = string.IsNullOrEmpty(_programModel) ? _productModel : _programModel;
                if (matrixModel != _matrixModel)
                    ApplyMatrixForModel(matrixModel);
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
        /// 格子点击：单击选中/取消选中（供"禁用/启用"按钮定位高亮）。
        /// V2.13 起支持交换：处于"交换位置"模式（_swapping）时，第一次点记起点（_swapA）、
        /// 第二次点执行交换（同相机内互换点位，跨相机提示不允许）；不在交换模式时仅选中/取消。
        /// 右键点击格子仍走禁用/启用（见 BuildMatrix 里挂的 MouseUp）。
        /// </summary>
        private void OnCellClick(int idx)
        {
            if (_swapping)
            {
                if (_swapA < 0)
                {
                    _swapA = idx;                      // 第一次点击：记起点并高亮
                    lblHint.Text = "已选中" + (idx + 1) + "号窗口作为交换起点，请再点一个要互换点位的窗口（同相机内）。";
                    RefreshCells();
                }
                else
                {
                    int b = idx;                       // 第二次点击：执行交换
                    int a = _swapA;
                    _swapA = -1;
                    _swapping = false;
                    SwapCells(a, b);
                    lblHint.Text = HintDefault;
                }
                return;
            }
            _selectedIdx = (_selectedIdx == idx) ? -1 : idx;
            RefreshCells();
        }

        /// <summary>常驻提示文案（V2.12.1 统一模型版 + V2.13 恢复编辑，Designer 里的默认 Text 也保持一致）。</summary>
        private const string HintDefault =
            "每个格子 = 主界面一个显示窗口。上方是【窗口编号】；下方是【归属相机·相机点位号】。\r\n" +
            "默认按\"前上相机后下相机、各相机点位表顺序\"铺排（随下方\"型号\"下拉联动）。\r\n" +
            "可选中窗口后点【编辑点位】（从相机点位表候选里换点）、【交换位置】（同相机内点两个窗口互换）、\r\n" +
            "【恢复默认】（重置该型号出厂铺排并全部启用）；【右键格子】或选中后点\"禁用/启用\"停用某窗口。\r\n" +
            "下方\"相机程序映射\"区照常可配：相机+型号 → 点位 → 相机程序号。";

        /// <summary>
        /// 切换"选中的格子"的启用状态（V1.12.28，"禁用/启用"按钮）。
        /// 无选中时提示用户先选中一个格子。
        /// </summary>
        private void ToggleSelectedDisabled()
        {
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

        /// <summary>当前铺排型号的窗口↔点位编辑副本（V2.13；构造/切型号时已保证存在）。</summary>
        private List<WindowPointItem> _windowEditMap()
        {
            if (!_windowPointEdits.TryGetValue(_matrixModel, out var map) || map == null)
            {
                map = DisplayConfig.DefaultWindowPointMap(_cameras, _matrixModel);
                _windowPointEdits[_matrixModel] = map;
            }
            return map;
        }

        /// <summary>
        /// 编辑点位（V2.13 恢复）：把当前选中窗口的 (相机,点位) 换成另一个候选点位。
        /// 候选来源 = 当前型号下各相机点位表里已存在的点位（数量=窗口数，不引入相机表外的点）；
        /// 已分配给其他窗口的"相机+点位"不出现在候选里（同一"相机+点位"只属于一个窗口，
        /// 避免 PLC 反查出两个窗口）；当前窗口自己当前的点位保留为默认选中项。
        /// </summary>
        private void EditSelectedPoint()
        {
            if (_selectedIdx < 0)
            {
                MessageBox.Show("请先单击选中要编辑点位的窗口格子（格子会高亮）。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var map = _windowEditMap();
            if (_selectedIdx >= map.Count) return;
            var cur = map[_selectedIdx];

            // 收集候选："相机·点位"集合（当前型号相机点位表） − 已被其他窗口占用的组合
            var options = new List<Tuple<WindowPointItem, string>>();   // 值 + 显示文案
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // "相机Idx:点位" → 窗口号
            for (int j = 0; j < map.Count; j++)
            {
                if (j == _selectedIdx || map[j] == null) continue;
                var it = map[j];
                used[$"{it.CameraIndex}:{it.StationNo}"] = j + 1;
            }
            for (int ci = 0; ci < _cameras.Count; ci++)
            {
                var cam = _cameras[ci];
                if (cam == null) continue;                 // 空安全
                var table = cam.ProgramsFor(_matrixModel);
                if (table == null) continue;
                string camName = string.IsNullOrWhiteSpace(cam.Name) ? $"相机{ci + 1}" : cam.Name;
                foreach (var it in table)
                {
                    if (it == null) continue;
                    if (used.ContainsKey($"{ci}:{it.StationNo}")) continue;   // 已被别的窗口占用 → 不给选
                    options.Add(Tuple.Create(
                        new WindowPointItem { CameraIndex = ci, StationNo = it.StationNo },
                        $"{camName}·点位{it.StationNo}"));
                }
            }
            if (options.Count == 0)
            {
                MessageBox.Show("当前型号相机点位表里没有可选的候选点位。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 弹选择框：用 ComboBox 列候选（含当前项），确定后写入编辑副本
            int sel = -1;
            using (var f = new Form
            {
                Text = "编辑点位 - 窗口" + (_selectedIdx + 1),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(300, 110)
            })
            {
                var lbl = new Label { Text = "请为该窗口选择一个相机点位：", Location = new Point(12, 12), AutoSize = true };
                var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, 36), Width = 276 };
                foreach (var o in options) cmb.Items.Add(o.Item2);
                // 默认选中当前项（若当前项仍合法且在候选中）
                string curText = ResolveWindowSource(_selectedIdx + 1);
                int def = cmb.Items.IndexOf(curText);
                cmb.SelectedIndex = def >= 0 ? def : 0;
                var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(206, 70), Width = 82 };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(118, 70), Width = 82 };
                f.Controls.Add(lbl); f.Controls.Add(cmb); f.Controls.Add(ok); f.Controls.Add(cancel);
                f.AcceptButton = ok; f.CancelButton = cancel;
                if (f.ShowDialog(this) == DialogResult.OK && cmb.SelectedIndex >= 0)
                    sel = cmb.SelectedIndex;
            }
            if (sel < 0) return;   // 用户取消

            map[_selectedIdx] = options[sel].Item1;
            LogHelper.Info($"窗口 {_selectedIdx + 1} 点位改为「{options[sel].Item2}」（点确定后生效）");
            _selectedIdx = -1;
            RefillStationColumn();   // 点位列候选随点位变化刷新（点位集合可能不变，保证一致性）
            RefreshCells();
        }

        /// <summary>切换"交换位置"模式：进入后点两个窗口交换点位（同相机内）；再点一次本按钮取消。</summary>
        private void ToggleSwapMode()
        {
            _swapping = !_swapping;
            _swapA = -1;
            lblHint.Text = _swapping
                ? "交换模式：请依次点击两个要互换点位的窗口（仅同相机内允许，跨相机请用\"编辑点位\"）。\r\n" +
                  "再次点击\"交换位置\"按钮可取消交换模式。"
                : HintDefault;
            RefreshCells();
        }

        /// <summary>
        /// 交换两个窗口的点位（V2.13）：仅同一台相机内允许（上下相机点位号各自从 1 起会重复，
        /// 跨相机交换点位会让"相机+点位→窗口"反查语义混乱）。交换后两窗口的标注跟随更新。
        /// 被禁用的窗口照常参与交换（交换的是点位归属，与启用状态无关）。
        /// </summary>
        private void SwapCells(int a, int b)
        {
            if (a == b) return;
            var map = _windowEditMap();
            if (a < 0 || a >= map.Count || b < 0 || b >= map.Count) return;
            var ia = map[a];
            var ib = map[b];
            if (ia == null || ib == null) return;
            if (ia.CameraIndex != ib.CameraIndex)
            {
                MessageBox.Show($"窗口{a + 1} 与窗口{b + 1} 属于不同相机（{ResolveWindowSource(a + 1)} 与 " +
                    $"{ResolveWindowSource(b + 1)}），不能直接交换。\r\n" +
                    "交换位置仅限同一台相机内（上下相机点位号各自从 1 起，跨相机交换会让点位反查语义混乱）。\r\n" +
                    "如需跨相机调整，请用\"编辑点位\"逐窗口改。", "无法交换",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var tmp = map[a];
            map[a] = new WindowPointItem { CameraIndex = ib.CameraIndex, StationNo = ib.StationNo };
            map[b] = new WindowPointItem { CameraIndex = tmp.CameraIndex, StationNo = tmp.StationNo };
            LogHelper.Info($"交换窗口 {a + 1} ↔ {b + 1} 的点位（{ResolveWindowSource(a + 1)} / {ResolveWindowSource(b + 1)}）（点确定后生效）");
            _selectedIdx = -1;
            RefillStationColumn();
            RefreshCells();
        }

        /// <summary>
        /// 恢复默认（V2.13 恢复）：把当前型号的窗口↔点位映射重置为"前上相机后下相机"出厂铺排，
        /// 并【全部窗口重新启用】（禁用是独立的开关，恢复默认一并清理，避免"恢复后还灰着"）。
        /// 仅影响当前正在编辑的型号（_matrixModel）；其他型号的编辑不受影响。
        /// </summary>
        private void ResetAll()
        {
            var map = _windowEditMap();
            var def = DisplayConfig.DefaultWindowPointMap(_cameras, _matrixModel);
            map.Clear();
            map.AddRange(def);
            for (int i = 0; i < _enabled.Count; i++) _enabled[i] = true;
            LogHelper.Info($"型号「{_matrixModel}」窗口↔点位已恢复默认铺排并全部启用（点确定后生效）");
            _selectedIdx = -1;
            _swapA = -1;
            _swapping = false;
            RefillStationColumn();
            RefreshCells();
        }

        /// <summary>
        /// 刷新所有格子的显示：窗口编号 + "相机名·点位号"（V2.12.1 起自适应/非自适应统一用
        /// 相机点位表标注——点位由相机表唯一决定，上下相机同号点位靠相机名区分开）。
        /// 选中的格子用浅黄高亮；【禁用的格子（V1.12.28）灰底 + "已禁用"】
        /// （禁用后主界面不显示该窗口、PLC 拍到此点位直接跳过）。
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
                    : $"窗口 {i + 1}\r\n{ResolveWindowSource(i + 1)}";
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
                    b.BackColor = (i == _selectedIdx)
                        ? Color.FromArgb(255, 224, 130)
                        : SystemColors.Control;
                    b.UseVisualStyleBackColor = (i != _selectedIdx);
                }
            }
        }

        /// <summary>
        /// 解析"窗口 w(1 起) → 相机名·点位号"显示文案（V2.12.1 起自适应/非自适应统一；
        /// V2.13 起改从窗口↔点位编辑副本 _windowPointEdits 查，支持手动编辑/交换后的标注）。
        /// 默认铺排（未编辑）= 前上相机后下相机，与旧"相机点位表区间"标注等价。
        /// 型号用 _matrixModel（随"程序映射区"型号下拉联动，切型号标注一起刷新）。
        /// 解析失败（编辑副本缺失/越界）兜底显示窗口编号，只影响展示、不影响配置。
        /// </summary>
        private string ResolveWindowSource(int w)
        {
            if (_windowPointEdits.TryGetValue(_matrixModel, out var map)
                && map != null && w >= 1 && w <= map.Count)
            {
                var it = map[w - 1];                 // Points[i] = 窗口 i+1 → (相机下标,点位号)
                if (it != null && it.CameraIndex >= 0 && it.CameraIndex < _cameras.Count)
                {
                    var cam = _cameras[it.CameraIndex];
                    string camName = (cam == null || string.IsNullOrWhiteSpace(cam.Name))
                        ? $"相机{it.CameraIndex + 1}" : cam.Name;
                    return $"{camName}·点位{it.StationNo}";
                }
                return $"窗口{w}";
            }
            // 兜底（编辑副本缺失）：退回按相机点位表区间定位（旧逻辑）
            var starts = DisplayConfig.AutoFitCameraStarts(_cameras, _matrixModel);
            for (int i = 0; i < _cameras.Count && i < starts.Count; i++)
            {
                if (_cameras[i] == null) continue;   // 空安全：配置被手改成 null 元素时跳过，不崩
                var table = _cameras[i].ProgramsFor(_matrixModel);
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
        /// 确定：把编辑副本整体写回目标（各相机点位→程序号映射，含按型号分表），再关闭。
        /// 写回规则（V2.8；V2.12.x 起不再写默认表）：
        ///   - 所有型号槽位 → CameraConfig.ModelStationPrograms（按型号名合并：已有同名表更新
        ///     Programs，没有的追加；没编辑过的型号表原样保留不丢）；StationPrograms 默认表不碰。
        /// V2.12.1 起【不写回 WindowStationMap】（已退役：点位由相机点位表唯一决定，运行时/显示/
        /// 存图都不读它，写回反而污染历史字段）；窗口禁用状态照常写回。
        /// 两处都是"同实例引用写回"，设置窗体点保存时自动带上最新值。
        /// </summary>
        private void OnOk()
        {
            FlushProgramGrid();                                   // 先把当前表格内容落回编辑副本
            // V1.12.28：把"窗口是否启用"编辑副本整体写回（同实例引用，设置窗体保存时自动落盘）
            _enabledTarget.Clear();
            _enabledTarget.AddRange(_enabled);

            // V2.13：把"窗口↔点位映射"编辑副本整体写回 WindowPointMaps（按型号分表合并）：
            //   每个编辑过的型号 → 同名表更新 Points（长度=该型号窗口总数，构造/切型号时已保证），
            //   没有同名表则追加；没编辑过的型号原样保留不丢。
            if (_windowPointMapsTarget != null)
            {
                foreach (var kv in _windowPointEdits)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;   // 空 key/空表防御
                    var m = _windowPointMapsTarget.FirstOrDefault(x => x != null
                        && string.Equals(x.ModelName, kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (m == null)
                    {
                        m = new ModelWindowPointMap { ModelName = kv.Key, Points = new List<WindowPointItem>() };
                        _windowPointMapsTarget.Add(m);
                    }
                    m.Points = kv.Value;
                }
            }

            // 被删空、本次【不写回】的型号槽（V2.10.1）：逐个收集，结尾弹窗提示用户，
            // 避免"删光映射行却发现没生效、也没提示"。
            var emptySlots = new List<string>();

            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                var dict = _programEdits[i];
                // V2.12.x 起不再编辑/写回 StationPrograms（默认不区分型号表）：型号下拉已无"默认"项，
                // 该表只作旧配置/无型号时的运行时回退（ProgramsFor 型号没配表才查它），保留原值不动，
                // 防止用户从旧配置升级后一点确定把默认表误清空成空表。
                // 型号表 → ModelStationPrograms：按型号名合并，未编辑的型号表不碰
                var dest = cam.ModelStationPrograms ?? new List<ModelStationPrograms>();
                foreach (var kv in dict)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;          // 空 key 防御（正常不会出现）
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
    }
}