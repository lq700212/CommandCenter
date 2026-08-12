using System.Collections.Generic;
using System.Drawing;

namespace CommandCenter.Models
{
    /// <summary>
    /// 顶层配置模型：一个 json 文件（Config/appconfig.json）承载全部可配置项。
    /// 将所有参数集中在此，配合 Utils/ConfigStore 读写在启动时加载、设置窗体修改。
    ///
    /// 【为什么合并成一个大模型而非拆成多个小 json】
    ///   现场维护人员只关心"改哪个值"，分成一堆小文件找起来反而费劲；
    ///   一个 appconfig.json + 一个设置窗体，改完即存即生效，最简单直接。
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 相机通讯配置列表（基恩士 IV4 系列，支持多台）。
        /// 【为什么是列表】现场可能有多台相机，每台的 IP/端口/FTP目录都独立配置；
        /// 一次"到位"信号会对列表中每台都触发一次、各取各的图（见 ProductionCoordinator）。
        /// 注意：存图点位与相机无关，由 DisplayConfig.WindowStationMap（窗口→点位映射）统一管理。
        /// </summary>
        public List<CameraConfig> Cameras { get; set; } = new List<CameraConfig>();

        /// <summary>PLC 通讯配置（汇川，Modbus TCP 从站）</summary>
        public PlcConfig Plc { get; set; } = new PlcConfig();

        /// <summary>主界面显示窗口 / 标题栏配置</summary>
        public DisplayConfig Display { get; set; } = new DisplayConfig();

        /// <summary>图像保存配置</summary>
        public ImageConfig Image { get; set; } = new ImageConfig();

        /// <summary>
        /// 扫码枪配置列表（可选，未启用时使用模拟数据）。
        /// 【为什么是列表】现场可能有多台扫码枪（不同工位/不同门各一把），每台的通讯方式
        /// （串口/TCP）、IP/串口名都独立配置；任何一台扫到的条码都会更新当前产品序列号
        /// （进标题栏与 {SN} 存图目录）。一台也够用——配置里只留一行即可。
        /// </summary>
        public List<ScanConfig> Scanners { get; set; } = new List<ScanConfig>();

        /// <summary>
        /// 管理员登录安全配置（V1.9.0）：控制"系统设置"按钮的使用权限。
        /// 启用后点系统设置必须先登录管理员账号（每次点都校验，不记忆登录状态），
        /// 防止现场操作员随意修改设备/相机/存图等关键配置。
        /// </summary>
        public SecurityConfig Security { get; set; } = new SecurityConfig();
    }

    /// <summary>
    /// 相机通讯配置（基恩士 IV4-500CA）。
    /// IV4 支持 "TCP/IP 无协议通信"（最多 2 路连接），本文档按该方式对接：
    ///   - 触发拍摄：上位机往相机的 CommandPort 发 ASCII 指令（T1/T2/RT，判定结果走指令回帧）；
    ///   - 图像回传（V1.7.0 两种来源二选一，见 ImageSource）：
    ///       Ftp：相机作为 FTP 客户端把照片推到 Image.FtpRootDir，上位机监听新文件（默认，成熟）；
    ///       Tcp：上位机发 BR 指令直接从相机读最新图像（24bit 位图），免 FTP 落盘中转。
    /// 具体指令帧格式需以《IV4 系列通信、连接指南》为准，本模型可配帧字符串。
    /// </summary>
    public class CameraConfig
    {
        /// <summary>相机 IP（如 192.168.1.100）</summary>
        public string IpAddress { get; set; } = "192.168.1.100";

        /// <summary>
        /// 该相机的 FTP 上传目录（相机作为 FTP 客户端把照片推到这台，独立监听）。
        /// 为空时回退用全局 ImageConfig.FtpRootDir——多台相机务必各自配不同目录，否则图会混。
        /// </summary>
        public string FtpUploadDir { get; set; } = "";

        /// <summary>控制指令发送端口（基恩士无协议通信常用 8500，按现场实际改）</summary>
        public int CommandPort { get; set; } = 8500;

        // ─── IV4 无协议通信指令表（《IV4 通信、连接指南》）───
        // 指令均以 CR(0x0D) 终止；T 系列指令含义见 docs/通讯接入.md

        /// <summary>仅触发拍摄指令（T1[CR]），响应回显 T1。用于"只触发、判定另取"场景。</summary>
        public string TriggerCommand { get; set; } = "T1";

        /// <summary>触发＋读取判定结果指令（T2[CR]），响应 RT, 工具结果(标准/详细)[CR]。</summary>
        public string TriggerAndReadCommand { get; set; } = "T2";

        /// <summary>单独读取判定结果指令（RT[CR]），响应同 T2。</summary>
        public string ReadResultCommand { get; set; } = "RT";

        /// <summary>
        /// 是否让相机直接回传判定结果（T2）。
        /// true(默认)：判定 OK/NG 直接来自 IV4 内部判定，准确；
        /// false：退化为"FTP 图到达即记 OK"的旧逻辑（仅现场未配判定时用）。
        /// </summary>
        public bool ReadResultFromCamera { get; set; } = true;

        /// <summary>
        /// 判定合格字符：标准结果里 8 位中的"合格位"。默认 '0' 表示该工具 OK。
        /// IV4 约定：'0'=OK、'1'=NG；另有 '4'(未进行) / '-'(该工具未启用)。
        /// 遇 '4'/'-'/未知一律保守判 NG，避免漏放不良。
        /// </summary>
        public string OkChar { get; set; } = "0";

        /// <summary>等待一条指令响应（除 Connect 外，如 T2/RT 的拍摄+判定耗时）毫秒数</summary>
        public int ResponseTimeoutMs { get; set; } = 5000;

        /// <summary>单次收发包超时（毫秒），防相机掉线后调用线程卡死</summary>
        public int TimeoutMs { get; set; } = 3000;

        /// <summary>触发后等相机 FTP 新图的最长毫秒数（超时视为取像失败）</summary>
        public int ImageWaitMs { get; set; } = 10000;

        /// <summary>
        /// 取图来源（V1.7.0，现场二选一实测后定，大小写不敏感）：
        ///   "Ftp"（默认）：相机作 FTP 客户端把照片推到上位机目录，上位机监听新图（现方案，成熟稳定）；
        ///   "Tcp"       ：上位机发 BR 指令直接从相机读最新图像（24bit 位图），触发后同步读回，
        ///                  链路更短（不经过 FTP 服务器落盘中转），依赖相机的 TCP/IP 无协议通信；
        /// 其他取值一律按 Ftp 兜底（旧配置无需迁移）。
        /// </summary>
        public string ImageSource { get; set; } = "Ftp";

        /// <summary>
        /// 读取图像数据指令名（仅 ImageSource=="Tcp" 时使用）：IV4 手册原文 "BR,m[CR]"，
        /// BR 读"最新图像"（24bit 位图格式），响应 "BR,nnnnnnnnnn,ddddddd,图像数据"，
        /// 其中 nnnnnnnnnn=合计触发编号（仅透出日志），ddddddd=图像数据的数据长度。
        /// 此字段只存指令名（默认 "BR"），参数 m 见 ReadImageMode，发送时拼成 "BR,m"。
        /// </summary>
        public string ReadImageCommand { get; set; } = "BR";

        /// <summary>
        /// BR 指令的数据格式参数 m（拼成 "BR,m" 发送）。m=压缩率：
        ///   "0"=无压缩（原图，数据量大、传输慢）；
        ///   "1"=1/2 压缩（数据量减半，现场默认，取图更快）。
        /// </summary>
        public string ReadImageMode { get; set; } = "1";

        /// <summary>
        /// 相机 FTP 主动上传目录 = 上位机 ImageConfig.FtpRootDir，
        /// 上位机用 FileSystemWatcher 监听新图。
        /// </summary>
        public bool EnableFtpMonitor { get; set; } = true;
    }

    /// <summary>
    /// PLC 通讯配置（汇川，Modbus TCP 从站，端口 502）。
    /// 汇川 D 寄存器映射到 Modbus 保持寄存器区（D0=40001, D100=40101），
    /// 寄存器绝对地址 = 40001 + D地址。下面暴露的均是"D 地址"，发送时 +40001 转绝对地址。
    /// 到位信号与完成信号若走位（M/Y）则用线圈读写，此处默认用位读出版社写法见 PlcService。
    /// </summary>
    public class PlcConfig
    {
        /// <summary>PLC IP（如 192.168.1.10）</summary>
        public string IpAddress { get; set; } = "192.168.1.10";

        /// <summary>Modbus TCP 端口，标准 502</summary>
        public int Port { get; set; } = 502;

        /// <summary>Modbus 从站号（UnitId），默认 1</summary>
        public byte UnitId { get; set; } = 1;

        /// <summary>单次读写超时（毫秒）</summary>
        public int TimeoutMs { get; set; } = 2000;

        // ─── 寄存器地址映射（与现场 PLC 程序确认后调整） ───

        /// <summary>PLC→上位机：相机运动到位信号（D 地址，读保持寄存器）</summary>
        public ushort MoveDoneAddress { get; set; } = 100;

        /// <summary>上位机→PLC：触发相机工作的"开始"信号（D，写）</summary>
        public ushort StartSignalAddress { get; set; } = 101;

        /// <summary>上位机→PLC：拍照完成信号（D，写 1=成功 2=取像失败）</summary>
        public ushort DoneSignalAddress { get; set; } = 102;

        /// <summary>上位机→PLC：配方号起始 D 地址（连续写 N 个字）</summary>
        public ushort RecipeAddress { get; set; } = 103;

        /// <summary>配方号写入的字数（支持 1~20，ASCII 每字 2 字符）</summary>
        public ushort RecipeLen { get; set; } = 5;

        /// <summary>上位机→PLC：检测总数（D）</summary>
        public ushort TotalCountAddress { get; set; } = 110;

        /// <summary>上位机→PLC：OK 数（D）</summary>
        public ushort OkCountAddress { get; set; } = 111;

        /// <summary>上位机→PLC：NG 数（D）</summary>
        public ushort NgCountAddress { get; set; } = 112;
    }

    /// <summary>
    /// 主界面显示配置：决定显示几行几列窗口、标题栏显示哪些项、OK/NG 颜色。
    /// 窗口数 = Rows × Columns，完全由本配置驱动，换机型只改 json 即可复用。
    /// </summary>
    public class DisplayConfig
    {
        /// <summary>显示窗口行数（每路相机/每个检测点一个窗口）</summary>
        public int Rows { get; set; } = 4;

        /// <summary>显示窗口列数</summary>
        public int Columns { get; set; } = 7;

        /// <summary>
        /// 窗口逻辑宽（px）。说明：现代码由 MainForm 用 TableLayoutPanel 将主区域等分，
        /// 所有窗口尺寸严格一致并铺满，本字段不参与布局计算，仅作为人工参考。
        /// </summary>
        public int WindowWidth { get; set; } = 220;

        /// <summary>窗口逻辑高（px，同上，仅供人工参考）</summary>
        public int WindowHeight { get; set; } = 160;

        /// <summary>窗口间距（px）</summary>
        public int WindowSpacing { get; set; } = 8;

        /// <summary>
        /// 窗口→存图点位映射（可视化配置的主数据）：第 i+1 号显示窗口存图用的点位号（进文件名 {点位}）。
        /// 【默认规则】点位 = 窗口编号（1、2、3…），即 1 号窗口存图名为 1.png；
        /// 【自定义】用户在"系统设置 → 窗口/点位配置…"里可把任意窗口的点位改成其他值
        ///   （例如 1 号窗口存图名改成 2.png）；
        /// 【窗口位置调整】交换两个窗口的点位值，等价于"把窗口内容搬到另一个格子"，
        ///   而窗口编号固定跟随格子（不管谁放第一位都是 1 号）。
        /// 说明：
        ///   - 长度 = 显示窗口总数(Rows×Columns)，由 ConfigStore 在加载/保存时自动对齐
        ///     （缺的补"点位=窗口编号"，多的截断），运行时 ProductionCoordinator 还有越界兜底；
        ///   - 存图文件名的 {点位} 用本映射值，相机配置不再有点位概念。
        /// </summary>
        public List<int> WindowStationMap { get; set; } = new List<int>();

        /// <summary>标题栏是否显示各字段（复用项目时可整体隐藏）</summary>
        public bool ShowProductModel { get; set; } = true;

        /// <summary>
        /// 标题栏是否显示"系统设置"按钮（V1.8.4）。
        /// 默认 true；生产现场为防止误点改配置，可在 appconfig.json 的 display 节点改 false
        /// 隐藏该按钮（隐藏后配置只读、只能由管理员改 json 恢复）。布局自动紧凑：隐藏的按钮
        /// 不占标题栏位置（RelayoutTitleBar 按 Visible 跳过）。
        /// </summary>
        public bool ShowSettingsButton { get; set; } = true;

        /// <summary>标题栏是否显示扫码序列号</summary>
        public bool ShowSerialNumber { get; set; } = true;

        /// <summary>标题栏是否显示来料总数</summary>
        public bool ShowTotalCount { get; set; } = true;

        /// <summary>标题栏是否显示 OK 数</summary>
        public bool ShowOkCount { get; set; } = true;

        /// <summary>标题栏是否显示 NG 数</summary>
        public bool ShowNgCount { get; set; } = true;

        /// <summary>标题栏左侧固定文案（产品型号说明），可含当前配方名</summary>
        public string ProductModelPrefix { get; set; } = "产品型号";

        /// <summary>OK 徽标颜色名（与 Windows 颜色名一致，如 Green）</summary>
        public string OkColorName { get; set; } = "Green";

        /// <summary>NG 徽标颜色名（如 Red）</summary>
        public string NgColorName { get; set; } = "Red";

        /// <summary>
        /// 标题栏 OK/NG 计数是否用"实心彩色色块 + 白字"高亮显示。
        /// 现场反馈"只显示带颜色数字不够醒目"，默认开：OK 用 OkColor 绿底白字、NG 用 NgColor 红底白字；
        /// false 回退为普通彩色文字（旧版样式）。
        /// </summary>
        public bool TitleOkNgHighlight { get; set; } = true;

        /// <summary>按配置反解出 OK/NG 实际画刷色，配置非法时回退默认</summary>
        public Color OkColor => ColorFromName(OkColorName, Color.Green);
        public Color NgColor => ColorFromName(NgColorName, Color.Red);

        private static Color ColorFromName(string name, Color fallback)
        {
            try { return Color.FromName(name).IsEmpty ? fallback : Color.FromName(name); }
            catch { return fallback; }
        }
    }

    /// <summary>
    /// 图像保存配置：存图目录按【逐级目录列表】归档，文件名按【模板】生成。
    /// 默认结构（现场要求）：
    ///   根目录 / 年月日(2026年08月11日) / SN号 / OK|NG / 点位号 / 文件
    /// 可视化配置入口在设置窗体的"配置目录结构…"，把每级目录名或生成规则编辑成列表。
    /// 注：年月日是【一个】目录名，不是年/月/日三级目录。
    /// </summary>
    public class ImageConfig
    {
        /// <summary>图像保存根目录（不在则自动创建）</summary>
        public string SaveRootDir { get; set; } = @"D:\CommandCenter\Images";

        /// <summary>
        /// 目录层级列表（可视化配置的主数据）：每个元素是一级目录名或生成规则，
        /// 按顺序逐级建目录。支持占位符（见下方占位符说明），固定文字原样保留。
        /// 默认（现场要求）：["{年月日}","{SN}","{OKNG}"] → 根/2026年08月11日/SN-0001/OK/
        /// 说明：
        ///   {年月日} 是一个整体目录名，展开成"2026年08月11日"（不是年/月/日三级）；
        ///   {OKNG}   按本次判定展开成 OK 或 NG 两个并列目录之一，满足现场分开放习惯；
        ///   点位号进文件名（见 FileNameTemplate），不作为目录层级。
        /// </summary>
        public List<string> SubDirs { get; set; } = new List<string> { "{年月日}", "{SN}", "{OKNG}" };

        /// <summary>
        /// 文件名模板（不含扩展名，统一存 .png）。支持的占位符（其余文字原样保留）：
        ///   {点位}   窗口存图点位（DisplayConfig.WindowStationMap，默认=窗口编号，可在设置里可视化改）
        ///   {时间}   精确到毫秒的时间戳 yyyyMMdd_HHmmss_fff（多张同点位防重名用）
        ///   {SN}     序列号（若文件名也要带 SN 可加）
        ///   例：默认 "{点位}" → 1.png
        /// </summary>
        public string FileNameTemplate { get; set; } = "{点位}";

        /// <summary>保留天数，0 表示不自动清理</summary>
        public int KeepDays { get; set; } = 30;

        /// <summary>相机 FTP 上传目录兜底（各相机未单独配 FtpUploadDir 时用它；多台务必分开配）</summary>
        public string FtpRootDir { get; set; } = @"D:\CommandCenter\Images\ftp";
    }

    /// <summary>
    /// 扫码枪配置（V1.8.0 起支持两种通讯方式，见 Mode；未启用则序列号走手动输入/模拟）。
    /// </summary>
    public class ScanConfig
    {
        /// <summary>
        /// 是否启用扫码枪。
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 通讯方式（V1.8.0 新增，大小写不敏感，其他值按 Serial 兜底）：
        ///   "Serial"：串口 RS-232 扫码枪（默认，扫完发一行条码+CR/LF）；
        ///   "Tcp"   ：基恩士 SR 系列扫码枪以太网 TCP/IP 无协议通讯——上位机作 TCP 客户端连扫码枪，
        ///              扫码枪读到条码后主动推送文本行，本程序按行切分（与串口行为一致）。
        /// </summary>
        public string Mode { get; set; } = "Serial";

        /// <summary>串口名，如 COM3（仅 Mode=Serial 使用）</summary>
        public string PortName { get; set; } = "COM3";

        /// <summary>波特率，扫码枪常见 115200 / 9600（仅 Mode=Serial 使用）</summary>
        public int BaudRate { get; set; } = 115200;

        /// <summary>停止位字符串，遵循项目约定："1"/"15"/"2"（仅 Mode=Serial 使用）</summary>
        public string StopBits { get; set; } = "1";

        /// <summary>校验位，标准枚举名 None/Odd/Even/Mark/Space（仅 Mode=Serial 使用）</summary>
        public string Parity { get; set; } = "None";

        /// <summary>扫码枪 IP（仅 Mode=Tcp 使用）。基恩士 SR 系列无协议通讯的默认监听端口请查
        /// 《SR 系列通信指南》，常见 9005 左右，现场按扫码枪设置改。</summary>
        public string IpAddress { get; set; } = "192.168.1.110";

        /// <summary>扫码枪 TCP 端口（仅 Mode=Tcp 使用，基恩士 SR 无协议默认端口，现场确认）</summary>
        public int Port { get; set; } = 9005;
    }

    /// <summary>
    /// 管理员登录安全配置（V1.9.0）。
    ///
    /// 【作用】控制系统设置的使用权限：只有登录管理员账号才能打开"系统设置"窗体
    /// （主界面 MainForm.OpenSettings 每次点击都弹 LoginForm 校验，校验通过才放行）。
    ///
    /// 【安全存储】密码不存明文，只存 SHA-256 哈希（Utils.SecurityUtil.HashPassword）。
    ///   登录时把用户输入做同样哈希再比对，配置里即使被看到也无法反推出明文密码。
    ///
    /// 【默认账号】出厂默认 admin / admin123，管理员首次登录后在登录对话框的
    ///   "修改密码"面板改掉（验证原密码 → 新密码两次一致且 ≥6 位 → 保存即时生效）。
    ///
    /// 【为什么每次点都要求登录】现场"系统设置"是高风险入口（改 IP/寄存器/存图/点位），
    ///   若登录一次长期有效，操作员容易忘记退出、旁人不费劲就能改配置；
    ///   每次点都校验，权限控制最严格、无记忆状态可钻空子。
    /// </summary>
    public class SecurityConfig
    {
        /// <summary>
        /// 是否启用管理员登录校验。true=点"系统设置"需先登录；false=直接打开（等同于旧版行为，
        /// 现场不需要防护时可关）。默认 true（需求即"只有登录管理员才能用系统设置"）。
        /// </summary>
        public bool AdminEnabled { get; set; } = true;

        /// <summary>管理员用户名（默认 "admin"，登录时大小写不敏感比对）</summary>
        public string AdminUser { get; set; } = "admin";

        /// <summary>
        /// 管理员密码的 SHA-256 哈希（hex 小写）。默认值是 "admin123" 的哈希，
        /// 即出厂默认密码 admin123（见 SecurityUtil.HashPassword 注释）。
        /// 配置里绝不存明文；改密码在登录对话框的"修改密码"面板操作，此处只落哈希。
        /// </summary>
        public string AdminPasswordHash { get; set; }
            = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9";
    }
}