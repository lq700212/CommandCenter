using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 系统设置窗体：直接编辑 AppConfig（引用同一实例，保存由上层 ConfigStore 完成）。
    ///
    ///     ┌─────────────────────────────────────────────────────────────┐
    /// │ PLC IP:  [19.87.6.1]  端口:[502] [产品型号配置…]                │
    /// │ 显示窗口: 行[4] 列[7] [√自适应]                              │
    /// │ 图片保存根目录: [E:\Images]                                    │
    /// │ 目录结构: [配置目录结构...] {年月日}/{SN}/{OKNG}             │
    /// │           ↑ 下方与文件名模板行留 12px 空隙（上下一致）      │
    /// │ 文件名模板:   [{点位}]   （占位符提示见界面）              │
    /// │ 窗口点位: [窗口/点位配置...] [√显示窗口编号] [√悬停提示] 点格改存图点位/可交换窗口位置 │
    /// │ OK/NG显示: [√标题栏高亮] [√窗口徽标]                        │
    /// │ 相机列表: ┌────┬────────┬────┬──────────┬────────────────────────┐ │
    /// │            │相机ID│ 相机IP│端口│ 取图方式  │ FTP上传目录            │ │
    /// │            ├────┼────────┼────┼──────────┼────────────────────────┤ │
    /// │            │ 2  │ 192…   │8500│ Ftp      │ D:\…\ftp\cam2          │ │
    /// │            └────┴────────┴────┴──────────┴────────────────────────┘ │
    /// │            [添加一台] [删除选中]                                     │
    /// │ 扫码枪列表(TCP): ┌────┬────────┬──────┬──────────┐               │
    /// │                   │启用│ IP     │ 端口 │ 触发指令 │               │
    /// │                   └────┴────────┴──────┴──────────┘               │
    /// │                   [添加一台] [删除选中]                            │
    /// │ 扫码枪列表(串口): ┌────┬────────┬────────┬────────┬────────┐     │
    /// │                   │启用│ 串口名 │ 波特率 │ 停止位 │ 校验位 │     │
    /// │                   └────┴────────┴────────┴────────┴────────┘     │
    /// │                   [添加一台] [删除选中]                            │
    /// │            （内容超窗体高度时右侧自动出竖滚动条，保存/取消固定底部）│
    /// └─────────────────────────────────────────────────────────────┘
    /// 布局（静态控件）在 SettingsForm.Designer.cs 里可视化维护；
    /// 本文件只负责"数据 ↔ 控件"：构造时把 AppConfig 填进界面（LoadFromConfig），
    /// 点保存回写（OnSave，仅改内存对象，返回 DialogResult.OK，上层写盘并热生效 V1.6.0 免重启）。
    /// 相机行数即相机台数：多台直接加行，各配各的 IP / 触发端口 / FTP 上传目录。
    /// 相机ID列（V2.13.4）=基恩士相机真正编号（上=2、下=1，与存图目录号一致，见
    /// CameraConfig.CameraId），独立存字段、不随行序；0 时按行序回退展示。
    /// 主界面显示规则"有名称显名称（上相机/下相机）、无名称显相机N"中的 N 优先用相机ID。
    /// 扫码枪行数即扫码枪台数（V1.8.1 起）：启用勾选=是否接入。
    /// V1.12.8 起 TCP 与串口拆为两张表（gridScannersTcp / gridScannersSerial），方式由所在表决定，
    /// 不再有"方式"下拉列——解决同一张表行间切 Tcp/Serial 导致整列显隐混乱的 bug。
    /// TCP 表配 IP/端口/触发指令，串口表配串口名/波特率/停止位/校验位（与相机配置风格一致）。
    /// 内容区 pnlScroll(AutoScroll) 超高自动出竖滚动条，保存/取消固定在底部 pnlBottom 不随滚动。
    /// "配置目录结构..."按钮打开 DirTreeEditForm，可视化编辑目录层级与文件名规则，
    /// 返回后把当前目录结构刷进该按钮的 ToolTip（原常驻预览标签 lblDirPreview 已删，
    /// 界面说明统一用悬停气泡，见 SettingsForm.Designer.cs 的 tip）。
    /// "窗口/点位配置..."按钮打开 WindowPointForm，可视化设置每个窗口的存图点位
    /// （默认点位=窗口编号，可自定义、可交换窗口位置，见 DisplayConfig.WindowStationMap）。
    /// </summary>
    public partial class SettingsForm : Form
    {
        private readonly AppConfig _cfg;

        // V2.14.24：设置页不再摆"产品型号"下拉（lblModel+cmbModel 已删）——当前型号唯一入口是
        // 主界面标题栏型号下拉 cmbModel（MainForm.SwitchModel 写 _cfg.ProductModel + 写盘），
        // 型号集合（增删/序号映射）统一在"产品型号配置…"弹窗（btnModelConfig → ModelIndexEditForm）
        // 维护。本字段 = 设置窗体打开的瞬间拿到的"当前运营型号"快照，作为：
        //   ① UpdateAutoFitUi 自适应铺排计算用的型号；
        //   ② 打开"窗口/点位配置"（WindowPointForm）时的初始型号；
        //   ③ OnSave 写 _cfg.ProductModel 的值（保存不改变当前型号，只原样写回）。
        // 取值优先级 = MainForm 标题栏当前选中值 > 配置 ProductModel > 预置第一候选（U171）。
        // WindowPointForm 里切型号经 modelLink 回调更新本字段（延迟生效，点保存才落盘）。
        private string _currentModel;

        /// <summary>
        /// 相机表行 Tag（V2.13.8 排序解耦）：绑定"来源配置对象 + 原始配置下标"。
        /// 【为什么需要原始下标】相机表在展示时按 CameraId 升序排序（LoadCameraRows），但
        /// "前上相机后下相机"默认铺排（DefaultWindowPointMap / AutoFitCameraStarts）依赖相机
        /// 【列表顺序】——若把排序后的表格行序直接回写 _cfg.Cameras，列表就变成 [下,上]，
        /// 任何重新生成默认铺排的路径（恢复默认/点位表长度变化重置）都会得到"先下后上"的翻转铺排，
        /// 窗口编号语义与 WindowEnabled 禁用错位。故保存时（CollectCamerasFromGrid）按本字段恢复
        /// 原始配置顺序，排序只影响"展示顺序"、不影响"持久化顺序"，两处彻底解耦。
        /// - Config：来源 CameraConfig 引用（复用保留 WindowPointForm 配好的 StationPrograms 映射表）；
        /// - OriginalIndex：该相机在 _cfg.Cameras 里的原始下标；-1 = 本次新增行（无原始下标，保存时排最后）。
        /// </summary>
        private class CameraRowTag
        {
            public CameraConfig Config;
            public int OriginalIndex = -1;
        }

        public SettingsForm(AppConfig cfg, string titleBarModel = null)
        {
            _cfg = cfg;
            // 当前型号快照（替代已删除的设置页"产品型号"下拉的取值逻辑）：
            // 主界面标题栏选中值 > 配置 ProductModel > 预置第一候选（保证恒非空、与主界面标题栏一致）
            _currentModel = (string.IsNullOrWhiteSpace(titleBarModel) ? cfg.ProductModel : titleBarModel) ?? "";
            if (string.IsNullOrWhiteSpace(_currentModel))
            {
                var defs = AppConfig.DefaultProductModels();
                if (defs != null && defs.Count > 0) _currentModel = defs[0];
            }

            InitializeComponent();          // 先解析设计器里的控件

            LoadFromConfig();               // 把当前配置值填进各输入框
            WireButtonEvents();             // 添加/删除相机按钮事件
            ApplyLanguage();                // V2.15.0 国际化：按配置语言初始化本窗体全部文本
        }

        /// <summary>把现有配置值填充到控件（配置来源是上层传来的 _cfg 实例）。</summary>
        private void LoadFromConfig()
        {
            // PLC 基础参数
            txtPlcIp.Text = _cfg.Plc.IpAddress;
            nudPlcPort.Value = _cfg.Plc.Port;
            // 显示窗口行列（V2.12.0 自适应开关：勾选后行/列输入框置灰，行列按相机点位表自动算）
            chkAutoFit.Checked = _cfg.Display.AutoFit;
            UpdateAutoFitUi();
            // OK/NG 显示配置（V1.5.0：标题栏 OK/NG 计数色块高亮开关；
            // V2.10.3：主界面窗口右下角 OK/NG 徽标显隐开关）
            chkTitleOkNg.Checked = _cfg.Display.TitleOkNgHighlight;
            chkWindowOkNg.Checked = _cfg.Display.WindowOkNgVisible;
            // V2.10.4：主界面窗口左上角窗口编号显示开关（默认开，与历史画面一致）
            chkWindowIndex.Checked = _cfg.Display.WindowIndexVisible;
            // V2.10.8：窗口悬停气泡提示开关（默认开，与历史行为一致）
            chkWindowToolTip.Checked = _cfg.Display.WindowToolTipVisible;
            // 图片保存根目录、目录结构与文件名模板（目录结构用只读预览，实际编辑进可视化对话框）
            txtSaveDir.Text = _cfg.Image.SaveRootDir;
            RefreshDirPreview();
            // V2.15.12：文件名模板框英文界面显示英文占位符（如 {Station}），保存时还原中文（见 OnSave）
            txtFileNameTpl.Text = PlaceholderLocalizer.ToDisplay(_cfg.Image.FileNameTemplate);
            // 相机表格：先建列，再逐行填数据
            SetupCameraGridColumns();
            LoadCameraRows();
            // 扫码枪表格：先建列，再逐行填数据（V1.8.1 起支持多台）
            SetupScannerGridColumns();
            LoadScannerRows();
            // V2.15.0 界面语言：切换入口在主界面标题栏（btnToggleLanguage，V2.15.1 移出本窗体），
            // 这里不需要任何语言初始化；I18n.Language 由主界面维护，保存时随 _cfg.Language 兜底写盘。
        }

        /// <summary>
        /// 同步"自适应"勾选与相关控件的可用状态（V2.12.0；V2.12.1 统一模型；V2.13 放开点位编辑；
        /// V2.14.18 非自适窗口总数=行列乘积）：
        ///   - 窗口总数 = ResolveLayout.windowCount：自适应 = 各相机按当前型号点位表条目和；
        ///     非自适 = 手填行×列（放不下点位时自动补行），点位不够多出的格子是【空窗口】——
        ///     主界面照样建窗占满显示区，空窗口默认无点位、可用"窗口/点位配置"的【交换位置】分配；
        ///   - 【勾选自适应】行/列输入框置灰（行列由相机点位表自动算，回填只读参考值）；
        ///   - 【V2.13 起】点位编辑（编辑/交换/恢复默认）在自适应与非自适应下【都可用】——
        ///     只影响矩阵行列形状，不影响"窗口↔点位"编辑（结果存 DisplayConfig.WindowPointMaps）。
        /// 本方法在 LoadFromConfig 与 CheckedChanged 两处调用；内部的 AutoFitLayout 用 _cfg.Cameras
        /// 计算（保存前表格未提交的新增相机不参与，行列只是给用户看的参考值，不写回配置）。
        /// </summary>
        private void UpdateAutoFitUi()
        {
            bool fit = chkAutoFit.Checked;
            nudRows.Enabled = !fit;
            nudCols.Enabled = !fit;
            if (fit)
            {
                var layout = DisplayConfig.AutoFitLayout(_cfg.Cameras, _currentModel ?? "");
                nudRows.Value = Math.Max(1, Math.Min(10, layout.rows));
                nudCols.Value = Math.Max(1, Math.Min(7, layout.cols)); // 自适应列数上限 7（V2.14.15 与手填一致）
            }
            else
            {
                // 非自适：按配置手填值回填。行数上限 10、列数上限 7（V2.14.15 起列数与自适应一致
                // 最多 7 列）；配置被手改成超限值时先钳到上限再赋给 nud.Value，防止越界抛异常。
                nudRows.Value = Math.Max(1, Math.Min(10, _cfg.Display.Rows));
                nudCols.Value = Math.Max(1, Math.Min(7, _cfg.Display.Columns));
            }
            // V2.13：点位编辑已放开（两模式都可编辑，见 WindowPointForm），ToolTip 说明可编辑能力
            tip.SetToolTip(btnEditPoints, AutoFitPointsButtonTipText());
        }

        /// <summary>勾选"自适应"后弹出的提示文案：明示自适下的行为（V2.12.1 统一模型说明；V2.13 更新；V2.14.18 非自适=行列乘积；V2.15.0 国际化改方法按语言返回）。</summary>
        private string AutoFitDisabledHintText()
        {
            return I18n.T(
            "已开启【自适应】：窗口矩阵按\"当前产品型号 + 各相机点位表\"自动铺排（行列自动算）。\r\n" +
            "自适应只影响【行/列形状】，不影响【点位配置】：\r\n" +
            "· 显示窗口 行/列（勾选自适应时由系统自动计算；不勾时手填【行×列】即窗口总数，\r\n" +
            "   点位不够多出的格子是\"空窗口\"——主界面照常建窗占满显示区，可在\"窗口/点位配置\"里\r\n" +
            "   用【交换位置】把点位分配过去）；\r\n" +
            "· 窗口/点位配置里的【编辑点位】【交换位置】【恢复默认】两模式下都可用（结果存按型号的 WindowPointMaps）；\r\n" +
            "  空窗口不支持【编辑点位】【禁用/启用】（选中时按钮自动置灰），只可【交换位置】。\r\n" +
            "仍可用：【禁用/启用】窗口、相机程序映射（点位→程序号）。",
            "Auto Fit enabled: the window matrix is laid out automatically by the current product model +\r\n" +
            "each camera's point table (rows/columns are auto-computed).\r\n" +
            "Auto Fit only affects the row/column shape, not point assignment:\r\n" +
            "· Display rows/columns (auto-computed when checked; otherwise manual rows×columns = total windows;\r\n" +
            "   extra cells are \"empty windows\" — still shown to fill the display area, assign points to them\r\n" +
            "   via Swap Position in Window/Point Config);\r\n" +
            "· Edit Point / Swap Position / Reset Default work in both modes (stored per-model in WindowPointMaps);\r\n" +
            "   Empty windows do NOT support Edit Point / Disable (buttons greyed out), only Swap Position.\r\n" +
            "Still available: Disable/Enable windows, camera program mapping (point → program).");
        }

        /// <summary>"窗口/点位配置..."按钮 ToolTip（V2.12.1 统一模型；V2.13 起支持手动编辑；V2.13.1 交换放开跨相机；V2.14.2 编辑点位自动互换 + 取消不生效；V2.15.0 国际化改方法按语言返回）。</summary>
        private string AutoFitPointsButtonTipText()
        {
            return I18n.T(
            "窗口/点位配置：格子显示\"归属相机·点位号\"，矩阵跟随【型号】联动。\r\n" +
            "默认按\"前上相机后下相机\"铺排；可【编辑点位】（从相机点位表全部点位里选，选到被占用点位\r\n" +
            "自动与该窗口互换）、【交换位置】（任意两窗口互换点位，含跨相机；交换的是\"窗口↔点位\"对应，\r\n" +
            "不改相机点位/程序表）、【恢复默认】（重置该型号铺排并全部启用）、【禁用/启用】窗口、\r\n" +
            "并配置【相机程序映射】（相机+型号 → 点位 → 相机程序号）。改动点【确定】才写回，点【取消】不生效。",
            "Window/Point Config: each cell shows \"camera·point number\", the matrix follows the selected model.\r\n" +
            "Default layout: up camera first, then down camera. You can Edit Point (pick from all points of the\r\n" +
            "model's camera tables; picking an occupied point auto-swaps with that window), Swap Position\r\n" +
            "(swap two windows' points, including cross-camera; swaps the window↔point mapping, does NOT change\r\n" +
            "camera point/program tables), Reset Default (reset this model's layout and enable all windows),\r\n" +
            "Disable/Enable windows, and configure Camera Program Mapping (camera+model → point → program).\r\n" +
            "Changes apply on OK; Cancel keeps the old values.");
        }

        /// <summary>
        /// 刷新"配置目录结构..."按钮的 ToolTip：把当前目录结构（层级用 / 拼接）动态挂到按钮上，
        /// 现场鼠标悬停即可查看当前配置，界面不再占用常驻标签行。
        /// </summary>
        private void RefreshDirPreview()
        {
            var dirs = _cfg.Image.SubDirs ?? new List<string>();
            // V2.15.12：英文界面目录结构预览同步显示英文占位符（与 DirTreeEditForm 同一 PlaceholderLocalizer）
            string cur = dirs.Count > 0 ? string.Join("/", dirs.Select(PlaceholderLocalizer.ToDisplay)) : I18n.T("（未配置）", "(not configured)");
            // V2.15.0 国际化：前缀文案双语，动态"当前结构"拼接在后（与语言无关的部分保持原文）
            tip.SetToolTip(btnEditDirs, I18n.T(
                "可视化编辑存图目录结构（目录层级列表 + 文件名规则），并实时预览 OK/NG 落盘路径。\r\n当前结构：" + cur,
                "Edit image directory structure (level list + file name rules) with a live OK/NG preview.\r\nCurrent structure: " + cur));
        }

        /// <summary>给相机表格建好列结构（列固定，运行时加一次即可，不用进设计器序列化）。
        /// 注意：旧版的"点位号"列已移除——存图点位统一由"窗口/点位配置…"（WindowStationMap）驱动；
        /// "取图方式"列（V1.7.0）原是 Ftp/Tcp 下拉，V1.12.18 起现场只保留 FTP 取图
        /// （相机 FTP 推图 0000.jpeg+0000.iv4p），故下拉只留 Ftp、不再提供 Tcp 直读选项；
        /// "程序号"列 V1.12.25 起已移除——点位→相机程序号改由"窗口/点位配置…"下半区按相机分表
        /// （CameraConfig.StationPrograms）配置，废弃字段 ProgramNo 仅保留做旧配置兼容读入、不再写回。</summary>
        private void SetupCameraGridColumns()
        {
            // 仅在还没有"相机IP"列时初始化，保证重复调用不会越建越多
            if (gridCameras.Columns["IpAddress"] == null)
            {
                // V2.13.4：第一列=相机ID（基恩士真编号，上=2/下=1，与存图目录号一致）。
                // V1.12.23 曾是"序号列=相机ID=列表行序"；V2.13.4 起编号独立存 CameraConfig.CameraId，
                // PLC 通道地址也独立存各相机 PlcRequestAddress（不再按列表位置自动分配）——列表顺序
                // 彻底自由，点位/通道/显示都以相机ID为准，编辑/显示都用 CameraId，空值回退行序展示。
                gridCameras.Columns.Add("CameraId", "相机ID");
                gridCameras.Columns["CameraId"].ReadOnly = false; // 可编辑：现场相机编号可能调整
                // Fill 模式下 Width 会被覆盖，必须用【FillWeight + MinimumWidth】控制列宽：
                // 相机ID 列给最小权重 1 + 下限 40px（不设 FillWeight 会用默认 100，把列宽全抢走，
                // 这就是之前"序号列超宽"的根因）。✓ 先设 MinimumWidth 再设 FillWeight。
                gridCameras.Columns["CameraId"].MinimumWidth = 40;
                gridCameras.Columns["CameraId"].FillWeight = 1;
                // Fill 模式按 FillWeight 比例分剩余宽度（窗体已加宽到 960）：
                // FTP 目录路径最长、PLC 两列次之，给大权重；IP/端口/取图方式/相机名适中，序号最小。
                gridCameras.Columns.Add("Name", "相机名称(上/下)");
                gridCameras.Columns["Name"].FillWeight = 3;
                gridCameras.Columns.Add("IpAddress", "相机IP");
                gridCameras.Columns["IpAddress"].FillWeight = 3;
                gridCameras.Columns.Add("CommandPort", "触发端口");
                gridCameras.Columns["CommandPort"].FillWeight = 2;
                gridCameras.Columns.Add("FtpUploadDir", "FTP取图目录（留空用全局目录）");
                gridCameras.Columns["FtpUploadDir"].FillWeight = 7;
                // 取图方式：现场只保留 Ftp（V1.12.18 起相机 FTP 推图是唯一取图方式）
                var srcCol = new DataGridViewComboBoxColumn
                {
                    Name = "ImageSource",
                    HeaderText = "取图方式",
                    SortMode = DataGridViewColumnSortMode.NotSortable // 组合列无意义，禁排序
                };
                srcCol.Items.Add("Ftp");
                gridCameras.Columns.Add(srcCol);
                gridCameras.Columns["ImageSource"].FillWeight = 2;
                // V2.12.6 每台相机一路 PLC 通道：请求/结果 DataStore 索引（PLC 协议号=索引+40000）。
                // V2.13.4 起【显式配置，废除"0=按相机序号自动"】：0=该相机通道未配置、不参与轮询。
                // 现场上相机=2/5（协议40002/40005）、下相机=3/6（协议40003/40006），默认配置已预置；
                // 新增相机必须与 PLC 梯形图协商好寄存器后在此填写，否则该相机不会收到 PLC 请求。
                gridCameras.Columns.Add("PlcRequestAddress", "PLC请求索引(0=未配置/必填)");
                gridCameras.Columns["PlcRequestAddress"].FillWeight = 4;
                gridCameras.Columns.Add("PlcResultAddress", "PLC结果索引(0=未配置/必填)");
                gridCameras.Columns["PlcResultAddress"].FillWeight = 4;
            }
        }

        /// <summary>把现有相机配置逐行填进表格，方便现场看着改。
        /// 第一列=相机ID（V2.13.4）：显示 CameraConfig.CameraId（基恩士真编号，上=2/下=1）；
        /// CameraId=0（旧配置没存这个字段）时按行序回退显示，保证老配置文件也能正常看。
        /// 主界面按"有名称显名称、无名称显相机N"对应（N 优先 CameraId、其次行序）。
        /// 空表格时按现场默认两台相机（V1.12.22；V2.13.3 修正 FTP 目录：上=19.87.6.213→D:\IV存图\2、
        /// 下=19.87.6.212→D:\IV存图\1）填两行模板行。
        /// 【行 Tag=原 CameraConfig 引用（V1.12.26）】每一行把来源配置对象挂到 Tag 上，
        /// 保存时优先复用该对象（保留 WindowPointForm 配好的 StationPrograms 映射表），
        /// 新增行 Tag=null→保存时按新相机建空表。防止"配好映射→点保存→映射全丢"。</summary>
        private void LoadCameraRows()
        {
            int seq = 0; // 行序兜底编号：CameraId 为 0 时按行序显示
            // V2.13.8：相机列表按 CameraId 升序展示/保存（1,2,3,…）。
            // 【为什么安全】点位映射（WindowPointMaps）、PLC 通道地址（PlcRequestAddress/
            //   PlcResultAddress）、存图目录（{相机} 层按 Name/CameraId）全部以 CameraId（或配置
            //   对象本身）为关联键，与列表顺序无关（V2.13.4/2.13.5 已彻底解耦）——这里排序只整理
            //   "显示顺序 + 保存顺序"，不改任何相机字段；排序后点保存，CollectCamerasFromGrid 按
            //   表格行序收集，cameras 配置也随之升序落盘。
            // 排序键：CameraId>0 用真编号升序；0（旧配置/未填编号）视为"无编号"排最后
            //   （按原相对顺序稳定，用原始下标打破相等）。
            var cams = new List<CameraConfig>(_cfg.Cameras ?? new List<CameraConfig>());
            int[] order = new int[cams.Count];
            var key = new List<int>(cams.Count);
            for (int i = 0; i < cams.Count; i++)
            {
                order[i] = i;
                key.Add(cams[i] != null && cams[i].CameraId > 0 ? cams[i].CameraId : 1000000 + i);
            }
            Array.Sort(order, (a, b) =>
            {
                int ka = key[a], kb = key[b];
                if (ka != kb) return ka.CompareTo(kb);
                return a.CompareTo(b); // 同编号（含未编号行）：按原顺序稳定
            });

            foreach (int idx in order)
            {
                var c = cams[idx];
                seq++;
                // V2.13.4：优先显示 CameraId（>0），0 时回退行序（旧配置/未填编号的新相机）
                int camId = c.CameraId > 0 ? c.CameraId : seq;
                // ImageSource 为空（旧配置）时按 Ftp 兜底显示
                string src = string.IsNullOrWhiteSpace(c.ImageSource) ? "Ftp" : c.ImageSource;
                var row = gridCameras.Rows[gridCameras.Rows.Add(camId, c.Name, c.IpAddress, c.CommandPort, c.FtpUploadDir, src,
                    c.PlcRequestAddress, c.PlcResultAddress)];
                // V2.13.8：Tag 绑"来源配置 + 原始下标"（排序只改展示，保存按原始顺序落盘，见 CameraRowTag 注释）
                row.Tag = new CameraRowTag { Config = c, OriginalIndex = idx };
            }
            // 至少留一行可见，别让表格空着无从下手
            if (gridCameras.Rows.Count == 0)
            {
                var defaults = CameraConfig.DefaultCameras();
                for (int i = 0; i < defaults.Count; i++)
                {
                    var c = defaults[i];
                    var row = gridCameras.Rows[gridCameras.Rows.Add(c.CameraId > 0 ? c.CameraId : ++seq, c.Name, c.IpAddress, c.CommandPort,
                        c.FtpUploadDir, "Ftp", c.PlcRequestAddress, c.PlcResultAddress)];
                    row.Tag = new CameraRowTag { Config = c, OriginalIndex = i };
                }
            }
        }

        /// <summary>
        /// 重排相机表"相机ID"列（V2.13.4）：只把 CameraId 为 0 的行按行序补齐，
        /// 已配置真编号（>0）的行保留不动——编号是相机真身份，新增/删除行不能把它冲掉。
        /// 新增/删除相机后调用，保证"没填编号的行也有可读 ID、已填编号的行不被覆盖"。
        /// </summary>
        private void RenumberCameraSeq()
        {
            int seq = 0;
            foreach (DataGridViewRow r in gridCameras.Rows)
            {
                if (r.Cells["CameraId"].Value == null) continue; // 末尾"新行"占位行跳过
                seq++;
                var cam = (r.Tag as CameraRowTag)?.Config; // V2.13.8：Tag 升级为 CameraRowTag
                // 真编号>0 保留；0 或行 Tag 都没有（全新行）→ 补行序，保证列里有数可看
                if (cam == null || cam.CameraId <= 0)
                    r.Cells["CameraId"].Value = seq;
            }
        }

        /// <summary>
        /// 从相机表格当前所有行收集相机配置列表（V1.12.26）。
        /// 【为什么要有它】① 映射页打开与保存时必须用"同一批相机对象"——表格行 Tag 上绑着来源
        /// CameraConfig（LoadCameraRows 时绑定，新增行 Tag=null），优先复用该对象并直接改字段，
        /// 从而完整保留 WindowPointForm 写回的 StationPrograms（点位→程序号映射表）不丢失；
        /// ② 打开映射页时也要传"含未保存新增相机"的列表，让新相机立刻能配它自己的映射表。
        /// 【V2.13.8 顺序恢复】收集后按 CameraRowTag.OriginalIndex 恢复【原始配置顺序】回写：
        ///   表格展示按 CameraId 升序排（LoadCameraRows），但默认铺排（DefaultWindowPointMap 等）
        ///   依赖相机列表顺序，若把排序后的行序写回 _cfg.Cameras 会翻转"前上相机后下相机"铺排。
        ///   故保存顺序恒为原始顺序（新增行排最后），排序只影响展示、不影响持久化与运行逻辑。
        /// 【约束】IP 空的行视为未填写自动剔除；复用对象时注意"废弃的 ProgramNo 不写回"。
        /// </summary>
        /// <returns>相机列表（与配置原始顺序一致，新增行排最后）</returns>
        private List<CameraConfig> CollectCamerasFromGrid()
        {
            // (相机, 原始配置下标；-1=本次新增行)
            var collected = new List<Tuple<CameraConfig, int>>();
            foreach (DataGridViewRow r in gridCameras.Rows)
            {
                string ip = r.Cells["IpAddress"].Value != null ? r.Cells["IpAddress"].Value.ToString().Trim() : "";
                if (string.IsNullOrEmpty(ip)) continue; // 空行/未填IP行忽略

                int port = 8500;
                string portTxt = r.Cells["CommandPort"].Value == null ? "" : r.Cells["CommandPort"].Value.ToString();
                if (!int.TryParse(portTxt, out port)) port = 8500;   // TryParse 失败会写 0，手动回默认
                // 取图方式：Ftp/Tcp（空值按 Ftp 兜底，与 ProductionCoordinator.IsTcpImage 判断一致）
                string imgSrc = r.Cells["ImageSource"].Value == null ? "Ftp" : r.Cells["ImageSource"].Value.ToString();

                // 复用行 Tag 上的原配置对象（保留它身上配好的 StationPrograms 映射表）；
                // 新增行（Tag=null/无 Config）才新建对象（默认空映射表，正好符合"新相机有自己的表"）。
                var tag = r.Tag as CameraRowTag;
                var cam = tag != null ? tag.Config : null;
                int origIdx = tag != null ? tag.OriginalIndex : -1;
                if (cam == null)
                {
                    cam = new CameraConfig();
                    // 关键：回绑到行 Tag，之后映射页配好的映射写回此对象，
                    // 保存时再走本方法复用同一对象，映射才不丢；新增行无原始下标 → -1（排最后）。
                    r.Tag = new CameraRowTag { Config = cam, OriginalIndex = -1 };
                }
                // V2.13.4：相机ID（基恩士真编号）从表格第一列读回；非法/空按 0（运行时回退行序）
                int camId = 0;
                string camIdTxt = r.Cells["CameraId"].Value == null ? "" : r.Cells["CameraId"].Value.ToString();
                if (!int.TryParse(camIdTxt, out camId) || camId < 0) camId = 0;
                cam.CameraId = camId;
                cam.Name = r.Cells["Name"].Value == null ? "" : r.Cells["Name"].Value.ToString().Trim();
                cam.IpAddress = ip;
                cam.CommandPort = Math.Max(1, port);
                cam.FtpUploadDir = r.Cells["FtpUploadDir"].Value == null ? "" : r.Cells["FtpUploadDir"].Value.ToString().Trim();
                cam.ImageSource = string.IsNullOrWhiteSpace(imgSrc) ? "Ftp" : imgSrc.Trim();
                // V2.12.6 每台相机一路 PLC 通道：请求/结果 DataStore 索引（0~65535；V2.13.4 起
                // 0=该相机通道未配置、不参与轮询，非法输入按 0 处理即"关掉该通道"，不再是"自动"）。
                int reqAddr = 0, resAddr = 0;
                string reqTxt = r.Cells["PlcRequestAddress"].Value == null ? "" : r.Cells["PlcRequestAddress"].Value.ToString();
                string resTxt = r.Cells["PlcResultAddress"].Value == null ? "" : r.Cells["PlcResultAddress"].Value.ToString();
                if (!int.TryParse(reqTxt, out reqAddr) || reqAddr < 0 || reqAddr > 65535) reqAddr = 0;
                if (!int.TryParse(resTxt, out resAddr) || resAddr < 0 || resAddr > 65535) resAddr = 0;
                cam.PlcRequestAddress = reqAddr;
                cam.PlcResultAddress = resAddr;
                // 注意：不再写回废弃的 ProgramNo（V1.12.25 起点位→程序号由 StationPrograms 表驱动，
                // 在"窗口/点位配置…"里配；此处不赋值则按默认 -1，保证旧值不残留误导现场）
                collected.Add(Tuple.Create(cam, origIdx));
            }

            // 恢复持久化顺序：原始下标升序（OrderBy 稳定），新增行（-1→int.MaxValue）排最后、
            // 多条新增行保持表格相对顺序。返回的列表顺序 = 下次运行 BuildServices/默认铺排看到的顺序。
            return collected
                .Select((t, i) => new { Cam = t.Item1, Orig = t.Item2 >= 0 ? t.Item2 : int.MaxValue, Seq = i })
                .OrderBy(x => x.Orig)
                .ThenBy(x => x.Seq)
                .Select(x => x.Cam)
                .ToList();
        }

        /// <summary>给两个扫码枪表格建好列结构（V1.12.8 起拆分为 TCP 表 + 串口表）。
        /// 【为什么拆两张表】V1.12.2 曾用"方式"下拉列 + 整列显隐切换：DataGridView 的列可见性
        /// 是【整列】属性，一行选 Tcp、另一行选 Serial 时只能全显所有列，混用状态下表格视觉混乱、
        /// 填错参数风险高（现场反馈异常）。拆表后：TCP 表只放网络参数、串口表只放串口参数，
        /// 各行用首列"启用"勾选控制接入，方式由"所在的表"决定（Tcp/Serial），不再需要显隐切换。</summary>
        private void SetupScannerGridColumns()
        {
            // TCP 表：网络参数列（对齐 ScanConfig 的 IpAddress/Port/TriggerCommand）
            if (gridScannersTcp.Columns["Enabled"] == null)
            {
                // 启用：勾选列，控制这台扫码枪是否接入
                gridScannersTcp.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    Name = "Enabled",
                    HeaderText = "启用",
                    Width = 50
                });
                gridScannersTcp.Columns.Add("IpAddress", "IP");
                gridScannersTcp.Columns.Add("Port", "端口");
                // 触发指令（V1.12.0）：基恩士 SR 连接后需发 LON 才读码，留空则不发
                //（对应扫码枪设成"上电自动读码"模式）
                gridScannersTcp.Columns.Add("TriggerCommand", "触发指令");
                // 读码失败文本过滤名单（V2.14.30）：逗号分隔、忽略大小写，命中=丢弃不当条码，并把扫码结果写 2 通知 PLC（死等补录）
                gridScannersTcp.Columns.Add("IgnoreScanTexts", "忽略文本");
            }

            // 串口表：串口参数列（对齐 ScanConfig 的 PortName/BaudRate/StopBits/Parity）
            if (gridScannersSerial.Columns["Enabled"] == null)
            {
                gridScannersSerial.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    Name = "Enabled",
                    HeaderText = "启用",
                    Width = 50
                });
                gridScannersSerial.Columns.Add("PortName", "串口名");
                gridScannersSerial.Columns.Add("BaudRate", "波特率");
                gridScannersSerial.Columns.Add("StopBits", "停止位");
                gridScannersSerial.Columns.Add("Parity", "校验位");
                // 读码失败文本过滤名单（V2.14.30）：与 TCP 表同含义，串口枪一样会有 ERROR/NG 状态文本
                gridScannersSerial.Columns.Add("IgnoreScanTexts", "忽略文本");
            }
        }

        /// <summary>把现有扫码枪配置分流填进两张表（V1.12.8 起）：Mode=Tcp 进 TCP 表，
        /// Mode=Serial 进串口表。两张表各至少留一行默认配置当模板——TCP 表默认用现场实测
        /// `19.87.6.100:9004 / LON`，串口表默认用模型默认串口参数（COM3/115200/1/None）。
        /// 【默认启用（V1.12.9）】TCP 模板行默认勾选"启用"：与代码默认实际使用的扫码枪一致
        /// （现场默认以太网无协议扫码枪，MainForm.BuildScanner 对 Mode=Tcp 建 ScannerTcpService，
        /// 主界面开机即接这把枪收码）；串口表模板行保持不勾选（代码默认不用串口枪，要接入再勾）。
        /// 空安全说明：Mode 为 null/空时按 TCP 处理（现场默认以太网扫码枪，防配置手改 null 崩）。</summary>
        private void LoadScannerRows()
        {
            bool hasTcp = false, hasSerial = false;
            foreach (var s in _cfg.Scanners ?? new List<ScanConfig>())
            {
                // 空安全比较：只有显式 "Serial"（大小写不敏感）才进串口表，其余（含 null/空）进 TCP 表
                if (s.Mode?.Trim().Equals("Serial", StringComparison.OrdinalIgnoreCase) == true)
                {
                    gridScannersSerial.Rows.Add(s.Enabled, s.PortName, s.BaudRate, s.StopBits, s.Parity, s.IgnoreScanTexts);
                    hasSerial = true;
                }
                else
                {
                    gridScannersTcp.Rows.Add(s.Enabled, s.IpAddress, s.Port, s.TriggerCommand, s.IgnoreScanTexts);
                    hasTcp = true;
                }
            }
            // 至少各留一行可见（V1.12.9 起 TCP 模板行"启用"默认勾选，串口行不勾）：
            // 现场扫码枪实测 IP 19.87.6.100:9004，触发指令 LON，与代码默认接入的那把枪保持一致
            if (!hasTcp)
                gridScannersTcp.Rows.Add(true, "19.87.6.100", 9004, "LON");
            if (!hasSerial)
                gridScannersSerial.Rows.Add(false, "COM3", 115200, "1", "None");
        }

        /// <summary>
        /// 挂上"添加一台/删除选中/保存"按钮的点击事件。
        /// （保存/取消 按钮的 DialogResult 已在设计器里设好；取消无需挂线）
        /// </summary>
        private void WireButtonEvents()
        {
            // V2.15.1 起语言切换入口移到主界面标题栏（btnToggleLanguage，见 MainForm），
            // 本窗体不再提供语言控件；保存时仍写 _cfg.Language（见 OnSave 前配置回写逻辑）。
            // 刚勾选时弹气泡明示"自适下哪些功能不可用"，避免误操作（见 UpdateAutoFitUi / AutoFitDisabledHint）
            chkAutoFit.CheckedChanged += (s, e) =>
            {
                UpdateAutoFitUi();
                if (chkAutoFit.Checked)
                    tip.Show(AutoFitDisabledHintText(), chkAutoFit, 150, 28, 9000);
            };

            // V2.14.14：产品型号配置按钮 → 打开"产品型号配置"弹窗，
            // 表格维护"型号名称 ↔ PLC 序号(40007)"映射（确定才写回 _cfg，见 ModelIndexEditForm）。
            btnModelConfig.Click += (s, e) =>
            {
                // 弹窗直接编辑 _cfg.Plc.ModelIndexes 引用（确定/取消见窗体内逻辑），
                // 保存由设置窗体底部【保存】统一写盘（见 OnSave），取消关窗不落盘。
                using (var dlg = new ModelIndexEditForm(_cfg.Plc.ModelIndexes))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        // 弹窗确定后：确认编辑结果已写回 _cfg.Plc.ModelIndexes。
                        // 无需额外动作——配置引用是同一个对象，保存/热更时自然带上。
                        LogHelper.Info("产品型号配置已更新：" + string.Join("/",
                            (_cfg.Plc.ModelIndexes ?? new List<ModelIndexItem>())
                            .ConvertAll(x => (x.ModelName ?? "") + "=" + x.ModelIndex)));
                    }
                }
            };

            // 添加一台相机：直接往表格追加一行默认值（默认取现场相机1：上相机 19.87.6.213 +
            // FTP 目录 D:\IV存图\2，V2.13.3 修正；上/下相机 FTP 目录与安装位置相反配对），
            // 现场改 IP/端口/取图方式即可
            btnAddCam.Click += (s, e) =>
            {
                var def = CameraConfig.DefaultCameras()[0];
                // 新行 CameraId 填 0（不硬编码默认相机 2）：走 RenumberCameraSeq 按行序兜底补号，
                // 避免添加多台时每行都复制"上相机=2"导致编号重复。保存时 0 保持不写，运行回退行序。
                gridCameras.Rows.Add(0, def.Name, def.IpAddress, 8500, def.FtpUploadDir, "Ftp");
                RenumberCameraSeq(); // 追加后给未填编号行补行序号，已配真编号行不动
            };
// 删除选中：把当前选中的行整行移除；没有选中行则什么都不做
            // 【V1.8.4 修复】末尾"新行"（AllowUserToAddRows 附带的 * 占位行）不在 SelectedRows 里，
            //   用户点击该空白行再点删除，原来会误报"未选中行"——现改为：删除=放弃该占位行。
            // V2.13.4：删除后给未填编号行补行序号（真编号行保留）
            btnDelCam.Click += (s, e) =>
            {
                DeleteSelectedRows(gridCameras, "相机");
                RenumberCameraSeq(); // 删中间某台后，未填编号行按行序前移补齐，真编号行不动
            };

            // 添加一台 TCP 扫码枪：追加一行默认配置（V1.12.8 起 TCP 独立成表；
            // 默认现场实测 IP/触发指令，V1.12.0；V1.12.9 起默认勾选"启用"——
            // 与 LoadScannerRows 模板行一致：代码默认接入的就是以太网扫码枪）
            btnAddScannerTcp.Click += (s, e) =>
            {
                gridScannersTcp.Rows.Add(true, "19.87.6.100", 9004, "LON");
            };
            // 删除选中的 TCP 扫码枪行（与相机同样的"先选中再删"交互；V1.8.4 同相机修复空白行误报）
            btnDelScannerTcp.Click += (s, e) => DeleteSelectedRows(gridScannersTcp, "扫码枪(TCP)");
            // 添加一台串口扫码枪：追加一行默认配置（V1.12.8 起串口独立成表；默认 COM3/115200）
            btnAddScannerSerial.Click += (s, e) =>
            {
                gridScannersSerial.Rows.Add(false, "COM3", 115200, "1", "None");
            };
            // 删除选中的串口扫码枪行
            btnDelScannerSerial.Click += (s, e) => DeleteSelectedRows(gridScannersSerial, "扫码枪(串口)");
            // 保存：把界面值回写内存配置，返回 DialogResult.OK（上层负责写盘与提示）
            btnSave.Click += OnSave;
            // 打开目录结构可视化配置对话框；改的是同一 _cfg.Image 实例，返回后刷新预览
            btnEditDirs.Click += (s, e) =>
            {
                using (var dlg = new DirTreeEditForm(_cfg.Image))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        RefreshDirPreview();
                }
            };
            // 打开"窗口/点位与相机程序配置"对话框（V1.12.25 起同页混排两个映射）：
            // ① 窗口↔相机点位（格子矩阵，V2.12.1 统一模型：点位由相机点位表唯一决定，
            //    编辑点位/交换位置/恢复默认锁定；矩阵跟随对话框内"型号"下拉联动刷新）；
            // ② 点位→相机程序号（每台相机各自一张表，触发时按点位切相机程序）。
            // 注意：行列数取【界面 nud 上的最新值】（用户可能刚改了行/列还没保存），
            // 而不是 _cfg.Display.Rows/Columns（那是上次已保存的旧值）——保证格子矩阵
            // 与"用户即将保存的新窗口行列形状"一致，改完行列再配置点位所见即所得。
            // 相机区传"当前表格里所有相机行"（V1.12.26：含刚新增未保存的行，均带各自 Tag 上
            // 的映射表），确定时各相机映射写回原位、点保存一起落盘；未保存的新增相机也能立刻
            // 配它自己的"点位→程序号"映射表（保存时按 Tag 复用同对象，映射不丢）。
            // V2.12.1 统一模型：把"是否自适应"与当前型号一并传入（自适应只影响行列是否自动算，
            // 窗口总数=相机点位和、点位编辑锁定则两种模式一致），详见 WindowPointForm。
            btnEditPoints.Click += (s, e) =>
            {
                // 型号变更（V2.12.x 延迟生效；V2.14.24 设置页已删"产品型号"下拉）：WindowPointForm
                // 程序映射区切了型号，只更新本窗体 `_currentModel` 字段（OnSave 时写 _cfg.ProductModel）
                // ——**不实时传给 MainForm 切运营**。主界面标题栏型号/窗口矩阵/协调器一律等用户点
                // 【保存】后由 MainForm.ApplyRuntimeConfig 统一刷新（避免配置对话框里翻型号时主界面
                // 矩阵跟着乱跳）。
                Action<string> modelLink = m =>
                {
                    if (string.IsNullOrWhiteSpace(m)) return;
                    _currentModel = m;
                };
                using (var dlg = new WindowPointForm(_cfg.Display.WindowStationMap,
                                                     (int)nudRows.Value, (int)nudCols.Value,
                                                     CollectCamerasFromGrid(),
                                                     _cfg.Display.WindowEnabled,
                                                     AppConfig.DefaultProductModels()
                                                         .Union(_cfg.ProductModels ?? new List<string>())
                                                         .ToList(),
                                                     chkAutoFit.Checked,
                                                     _currentModel ?? "",
                                                     _cfg.Display.WindowPointMaps,
                                                     modelLink))
                {
                    dlg.ShowDialog(this);
                }
            };
        }

        /// <summary>
        /// 删除 DataGridView 中选中的真实数据行（相机/扫码枪共用，V1.8.4 修复）。
        ///
        /// 【V1.8.4 修复的 bug】表格开了 AllowUserToAddRows=true，末尾会有一个"新行"
        /// （DataGridViewRow.IsNewRow，显示为带 * 的空白行）供用户直接输入新增。但：
        ///   ① 用户点击这个末尾空白行时，DataGridView 不把它放进 SelectedRows 集合；
        ///   ② 新行本身也无法用 Rows.Remove 删除（Remove 对它抛 ArgumentOutOfRange）。
        ///   因此旧实现"SelectedRows 为空就弹'未选中行'"对用户点末尾空白行再点删除的场景
        ///   是误报——用户明明选中了一行却提示没选中。
        /// 本方法的三段式处理：
        ///   1) 优先删 SelectedRows 中的真实行（整行高亮选中的正常场景）；
        ///   2) 若 SelectedRows 为空但光标（CurrentRow）停在一个真实行上，按"当前行"删除
        ///      （点中单元格即视为选中该行，与 FullRowSelect 的直觉一致）；
        ///   3) 若光标停在末尾新行上，说明用户想删的是"这个空白占位行"：临时把
        ///      AllowUserToAddRows 置 false 再恢复 true，新行会随之为空重建，等效"删掉了空白行"，
        ///      不再误报"未选中行"。
        /// 真实数据行一个都不剩时，删除后表格自然只留新行；保存时 OnSave 对空行有兜底。
        /// </summary>
        /// <param name="grid">要操作的目标表格（gridCameras / gridScanners）</param>
        /// <param name="rowName">提示文案里的行名（"相机" / "扫码枪"），便于区分两台表格</param>
        private void DeleteSelectedRows(DataGridView grid, string rowName)
        {
            // 1) 选中集合里的真实行（排除新行——新行删不了）
            var rows = grid.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow).ToList();

            // 2) 没整行选中时，光标所在真实行也算"选中"（点单元格即选中行）
            if (rows.Count == 0 && grid.CurrentRow != null && !grid.CurrentRow.IsNewRow)
                rows.Add(grid.CurrentRow);

            if (rows.Count > 0)
            {
                foreach (var r in rows)
                    grid.Rows.Remove(r);
                return;
            }

            // 3) 到这里说明 SelectedRows 与 CurrentRow 都是空的/新行——用户点的是末尾空白占位行。
            //    临时关闭自动新行再恢复：新行随之为空重建，即"删除空白行"，不再误报。
            if (grid.CurrentRow != null && grid.CurrentRow.IsNewRow)
            {
                grid.AllowUserToAddRows = false;
                grid.AllowUserToAddRows = true;
                return;
            }

            // 兜底：确实没有可删的行（表格没有焦点/没有任何行）才提示
            // V2.15.0 国际化：行名（"相机"/"扫码枪(TCP)"/"扫码枪(串口)"）按中文原值映射成英文提示文案
            string rowNameEn = rowName.Contains("TCP") ? "scanner (TCP)"
                : rowName.Contains("串口") ? "scanner (Serial)" : "camera";
            MessageBox.Show(I18n.T(
                $"请先点击表格中要删除的{rowName}行（整行高亮），再点\"删除选中\"。",
                $"Please select the {rowNameEn} row to delete (full row highlighted) first, then click \"Delete Selected\"."),
                I18n.T("提示", "Notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// V2.15.0 国际化：按当前语言刷新本窗体全部界面文字（静态标签/按钮/表格列头/悬停气泡）。
        /// 在构造函数末尾（首次按配置语言初始化）与语言下拉切换时调用；Designer 里的静态中文
        /// 文本保持原样（方便 VS 设计器维护布局），运行时统一在这里覆盖成当前语言。
        /// </summary>
        private void ApplyLanguage()
        {
            // ---- 窗体标题与静态标签/按钮（Designer 里是中文，运行时按语言覆盖）----
            this.Text = I18n.T("系统设置", "System Settings");
            lblPlcIp.Text = I18n.T("PLC IP:", "PLC IP:");
            lblPlcPort.Text = I18n.T("端口:", "Port:");
            btnModelConfig.Text = I18n.T("产品型号配置…", "Model Config…");
            lblRows.Text = I18n.T("显示窗口行:", "Display Rows:");
            lblCols.Text = I18n.T("列:", "Columns:");
            lblDir.Text = I18n.T("图片保存根目录:", "Image Root Dir:");
            btnEditDirs.Text = I18n.T("配置目录结构...", "Configure Dirs...");
            lblFile.Text = I18n.T("文件名模板:", "File Name Template:");
            lblPoints.Text = I18n.T("窗口点位:", "Window Points:");
            btnEditPoints.Text = I18n.T("窗口/点位配置...", "Window/Point Config...");
            lblOkNg.Text = I18n.T("OK/NG显示:", "OK/NG Display:");
            chkTitleOkNg.Text = I18n.T("标题栏高亮", "Title Highlight");
            chkWindowOkNg.Text = I18n.T("窗口徽标", "Window Badge");
            chkWindowIndex.Text = I18n.T("显示窗口编号", "Show Window Index");
            chkWindowToolTip.Text = I18n.T("悬停提示", "Hover ToolTip");
            chkAutoFit.Text = I18n.T("自适应", "Auto Fit");
            lblCams.Text = I18n.T("相机列表:", "Cameras:");
            btnAddCam.Text = I18n.T("添加一台", "Add");
            btnDelCam.Text = I18n.T("删除选中", "Delete Selected");
            lblScannersTcp.Text = I18n.T("扫码枪列表(TCP):", "Scanners (TCP):");
            btnAddScannerTcp.Text = I18n.T("添加一台", "Add");
            btnDelScannerTcp.Text = I18n.T("删除选中", "Delete Selected");
            lblScannersSerial.Text = I18n.T("扫码枪列表(串口):", "Scanners (Serial):");
            btnAddScannerSerial.Text = I18n.T("添加一台", "Add");
            btnDelScannerSerial.Text = I18n.T("删除选中", "Delete Selected");
            btnSave.Text = I18n.T("保存", "Save");
            btnCancel.Text = I18n.T("取消", "Cancel");

            // ---- 表格列头（列在运行时 SetupXxxGridColumns 创建，HeaderText 这里按语言刷新）----
            if (gridCameras.Columns["CameraId"] != null)
            {
                gridCameras.Columns["CameraId"].HeaderText = I18n.T("相机ID", "Cam ID");
                gridCameras.Columns["Name"].HeaderText = I18n.T("相机名称(上/下)", "Name (Up/Down)");
                gridCameras.Columns["IpAddress"].HeaderText = I18n.T("相机IP", "IP");
                gridCameras.Columns["CommandPort"].HeaderText = I18n.T("触发端口", "Trigger Port");
                gridCameras.Columns["FtpUploadDir"].HeaderText = I18n.T("FTP取图目录（留空用全局目录）", "FTP Dir (empty=global)");
                gridCameras.Columns["ImageSource"].HeaderText = I18n.T("取图方式", "Source");
                gridCameras.Columns["PlcRequestAddress"].HeaderText = I18n.T("PLC请求索引(0=未配置/必填)", "PLC Req Addr (0=off)");
                gridCameras.Columns["PlcResultAddress"].HeaderText = I18n.T("PLC结果索引(0=未配置/必填)", "PLC Res Addr (0=off)");
            }
            if (gridScannersTcp.Columns["Enabled"] != null)
            {
                gridScannersTcp.Columns["Enabled"].HeaderText = I18n.T("启用", "Enabled");
                gridScannersTcp.Columns["IpAddress"].HeaderText = "IP";
                gridScannersTcp.Columns["Port"].HeaderText = I18n.T("端口", "Port");
                gridScannersTcp.Columns["TriggerCommand"].HeaderText = I18n.T("触发指令", "Trigger");
                gridScannersTcp.Columns["IgnoreScanTexts"].HeaderText = I18n.T("忽略文本", "Ignore Texts");
            }
            if (gridScannersSerial.Columns["Enabled"] != null)
            {
                gridScannersSerial.Columns["Enabled"].HeaderText = I18n.T("启用", "Enabled");
                gridScannersSerial.Columns["PortName"].HeaderText = I18n.T("串口名", "Port");
                gridScannersSerial.Columns["BaudRate"].HeaderText = I18n.T("波特率", "Baud");
                gridScannersSerial.Columns["StopBits"].HeaderText = I18n.T("停止位", "StopBits");
                gridScannersSerial.Columns["Parity"].HeaderText = I18n.T("校验位", "Parity");
                gridScannersSerial.Columns["IgnoreScanTexts"].HeaderText = I18n.T("忽略文本", "Ignore Texts");
            }

            // ---- 悬停气泡（Designer 里的静态中文提示，运行时按语言刷新）----
            tip.SetToolTip(txtPlcIp, I18n.T(
                "上位机从站监听绑定 IP（V1.12.11 起 PLC 做主站、上位机做从站）。\r\n填 0.0.0.0 监听所有网卡，或填本机指定 IP（如 19.87.6.230）；\r\n保存后即时生效（自动重启从站监听）。",
                "Slave bind IP (PLC is the master, this PC is the Modbus TCP slave).\r\nUse 0.0.0.0 to listen on all NICs, or a specific local IP (e.g. 19.87.6.230).\r\nTakes effect immediately after Save (slave restarts automatically)."));
            tip.SetToolTip(nudPlcPort, I18n.T(
                "上位机从站监听端口（Modbus TCP 标准 502，需与汇川主站通讯指令里的端口一致）。\r\n保存后即时生效（自动重启从站监听）。",
                "Slave listen port (Modbus TCP standard 502, must match the master's comm instruction port).\r\nTakes effect immediately after Save."));
            tip.SetToolTip(btnModelConfig, I18n.T(
                "打开【产品型号配置】对话框（V2.14.14）：用表格维护\"型号名称 ↔ PLC 序号(40007)\"映射。\r\n表格两列：序号、型号名称；前几行默认预载当前已有型号与序号，可增删改。\r\n【确定】把当前对应关系保存到配置（重启后自动加载），【取消】关闭不保存。\r\n现场默认 Z121=1、U171=2；每次扫码上位机先写 40007=本序号，再写 40008~40012=型号 ASCII 字符串。",
                "Model Config dialog: maintain the \"model name ↔ PLC index (40007)\" mapping in a table.\r\nTwo columns: index, model name. Default rows are preloaded from current config; add/edit/delete freely.\r\nOK writes back to config, Cancel discards. Site defaults: Z121=1, U171=2."));
            tip.SetToolTip(nudRows, I18n.T(
                "主界面显示窗口的行数。窗口总数=行×列；保存后即时生效。\r\n新增窗口的存图点位默认=窗口编号，可在下方\"窗口/点位配置...\"里改。\r\n勾选\"自适应\"后本框自动置灰（行数由相机点位表自动计算）。",
                "Number of rows in the main window matrix. Total windows = rows×columns.\r\nNew windows default to point = window index, changeable in Window/Point Config below.\r\nGreyed out while Auto Fit is checked (rows auto-computed from camera point tables)."));
            tip.SetToolTip(nudCols, I18n.T(
                "主界面显示窗口的列数。窗口总数=行×列；保存后即时生效。\r\n新增窗口的存图点位默认=窗口编号，可在下方\"窗口/点位配置...\"里改。\r\n勾选\"自适应\"后本框自动置灰（列数由相机点位表自动计算）。",
                "Number of columns in the main window matrix. Total windows = rows×columns.\r\nGreyed out while Auto Fit is checked (columns auto-computed from camera point tables)."));
            tip.SetToolTip(txtSaveDir, I18n.T(
                "图片保存的根目录（绝对路径）。\r\n实际目录结构按\"配置目录结构...\"里的层级逐级创建。",
                "Root directory for saved images (absolute path).\r\nSub-structure is created per the levels in Configure Dirs..."));
            tip.SetToolTip(txtFileNameTpl, I18n.T(
                "图片文件名规则，占位符会自动替换：\r\n{点位}→窗口点位号（如 1.png）  {SN}→序列号  {OKNG}→OK 或 NG\r\n{年}/{月}/{日}→日期  {时间}→毫秒时间戳；其余文字原样保留。\r\n目录结构里的层级同样支持这些占位符。",
                "Image file name rule; placeholders are replaced automatically:\r\n{Station}=window point (e.g. 1.png)  {SN}=serial  {OKNG}=OK or NG\r\n{Year}/{Month}/{Day}=date  {Time}=ms timestamp; other text is kept as-is.\r\nDirectory levels support the same placeholders."));
            tip.SetToolTip(btnAddCam, I18n.T(
                "在列表末尾添加一台相机（默认值可直接改 IP / 端口 / FTP 上传目录）。",
                "Add a camera at the end of the list (edit IP / port / FTP dir as needed)."));
            tip.SetToolTip(chkTitleOkNg, I18n.T(
                "标题栏的 OK / NG 计数用\"实心彩色色块 + 白字\"高亮（绿底=OK、红底=NG），\r\n比普通彩色文字醒目得多。取消则回退彩色文字样式。保存后即时生效。",
                "Title-bar OK/NG counters use a solid color block with white text (green=OK, red=NG).\r\nUncheck to fall back to plain colored text. Takes effect immediately after Save."));
            tip.SetToolTip(chkWindowOkNg, I18n.T(
                "主界面每个显示窗口右下角叠加一个【矩形框 OK/NG 徽标】（样子同标题栏色块，\r\n颜色随 \"OK颜色/NG颜色\" 配置）。默认开启（V2.14.24）。保存后即时生效。",
                "Each display window shows a rectangular OK/NG badge at its bottom-right corner\r\n(colors follow the OK/NG color config). On by default. Takes effect immediately after Save."));
            tip.SetToolTip(chkWindowIndex, I18n.T(
                "主界面每个显示窗口左上角是否显示【窗口编号】（半透明白底 + 深蓝灰字，辅助现场定位第几路）。\r\n默认勾选；现场嫌编号碍眼可取消勾选，保存后即时生效。",
                "Show the window number at the top-left of each display window (semi-transparent white background).\r\nOn by default; uncheck to hide. Takes effect immediately after Save."));
            tip.SetToolTip(chkWindowToolTip, I18n.T(
                "鼠标放到主界面任一显示窗口内停留片刻，是否弹出【双击放大/还原】气泡提示。\r\n默认勾选（方便新手操作员发现双击功能）；现场嫌气泡挡画面可取消勾选，保存后即时生效。",
                "Show the \"double-click to zoom\" bubble when hovering over a display window.\r\nOn by default; uncheck if the bubble blocks the view. Takes effect immediately after Save."));
            tip.SetToolTip(chkAutoFit, I18n.T(
                "勾选【自适应】后主界面窗口矩阵【不再手动指定行列】，而是按当前产品型号 + 各相机\r\n\"点位→程序号\"表自动铺排（窗口总数=各相机点位和、前上相机后下相机）。\r\n\r\n【自适应只影响行/列形状，不影响点位配置】\r\n· 显示窗口 行/列 输入框（勾选时行列由系统自动算，不勾时可手填排列宽度）；\r\n· 窗口/点位配置里的【编辑点位】【交换位置】【恢复默认】两模式下都可编辑；\r\n仍可用：【禁用/启用】窗口、相机程序映射（点位→程序号）。",
                "Auto Fit: the window matrix is no longer sized manually — rows/columns are computed from\r\nthe current model + each camera's point→program table (total = sum of camera points,\r\nup camera first then down camera).\r\n\r\nAuto Fit only affects the row/column shape, not point assignment:\r\n· Rows/columns inputs are auto-computed while checked;\r\n· Edit Point / Swap Position / Reset Default stay available in both modes;\r\nStill available: Disable/Enable windows, camera program mapping."));
            tip.SetToolTip(btnDelCam, I18n.T(
                "删除选中的相机行；未选中时先点选要删的行。",
                "Delete the selected camera row; select a row first if none is selected."));
            tip.SetToolTip(lblScannersTcp, I18n.T(
                "TCP 扫码枪列表：基恩士 SR 系列以太网扫码枪，一台一行。\r\n任何一台扫到的条码都会更新当前序列号（标题栏与存图目录同步）。\r\n\"启用\"不打勾则这台不接入。",
                "TCP scanners: KEYENCE SR ethernet scanners, one per row.\r\nAny scanned code updates the current serial number (title bar and save dirs).\r\nUnchecked \"Enabled\" rows are not connected."));
            tip.SetToolTip(lblScannersSerial, I18n.T(
                "串口扫码枪列表：RS-232 串口扫码枪，一台一行。\r\n串口扫码枪上电即读码、无需触发指令（与 TCP 不同）。\r\n\"启用\"不打勾则这台不接入。",
                "Serial scanners: RS-232 scanners, one per row.\r\nSerial scanners read on power-up and need no trigger command (unlike TCP).\r\nUnchecked \"Enabled\" rows are not connected."));
            tip.SetToolTip(btnAddScannerTcp, I18n.T(
                "添加一台 TCP 扫码枪（默认 19.87.6.100 / 9004 / LON，可直接改）。",
                "Add a TCP scanner (default 19.87.6.100 / 9004 / LON)."));
            tip.SetToolTip(btnDelScannerTcp, I18n.T(
                "删除选中的 TCP 扫码枪行；未选中时先点选要删的行。",
                "Delete the selected TCP scanner row; select a row first if none is selected."));
            tip.SetToolTip(btnAddScannerSerial, I18n.T(
                "添加一台串口扫码枪（默认 COM3 / 115200 / 1 / None，可直接改）。",
                "Add a serial scanner (default COM3 / 115200 / 1 / None)."));
            tip.SetToolTip(btnDelScannerSerial, I18n.T(
                "删除选中的串口扫码枪行；未选中时先点选要删的行。",
                "Delete the selected serial scanner row; select a row first if none is selected."));
            tip.SetToolTip(btnSave, I18n.T(
                "保存所有设置并写盘到 Config/appconfig.json，保存后即时生效（V1.6.0 免重启）。\r\n服务层按新配置自动重建，设备短暂断连后几秒内自动连回。",
                "Save all settings to Config/appconfig.json and apply immediately (no restart needed).\r\nServices rebuild on the new config; devices reconnect within a few seconds."));
            tip.SetToolTip(btnCancel, I18n.T(
                "放弃本次修改并关闭，不写盘。",
                "Discard changes and close without saving."));

            // 两个动态 ToolTip（含当前配置内容）随语言刷新
            tip.SetToolTip(btnEditPoints, AutoFitPointsButtonTipText());
            RefreshDirPreview();
        }

        /// <summary>
        /// 把界面值回写内存配置（V1.6.0：保存后由 MainForm 热生效，免重启）。</summary>
        private void OnSave(object sender, EventArgs e)
        {
            // V2.13.10【R2 拦截】相机ID唯一性校验——放在最开头、任何配置回写之前：
            // 若两台相机填了同一个 CameraId(>0)，运行时 IndexOfCamera 恒命中第一台 →
            // 相机路由/存图目录/结果通道张冠李戴（审查报告 R2）。发现重复立即提示并中止本次保存
            // （return），保证 _cfg 一个字段都不改；0（未填）不算重复——后续由
            // ConfigStore.EnsureCameraIdentity 全局唯一补号，天然不会撞。
            var camIds = new HashSet<int>();
            foreach (DataGridViewRow r in gridCameras.Rows)
            {
                if (r.IsNewRow) continue;   // 末尾"新行"占位行跳过
                string ip = r.Cells["IpAddress"].Value == null ? "" : r.Cells["IpAddress"].Value.ToString().Trim();
                if (string.IsNullOrWhiteSpace(ip)) continue; // 空行/未填IP行（收集时也会剔除）
                int id = 0;
                string idTxt = r.Cells["CameraId"].Value == null ? "" : r.Cells["CameraId"].Value.ToString();
                if (int.TryParse(idTxt, out id) && id > 0 && !camIds.Add(id))
                {
                    MessageBox.Show(I18n.T(
                        $"相机ID不能重复：已存在「相机{id}」。请把其中一台改成别的编号（>0），" +
                        "或清空该项让保存时自动补一个不重复的编号。",
                        $"Camera ID duplicated: camera {id} already exists. Change one to a different number (>0), " +
                        "or leave it empty so an unused ID is assigned automatically on save."),
                        I18n.T("相机ID重复", "Duplicate Camera ID"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return; // 中止本次保存，_cfg 未做任何回写
                }
            }

            _cfg.Plc.IpAddress = txtPlcIp.Text.Trim();
            _cfg.Plc.Port = (int)nudPlcPort.Value;
            // 固定产品型号（V2.7 协议）：保存后每次扫码上位机把型号写入 PLC 40007~40011；
            // V2.8：型号同时决定"点位→相机程序号"查哪张表。
            // V2.14.24：设置页已删"产品型号"下拉，_currentModel = 打开时主界面标题栏选中值（或
            // WindowPointForm 里改过的值）——保存只原样写回、不改变当前型号；型号集合的增删统一
            // 在"产品型号配置…"弹窗（ModelIndexEditForm → plc.modelIndexes）里做，保存时
            // ConfigStore.EnsureModelIndexes 会把型号集合与映射表双向对齐（见 ConfigStore）。
            string model = (_currentModel ?? "").Trim();
            _cfg.ProductModel = model;
            if (model.Length > 0)
            {
                var models = _cfg.ProductModels ?? (_cfg.ProductModels = new List<string>());
                if (!models.Any(x => string.Equals(x, model, StringComparison.OrdinalIgnoreCase)))
                    models.Add(model);
            }
            // V2.14.14：型号→PLC 序号映射由"产品型号配置"弹窗（btnModelConfig → ModelIndexEditForm）
            // 统一维护（确定才写回 _cfg.Plc.ModelIndexes）。这里不再对单个型号写序号——保存时
            // ConfigStore.EnsureModelIndexes 会自动补齐候选型号里缺失的映射，见 ConfigStore.Save。
            // 显示窗口行列与自适应（V2.12.0）：勾选自适应时行列由系统按相机点位表自动算、不落盘
            // （保留用户手动行列作参考，关掉自适应后仍用原手填值；不污染 Rows/Columns）。
            if (!chkAutoFit.Checked)
            {
                _cfg.Display.Rows = (int)nudRows.Value;
                _cfg.Display.Columns = (int)nudCols.Value;
            }
            _cfg.Display.AutoFit = chkAutoFit.Checked;
            _cfg.Display.TitleOkNgHighlight = chkTitleOkNg.Checked;
            _cfg.Display.WindowOkNgVisible = chkWindowOkNg.Checked; // V2.10.3：窗口右下角 OK/NG 徽标开关（V2.14.24 默认开；徽标"拿到相机结果才显示"见 CameraDisplayControl）
            _cfg.Display.WindowIndexVisible = chkWindowIndex.Checked; // V2.10.4：窗口左上角窗口编号开关
            _cfg.Display.WindowToolTipVisible = chkWindowToolTip.Checked; // V2.10.8：窗口悬停气泡提示开关
            // V2.15.0 界面语言：写盘持久化（切换入口在主界面标题栏，按钮点击即切即存；
            // 这里按当前全局语言兜底写盘，保证任何保存动作都落一次语言配置）
            _cfg.Language = I18n.Language;
            _cfg.Image.SaveRootDir = txtSaveDir.Text.Trim();
            // V2.15.12：文件名模板框英文界面显示英文占位符，保存前还原成中文（RenderTemplate 只认中文）
            _cfg.Image.FileNameTemplate = PlaceholderLocalizer.ToStorage(txtFileNameTpl.Text.Trim());
            // 目录结构由 DirTreeEditForm 直接写入 _cfg.Image.SubDirs，这里不用回写；
            // 未打开过对话框则保持 SubDirs 原值（首次为模型默认的三层）。

// 相机：从表格行收集（含未保存新增行），复用行 Tag 上的原对象保留映射表。
            // 注意：存图点位不在此配置（由"窗口/点位配置…"的 WindowStationMap 驱动，见 DisplayConfig）
            var cams = CollectCamerasFromGrid();
            if (cams.Count == 0) cams.AddRange(CameraConfig.DefaultCameras()); // 兜底：至少现场两台默认相机
            _cfg.Cameras = cams;

            // 扫码枪（V1.12.8 起拆两张表）：TCP 表行→Mode="Tcp"，串口表行→Mode="Serial"，
            // 合并成一个列表；未勾选"启用"的行也会保留进配置
            //（Enabled=false 时主程序不建实例，序列号走手动输入/模拟）。
            var scanners = new List<ScanConfig>();

            // TCP 表：IP/端口/触发指令（方式固定 Tcp，不再有"方式"下拉列）
            foreach (DataGridViewRow r in gridScannersTcp.Rows)
            {
                if (r.IsNewRow) continue; // 表格末尾的"新行"不算真实扫码枪
                bool enabled = r.Cells["Enabled"].Value is bool b && b;
                string ip = r.Cells["IpAddress"].Value == null ? "" : r.Cells["IpAddress"].Value.ToString().Trim();
                int port = 9004;
                string portTxt = r.Cells["Port"].Value == null ? "" : r.Cells["Port"].Value.ToString();
                if (!int.TryParse(portTxt, out port)) port = 9004;
                // V1.12.0 触发指令：基恩士 SR 的 LON，留空表示连上后不发指令
                string trigger = r.Cells["TriggerCommand"].Value == null ? "" : r.Cells["TriggerCommand"].Value.ToString().Trim();
                // V2.14.30 读码失败文本过滤名单：逗号分隔，漏填按默认值（真实错误文本不过滤也不会重复出现）
                string ignoreTexts = r.Cells["IgnoreScanTexts"].Value == null
                    ? "ERROR,ERR,NG,NOREAD"
                    : r.Cells["IgnoreScanTexts"].Value.ToString().Trim();
                // 全空的模板行（IP 都没填）忽略，避免保存一堆垃圾行
                if (string.IsNullOrWhiteSpace(ip)) continue;
                scanners.Add(new ScanConfig
                {
                    Enabled = enabled,
                    Mode = "Tcp",
                    IpAddress = string.IsNullOrWhiteSpace(ip) ? "19.87.6.100" : ip,
                    Port = Math.Max(1, port),
                    TriggerCommand = trigger,
                    IgnoreScanTexts = ignoreTexts
                });
            }

            // 串口表：串口名/波特率/停止位/校验位（方式固定 Serial）
            foreach (DataGridViewRow r in gridScannersSerial.Rows)
            {
                if (r.IsNewRow) continue;
                bool enabled = r.Cells["Enabled"].Value is bool b2 && b2;
                string portName = r.Cells["PortName"].Value == null ? "" : r.Cells["PortName"].Value.ToString().Trim();
                int baud = 115200;
                string baudTxt = r.Cells["BaudRate"].Value == null ? "" : r.Cells["BaudRate"].Value.ToString();
                if (!int.TryParse(baudTxt, out baud)) baud = 115200;
                string stopBits = r.Cells["StopBits"].Value == null ? "" : r.Cells["StopBits"].Value.ToString().Trim();
                string parity = r.Cells["Parity"].Value == null ? "" : r.Cells["Parity"].Value.ToString().Trim();
                string ignoreTexts = r.Cells["IgnoreScanTexts"].Value == null
                    ? "ERROR,ERR,NG,NOREAD"
                    : r.Cells["IgnoreScanTexts"].Value.ToString().Trim();
                // 全空的模板行（串口名都没填）忽略
                if (string.IsNullOrWhiteSpace(portName)) continue;
                scanners.Add(new ScanConfig
                {
                    Enabled = enabled,
                    Mode = "Serial",
                    PortName = portName,
                    BaudRate = Math.Max(1, baud),
                    StopBits = string.IsNullOrWhiteSpace(stopBits) ? "1" : stopBits,
                    Parity = string.IsNullOrWhiteSpace(parity) ? "None" : parity,
                    IgnoreScanTexts = ignoreTexts
                });
            }

            // 兜底（V1.12.9 起）：保留一条默认与界面模板行一致——TCP 现场默认扫码枪且"启用"，
            // 避免"把两张表都删空再保存"后重开设置，出现与界面（TCP 行默认勾选）不符的串口未启用条目；
            // 此前兜底是 new ScanConfig()（Mode=Serial、未启用），与界面默认展示不一致。
            if (scanners.Count == 0)
                scanners.Add(new ScanConfig
                {
                    Mode = "Tcp",
                    Enabled = true,
                    IpAddress = "19.87.6.100",
                    Port = 9004,
                    TriggerCommand = "LON"
                });
            _cfg.Scanners = scanners;
        }
    }
}