using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CommandCenter.Models;

namespace CommandCenter.Views
{
    /// <summary>
    /// 系统设置窗体：直接编辑 AppConfig（引用同一实例，保存由上层 ConfigStore 完成）。
    ///
    /// ┌─────────────────────────────────────────────────────────────┐
    /// │ PLC IP:  [19.87.6.1]  端口:[502]  产品型号:[Z1212]             │
    /// │ 显示窗口: 行[4] 列[7]                                       │
    /// │ 图片保存根目录: [E:\Images]                                    │
    /// │ 目录结构: [配置目录结构...] {年月日}/{SN}/{OKNG}             │
    /// │           ↑ 下方与文件名模板行留 12px 空隙（上下一致）      │
    /// │ 文件名模板:   [{点位}]   （占位符提示见界面）              │
    /// │ 窗口点位: [窗口/点位配置...] 点格改存图点位/可交换窗口位置   │
    /// │ OK/NG显示: [√标题栏高亮] [√窗口徽标]                        │
    /// │ 相机列表: ┌────┬────────┬────┬──────────┬────────────────────────┐ │
    /// │            │序号│ 相机IP │端口│ 取图方式  │ FTP上传目录            │ │
    /// │            ├────┼────────┼────┼──────────┼────────────────────────┤ │
    /// │            │ 1  │ 192…   │8500│ Ftp      │ D:\…\ftp\cam1          │ │
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
    /// 序号列=相机ID（V1.12.23）：只读展示 1 起的行序，主界面显示规则"
    /// 有名称显名称（上相机/下相机）、无名称显相机N"中的 N 就是这一列。
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
        // V2.10.1：主窗体标题栏型号下拉的"当前选中值"快照（打开时由 MainForm.OpenSettings 传入）。
        // 目的：保证设置页"产品型号"下拉与主窗体所见一致。背景：主窗体下拉在配置 ProductModel 为空时
        // 会默认选中第一个候选（U171），但此时 _cfg.ProductModel 仍是空串，若设置页直接用它就会显示空白，
        // 与标题栏所见型号不一致。故本字段优先级最高：MainForm 当前选中值 > 配置 ProductModel > 首个候选。
        private readonly string _titleBarModel;

        public SettingsForm(AppConfig cfg, string titleBarModel = null)
        {
            _cfg = cfg;
            _titleBarModel = titleBarModel;
            InitializeComponent();          // 先解析设计器里的控件

            LoadFromConfig();               // 把当前配置值填进各输入框
            WireButtonEvents();             // 添加/删除相机按钮事件
        }

        /// <summary>把现有配置值填充到控件（配置来源是上层传来的 _cfg 实例）。</summary>
        private void LoadFromConfig()
        {
            // PLC 基础参数
            txtPlcIp.Text = _cfg.Plc.IpAddress;
            nudPlcPort.Value = _cfg.Plc.Port;
            // 固定产品型号（V2.7 协议，每次扫码写入 PLC 40007~40011，最多 10 字符；V2.8 可编辑下拉）
            // 下拉候选 = "预置三型号 U171/U172/Z121（DefaultProductModels）∪ 配置追加型号（ProductModels）"：
            // 用户要求"直接预置"，即使 appconfig 缺 productModels 字段/为空，这里也恒能选到三型号；
            // 手输新型号保存后自动加入候选（见 OnSave）。
            cmbModel.Items.Clear();
            var modelCandidates = new List<string>();
            foreach (var m in AppConfig.DefaultProductModels())
                if (!string.IsNullOrWhiteSpace(m)) modelCandidates.Add(m);
            foreach (var m in _cfg.ProductModels ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(m) && !modelCandidates.Contains(m)) modelCandidates.Add(m);
            foreach (var m in modelCandidates) cmbModel.Items.Add(m);
            // 默认显示当前型号，优先级：主窗体标题栏选中值 > 配置 ProductModel > 首个候选（V2.10.1，
            // 前两者都为空时兜底第一候选，避免设置页出现空白下拉与标题栏所见不一致）。
            string curModel = (string.IsNullOrWhiteSpace(_titleBarModel) ? _cfg.ProductModel : _titleBarModel) ?? "";
            if (string.IsNullOrWhiteSpace(curModel) && cmbModel.Items.Count > 0)
                curModel = cmbModel.Items[0].ToString();
            cmbModel.Text = curModel;
            // 显示窗口行列
            nudRows.Value = _cfg.Display.Rows;
            nudCols.Value = _cfg.Display.Columns;
            // OK/NG 显示配置（V1.5.0：标题栏 OK/NG 计数色块高亮开关；
            // V2.10.3：主界面窗口右下角 OK/NG 徽标显隐开关）
            chkTitleOkNg.Checked = _cfg.Display.TitleOkNgHighlight;
            chkWindowOkNg.Checked = _cfg.Display.WindowOkNgVisible;
            // 图片保存根目录、目录结构与文件名模板（目录结构用只读预览，实际编辑进可视化对话框）
            txtSaveDir.Text = _cfg.Image.SaveRootDir;
            RefreshDirPreview();
            txtFileNameTpl.Text = _cfg.Image.FileNameTemplate;
            // 相机表格：先建列，再逐行填数据
            SetupCameraGridColumns();
            LoadCameraRows();
            // 扫码枪表格：先建列，再逐行填数据（V1.8.1 起支持多台）
            SetupScannerGridColumns();
            LoadScannerRows();
        }

        /// <summary>
        /// 刷新"配置目录结构..."按钮的 ToolTip：把当前目录结构（层级用 / 拼接）动态挂到按钮上，
        /// 现场鼠标悬停即可查看当前配置，界面不再占用常驻标签行。
        /// </summary>
        private void RefreshDirPreview()
        {
            var dirs = _cfg.Image.SubDirs ?? new List<string>();
            string cur = dirs.Count > 0 ? string.Join("/", dirs) : "（未配置）";
            tip.SetToolTip(btnEditDirs,
                "可视化编辑存图目录结构（目录层级列表 + 文件名规则），并实时预览 OK/NG 落盘路径。\r\n当前结构：" + cur);
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
                // V1.12.23：序号列=相机ID（=列表位置 1 起的编号），只读不参与编辑，
                // 主界面显示规则以它为准（有名称显示名称、无名称显示"相机N"=序号）
                gridCameras.Columns.Add("SeqNo", "序号");
                gridCameras.Columns["SeqNo"].ReadOnly = true;
                gridCameras.Columns["SeqNo"].Width = 52;
                gridCameras.Columns.Add("Name", "相机名称(上/下)");
                gridCameras.Columns.Add("IpAddress", "相机IP");
                gridCameras.Columns.Add("CommandPort", "触发端口");
                gridCameras.Columns.Add("FtpUploadDir", "FTP取图目录（留空用全局目录）");
                // 取图方式：现场只保留 Ftp（V1.12.18 起相机 FTP 推图是唯一取图方式）
                var srcCol = new DataGridViewComboBoxColumn
                {
                    Name = "ImageSource",
                    HeaderText = "取图方式",
                    SortMode = DataGridViewColumnSortMode.NotSortable // 组合列无意义，禁排序
                };
                srcCol.Items.Add("Ftp");
                gridCameras.Columns.Add(srcCol);
            }
        }

        /// <summary>把现有相机配置逐行填进表格，方便现场看着改。
        /// 序号列=ID（V1.12.23）：显示列表位置 1 起的编号（相机1=1、相机2=2…），
        /// 主界面按"有名称显名称、无名称显相机N"对应。
        /// 空表格时按现场默认两台相机（V1.12.22，相机1=上=19.87.6.213→D:\IV存图\1、
        /// 相机2=下=19.87.6.212→D:\IV存图\2）填两行模板行。
        /// 【行 Tag=原 CameraConfig 引用（V1.12.26）】每一行把来源配置对象挂到 Tag 上，
        /// 保存时优先复用该对象（保留 WindowPointForm 配好的 StationPrograms 映射表），
        /// 新增行 Tag=null→保存时按新相机建空表。防止"配好映射→点保存→映射全丢"。</summary>
        private void LoadCameraRows()
        {
            int seq = 0; // 相机ID：从 1 开始编号，等于列表位置（数组 index+1）
            foreach (var c in _cfg.Cameras ?? new List<CameraConfig>())
            {
                seq++;
                // ImageSource 为空（旧配置）时按 Ftp 兜底显示
                string src = string.IsNullOrWhiteSpace(c.ImageSource) ? "Ftp" : c.ImageSource;
                var row = gridCameras.Rows[gridCameras.Rows.Add(seq, c.Name, c.IpAddress, c.CommandPort, c.FtpUploadDir, src)];
                row.Tag = c;   // 记下来源配置，保存时保留它配好的 StationPrograms 映射表
            }
            // 至少留一行可见，别让表格空着无从下手
            if (gridCameras.Rows.Count == 0)
                foreach (var c in CameraConfig.DefaultCameras())
                {
                    var row = gridCameras.Rows[gridCameras.Rows.Add(++seq, c.Name, c.IpAddress, c.CommandPort, c.FtpUploadDir, "Ftp")];
                    row.Tag = c;
                }
        }

        /// <summary>
        /// 重排相机表"序号"列（V1.12.23）：序号=相机ID=表格行序 1 起的编号。
        /// 新增/删除相机后调用，保证与主界面"相机N"及配置列表位置始终一致；
        /// 保存时读配置列表顺序，序号列只作展示 ID 不落盘。
        /// </summary>
        private void RenumberCameraSeq()
        {
            int seq = 0;
            foreach (DataGridViewRow r in gridCameras.Rows)
            {
                if (r.Cells["SeqNo"].Value == null) continue; // 末尾"新行"占位行跳过
                r.Cells["SeqNo"].Value = ++seq;
            }
        }

        /// <summary>
        /// 从相机表格当前所有行收集相机配置列表（V1.12.26）。
        /// 【为什么要有它】① 映射页打开与保存时必须用"同一批相机对象"——表格行 Tag 上绑着来源
        /// CameraConfig（LoadCameraRows 时绑定，新增行 Tag=null），优先复用该对象并直接改字段，
        /// 从而完整保留 WindowPointForm 写回的 StationPrograms（点位→程序号映射表）不丢失；
        /// ② 打开映射页时也要传"含未保存新增相机"的列表，让新相机立刻能配它自己的映射表。
        /// 【约束】IP 空的行视为未填写自动剔除；复用对象时注意"废弃的 ProgramNo 不写回"。
        /// </summary>
        /// <returns>相机列表（与表格行一一对应，顺序=行序）</returns>
        private List<CameraConfig> CollectCamerasFromGrid()
        {
            var cams = new List<CameraConfig>();
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
                // 新增行（Tag=null）才新建对象（默认空映射表，正好符合"新相机有自己的表"）。
                var cam = r.Tag as CameraConfig;
                if (cam == null)
                {
                    cam = new CameraConfig();
                    r.Tag = cam;   // 关键：回绑到行 Tag，之后映射页配好的映射写回此对象，
                                   // 保存时再走本方法复用同一对象，映射才不丢
                }
                cam.Name = r.Cells["Name"].Value == null ? "" : r.Cells["Name"].Value.ToString().Trim();
                cam.IpAddress = ip;
                cam.CommandPort = Math.Max(1, port);
                cam.FtpUploadDir = r.Cells["FtpUploadDir"].Value == null ? "" : r.Cells["FtpUploadDir"].Value.ToString().Trim();
                cam.ImageSource = string.IsNullOrWhiteSpace(imgSrc) ? "Ftp" : imgSrc.Trim();
                // 注意：不再写回废弃的 ProgramNo（V1.12.25 起点位→程序号由 StationPrograms 表驱动，
                // 在"窗口/点位配置…"里配；此处不赋值则按默认 -1，保证旧值不残留误导现场）
                cams.Add(cam);
            }
            return cams;
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
                    gridScannersSerial.Rows.Add(s.Enabled, s.PortName, s.BaudRate, s.StopBits, s.Parity);
                    hasSerial = true;
                }
                else
                {
                    gridScannersTcp.Rows.Add(s.Enabled, s.IpAddress, s.Port, s.TriggerCommand);
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
            // 添加一台相机：直接往表格追加一行默认值（默认取现场相机1：上相机 19.87.6.213 +
            // FTP 目录 D:\IV存图\1，V1.12.22），现场改 IP/端口/取图方式即可
            btnAddCam.Click += (s, e) =>
            {
                var def = CameraConfig.DefaultCameras()[0];
                gridCameras.Rows.Add(0, def.Name, def.IpAddress, 8500, def.FtpUploadDir, "Ftp");
                RenumberCameraSeq(); // 追加后重排序号（ID=行序），删除/排序后同样调用
            };
// 删除选中：把当前选中的行整行移除；没有选中行则什么都不做
            // 【V1.8.4 修复】末尾"新行"（AllowUserToAddRows 附带的 * 占位行）不在 SelectedRows 里，
            //   用户点击该空白行再点删除，原来会误报"未选中行"——现改为：删除=放弃该占位行。
            // V1.12.23：删除后重排序号列（序号=ID=行序）
            btnDelCam.Click += (s, e) =>
            {
                DeleteSelectedRows(gridCameras, "相机");
                RenumberCameraSeq(); // 删中间某台后，后续相机序号自动前移，保持连续
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
            // ① 窗口→存图点位（格子矩阵，可编辑点位/交换窗口位置/恢复默认）；
            // ② 点位→相机程序号（每台相机各自一张表，触发时按点位切相机程序）。
            // 注意：行列数取【界面 nud 上的最新值】（用户可能刚改了行/列还没保存），
            // 而不是 _cfg.Display.Rows/Columns（那是上次已保存的旧值）——保证格子矩阵
            // 与"用户即将保存的新窗口总数"一致，改完行列再配置点位所见即所得。
            // 相机区传"当前表格里所有相机行"（V1.12.26：含刚新增未保存的行，均带各自 Tag 上
            // 的映射表），确定时各相机映射写回原位、点保存一起落盘；未保存的新增相机也能立刻
            // 配它自己的"点位→程序号"映射表（保存时按 Tag 复用同对象，映射不丢）。
            btnEditPoints.Click += (s, e) =>
            {
                using (var dlg = new WindowPointForm(_cfg.Display.WindowStationMap,
                                                     (int)nudRows.Value, (int)nudCols.Value,
                                                     CollectCamerasFromGrid(),
                                                     _cfg.Display.WindowEnabled,
                                                     AppConfig.DefaultProductModels()
                                                         .Union(_cfg.ProductModels ?? new List<string>())
                                                         .ToList()))
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
            MessageBox.Show($"请先点击表格中要删除的{rowName}行（整行高亮），再点\"删除选中\"。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 把界面值回写内存配置（V1.6.0：保存后由 MainForm 热生效，免重启）。</summary>
        private void OnSave(object sender, EventArgs e)
        {
            _cfg.Plc.IpAddress = txtPlcIp.Text.Trim();
            _cfg.Plc.Port = (int)nudPlcPort.Value;
            // 固定产品型号（V2.7 协议）：保存后每次扫码上位机把型号写入 PLC 40007~40011；
            // V2.8：型号同时决定"点位→相机程序号"查哪张表。手输的新型号自动加入候选列表
            // （ProductModels），供"窗口/点位配置"的型号下拉使用。
            string model = cmbModel.Text.Trim();
            _cfg.ProductModel = model;
            if (model.Length > 0)
            {
                var models = _cfg.ProductModels ?? (_cfg.ProductModels = new List<string>());
                if (!models.Any(x => string.Equals(x, model, StringComparison.OrdinalIgnoreCase)))
                    models.Add(model);
            }
            _cfg.Display.Rows = (int)nudRows.Value;
            _cfg.Display.Columns = (int)nudCols.Value;
            _cfg.Display.TitleOkNgHighlight = chkTitleOkNg.Checked;
            _cfg.Display.WindowOkNgVisible = chkWindowOkNg.Checked; // V2.10.3：窗口右下角 OK/NG 徽标开关
            _cfg.Image.SaveRootDir = txtSaveDir.Text.Trim();
            _cfg.Image.FileNameTemplate = txtFileNameTpl.Text.Trim();
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
                // 全空的模板行（IP 都没填）忽略，避免保存一堆垃圾行
                if (string.IsNullOrWhiteSpace(ip)) continue;
                scanners.Add(new ScanConfig
                {
                    Enabled = enabled,
                    Mode = "Tcp",
                    IpAddress = string.IsNullOrWhiteSpace(ip) ? "19.87.6.100" : ip,
                    Port = Math.Max(1, port),
                    TriggerCommand = trigger
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
                // 全空的模板行（串口名都没填）忽略
                if (string.IsNullOrWhiteSpace(portName)) continue;
                scanners.Add(new ScanConfig
                {
                    Enabled = enabled,
                    Mode = "Serial",
                    PortName = portName,
                    BaudRate = Math.Max(1, baud),
                    StopBits = string.IsNullOrWhiteSpace(stopBits) ? "1" : stopBits,
                    Parity = string.IsNullOrWhiteSpace(parity) ? "None" : parity
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