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
    ///      **V2.13 恢复手动编辑**：编辑点位（从相机点位表已有点位里选）/ 交换位置（任意两窗口
    ///      互换，含跨相机——不同相机点号相同但点位不同，靠"归属相机·点位号"二元组区分）/
    ///      恢复默认（重置出厂铺排 + 全部启用）——自适应/非自适应都可用，
    ///      结果存进 DisplayConfig.WindowPointMaps（按型号分表）。
    ///   ② 【点位 → 相机程序号】每台相机各自一张表（V1.12.25 新增），**V2.8 起再按产品型号分表**：
    ///      同一台相机的程序库会随产品型号变化（如"上相机"型号 U171 用 P000~P012、Z121 用 P013~P028），
    ///      所以型号下拉【只列真实产品型号】（V2.12.x 起移除"默认（不区分型号）"项），打开时
    ///      相机默认=第一台相机、型号默认=主界面（MainForm）当前型号（不在该相机候选则用其第一个
    ///      选项），选某型号即编辑该相机在该型号下的映射表（ModelStationPrograms）；本窗体里切型号
    ///      只同步设置页"产品型号"下拉（onModelChanged 回调），【不实时切主界面运营型号】——等用户
    ///      点设置页"保存"后 MainForm.ApplyRuntimeConfig 统一刷新标题栏/矩阵/协调器（延迟生效）。
    ///      触发时按"当前产品型号→点位"切到对应程序（见
    ///      ProductionCoordinator.ResolveProgramForStation）。
    ///      **V2.14.x 相机↔型号单向联动**：某相机在某型号下"有点位"=ProgramsFor(型号).Count>0。
    ///      cmbCamera 恒列【所有相机】（不再过滤）；选定相机后，型号下拉**只列该相机有点位的型号**
    ///      （该相机没点位的型号不出现，选择顺序固定为"先选相机 → 再选型号"，单向，避免双向互相
    ///      牵扯导致"选型号把相机跳走"的困惑）；切型号不再反过来过滤相机下拉。
    ///      过滤空集回退全量（防手改配置空表把下拉弄空）。
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
    ///   - 窗口总数 = 布局窗口数（DisplayConfig.ResolveLayout.windowCount：自适应=各相机按当前型号
    ///     点位表条目和，上下相机点位号从 1 起会重复；非自适=用户行列乘积、点位不够时多出的格子是
    ///     【空窗口】，见下方"空窗口"说明）。默认窗口=点位表条目顺序铺排；V2.13 起在保持该总数
    ///     前提下允许手动调整"哪个窗口对应哪台相机的哪个点位"（现场调整两路内容、给窗口换点位），
    ///     自适应/非自适应都可编辑——只影响矩阵行列形状，不影响点位编辑。
    ///   - 相机程序映射是"同页混排"新增区：因为点位和相机程序是强关联的（一次到的件、谁拍、
    ///     拍时切哪个程序），放同一对话框里一起配，避免到处找。
    ///   - 相机下拉只影响【哪个相机的表被编辑】，不影响上面的窗口↔点位矩阵。
    ///   【统一模型（V2.12.1）+ 独立映射（V2.13）+ 空窗口（V2.14.18）】窗口总数 = ResolveLayout
    ///     .windowCount（自适应=点位数；非自适=行列乘积、含空窗口）；窗口↔点位对应默认=相机点位表
    ///     顺序铺排（尾部多出的格子=空窗口 null 条目），手动编辑后写 DisplayConfig.WindowPointMaps
    ///     （按型号分表，见 DefaultWindowPointMap / ResolveWindowPointMap）。
    ///     主界面切型号、或本窗体"程序映射区"型号下拉切型号时，矩阵都会跟随重建（ApplyMatrixForModel）。
    ///     存图点位 = 相机点位号（文件名 {点位}），靠存图目录的 {相机} 层按相机隔开（见 ImageStore）。
    ///   【空窗口（V2.14.18）】非自适下点位不够时，行列乘积多出的格子显示为"空窗口（无点位）"：
    ///     - 主界面照样建这个窗口占位（显示区被填满），只是不接图；协调器不会给空窗口发图；
    ///     - 【交换位置】可把点位搬进空窗口（点空窗口 + 有点位的窗口，两者互换——空窗口变成该点位
    ///       的窗口、原窗口变空），"把点位换到空窗口"就靠它；
    ///     - 【编辑点位】【禁用/启用】对空窗口不可用（无点位可编辑、无"点位坏了停用"语义），
    ///       选中空窗口时这两个按钮自动置灰；【恢复默认】把空窗口还原为出厂铺排（尾部 null）。
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

        /// <summary>是否处于"交换位置"模式（点完两个窗口自动互换，V2.13 恢复；V2.13.1 起放开
        /// 跨相机——任意两窗口可互换，窗口↔点位映射用"归属相机+点位号"二元组区分同名点位）。</summary>
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
        /// 打开时默认=第一台相机的第一个有效型号，见 BuildProgramGrid）。</summary>
        private string _programModel = "";

        private int _rows;   // 矩阵行数（与主界面一致；切型号会随点位表重算）
        private int _cols;   // 矩阵列数（切型号会随点位表重算）

        /// <summary>是否自适应模式（V2.12.0；V2.14.18 窗口总数两模式不再一致——只自适应=点位数、
        /// 非自适=行列乘积含空窗口，见 ResolveLayout）：自适应时矩阵行列是否按点位自动算。</summary>
        private readonly bool _autoFit;

        /// <summary>当前产品型号（V2.12.0，构建传入：用于初始化矩阵铺排与点位表解析）。</summary>
        private readonly string _productModel;

        /// <summary>当前矩阵正在铺排用的产品型号（V2.12.1）：初始=构建传入当前型号，
        /// 用户在"相机程序映射区"型号下拉切型号时矩阵跟随重建（见 ApplyMatrixForModel）。</summary>
        private string _matrixModel;

        /// <summary>用户手填行列（V2.12.1）：非自适下作为"排列宽度/行数"的形状基准，切型号重建沿用。</summary>
        private readonly int _manualRows;
        private readonly int _manualCols;

        /// <summary>窗口控件总数（V2.14.18）：= DisplayConfig.ResolveLayout.windowCount（自适应=各相机
        /// 按当前矩阵型号点位表条目数之和；非自适=行列乘积，点位不够时多出的格子=空窗口，见类头注释；
        /// 切型号会重算）。矩阵格子数 = rows×cols = _windowCount（自适下尾部缺格留空不建格）。</summary>
        private int _windowCount;

        /// <summary>格子按钮矩阵（行×列），Tag 存格子序号（0 起）</summary>
        private Button[,] _cells;

        /// <summary>当前选中的格子序号（-1 = 未选中）；用于"禁用/启用"定位。</summary>
        private int _selectedIdx = -1;

        /// <summary>当前相机映射区正在编辑哪台相机（cmbCamera 下标，-1=还没选）</summary>
        private int _programCamIdx = -1;

        /// <summary>全量型号候选（_productModel ∪ _productModels，型号候选过滤的原始集合，见 BuildProgramGrid）。</summary>
        private List<string> _allModels = new List<string>();

        /// <summary>全量相机下标列表（1:1 对应 _cameras，cmbCamera 的恒定候选——单向下拉里相机恒列全量）。</summary>
        private List<int> _allCameraIdx = new List<int>();

        /// <summary>cmbCamera 当前位置 → 相机下标 的映射（V2.14.x 单向联动后相机下拉恒列全量、位置=相机下标，
        /// 但保留这张换算表更安全——未来若再加过滤不破坏 SelectedIndex 换算，见 PopulateCameraItems）。</summary>
        private List<int> _cameraPositions = new List<int>();

        /// <summary>ApplySelections 重建下拉期间置 true，抑制 SelectedIndexChanged 重入（避免重建时事件连环触发）。</summary>
        private bool _syncing;

        /// <summary>
        /// 型号变更通知回调（V2.12.x，延迟生效）：用户在程序映射区型号下拉里切型号时触发（传新型号名）。
        /// 现在上游（SettingsForm）只在回调里同步设置页"产品型号"下拉（OnSave 写 _cfg.ProductModel），
        /// **不实时切主界面运营型号**——MainForm 的标题栏型号/窗口矩阵/协调器统一等用户点【保存】后
        /// ApplyRuntimeConfig 刷新（避免配置对话框里翻型号时主界面矩阵跟着乱跳）。为 null 时不回调。
        /// </summary>
        private readonly Action<string> _onModelChanged;

public WindowPointForm(List<int> targetMap, int rows, int cols, List<CameraConfig> cameras,
            List<bool> enabledTarget, List<string> productModels, bool autoFit, string productModel,
            List<ModelWindowPointMap> windowPointMaps, Action<string> onModelChanged = null)
        {
            // 注意：targetMap（WindowStationMap）参数仍接收但 V2.12.1 起不再写回（点位由相机点位表
            // 决定），保留参数仅为兼容调用方签名；本窗体实际落盘的只有 WindowEnabled + 相机点位表
            // + V2.13 的窗口↔点位独立映射（windowPointMaps，见 _windowPointMapsTarget）。
            _cameras = cameras ?? new List<CameraConfig>();
            _productModels = productModels ?? new List<string>();
            _autoFit = autoFit;
            _productModel = productModel ?? "";
            _onModelChanged = onModelChanged;
            _windowPointMapsTarget = windowPointMaps ?? new List<ModelWindowPointMap>();

            // 矩阵当前铺排用的产品型号（V2.12.1）：初始=构建传入的当前运营型号；之后用户在下部
            // "相机程序映射区"型号下拉里切型号时矩阵跟随重建（见 cmbModel.SelectedIndexChanged）。
            _matrixModel = _productModel;
            _manualRows = Math.Max(1, rows);
            _manualCols = Math.Max(1, cols);

            // 统一布局（V2.12.1；V2.14.18 语义更新）：窗口总数 = ResolveLayout.windowCount——
            // 自适应 = 各相机按当前型号点位表条目和、行列自动算（非自适行列不足自动补行）；
            // 非自适 = 手填行×列（放不下点位自动补行，windowCount=rows×cols，点位不够多出空窗口）。
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
            // 【V2.14.x 修复"取消也生效"】目标配置（WindowPointMaps）里的 Points 是持久化引用，
            // 必须深拷贝一份当编辑副本。若直接引用它，SwapCells/EditSelectedPoint/ResetAll
            // 的改动会立刻污染配置——用户点【取消】点位改动照样生效、后续设置页保存照落盘
            // （WindowEnabled/_programEdits 本来就是副本，唯独这里漏了拷贝）。
                var defMap = DisplayConfig.DefaultWindowPointMap(_cameras, _matrixModel, _windowCount);
            var seed = _windowPointMapsTarget.FirstOrDefault(x => x != null
                && string.Equals(x.ModelName, _matrixModel, StringComparison.OrdinalIgnoreCase));
            if (seed != null && seed.Points != null && seed.Points.Count == defMap.Count)
                _windowPointEdits[_matrixModel] = ClonePoints(seed.Points);
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
            // DataError 兜底：DataGridViewComboBoxCell 单元格值不在下拉候选里时（旧配置里非法点位/
            // 程序号、切型号窗口期 _matrixModel 与 _programModel 不一致等边界），DataGridView 默认会
            // 弹"值无效"异常对话框。ReloadProgramGrid 已把候选补齐/规范化，这里再兜底吞掉漏网的，
            // 保证打开对话框不弹窗；该行数据仍保留，用户编辑该行自然修正。
            dgvPrograms.DataError += (s, e) => e.ThrowException = false;
            BuildMatrix();              // 按 _rows×_cols 动态生成窗口格子按钮
            BuildProgramGrid();         // 初始化相机程序映射区：下拉 + 表格列
            WireEvents();               // 挂按钮/格子交互
            RefreshCells();             // 首次填充"编号 + 相机·点位"文字
        }

        /// <summary>
        /// 按某产品型号重建窗口矩阵（V2.12.1）：窗口总数/行列随型号点位表变化（U171=上20+下4=24 窗、
        /// Z121=上26+下3=29 窗…），切型号必须重建 TableLayoutPanel，否则矩阵跟不上新型号（用户实测的
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
            // 【V2.14.x】载入已编辑映射时同样深拷贝（见构造处注释：直接引用会让"取消"也生效）。
            if (!_windowPointEdits.ContainsKey(_matrixModel))
            {
            var defMap = DisplayConfig.DefaultWindowPointMap(_cameras, _matrixModel, _windowCount);
                var found = _windowPointMapsTarget.FirstOrDefault(x => x != null
                    && string.Equals(x.ModelName, _matrixModel, StringComparison.OrdinalIgnoreCase));
                if (found != null && found.Points != null && found.Points.Count == defMap.Count)
                    _windowPointEdits[_matrixModel] = ClonePoints(found.Points);
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
        /// 只生成 _windowCount 个格子（V2.14.18：非自适 _windowCount=行×列、全生成（含空窗口占位）；
        /// 自适应 _windowCount=相机点位和，布局网格 rows×cols 中尾部多出的空格子不生成、保持空白）。
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
        /// 初始化相机程序映射区（V1.12.25；V2.8 加型号维度；V2.12.x 移默认项；V2.14.x 改单向联动）：
        ///   - 相机下拉【恒列所有相机】（不再过滤）；型号下拉【联动候选】＝当前所选相机"有点位"的
        ///     型号（该相机没点位的型号不出现）——选择顺序固定"先选相机 → 再选型号"（单向），
        ///     选型号不再反过去过滤/跳转相机，避免双向互相牵扯；
        ///   - 打开时相机默认=第一台相机、型号默认=主界面当前型号
        ///     （不在该相机候选里则用其第一个有效型号）；切型号经 onModelChanged 回调只同步设置页型号
        ///     （延迟生效，不实时切主界面运营型号）；
        ///   - DataGridView 两列：点位 / 相机程序号（V1.12.26 起下拉选择，不必手输）；
        ///   - 选中某台相机 + 某型号时把该组合的编辑副本灌进表格。
        /// 【下拉可选项·V1.12.26 澄清；V2.14.18 更新】点位列＝相机点位表里的点位号（候选来自
        ///   `ProgramsFor(型号)`，与窗口数无关；窗口矩阵里每个窗口对一个点位或空窗口，映射表长度=窗口数）；
        ///   程序号列＝相机侧程序库（"不切换"+0~127，程序数量和编号
        ///   由相机实际装的程序决定、与窗口数量无关，现场动态选）。
        /// 【型号过滤的边界】若某相机在任何型号下都没有点位（未配任何点位表），候选会回退全量——
        ///   保证下拉永不为空、界面可用。
        /// </summary>
        private void BuildProgramGrid()
        {
            // 全量候选（型号过滤的原始集合）：
            //   相机 = 所有相机（_allCameraIdx，1:1 对应 _cameras，恒作相机下拉候选）；
            //   型号 = 全局产品型号列表（AppConfig.ProductModels）+ 当前运营型号（_productModel，
            //           即主界面标题栏 cmbModel 的选中值，来自 SettingsForm 传入。主界面候选是预置三型号，
            //           与本窗体候选不同源，先把当前型号加进去保证能选中、能编辑到这张表，重复值去重）。
            _allCameraIdx = new List<int>();
            for (int i = 0; i < _cameras.Count; i++) _allCameraIdx.Add(i);
            _allModels = new List<string>();
            if (!string.IsNullOrWhiteSpace(_productModel)) _allModels.Add(_productModel);
            foreach (var m in _productModels)
                if (!string.IsNullOrWhiteSpace(m) && !_allModels.Contains(m)) _allModels.Add(m);

            // 单向联动初始值：相机默认 = 第一台相机（_cameras[0]，现场=上相机）；型号默认 = 优先主界面
            // 当前型号（_productModel，打开时与 MainForm 标题栏 cmbModel 对齐），若该型号不在第一台
            // 相机的有效候选里（该相机没有它的点位），回退到该相机候选的第一个选项。之后切相机时
            // 型号候选随相机过滤（SyncModelForCamera），切型号不再影响相机下拉——单向。
            int initCam = _allCameraIdx.Count > 0 ? _allCameraIdx[0] : -1;
            var initModels = ModelCandidatesFor(initCam);
            string initModel = !string.IsNullOrWhiteSpace(_productModel) && initModels.Contains(_productModel)
                ? _productModel                                     // 主界面当前型号可用 → 对齐主界面
                : (initModels.Count > 0 ? initModels[0] : "");      // 否则该相机第一个有效型号
            string modelSel = SyncModelForCamera(initCam, initModel);
            _programCamIdx = initCam;
            _programModel = modelSel;
            // 填充两个下拉（此时 WireEvents 尚未挂，SelectedIndex 赋值不会触发联动回调，安全）
            PopulateCameraItems(initCam);
            PopulateModelItems(modelSel);

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
                // 相机候选已按联动填充、_programCamIdx 已同步，此处显式灌入表格（构造时事件未挂，需手动调一次）
                ReloadProgramGrid();
            }
            if (_cameras.Count == 0)
            {
                dgvPrograms.Enabled = false;
                btnAddProg.Enabled = false;
                btnDelProg.Enabled = false;
            }
        }

        /// <summary>
        /// 型号候选列表：= 当前相机（camIdx）"有点位"的型号
        /// （该相机 ProgramsFor(型号).Count>0）。边界：该相机在任何型号下都没点位时回退全量型号。
        /// 【V2.14.x 单向联动】型号候选只由"所选相机"决定；相机候选恒列所有相机（见 PopulateCameraItems），
        /// 不再反过来由型号过滤相机。
        /// </summary>
        private List<string> ModelCandidatesFor(int camIdx)
        {
            var list = new List<string>();
            if (camIdx >= 0 && camIdx < _cameras.Count)
            {
                var cam = _cameras[camIdx];
                foreach (var m in _allModels)
                    if (cam != null && cam.ProgramsFor(m).Count > 0) list.Add(m);
            }
            return list.Count > 0 ? list : new List<string>(_allModels);
        }

        /// <summary>
        /// 单向联动收敛（V2.14.x 起替代 V2.12.x 的双向 SyncDropDowns）：只按"当前相机"过滤并收敛
        /// 【型号】候选。用户选择顺序固定 = 先选相机 → 型号下拉只列该相机有点位的型号。具体：preferModel
        /// （当前选中型号）若还在新相机候选里就原样保留，否则自动落到该相机第一个有效型号（不可能出现
        /// 无效组合，如"上相机+Z121"这种该相机没有点位的型号根本不会出现在候选里）。切型号不再反过去
        /// 过滤/跳转相机下拉——单向，避免双向过滤时"选型号把相机跳走"的困惑。
        /// </summary>
        private string SyncModelForCamera(int camIdx, string preferModel)
        {
            var models = ModelCandidatesFor(camIdx);
            return models.Contains(preferModel) ? preferModel : (models.Count > 0 ? models[0] : "");
        }

        /// <summary>把收敛后的 (相机,型号) 落到两个下拉。_syncing 抑制事件重入（重建下拉期间
        /// 的 SelectedIndex 变化不再触发 SelectedIndexChanged，由调用方统一 ReloadProgramGrid 与
        /// 矩阵/上层同步）。</summary>
        private void ApplySelections(int camIdx, string model)
        {
            _syncing = true;
            try
            {
                _programCamIdx = camIdx;
                _programModel = model;
                PopulateCameraItems(camIdx);
                PopulateModelItems(model);
            }
            finally { _syncing = false; }
        }

        /// <summary>重建相机下拉：恒列【所有相机】（单向联动，相机候选不再被型号过滤），选中
        /// selectedCam（相机下标）。同时维护 _cameraPositions 映射（位置→相机下标），供 SelectedIndexChanged
        /// 换算用——当前恒为全量下标（位置=相机下标），保留映射代码以便未来再过滤时不破坏换算。
        /// 注：cmbModel.SelectedIndexChanged 与 cmbCamera.SelectedIndexChanged 都依赖本方法重建相机下拉。</summary>
        private void PopulateCameraItems(int selectedCam)
        {
            var list = new List<int>(_allCameraIdx);
            _cameraPositions = list;
            cmbCamera.Items.Clear();
            foreach (int ci in list)
            {
                var cam = _cameras[ci];
                // V2.13.4：无名称时优先 CameraId 真编号、其次行序，与设置页第一列一致
                string name = string.IsNullOrWhiteSpace(cam.Name)
                    ? (cam.CameraId > 0 ? $"相机{cam.CameraId}" : $"相机{ci + 1}")
                    : cam.Name;
                cmbCamera.Items.Add($"{name}  {cam.IpAddress}");
            }
            int idx = list.IndexOf(selectedCam);
            if (idx < 0 && list.Count > 0) idx = 0;
            if (idx >= 0) cmbCamera.SelectedIndex = idx;
        }

        /// <summary>按当前相机（_programCamIdx）候选重建型号下拉（只列该相机有点位的型号，单向），
        /// 并选中 selectedModel。型号候选是字符串（型号名本身），SelectedItem 即型号，无需额外位置映射。</summary>
        private void PopulateModelItems(string selectedModel)
        {
            var list = ModelCandidatesFor(_programCamIdx);
            cmbModel.Items.Clear();
            foreach (var m in list) cmbModel.Items.Add(m);
            int idx = list.IndexOf(selectedModel);
            if (idx < 0 && list.Count > 0) idx = 0;
            if (idx >= 0) cmbModel.SelectedIndex = idx;
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

        /// <summary>
        /// 深拷贝一份"窗口↔(相机,点位)映射"表（V2.14.x 新增，编辑副本用）。
        /// 【为什么必须深拷贝】DisplayConfig.WindowPointMaps（目标配置）里的 Points 是持久化引用，
        /// 交换/编辑/恢复默认直接改它会让【取消】也生效（见构造函数注释）；克隆出一份独立列表后，
        /// 所有编辑只落在这份副本上，点【确定】（OnOk 把副本整体赋回目标）才生效。
        /// 【空窗口（V2.14.18）】null 条目（空窗口）必须【原样保留】——不能丢弃，否则副本长度
        /// 变短、与窗口总数不一致，点确定写回后 ResolveWindowPointMap 因长度不匹配回退默认铺排。
        /// </summary>
        private static List<WindowPointItem> ClonePoints(List<WindowPointItem> src)
        {
            var copy = new List<WindowPointItem>();
            if (src != null)
            {
                foreach (var p in src)
                    copy.Add(p == null ? null
                        : new WindowPointItem { CameraId = p.CameraId, StationNo = p.StationNo });
            }
            return copy;
        }

        /// <summary>重新把当前"相机+型号"组合的编辑副本灌入表格（切换相机/型号/增删行后调用）。
        /// 下拉列填值：点位/程序号都直接放 int；程序号 -1 或超界用"不切换"文案（与下拉选项一致）。
        /// 【DataError 防控】DataGridViewComboBoxCell 的单元格值【必须在下拉候选里】，否则渲染时抛
        /// "值无效"的 ArgumentException（现场实测报错）。防三件事：
        ///   ① 程序号 >127（非法，配置越界）统一按"不切换"显示，不放进下拉（候选只到 127）；
        ///   ② 点位列值可能来自"该相机某型号编辑副本"，而候选按矩阵型号（_matrixModel）生成——
        ///      打开时 _matrixModel 与程序映射区型号 _programModel 可能不一致（主界面当前型号不在
        ///      第一台相机候选时），点位超出候选。这里逐行把实际点位/程序号**动态补进下拉候选**，
        ///      保证任何值都能显示（下拉多一项无副作用，重选仍可编辑）；
        ///   ③ 仍有漏网的（用户手改配置/边界值）由 dgvPrograms.DataError 兜底吞掉（见构造）。</summary>
        private void ReloadProgramGrid()
        {
            if (_programCamIdx < 0 || _programCamIdx >= _programEdits.Count) return;
            dgvPrograms.Rows.Clear();
            foreach (var item in _slot())
            {
                // 程序号：<0（不切换）或 >127（越界）→ "不切换"；其余 0~127 合法才放 int。
                object prog = (item.ProgramNo < 0 || item.ProgramNo > 127) ? "不切换" : (object)item.ProgramNo;

                // 点位列候选补齐：复制表里有点位、但当前矩阵型号候选没有 → 动态加入，
                // 否则 DataGridViewComboBoxCell 值无效直接弹异常（见方法注释②）。
                if (item.StationNo >= 1 && !colStation.Items.Contains(item.StationNo))
                    colStation.Items.Add(item.StationNo);
                // 程序号列候选补齐：正常情况下 0~127 已在初始候选里，防列被清空/边界情况再保一手。
                if (prog is int p && !colProgram.Items.Contains(p))
                    colProgram.Items.Add(p);

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
                // 重建下拉期间（_syncing）触发的 SelectedIndex 变化一律忽略，由 ApplySelections
                // 统一刷新表格，避免重建时事件连环触发。
                if (_syncing) return;
                // 切换相机：先把"旧相机+当前型号"的表格内容留存在副本里，再切到新相机的映射。
                // 相机下拉恒列全量相机，位置 = 相机下标（仍经 _cameraPositions 换算，见 PopulateCameraItems）。
                FlushProgramGrid();
                _programCamIdx = (cmbCamera.SelectedIndex >= 0 && cmbCamera.SelectedIndex < _cameraPositions.Count)
                    ? _cameraPositions[cmbCamera.SelectedIndex] : -1;
                // 单向联动：只按所选相机收敛"型号"候选（该相机没点位的型号不出现）；型号不再反过来
                // 过滤相机下拉。切到一台新相机时，若原型号不在它的候选里会自动落到第一个有效型号。
                string oldModel = _programModel;
                string newModel = SyncModelForCamera(_programCamIdx, oldModel);
                ApplySelections(_programCamIdx, newModel);
                ReloadProgramGrid();
                // 型号因切相机被自动换成该相机的第一个有效型号时，矩阵必须跟随新型号重建、并通知上层
                // 同步型号（与 cmbModel.SelectedIndexChanged 里的矩阵重建/上层通知语义一致）。
                if (newModel != oldModel)
                {
                    string matrixModel = string.IsNullOrEmpty(newModel) ? _productModel : newModel;
                    if (matrixModel != _matrixModel)
                        ApplyMatrixForModel(matrixModel);
                    if (!string.IsNullOrWhiteSpace(newModel))
                        _onModelChanged?.Invoke(newModel);
                }
            };
            cmbModel.SelectedIndexChanged += (s, e) =>
            {
                if (_syncing) return;
                // 切换型号：先把"当前相机+旧型号"的表格内容留存在副本里，再切到新型号映射。
                // 候选已按当前相机过滤（只列该相机有点位的型号），SelectedItem 即型号名，无需位置映射。
                // 单向联动：切型号只影响型号自身（矩阵/上层同步），相机下拉不再跟随改变。
                FlushProgramGrid();
                string model = cmbModel.SelectedItem?.ToString() ?? "";
                ApplySelections(_programCamIdx, model);
                ReloadProgramGrid();
                // V2.12.1：型号决定窗口矩阵（总数/行列/相机标注点位都按型号点位表），程序映射区
                // 切型号时矩阵必须跟随重建——否则矩阵还停留在旧型号布局（用户实测 bug）。
                string matrixModel = string.IsNullOrEmpty(model) ? _productModel : model;
                if (matrixModel != _matrixModel)
                    ApplyMatrixForModel(matrixModel);
                // V2.12.x：型号最终落定后通知上层（SettingsForm/MainForm）同步主界面标题栏 cmbModel
                // ——配置对话框里选什么型号，主界面就切过去运营，两个 cmbModel 始终对齐。
                if (!string.IsNullOrWhiteSpace(model))
                    _onModelChanged?.Invoke(model);
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
        /// 第二次点执行交换（任意两窗口互换点位，含跨相机）；不在交换模式时仅选中/取消。
        /// 右键点击格子仍走禁用/启用（见 BuildMatrix 里挂的 MouseUp）。
        /// </summary>
        private void OnCellClick(int idx)
        {
            if (_swapping)
            {
                if (_swapA < 0)
                {
                    _swapA = idx;                      // 第一次点击：记起点并高亮
                    lblHint.Text = "已选中" + (idx + 1) + "号窗口作为交换起点，请再点一个要互换点位的窗口（可跨相机）。";
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

        /// <summary>常驻提示文案（V2.12.1 统一模型版 + V2.13 恢复编辑 + V2.14.18 空窗口，Designer 里的默认 Text 也保持一致）。</summary>
        private const string HintDefault =
            "每个格子 = 主界面一个显示窗口。上方是【窗口编号】；下方是【归属相机·相机点位号】。\r\n" +
            "默认按\"前上相机后下相机、各相机点位表顺序\"铺排（随下方\"型号\"下拉联动）。\r\n" +
            "【空窗口（无点位）】= 非自适下点位不够、行列乘积多出的占位格：可用【交换位置】把点位\r\n" +
            "搬进去（点空窗口 + 有点位的窗口互换）；空窗口不支持【编辑点位】【禁用/启用】（选中时按钮自动置灰）。\r\n" +
            "可选中有点位的窗口后点【编辑点位】（从相机点位表候选里换点；选中的点位若被别的窗口占用会【自动互换】）、\r\n" +
            "【交换位置】（点两个窗口互换，可跨相机、可含空窗口）、【恢复默认】（重置该型号出厂铺排并全部启用）；\r\n" +
            "【右键格子】或选中后点\"禁用/启用\"停用某窗口。\r\n" +
            "下方\"相机程序映射\"区照常可配：先选相机，型号下拉跟随该相机可选型号 → 点位 → 相机程序号。";

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

        /// <summary>翻转某个窗口的启用状态（右键点击格子 / 禁用按钮共用），并刷新显示。
        /// 【空窗口（V2.14.18）】不支持禁用/启用：空窗口没有点位，"点位坏了停用"对它无意义
        /// （按钮已置灰，这里拦右键入口，提示用户改用交换位置配点位）。</summary>
        private void ToggleWindowDisabled(int idx)
        {
            if (idx < 0 || idx >= _enabled.Count) return;
            if (IsEmptyWindow(idx + 1))
            {
                MessageBox.Show("该窗口是【空窗口】（未配置点位），不支持禁用/启用。\r\n" +
                    "如需配置该窗口，请用【交换位置】把某个点位搬进这个窗口。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _enabled[idx] = !_enabled[idx];
            LogHelper.Info($"窗口 {idx + 1} 已{( _enabled[idx] ? "启用" : "禁用")}（点确定后生效）");
            RefreshCells();
        }

        /// <summary>当前铺排型号的窗口↔点位编辑副本（V2.13；构造/切型号时已保证存在）。</summary>
        private List<WindowPointItem> _windowEditMap()
        {
            if (!_windowPointEdits.TryGetValue(_matrixModel, out var map) || map == null)
            {
                map = DisplayConfig.DefaultWindowPointMap(_cameras, _matrixModel, _windowCount);
                _windowPointEdits[_matrixModel] = map;
            }
            return map;
        }

        /// <summary>
        /// 编辑点位（V2.13 恢复；V2.14.x 修复候选恒 1；V2.14.18 支持空窗口）：把当前选中窗口的
        /// (相机,点位) 换成另一个点位。候选 = 当前型号下各相机点位表里【全部】已有点位（数量=点位数，
        /// 不引入相机表外的点）。
        /// 【V2.14.x 修复】旧实现把"已被其他窗口占用的组合"排除出候选——但窗口总数 = 相机点位表
        /// 条目和、默认铺排恰是一一对应，排除后候选恒只剩当前窗口自己的点位，"编辑点位"实际
        /// 换不了点位（现场点按钮弹窗只有一个选项、等于没反应）。改为：候选 = 全部点位；
        /// 若选中的点位恰好被另一窗口占用，自动与该窗口【互换点位】（窗口↔点位映射本就是用
        /// "归属相机+点位号"二元组区分，交换不改变值集合、运行时反查仍唯一），实现"给窗口
        /// 换点位"的真实诉求；选到未占用（理论不发生）或自己当前点位则直接赋值。
        /// 【空窗口（V2.14.18）】非自适下多出的空窗口条目为 null。空窗口**不支持编辑点位**
        /// （无点位可换；用户在 WindowPointForm 里选中空窗口时"编辑点位"按钮置灰），本方法对
        /// 空窗口做防御直接返回。把点位搬进空窗口请用【交换位置】。
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
            if (cur == null)
            {
                // 空窗口：没有点位可编辑（界面已把按钮置灰，这里仅防御）
                MessageBox.Show("该窗口是【空窗口】（未配置点位），不能直接编辑点位。\r\n" +
                    "如需把某个点位放到这个窗口，请用【交换位置】：点这个空窗口 + 一个有点位的窗口。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 收集候选：全部"相机·点位"（当前型号相机点位表）+ 占用该组合的窗口号（用于自动互换）。
            var options = new List<Tuple<WindowPointItem, string>>();   // 值 + 显示文案
            var owner = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // "相机ID:点位" → 占用窗口号(1起)
            for (int j = 0; j < map.Count; j++)
            {
                if (map[j] == null) continue;
                var it = map[j];
                // 正常情况"相机+点位"只属于一个窗口；异常重复时记最后一个，不影响候选（见下）
                owner[$"{it.CameraId}:{it.StationNo}"] = j + 1;
            }
            int defIdx = 0;   // 弹窗默认选中项 = 当前窗口自己的点位（找不到则第一个）
            for (int ci = 0; ci < _cameras.Count; ci++)
            {
                var cam = _cameras[ci];
                if (cam == null) continue;                 // 空安全
                var table = cam.ProgramsFor(_matrixModel);
                if (table == null) continue;
                string camName = string.IsNullOrWhiteSpace(cam.Name)
                    ? (cam.CameraId > 0 ? $"相机{cam.CameraId}" : $"相机{ci + 1}")
                    : cam.Name;
                // V2.13.4：关联键 = 相机ID（CameraId>0 用真编号，0 回退行序，与 ProductionCoordinator
                // 的 CameraIdFor 兜底规则一致，保证"编辑候选"与"运行时反查"用同一把钥匙）
                int camId = cam.CameraId > 0 ? cam.CameraId : ci + 1;
                foreach (var it in table)
                {
                    if (it == null) continue;
                    if (cur != null && camId == cur.CameraId && it.StationNo == cur.StationNo)
                        defIdx = options.Count;            // 记住"当前窗口自己点位"的位置，弹窗默认选中它
                    string note = "";
                    if (owner.TryGetValue($"{camId}:{it.StationNo}", out int occ) && occ != _selectedIdx + 1)
                        note = $"（当前窗口{occ}，选中即互换）";   // 被别的窗口占着 → 明示会自动交换
                    options.Add(Tuple.Create(
                        new WindowPointItem { CameraId = camId, StationNo = it.StationNo },
                        $"{camName}·点位{it.StationNo}{note}"));
                }
            }
            if (options.Count == 0)
            {
                MessageBox.Show("当前型号相机点位表里没有可选的候选点位。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 弹选择框：用 ComboBox 列候选（默认选中当前项），确定后交换或写入编辑副本
            int sel = -1;
            using (var f = new Form
            {
                Text = "编辑点位 - 窗口" + (_selectedIdx + 1),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(330, 110)
            })
            {
                var lbl = new Label { Text = "请为该窗口选择一个相机点位：", Location = new Point(12, 12), AutoSize = true };
                var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, 36), Width = 306 };
                foreach (var o in options) cmb.Items.Add(o.Item2);
                cmb.SelectedIndex = defIdx < options.Count ? defIdx : 0;   // 默认当前点位；不在候选则选第一个
                var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(236, 70), Width = 82 };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(148, 70), Width = 82 };
                f.Controls.Add(lbl); f.Controls.Add(cmb); f.Controls.Add(ok); f.Controls.Add(cancel);
                f.AcceptButton = ok; f.CancelButton = cancel;
                if (f.ShowDialog(this) == DialogResult.OK && cmb.SelectedIndex >= 0)
                    sel = cmb.SelectedIndex;
            }
            if (sel < 0) return;   // 用户取消

            var chosen = options[sel].Item1;
            // 目标组合已被另一窗口占用 → 与该窗口互换点位（值集合不变 → 运行时反查仍唯一）。
            // 判定直接用上面统计的 owner（与弹窗标注"当前窗口N"同源，行为一致）
            int conflict = -1;
            if (owner.TryGetValue($"{chosen.CameraId}:{chosen.StationNo}", out int ownerOcc) && ownerOcc != _selectedIdx + 1)
                conflict = ownerOcc - 1;
            if (conflict >= 0)
            {
                // 互换两个槽位的条目引用（当前窗口非空才会走到这里，但被选点位可能被别的窗口占用，
                // 直接交换引用即可；与 SwapCells 语义一致——交换"窗口↔点位"对应，值集合不变、反查唯一）。
                var tmp = map[_selectedIdx];
                map[_selectedIdx] = map[conflict];
                map[conflict] = tmp;
                LogHelper.Info($"窗口 {_selectedIdx + 1} 点位改为「{options[sel].Item2}」，原占用窗口 {conflict + 1} 与本窗口互换（点确定后生效）");
            }
            else
            {
                map[_selectedIdx] = chosen;
                LogHelper.Info($"窗口 {_selectedIdx + 1} 点位改为「{options[sel].Item2}」（点确定后生效）");
            }
            _selectedIdx = -1;
            RefillStationColumn();   // 点位列候选随点位变化刷新（点位集合可能不变，保证一致性）
            RefreshCells();
        }

        /// <summary>切换"交换位置"模式：进入后点两个窗口交换点位（任意两窗口，可跨相机）；
        /// 再点一次本按钮取消。</summary>
        private void ToggleSwapMode()
        {
            _swapping = !_swapping;
            _swapA = -1;
            lblHint.Text = _swapping
                ? "交换模式：请依次点击两个要互换点位的窗口（可跨相机；交换的是\"窗口↔归属相机·点位号\"\r\n" +
                  "的对应关系，不改相机自身的点位/程序表）。再次点击\"交换位置\"按钮可取消交换模式。"
                : HintDefault;
            RefreshCells();
        }

        /// <summary>
        /// 交换两个窗口的点位（V2.13；V2.13.1 起放开跨相机；V2.14.18 起支持空窗口）：**任意两窗口**
        /// （含跨相机、含空窗口）直接互换它们对应的 (归属相机, 点位号)。
        /// 为什么跨相机允许（V2.13.1 修正）：窗口↔点位映射用"归属相机+点位号"**二元组**区分同名点位
        /// （上相机·点位3 与下相机·点位3 是不同的点位），运行时反查键 = (CameraId, StationNo)，
        /// 两窗口互换只是互换映射值、值集合不变且每个值仍只占一个窗口，所以"相机+点位→窗口"反查
        /// 保持唯一、不会混乱——V2.13 曾误判"跨相机交换会让反查语义混乱"而禁止，经复核该担心不成立。
        /// 交换位置【不改变相机和点位的对应关系】（各相机点位表 / 程序映射 ModelStationPrograms 不动），
        /// 只改变【窗口和点位的对应关系】（写回 WindowPointMaps），与"编辑点位"同语义、只是快速互换。
        /// 【空窗口（V2.14.18）】空窗口条目为 null，**可以参与交换**：跟有点位的窗口互换后，
        /// 点位搬进空窗口、原窗口变成空窗口——这是"把点位放到空窗口"的入口（编辑点位不支持空窗口）。
        /// 被禁用的窗口照常参与交换（交换的是点位归属，与启用状态无关）。
        /// 【V2.13.4】交换条目以相机ID（CameraId）为关联键，跨相机交换后反查键 (CameraId,StationNo)
        /// 仍唯一（值集合不变）。
        /// </summary>
        private void SwapCells(int a, int b)
        {
            if (a == b) return;
            var map = _windowEditMap();
            if (a < 0 || a >= map.Count || b < 0 || b >= map.Count) return;
            // 直接互换两个槽位的条目引用（条目可为 null=空窗口；与 EditSelectedPoint 的互换同语义）。
            var tmp = map[a];
            map[a] = map[b];
            map[b] = tmp;
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
            var def = DisplayConfig.DefaultWindowPointMap(_cameras, _matrixModel, _windowCount);
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
        /// 【空窗口（V2.14.18）】未配置点位的格子浅灰底 + "空窗口（无点位）"，可用【交换位置】
        /// 把点位搬进来（编辑点位/禁用启用不支持空窗口，见 UpdateActionButtons）。
        /// </summary>
        private void RefreshCells()
        {
            for (int i = 0; i < _windowCount; i++)
            {
                int r = i / _cols, c = i % _cols;
                var b = _cells[r, c];
                bool disabled = i >= _enabled.Count || !_enabled[i];
                bool empty = IsEmptyWindow(i + 1);
                if (disabled)
                {
                    // 禁用：灰底 + 灰字，醒目区分于普通格子
                    b.Text = $"窗口 {i + 1}\r\n已禁用";
                    b.BackColor = Color.FromArgb(222, 222, 222);
                    b.ForeColor = Color.FromArgb(150, 150, 150);
                    b.FlatStyle = FlatStyle.Flat;
                }
                else if (empty)
                {
                    // 空窗口（V2.14.18）：主界面占位格子、未配置点位（默认=非自适行列乘积多出的格），
                    // 浅灰底 + 灰字提示，可用【交换位置】把点位搬进来
                    b.Text = $"窗口 {i + 1}\r\n空窗口（无点位）";
                    b.ForeColor = Color.FromArgb(140, 140, 140);
                    b.BackColor = (i == _selectedIdx)
                        ? Color.FromArgb(255, 240, 200)
                        : Color.FromArgb(245, 245, 245);
                    b.UseVisualStyleBackColor = (i != _selectedIdx);
                }
                else
                {
                    b.Text = $"窗口 {i + 1}\r\n{ResolveWindowSource(i + 1)}";
                    b.ForeColor = Color.Black;
                    b.BackColor = (i == _selectedIdx)
                        ? Color.FromArgb(255, 224, 130)
                        : SystemColors.Control;
                    b.UseVisualStyleBackColor = (i != _selectedIdx);
                }
            }
            UpdateActionButtons();
        }

        /// <summary>
        /// 按当前选中格子类型刷新底部操作按钮可用状态（V2.14.18 空窗口支持）：
        ///   - 空窗口（未配置点位）：没有点位可编辑、也没有"点位坏了停用"的语义，
        ///     【编辑点位】【禁用/启用】置灰不可用；
        ///   - 【交换位置】【恢复默认】始终可用——交换位置可以把点位搬进空窗口（含空↔空无效果），
        ///     恢复默认重置铺排不影响。
        /// 无选中（_selectedIdx&lt;0）时视为"没有选中空窗口"，按钮全部恢复可用（点了再提示先选中）。
        /// </summary>
        private void UpdateActionButtons()
        {
            bool selEmpty = _selectedIdx >= 0 && IsEmptyWindow(_selectedIdx + 1);
            btnEditPoint.Enabled = !selEmpty;
            btnDisable.Enabled = !selEmpty;
            btnSwap.Enabled = true;
            btnReset.Enabled = true;
        }

        /// <summary>某号窗口（1 起）在当前铺排型号映射里是否"空窗口"（null 条目=未配置点位，
        /// V2.14.18）：非自适下点位不够、行列乘积多出的格子默认就是空窗口；空窗口不参与编辑
        /// 点位/禁用启用，只可交换位置。</summary>
        private bool IsEmptyWindow(int w)
        {
            if (_windowPointEdits.TryGetValue(_matrixModel, out var map) && map != null)
                return w < 1 || w > map.Count || map[w - 1] == null;
            return false;
        }

        /// <summary>
        /// 解析"窗口 w(1 起) → 相机名·点位号"显示文案（V2.12.1 起自适应/非自适应统一；
        /// V2.13 起改从窗口↔点位编辑副本 _windowPointEdits 查，支持手动编辑/交换后的标注；
        /// V2.14.18 空窗口返回"空窗口（无点位）"）。
        /// 默认铺排（未编辑）= 前上相机后下相机，与旧"相机点位表区间"标注等价。
        /// 型号用 _matrixModel（随"程序映射区"型号下拉联动，切型号标注一起刷新）。
        /// 解析失败（编辑副本缺失/越界）兜底显示窗口编号，只影响展示、不影响配置。
        /// </summary>
        private string ResolveWindowSource(int w)
        {
            if (_windowPointEdits.TryGetValue(_matrixModel, out var map)
                && map != null && w >= 1 && w <= map.Count)
            {
                var it = map[w - 1];                 // Points[i] = 窗口 i+1 → (相机ID,点位号)
                if (it == null) return "空窗口（无点位）";   // 空窗口（V2.14.18）
                var cam = FindCameraById(it.CameraId);
                if (cam != null)
                {
                    string camName = string.IsNullOrWhiteSpace(cam.Name)
                        ? ((cam.CameraId > 0) ? $"相机{cam.CameraId}" : $"相机{IndexOfCameraById(it.CameraId) + 1}")
                        : cam.Name;
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
                        ? ((_cameras[i].CameraId > 0) ? $"相机{_cameras[i].CameraId}" : $"相机{i + 1}")
                        : _cameras[i].Name;
                    return $"{camName}·点位{(it == null ? w : it.StationNo)}";
                }
            }
            return $"窗口{w}";
        }

        /// <summary>按相机ID在配置列表里找相机；找不到返回 null。兜底规则与 ProductionCoordinator
        /// 的 CameraIdFor 一致（CameraId>0 用真编号，0 回退行序），保证编辑显示与运行时反查同钥匙。</summary>
        private CameraConfig FindCameraById(int cameraId)
        {
            if (cameraId <= 0 || _cameras == null) return null;
            for (int i = 0; i < _cameras.Count; i++)
            {
                if (_cameras[i] != null)
                {
                    int id = _cameras[i].CameraId > 0 ? _cameras[i].CameraId : i + 1;
                    if (id == cameraId) return _cameras[i];
                }
            }
            return null;
        }

        /// <summary>按相机ID反查相机列表下标（0 起）；找不到返回 -1。见 FindCameraById。</summary>
        private int IndexOfCameraById(int cameraId)
        {
            if (cameraId <= 0 || _cameras == null) return -1;
            for (int i = 0; i < _cameras.Count; i++)
            {
                if (_cameras[i] != null)
                {
                    int id = _cameras[i].CameraId > 0 ? _cameras[i].CameraId : i + 1;
                    if (id == cameraId) return i;
                }
            }
            return -1;
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
                        emptySlots.Add($"相机「{(string.IsNullOrWhiteSpace(cam.Name) ? (cam.CameraId > 0 ? "相机" + cam.CameraId : "相机" + (i + 1)) : cam.Name)}」型号「{kv.Key}」");
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