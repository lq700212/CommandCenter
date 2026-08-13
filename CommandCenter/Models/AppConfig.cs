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
        /// 【为什么是列表】现场有多台相机，每台的 IP/端口/FTP目录都独立配置；
        /// 一次"到位"信号会对列表中每台都触发一次、各取各的图（见 ProductionCoordinator）。
        /// 注意：存图点位与相机无关，由 DisplayConfig.WindowStationMap（窗口→点位映射）统一管理。
        ///
        /// 【默认值（V1.9.8 现场定稿）】现场固定两台相机，IP 已确定——相机1=19.87.6.212、
        /// 相机2=19.87.6.213，无配置/空配置时用 CameraConfig.DefaultCameras() 兜底这两台，
        /// 设置窗体默认行 / 主窗体空配置兜底与这里保持一致（改现场 IP 只改这一处工厂方法即可）。
        ///
        /// 【为什么初始化器用空列表而不是 DefaultCameras()（V1.9.9 修复 4 台 bug）】
        /// Newtonsoft 反序列化对"属性已有实例的集合"默认是复用该实例并 Add 进 json 元素，
        /// 而不是整值替换。若这里预置 2 台默认，json 里又有 2 台，反序列化就会叠成 4 台
        /// （实测 AppConfig.Cameras.Count == 4）。因此初始化器必须给空列表，默认两台相机
        /// 统一交给 ConfigStore.Load 的"空/缺省兜底"与 MainForm/SettingsForm 的 Count==0 兜底。
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
        /// <summary>相机 IP（V1.9.8 现场定稿：默认=现场相机1 19.87.6.212，相机2 见 DefaultCameras）</summary>
        public string IpAddress { get; set; } = "19.87.6.212";

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
        /// 切换相机程序编号（PW 指令，V1.12.18，0~127，负值=不切换）。
        /// 触发拍摄前上位机先发 "PW,nnn[CR]" 把相机切到指定程序（每个程序对应一组视觉工具），
        /// 再发 T2 触发+读判定。nnn 补足 3 位（如程序 9 → "PW,009"）。
        /// 默认 -1 表示"不切换，用相机当前程序"，旧配置无需迁移、行为不变。
        /// ⚠️ TODO（待与 PLC 沟通）：现场一个相机可能拍多个点位、每个点位用不同程序。
        ///   等 PLC 到位信号带上点位信息后，这里应升级为"点位→程序号"映射，触发时按当前点位切换；
        ///   当前实现按"每相机一个固定程序号"落地，点位级映射留待点位信息定稿。
        /// </summary>
        public int ProgramNo { get; set; } = -1;

        /// <summary>
        /// 判定结果输出格式（OF 指令，V1.12.18）：留空/非法则不发送（相机用默认标准格式）。
        /// 可选值（固定 2 字符）：
        ///   "00" 标准（多主控无效/分类）——T2 响应 "RT,工具结果(标准)[CR]"（默认，8 位判定位）；
        ///   "01" 详细（多主控无效/分类）——T2 响应 "RT,工具结果(详细)[CR]"；
        ///   "02" 标准（主控编号）；
        ///   "03" 详细（主控编号）。
        /// 触发前若配置则先发 "OF,nn[CR]"（响应 "OF[CR]"）再切程序/触发。设置后连接断开或断电前一直保持。
        /// </summary>
        public string OutputFormat { get; set; } = "";

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

        /// <summary>
        /// 现场默认的两台相机（V1.9.8 定稿：IP 已写死，无需现场改 IP）。
        /// 相机1=19.87.6.212、相机2=19.87.6.213，其余参数取模型默认。
        /// 【为什么收敛成一个方法】三处需要"默认两台相机"（未配置时 AppConfig.Cameras 的
        ///   初值、主窗体 BuildServices 的空配置兜底、设置窗体空表格默认行/添加行），
        ///   若各自硬编码 IP，改现场 IP 要改好几个地方、极易漏。统一走本方法，
        ///   现场换相机 IP 只改这一处即可。
        /// 【注意】返回的是全新实例列表，调用方可直接 AddRange/遍历，不会与原来的配置共享引用。
        /// </summary>
        public static List<CameraConfig> DefaultCameras() => new List<CameraConfig>
        {
            new CameraConfig { IpAddress = "19.87.6.212" },
            new CameraConfig { IpAddress = "19.87.6.213" }
        };
    }

    /// <summary>
    /// PLC 通讯配置（V1.12.11 起角色反转：现场汇川 PLC 做 Modbus TCP 主站，上位机做从站）。
    /// 上位机监听本机 Port 端口（标准 502），等汇川主站 TCP 连入并读写上位机的保持寄存器区。
    /// IpAddress = 上位机监听绑定 IP（"0.0.0.0"=监听所有网卡，现场主机多网卡时可绑指定 IP）。
    ///
    /// 【寄存器地址沿用 D100~D112，读写方向反转】
    ///   D100 到位信号：PLC 主站写 1 → 上位机读后清 0；
    ///   D101 开始信号：上位机写自己区，PLC 来读；
    ///   D102 完成信号：上位机写自己区(1=成功/2=取像异常)，PLC 来读；
    ///   D103~D(103+len-1) 配方号：上位机写自己区(ASCII)，D108 配方更新标志=1，PLC 读走后写 0 回执；
    ///   D110/111/112 总数/OK/NG：上位机写自己区，PLC 来读。
    /// （Modbus 是主从问答协议，从站不能主动发；"上位机→PLC"全靠 PLC 轮询来读）
    /// </summary>
    public class PlcConfig
    {
        /// <summary>上位机从站监听绑定 IP（"0.0.0.0"=所有网卡；多网卡可填 19.87.6.230 绑定指定网卡）</summary>
        public string IpAddress { get; set; } = "0.0.0.0";

        /// <summary>上位机从站监听端口，Modbus TCP 标准 502</summary>
        public int Port { get; set; } = 502;

        /// <summary>上位机从站 UnitId（需与汇川主站通讯指令里的 UnitId 一致，默认 1）</summary>
        public byte UnitId { get; set; } = 1;

        /// <summary>单次读写超时（毫秒，从站模式主要用于日志/容错，不再阻塞主动连接）</summary>
        public int TimeoutMs { get; set; } = 2000;

        // ─── 寄存器地址映射（与现场 PLC 程序确认后调整；方向见类注释） ───

        /// <summary>
        /// PLC→上位机：扫码枪运动到位信号（V1.12.16 两阶段流程新增，D 地址）。
        /// 位 1=机器人/机械臂带着扫码枪运动到位、可以扫码；上位机读到后清 0 复位。
        /// ⚠️ **地址为占位待定稿（默认 D99）**：与现场 PLC 程序核对后统一替换本值即可，
        ///    不必改程序（其余字段同理，见 docs/通讯接入.md §3.2）。
        /// </summary>
        public ushort ScanMoveDoneAddress { get; set; } = 99;

        /// <summary>PLC→上位机：相机运动到位信号（D 地址，PLC 写 1，上位机读后清 0）</summary>
        public ushort MoveDoneAddress { get; set; } = 100;

        /// <summary>
        /// PLC→上位机：当前到位点位号（D 地址，V1.12.18 占位，默认 D113，待与 PLC 沟通定稿）。
        /// 【背景】现场一个相机可能拍多个点位、每个点位用不同相机程序（见 CameraConfig.ProgramNo）。
        ///   为支持"按点位切程序"，PLC 在相机到位信号（MoveDoneAddress）的同时，应把当前点位号
        ///   写入本寄存器；上位机触发前读它、查"点位→程序号"映射后再发 PW 切换。
        /// 【TODO】当前未实现读取/映射：点位号含义与映射规则待现场 PLC 程序定稿，
        ///   届时在 ProductionCoordinator 触发流程中补"读点位号→按映射切程序"逻辑，本字段只做占位。
        /// </summary>
        public ushort PointInfoAddress { get; set; } = 113;

        /// <summary>上位机→PLC：触发相机工作的"开始"信号（D，上位机写自己区，PLC 来读）</summary>
        public ushort StartSignalAddress { get; set; } = 101;

        /// <summary>上位机→PLC：拍照完成信号（D，上位机写自己区：1=成功 2=取像失败，PLC 来读）</summary>
        public ushort DoneSignalAddress { get; set; } = 102;

        /// <summary>上位机→PLC：配方号起始 D 地址（连续写 N 个字，ASCII 每字 2 字符）</summary>
        public ushort RecipeAddress { get; set; } = 103;

        /// <summary>配方号写入的字数（支持 1~20，ASCII 每字 2 字符）</summary>
        public ushort RecipeLen { get; set; } = 5;

        /// <summary>上位机→PLC：配方更新标志位（V1.12.11 新增，D108）。上位机写 1=有新配方待切换，PLC 读走后写 0 回执。</summary>
        public ushort RecipeFlagAddress { get; set; } = 108;

        /// <summary>上位机→PLC：检测总数（D，上位机写自己区，PLC 来读）</summary>
        public ushort TotalCountAddress { get; set; } = 110;

        /// <summary>上位机→PLC：OK 数（D，上位机写自己区，PLC 来读）</summary>
        public ushort OkCountAddress { get; set; } = 111;

        /// <summary>上位机→PLC：NG 数（D，上位机写自己区，PLC 来读）</summary>
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
        public string SaveRootDir { get; set; } = @"E:\Images";

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

        /// <summary>
        /// 存图文件名是否默认追加时间戳后缀（V1.12.18）。
        /// true(默认)：最终文件名 = 模板渲染结果 + "_" + 时间戳(yyyyMMdd_HHmmss_fff)。
        ///   防止"同点位重复拍照/重复触发"时覆盖旧图——现场同点位可能被多次触发，
        ///   每张图都要保留（旧版仅靠 _2/_3 递增兜底，命名不清）。
        /// false：保持模板渲染结果原名（模板带 {时间} 时基本不重名，此开关仅作保险）。
        /// </summary>
        public bool FileTimestampSuffix { get; set; } = true;

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
        /// 通讯方式（大小写不敏感）：
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
        public string IpAddress { get; set; } = "19.87.6.100";

        /// <summary>扫码枪 TCP 端口（仅 Mode=Tcp 使用，基恩士 SR 无协议默认端口，现场确认）</summary>
        public int Port { get; set; } = 9004;

        /// <summary>
        /// TCP 模式连接成功后的"触发/启动读码"指令（V1.12.0 现场实测，仅 Mode=Tcp 使用）。
        /// 基恩士 SR 系列无协议通讯：上位机连接后要先发打开激光/开始读取的指令，扫码枪才会
        /// 开始读码并推送条码；本现场实测指令为 "LON"（Laser ON），帧尾补 CRLF。
        /// 发送时自动在该指令后补 "\r\n" 帧结束符（读码端大小写不敏感，见 ScannerTcpService.SendTriggerCommand）。
        /// 留空则不发送（对应扫码枪设为"上电自动连续读码"模式的场景）。
        /// </summary>
        public string TriggerCommand { get; set; } = "LON";
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

        // ─────────────────── 开发者账号（V1.12.0，功能测试登录）───────────────────
        //
        // 【为什么加这个账号】PLC 业务逻辑未写完时，需要单独验证"相机↔上位机""PLC↔上位机"
        //   的通讯链路。若用管理员账号登录会进系统设置（改配置风险高），也不符合角色职责。
        //   开发者账号登录后进的是【功能测试窗体 DevTestForm】——只做通讯触发/读写验证，
        //   复用主窗体已建好的连接（不重复建连），不碰业务配置。
        //
        // 【安全说明】与管理员密码同一套规则：只存 SHA-256 哈希、不存明文。
        //   开发者密码暂不支持在界面上修改（改密码面板仅服务管理员），
        //   如需变更请用 SecurityUtil.HashPassword 算好哈希后改本字段/配置文件。
        //   开发者登录默认不参与"记住密码"（回填逻辑只认管理员账号）。

        /// <summary>是否启用开发者账号登录（false 则开发者账号无法通过登录校验）</summary>
        public bool DevEnabled { get; set; } = true;

        /// <summary>开发者用户名（默认 "dev"，登录时大小写不敏感比对）</summary>
        public string DevUser { get; set; } = "dev";

        /// <summary>
        /// 开发者密码的 SHA-256 哈希（hex 小写）。默认值是 "dev123" 的哈希，
        /// 即出厂默认开发者密码 dev123。改密码请参考类注释说明，此处只落哈希。
        /// </summary>
        public string DevPasswordHash { get; set; }
            = "87274af01876341455b32d805946f272871bb42effa6604dccf28bb027afa82b";
    }
}