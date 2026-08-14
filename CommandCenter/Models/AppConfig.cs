using System;
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
        /// 【默认值（V1.12.22 现场定稿；V2.13.3 修正 FTP 目录）】现场固定两台相机，
        /// 相机1=上相机=19.87.6.213、相机2=下相机=19.87.6.212，FTP 取图目录为上相机
        /// D:\IV存图\2 / 下相机 D:\IV存图\1（注意：上/下相机的取图目录与安装位置相反配对，
        /// 现场实测相机推图目标如此——上相机推到 \2、下相机推到 \1，见
        /// CameraConfig.DefaultCameras），无配置/空配置时用它兜底这两台。
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

        /// <summary>
        /// 固定产品型号（V2.7 协议）：每次扫码完成，上位机把本值写入 PLC 协议 40007~40011
        /// （最多 10 个 ASCII 字符，多余部分用 0x00 补齐）。现场型号固定不变，改这里即可，
        /// 不用每次从 SN 解析。设置窗体"系统设置"里可改。
        /// 【V2.8 型号切换】型号同时决定"点位→相机程序号"映射查哪张表（见
        ///   CameraConfig.ModelStationPrograms）——切型号时把本值改成对应型号，上位机
        ///   就会按该型号的表切相机程序。保存热更后立即生效（coordinator 重建）。
        /// 【V2.12.3 默认 U171】默认值=预置型号第一个（现场产线首款型号）。窗口总数=各相机
        ///   按当前型号点位表条目和（见 DisplayConfig.WindowCountFor），默认值非空才能保证
        ///   【无配置文件首次启动就有对应该型号点位的窗口数】（此前默认空串导致首启按"空型号"
        ///   查点位表 → 全部回退空默认表 → 窗口数塌成 1，主界面只显示一个窗口）。
        /// </summary>
        public string ProductModel { get; set; } = "U171";

        /// <summary>
        /// 产品型号候选列表（V2.8）：现场可切换的型号清单，默认预置现场型号
        ///   ["U171", "Z121"]（见 DefaultProductModels）。
        /// 用途：
        ///   ① 设置窗体"产品型号"可编辑下拉的候选项（也可手动输入新型号，保存时自动加入本列表）；
        ///   ② "窗口/点位配置…"里"点位→相机程序号"映射按型号分表编辑时的型号下拉候选。
        /// 【为什么不写成属性初始化器默认值】与 Cameras 同理（V1.9.9）：Newtonsoft 反序列化对
        ///   已有实例的集合是复用并 Add 进 json 元素而非整值替换，预置默认会把 json 里的型号
        ///   叠加重复。故初始化器给空列表，默认型号统一交给 ConfigStore.Load 的"空/缺省兜底"。
        /// </summary>
        public List<string> ProductModels { get; set; } = new List<string>();

        /// <summary>
        /// 现场默认产品型号候选（V2.8，见 ProductModels）。两型号对应现场两套相机程序映射：
        ///   U171：上相机点位 1~20、下相机点位 1~4；Z121：上相机点位 1~26、下相机点位 1~3。
        /// 返回全新列表实例，调用方可直接 AddRange/复制，不共享引用。
        /// </summary>
        public static List<string> DefaultProductModels() =>
            new List<string> { "U171", "Z121" };

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
        /// <summary>
        /// 相机编号/相机ID（V2.13.4）：**基恩士相机真正的编号**，与存图目录号一一对应——
        /// 上相机=`D:\IV存图\2`→CameraId=2、下相机=`D:\IV存图\1`→CameraId=1
        /// （V2.13.3 已确认存图地址与安装位置相反配对：上相机推到 \2、下相机推到 \1）。
        /// 设置窗体相机表第一列显示/编辑本值（从"行序序号"升级为"真正编号"，见 SettingsForm）。
        /// 【为什么不能直接用列表行序】PLC 通道地址按"相机列表位置"自动分配（第1台=协议40002=上相机），
        ///   列表顺序一旦为迁就编号而交换，40002 就会从"上相机"变成"下相机"，与 PLC 主站程序冲突。
        ///   故相机真编号独立存本字段，列表顺序保持不变；显示/日志归属用 CameraId（>0），
        ///   CameraId=0（旧配置无此字段）时回退"相机N"（N=列表位置 1 起的行序）兼容。
        /// 纯标识/展示字段，不参与任何通讯逻辑（PLC 通道仍按列表位置、存图点位仍按相机下标）。
        /// </summary>
        public int CameraId { get; set; }

        /// <summary>
        /// 相机名称/位置（V1.12.22，界面与日志显示用）：如"上相机"/"下相机"。
        /// 现场按安装位置称呼（上相机=19.87.6.213、下相机=19.87.6.212，CameraId 分别为 2/1，
        /// 见 DefaultCameras 与 docs/CommandCenter.md §2.3/§7），显示在下拉框/状态灯文案里，
        /// 空值则界面只显示"相机N IP"。纯展示字段，不影响任何通讯逻辑。
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>相机 IP（V1.12.22 现场定稿：相机1=上相机=19.87.6.213，相机2 见 DefaultCameras）</summary>
        public string IpAddress { get; set; } = "19.87.6.213";

        /// <summary>
        /// 该相机的 FTP 上传目录（相机作为 FTP 客户端把照片推到这台，独立监听）。
        /// 为空时回退用全局 ImageConfig.FtpRootDir——多台相机务必各自配不同目录，否则图会混。
        /// </summary>
        public string FtpUploadDir { get; set; } = "";

        /// <summary>控制指令发送端口（基恩士无协议通信常用 8500，按现场实际改）</summary>
        public int CommandPort { get; set; } = 8500;

        /// <summary>
        /// 本相机在 PLC 从站的"拍照请求" DataStore 索引（V2.12.6 起每台相机一路 PLC 通道）。
        /// PLC 触发本相机拍照时写该地址=点位编号（1~255）、读走结果后复位写 0。
        /// 配置=DataStore 索引（PLC 协议号 = 索引 + 40000，填 2 就是协议 40002）。
        /// 【V2.13.4 起显式配置，废除"0=按相机序号自动"】每台相机的通道地址独立存本字段：
        ///   现场上相机=2（协议40002）、下相机=3（协议40003），默认相机已在此预置（见
        ///   DefaultCameras）。0=未配置该相机通道——运行时不轮询此相机（请求恒视为无），
        ///   新增相机必须与 PLC 梯形图协商好寄存器（建议请求 40008 起，避开扫码 1/型号 7~11、
        ///   扫码结果 4、上相机 2/下相机 3）后在此显式填写。列表顺序从此不再决定通道地址。
        /// </summary>
        public int PlcRequestAddress { get; set; }

        /// <summary>
        /// 本相机在 PLC 从站的"拍照结果" DataStore 索引（上位机写：0=复位、1=OK、2=NG、3=点位禁用跳过）。
        /// 【V2.13.4 起显式配置，废除"0=按相机序号自动"】上相机=5（协议40005）、下相机=6（协议40006），
        /// 默认相机已在此预置；0=未配置结果通道（WriteCameraResult 跳过该台不写）。
        /// </summary>
        public int PlcResultAddress { get; set; }

        // ─── IV4 无协议通信指令表（《IV4 通信、连接指南》）───
        // 指令均以 CR(0x0D) 终止；T 系列指令含义见 docs/CommandCenter.md 第四部分

        /// <summary>仅触发拍摄指令（T1[CR]），响应回显 T1。用于"只触发、判定另取"场景。</summary>
        public string TriggerCommand { get; set; } = "T1";

        /// <summary>触发＋读取判定结果指令（T2[CR]），响应 RT, 工具结果(标准/详细)[CR]。</summary>
        public string TriggerAndReadCommand { get; set; } = "T2";

        /// <summary>单独读取判定结果指令（RT[CR]），响应同 T2。</summary>
        public string ReadResultCommand { get; set; } = "RT";

        /// <summary>
        /// 切换相机程序编号（PW 指令，0~127，负值=不切换）。
        /// ⚠️ **V1.12.25 起废弃（点位级映射接管）**：触发切程序不再读本字段，改由
        ///    StationPrograms（点位→程序号映射表）决定——每台相机在表里配了哪些点位、
        ///    就切到对应的程序号；没配的点位一律不切换（保持相机当前程序）。
        ///    本字段仅保留给旧配置兼容（防止老 json 里的 programNo 被反序列化时丢失，无害）。
        /// </summary>
        public int ProgramNo { get; set; } = -1;

        /// <summary>
        /// 本相机的"点位→相机程序号"映射表（V1.12.25，设置页 WindowPointForm 同页编辑）。
        ///
        /// 【为什么用"每相机一张表"】现场是"28 个窗口（点位）对应两台相机"：上相机拍一部分点位、
        /// 下相机拍另一部分（比如上相机管前 14 个点、下相机管后 14 个），不是每台相机都拍全部点位。
        /// 所以点位→程序号必须按相机分表：某相机表里配了哪些点位 == 这台相机负责拍哪些点位；
        /// 且不同相机的同名程序（P000/P001…）是各相机自己的程序库，互相独立，必须各自配置。
        ///
        /// 【V2.8 按型号分表】本字段作为"无型号/默认"的表兜底——运行时优先查本相机
        ///   ModelStationPrograms 里与当前产品型号同名的那张表（见 ProductionCoordinator
        ///   ResolveProgramForStation），没配该型号的表才回退本默认表。历史上本字段是唯一映射源，
        ///   保留它保证旧配置兼容、也让"不管型号"的场景不用每个型号都建表。
        ///
        /// 【触发逻辑】ProductionCoordinator.TriggerOneCamera 在触发前查出"本轮该相机要填的窗口"对应
        /// 的点位号，在本表里查该点位对应的程序号：命中→发 PW 切换后再触发；未命中→不切换
        /// （该点位不归本相机拍，或还没配映射，保持相机当前程序）。不会像旧固定 ProgramNo 那样误切。
        ///
        /// 【JSON 形态】stationPrograms: [ { "stationNo":1, "programNo":0 }, ... ]，小驼峰。
        /// 点位号对应存图点位（DisplayConfig.WindowStationMap 的取值，1~9999）；程序号 0~127 合法（0 也是程序）。
        /// </summary>
        public List<StationProgramItem> StationPrograms { get; set; } = new List<StationProgramItem>();

        /// <summary>
        /// 本相机按【产品型号】分组的"点位→相机程序号"映射表（V2.8，设置页 WindowPointForm 型号下拉编辑）。
        ///
        /// 【为什么按型号分表】现场同一台相机的程序库分型号：U171/Z121 等不同产品型号对应的相机
        ///   程序号不同（例：上相机型号 U171 用 P000~P012、型号 Z121 用 P013~P028，点位归属也不同）。
        ///   型号列表来自 AppConfig.ProductModels，切型号后查对应型号的表切程序，型号没配的表就
        ///   回退 StationPrograms 默认表。每张表结构同 StationPrograms（点位→程序号）。
        ///
        /// 【JSON 形态】modelStationPrograms: [
        ///     { "modelName":"U171", "programs":[ { "stationNo":1, "programNo":0 }, ... ] }, ... ]。
        /// </summary>
        public List<ModelStationPrograms> ModelStationPrograms { get; set; } = new List<ModelStationPrograms>();

        /// <summary>
        /// 当前产品型号下的"点位→程序号"映射表（V2.12.1，自适应布局/窗口解析共用）：
        ///   ① 优先在 ModelStationPrograms 里找与指定型号同名的那张表（大小写不敏感），命中即返回它；
        ///   ② 型号没配表 / ModelStationPrograms 为空 → 回退默认表 StationPrograms（旧兼容 + 不区分型号）。
        /// 返回的列表可能为空（该相机在当前型号下没有任何点位配置），调用方按空表处理即可。
        /// 【为什么抽出来】自适应窗口布局（窗口总数=各相机点位和）、运行时"PLC点位→窗口"解析、
        ///   点位矩阵相机标签展示三处都需要"按型号取某相机的点位表"，收敛成一处查表逻辑避免三处各写。
        /// </summary>
        public List<StationProgramItem> ProgramsFor(string productModel)
        {
            if (!string.IsNullOrWhiteSpace(productModel) && ModelStationPrograms != null)
            {
                foreach (var m in ModelStationPrograms)
                {
                    if (m != null && m.Programs != null
                        && string.Equals(m.ModelName, productModel, StringComparison.OrdinalIgnoreCase))
                        return m.Programs;
                }
            }
            return StationPrograms ?? new List<StationProgramItem>();
        }

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
        /// 现场默认的两台相机（V1.12.22 定稿：相机1=上相机、相机2=下相机）。
        /// 相机1=上相机=19.87.6.213 → FTP 取图目录 D:\IV存图\2；
        /// 相机2=下相机=19.87.6.212 → FTP 取图目录 D:\IV存图\1。
        /// ⚠️【V2.13.3 目录互换修正】上/下相机的 FTP 取图目录与"安装位置"相反配对（上相机推到
        ///   \2、下相机推到 \1）——这是现场实测相机推图目标（基恩士工程师配置如此）。
        ///   此前版本默认写成"上相机→\1、下相机→\2"，导致：相机触发走 IP 指令【正确】，
        ///   但取图扫 FTP 目录【拿到对面相机的图】——MainForm 窗口显示的图片与触发相机错位。
        ///   本次互换回来；设备推图方向不变，仅是"上位机取图目录"对齐实际推图位置。
        /// 其余参数取模型默认。
        /// 【V2.8 预置型号映射】每台相机按型号预置"点位→程序号"表（ModelStationPrograms，
        ///   现场定稿，见下）：切到对应型号后触发相机前自动切程序。
        ///   上相机：U171→点位1~20/P000~P012、Z121→点位1~26/P013~P028；
        ///   下相机：U171→点位1~4/P000~P003、Z121→点位1~3/P005~P007。
        ///   没列出的型号（如下相机 U171 的点位不在下相机表里）运行时查不到型号表，
        ///   回退默认表 StationPrograms（本次未预置 → 不切换程序，保持相机当前程序）。
        /// 【为什么收敛成一个方法】三处需要"默认两台相机"（未配置时 AppConfig.Cameras 的
        ///   初值、主窗体 BuildServices 的空配置兜底、设置窗体空表格默认行/添加行），
        ///   若各自硬编码 IP，改现场 IP 要改好几个地方、极易漏。统一走本方法，
        ///   现场换相机 IP 只改这一处即可。
        /// 【注意】返回的是全新实例列表，调用方可直接 AddRange/遍历，不会与原来的配置共享引用。
        /// </summary>
        public static List<CameraConfig> DefaultCameras()
        {
            return new List<CameraConfig>
            {
                new CameraConfig
                {
                    Name = "上相机",
                    CameraId = 2,   // V2.13.4：基恩士真编号=存图目录号（上相机→D:\IV存图\2）
                    IpAddress = "19.87.6.213",
                    FtpUploadDir = @"D:\IV存图\2",
                    PlcRequestAddress = 2,   // 协议 40002 上相机请求（V2.13.4 起显式配置，不再按列表序自动）
                    PlcResultAddress = 5,    // 协议 40005 上相机结果
                    ModelStationPrograms = new List<ModelStationPrograms>
                    {
                        new ModelStationPrograms { ModelName = "U171", Programs = Table(
                            (1, 0), (2, 1), (3, 2), (4, 2), (5, 2), (6, 2), (7, 3), (8, 4),
                            (9, 5), (10, 6), (11, 7), (12, 8), (13, 9), (14, 10), (15, 10),
                            (16, 11), (17, 12), (18, 10), (19, 10), (20, 1)) },
                        new ModelStationPrograms { ModelName = "Z121", Programs = Table(
                            (1, 13), (2, 14), (3, 14), (4, 28), (5, 15), (6, 15), (7, 15),
                            (8, 15), (9, 15), (10, 16), (11, 17), (12, 18), (13, 18),
                            (14, 19), (15, 20), (16, 21), (17, 21), (18, 22), (19, 23),
                            (20, 19), (21, 24), (22, 25), (23, 26), (24, 26), (25, 27), (26, 27)) }
                    }
                },
                new CameraConfig
                {
                    Name = "下相机",
                    CameraId = 1,   // V2.13.4：基恩士真编号=存图目录号（下相机→D:\IV存图\1）
                    IpAddress = "19.87.6.212",
                    FtpUploadDir = @"D:\IV存图\1",
                    PlcRequestAddress = 3,   // 协议 40003 下相机请求（V2.13.4 起显式配置，不再按列表序自动）
                    PlcResultAddress = 6,    // 协议 40006 下相机结果
                    ModelStationPrograms = new List<ModelStationPrograms>
                    {
                        new ModelStationPrograms { ModelName = "U171", Programs = Table(
                            (1, 0), (2, 1), (3, 2), (4, 3)) },
                        new ModelStationPrograms { ModelName = "Z121", Programs = Table(
                            (1, 5), (2, 6), (3, 7)) }
                    }
                }
            };
        }

        /// <summary>
        /// 构建一张"点位→程序号"映射表（默认相机预置表用，见 DefaultCameras 与
        /// ModelStationPrograms）。每个参数是 (点位, 程序号) 元组，直接转成 StationProgramItem。
        /// 程序号范围 0~127（0 是合法程序 P000），越界值可在触发时由 SwitchProgram 自动夹取。
        /// </summary>
        private static List<StationProgramItem> Table(params (int station, int program)[] rows)
        {
            var list = new List<StationProgramItem>();
            if (rows != null)
            {
                foreach (var r in rows)
                    list.Add(new StationProgramItem { StationNo = r.station, ProgramNo = r.program });
            }
            return list;
        }
    }

    /// <summary>
    /// 单个"点位→相机程序号"映射条目（V1.12.25，装在 CameraConfig.StationPrograms 列表里）。
    /// 每个条目含义：本相机在 Photograph/拍摄"点位 StationNo"前，先发 PW 把相机切到"程序 ProgramNo"。
    /// - StationNo：存图点位号（1~9999，与 DisplayConfig.WindowStationMap 的取值一致；对应上位机"检测点位"）；
    /// - ProgramNo：相机程序号（0~127 合法，0 也是真实程序——默认配置注意别把 0 当"未设置"）。
    /// 触发时按当前点位查本表，命中→切换，未命中→不切换（该点位不归本相机/还没配映射）。
    /// </summary>
    public class StationProgramItem
    {
        /// <summary>拍照点位号（1~9999，相机局部点位：上下相机各自从 1 起、会重复，
        /// 与"存图 {点位}"同源，存图靠 {相机} 目录层隔离；见 CameraConfig.ProgramsFor 注释）。</summary>
        public int StationNo { get; set; }

        /// <summary>该点位在本相机上对应的相机程序号（0~127，0 合法）</summary>
        public int ProgramNo { get; set; } = -1;
    }

    /// <summary>
    /// 某个【产品型号】下，本相机的"点位→相机程序号"映射表（V2.8，装在
    /// CameraConfig.ModelStationPrograms 列表里，每个型号一张表）。
    /// - ModelName：产品型号名（对应 AppConfig.ProductModels 里的型号，如 "U171"；
    ///   运行时与 AppConfig.ProductModel 按名称匹配，大小写不敏感）；
    /// - Programs：与 StationPrograms 相同结构的点位→程序号表（StationProgramItem 列表）。
    /// 触发切程序时按当前型号查本表；型号没配表 → 回退 CameraConfig.StationPrograms 默认表。
    /// </summary>
    public class ModelStationPrograms
    {
        /// <summary>产品型号名（与 AppConfig.ProductModel/ProductModels 对应，如 "U171"）</summary>
        public string ModelName { get; set; } = "";

        /// <summary>该型号下本相机的"点位→程序号"映射表（结构同 StationPrograms）</summary>
        public List<StationProgramItem> Programs { get; set; } = new List<StationProgramItem>();
    }

    /// <summary>
    /// 单个"窗口→(相机, 点位)"映射条目（V2.13，装在 ModelWindowPointMap.Points 列表里；
    /// V2.13.4 起关联键由"相机列表下标"升级为"相机ID CameraId"）。
    /// 含义：某个显示窗口对应"相机 CameraId 的点位 StationNo"。
    /// - CameraId：相机 ID（CameraConfig.CameraId，基恩士真编号，现场上=2/下=1）。
    ///   【为什么不用列表下标（V2.13.4 彻底重构）】点位归属是相机的"身份"属性，必须跟相机本身走；
    ///   若存列表下标，一旦相机增删/换序，映射就错位指向别的相机。存 CameraId 则列表顺序彻底自由
    ///   （PLC 通道由每台相机自己的 PlcRequestAddress 决定，不再按列表位置推导）。
    ///   旧配置（V2.13 存的 cameraIndex）反序列化后 CameraId=0，由 ConfigStore 检测后重置默认铺排。
    /// - StationNo：相机点位号（1~9999，本相机点位表里的点位号，上下相机各自从 1 起会重复）。
    /// 运行时 PLC 请求 (相机, 点位) → 反查本表定位唯一窗口（见 ProductionCoordinator
    /// TryResolveActiveWindow）；存图点位=StationNo（文件名 {点位}，靠 {相机} 目录层隔离）。
    /// </summary>
    public class WindowPointItem
    {
        /// <summary>相机 ID（CameraConfig.CameraId，基恩士真编号，上=2/下=1；0=旧配置回退行序）</summary>
        public int CameraId { get; set; }

        /// <summary>相机点位号（1~9999，本相机点位表里的点位号）</summary>
        public int StationNo { get; set; }
    }

    /// <summary>
    /// 某个【产品型号】下，窗口↔点位 的独立映射表（V2.13，装在
    /// DisplayConfig.WindowPointMaps 列表里，每个型号一张表）。
    /// - ModelName：产品型号名（与 AppConfig.ProductModel/ProductModels 对应，如 "U171"）；
    /// - Points：窗口→(相机,点位) 映射（长度 = 该型号窗口总数 = 各相机点位表条目和）。
    /// 默认铺排 = "前上相机后下相机、各相机点位表顺序"（DefaultWindowPointMap）；
    /// 手动编辑点位/交换位置后此表偏离默认；恢复默认=重置为默认铺排。
    /// </summary>
    public class ModelWindowPointMap
    {
        /// <summary>产品型号名（与 AppConfig.ProductModel/ProductModels 对应，如 "U171"）</summary>
        public string ModelName { get; set; } = "";

        /// <summary>窗口→(相机,点位) 映射（长度=该型号窗口总数，Points[i]=窗口 i+1）</summary>
        public List<WindowPointItem> Points { get; set; } = new List<WindowPointItem>();
    }

    /// <summary>
    /// PLC 通讯配置（V1.12.11 起角色反转：现场汇川 PLC 做 Modbus TCP 主站，上位机做从站）。
    /// 上位机监听本机 Port 端口（标准 502），等汇川主站 TCP 连入并读写上位机的保持寄存器区。
    /// IpAddress = 上位机监听绑定 IP（"0.0.0.0"=监听所有网卡，现场主机多网卡时可绑指定 IP）。
    ///
    /// 【V2.7 协议（docs/CommandCenter.md §5.5）】请求-结果-复位三拍式握手：
    ///   PLC只写（上位机读）：40001 扫码请求(0/1)、40002 上相机拍照请求(1~255=点位)、40003 下相机拍照请求；
    ///   PLC只读（上位机写）：40004 扫码结果(0/1/2)、40005 上相机结果、40006 下相机结果、
    ///                        40007~40011 产品型号(10 字符 ASCII，每寄存器 2 字符，高字节在前)。
    ///   流程：PLC 写请求=非0 → 上位机处理完写结果≠0 → PLC 读结果并复位请求=0 →
    ///         上位机看到请求=0 再复位结果=0，进入下一请求。
    ///   【地址说明（V2.12.3 定稿，替换 V2.12.2 的"减 40000 换算"）】配置里的地址字段统一存
    ///   【DataStore 索引】，PLC 侧协议号 = 索引 + 40000（协议 40002 上相机请求 → 索引 2）。
    ///   现场实测：PLC 写协议 40002 → 从站 DataStore[2]（功能测试 txtReadAddr 填 2 即读到），
    ///   配置直接填索引、业务层【零换算】（PlcService 已删除 ProtocolToIndex）。索引就是
    ///   汇川 PLC D 区习惯叫的 "D2/D3/D5" 这类数字，填 2 就是 2，不搞 40000 段的操作。
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

        // ─── 寄存器地址映射（V2.7 协议，见 docs/CommandCenter.md §5.5）───
        // 设计原则：定长请求放前面，结果与变长数据（型号）放后面，地址可向后扩展。

        /// <summary>PLC→上位机：扫码请求（V2.7）。PLC 写 1=请求扫码、0=无请求；上位机读到 1 触发扫码枪。配置=DataStore 索引 1（协议 40001）。</summary>
        public ushort ScanRequestAddress { get; set; } = 1;

        /// <summary>上位机→PLC：扫码结果（V2.7）。0=默认/复位，1=扫码OK，2=扫码NG（超时）。配置=索引 4（协议 40004）。</summary>
        public ushort ScanResultAddress { get; set; } = 4;

        /// <summary>上位机→PLC：产品型号序号地址（V2.14.13，协议 40007）。每次写型号时先把"该型号对应的序号"
        /// 写入本寄存器（型号序号来自 ModelIndexes 映射，见下方字段与 docs/CommandCenter.md §5.5）；
        /// PLC 拿 40007 的序号即可快速区分型号（如 Z121=1、U171=2），不必解析型号字符串。</summary>
        public ushort ProductModelIndexAddress { get; set; } = 7;

        /// <summary>上位机→PLC：产品型号起始地址（V2.14.13，连续写 ProductModelLen 个寄存器，最多 10 字符）。
        /// 配置=索引 8（协议 40008）——40007 已让给型号序号，型号字符串整体后移一位从 40008 起写。</summary>
        public ushort ProductModelAddress { get; set; } = 8;

        /// <summary>产品型号寄存器数（V2.14.13，每个寄存器 2 字符，默认 5 个=10 字符；超 10 字符按文档
        /// 从索引 13（协议 40013）扩展地址后调整本值，40007 序号位不受影响）。</summary>
        public int ProductModelLen { get; set; } = 5;

        /// <summary>
        /// 产品型号 → PLC 型号序号 映射表（V2.14.13，JSON `modelIndexes`）：
        /// 每个产品型号对应一个 PLC 序号（写 40007），现场默认 Z121=1、U171=2（见 DefaultModelIndexes）。
        /// 设置窗体 PLC 区"型号→PLC序号"表格里维护（可增删行、改序号），运行时
        /// `PlcService.WriteProductModel` 按型号名（忽略大小写）查本表得序号写 40007；
        /// 型号没配序号时写 0（PLC 端视为未配置）。
        /// 【为什么放在 PlcConfig】序号是"型号在 PLC 通讯里的编码"，只被 PLC 写型号流程使用，
        /// 放 PLC 配置里与 40007 地址语义内聚；设置页 PLC 区同时展示地址与映射，一站式配置。
        /// </summary>
        public List<ModelIndexItem> ModelIndexes { get; set; } = new List<ModelIndexItem>();

        /// <summary>现场默认"产品型号 → PLC 序号"映射（V2.14.13，见 ModelIndexes）：
        /// Z121=1、U171=2。返回全新列表实例，调用方可直接修改/复制，不共享引用。</summary>
        public static List<ModelIndexItem> DefaultModelIndexes() =>
            new List<ModelIndexItem>
            {
                new ModelIndexItem { ModelName = "Z121", ModelIndex = 1 },
                new ModelIndexItem { ModelName = "U171", ModelIndex = 2 },
            };
    }

    /// <summary>
    /// 型号→PLC 序号映射项（V2.14.13，JSON 数组元素 `modelIndexes`）：一个产品型号对应一个 PLC 序号。
    /// - ModelName：产品型号名（与 AppConfig.ProductModel/ProductModels 对应，如 "Z121"，匹配忽略大小写）；
    /// - ModelIndex：该型号在 PLC 40007 寄存器里的序号（&gt;0 有效，0=未配置/不写序号）。
    /// </summary>
    public class ModelIndexItem
    {
        /// <summary>产品型号名（与 ProductModel/ProductModels 对应，如 "Z121"）</summary>
        public string ModelName { get; set; } = "";

        /// <summary>该型号的 PLC 型号序号（写 40007，&gt;0 有效，0=未配置）</summary>
        public int ModelIndex { get; set; }
    }

    /// <summary>
    /// 主界面显示配置：决定显示窗口矩阵、标题栏显示哪些项、OK/NG 颜色。
    /// V2.12.1 起窗口总数由"相机点位表"唯一决定（见 WindowCountFor），Rows/Columns
    /// 只在非自适模式下决定"排列宽度"（列数），行数不够自动补（见 ResolveLayout）。
    /// 【V2.13 窗口↔点位独立映射（WindowPointMaps）】在"窗口总数=各相机点位表条目和"的
    /// 前提下，允许手动调整"每个窗口显示哪个相机的哪个点位"（编辑点位/交换位置/恢复默认），
    /// 见 WindowPointMaps 字段注释。
    /// </summary>
    public class DisplayConfig
    {
        /// <summary>显示窗口行数（非自适模式下"期望行数"，放不下时自动补行；自适应下不使用）</summary>
        public int Rows { get; set; } = 4;

        /// <summary>显示窗口列数（非自适模式下决定矩阵宽度；自适应下不使用）</summary>
        public int Columns { get; set; } = 7;

        /// <summary>
        /// 显示窗口矩阵是否"自适应"（V2.12.0）：窗口行列的形状是否按点位自动算。
        /// 【窗口总数】无论勾不勾都 = 各相机按当前运营产品型号查到的"点位→程序号"表
        ///   （ModelStationPrograms 对应型号表，型号没配表回退 StationPrograms 默认表）
        ///   点位条目数之和（上相机点位 + 下相机点位）——点位由相机点位表唯一决定，
        ///   上下相机点位号各自从 1 起会重复，窗口只是把点位条目"前上相机后下相机"
        ///   顺序铺排的格子（表里第 i 条点位 = 该相机起始窗口 + i）。
        /// 【行列形状】
        ///   - 自适应（V2.14 起）：列最多 7，遍历列数取"行列和最小"（尽量接近方形）、
        ///     并列时列多者优先（优先增加列），让最后一行缺失的窗口最少；
        ///     例：1→1×1（单窗占满）、2→1×2、3→1×3、4→2×2、5→2×3、6→2×3、7→2×4、
        ///     22→4×6、28→4×7（铺满）。行多放不下时主窗体自动出竖直滚动条（见
        ///     MainForm.ApplyGridScrollLayout）。
        ///   - 非自适应：列 = 手填 Columns、行 = 手填 Rows（乘积不够自动补行），
        ///     手填只决定排列宽度，窗口数仍=点位和，保证与自适应窗口↔点位一一对应；
        ///     主窗体显示时两种模式走同一套"按显示区高度动态铺满/滚动"逻辑（V2.14.15：
        ///     行数≤10 一律铺满平分显示区高度，行高=显示区高/行数，与自适应铺满效果一致，
        ///     见 MainForm.ApplyGridScrollLayout）。
        /// 勾选自适应后，设置在设置窗体的"显示窗口"行、切型号（标题栏/设置页）时自动重算；
        /// 【为什么加】现场 28 个窗口点位由两台相机分工，窗口数与型号点位强相关，手填行列容易
        /// 对不上；让窗口数跟随点位自动铺排，换型号即所见即所得。
        /// 【V2.14.1 默认勾选】默认值由 false 改 true：现场点位表是窗口数的唯一来源，自适应铺排
        /// 与存图点位/窗口映射解耦（V2.13 起点位可手动编辑，与行列形状无关），默认自适应更省心；
        /// 需要固定排列宽度时再取消勾选手填行列。
        /// </summary>
        public bool AutoFit { get; set; } = true;

        /// <summary>
        /// 窗口总数（V2.12.1）：各相机按"当前产品型号"点位表 ProgramsFor(型号) 条目数之和，
        /// 至少 1（防除 0/空矩阵）。**无论是否勾选"自适应"，窗口数都按这个算**——
        /// 点位由相机点位表唯一决定（上下相机点位号各自从 1 起、会重复），窗口只是将这些
        /// 点位条目"前上相机后下相机"顺序铺排的格子，"自适应"开关只影响行列的形状计算。
        /// 主窗体矩阵 / WindowPointForm / 协调器 / ConfigStore / 设置页预览统一走本方法，
        /// 禁止各层再各写一套窗口数计算。
        /// </summary>
        public static int WindowCountFor(IEnumerable<CameraConfig> cameras, string productModel)
        {
            int total = 0;
            foreach (var cam in cameras ?? new List<CameraConfig>())
                if (cam != null)
                    total += cam.ProgramsFor(productModel).Count;
            return Math.Max(1, total);
        }

        /// <summary>
        /// 统一窗口布局计算（V2.12.1）：返回 (行, 列, 窗口总数)，所有使用窗口矩阵的层共用。
        /// 【窗口总数】恒 = WindowCountFor（相机点位表条目和，≥1）——点位由相机表唯一决定。
        /// 【行列形状】
        ///   - 自适应（autoFit=true，V2.14 新规则）：列数遍历 1..min(7,总数)，行 = ceil(总数/列)，
        ///     取"行+列"最小的方案（越接近方形越好），并列时列数多者优先（优先增加列）——
        ///     效果是让缺的格子集中在最后一行且最少，窗口尽量大、占满显示区域；
        ///     例：1→1×1、2→1×2、3→1×3、4→2×2、5→2×3、6→2×3、7→2×4、28→4×7。
        ///   - 非自适应（autoFit=false）：列 = 手动 columns（≥1，上限 7，与自适应一致）、
        ///     行 = 手动 rows（≥1）；手填的 rows×columns 若放不下全部窗口，自动补行到
        ///     ceil(总数/列)（多余格子留空），保证"窗口↔相机点位一一对应"与自适应一致，
        ///     手填行列只决定排列宽度。
        /// 主窗体 BuildWindowGrid / WindowPointForm / 设置页预览 / ConfigStore 统一调用。
        /// </summary>
        public static (int rows, int cols, int windowCount) ResolveLayout(
            IEnumerable<CameraConfig> cameras, string productModel,
            bool autoFit, int manualRows, int manualCols)
        {
            int total = Math.Max(1, WindowCountFor(cameras, productModel));
            if (autoFit)
            {
                // V2.14 自适应铺排：遍历所有可用列数，选"行列和最小"者（尽量接近方形）、
                // 并列时列多者优先（优先增加列）。同时满足"窗口尽量放大占满显示区"（行少）与
                // "最后一行缺失最少"（缺格 R*C-N 在方形排布下自然少）。行多为极端场景，
                // 由主窗体 ApplyGridScrollLayout 出滚动条承接，这里只保证形状。
                int bestRows = 1, bestCols = 1, bestSum = int.MaxValue;
                int maxCols = Math.Min(7, Math.Max(1, total));   // 列数最多 7
                for (int cols = 1; cols <= maxCols; cols++)
                {
                    int rows = (total + cols - 1) / cols;        // ceil(total / cols)
                    int sum = rows + cols;
                    if (sum < bestSum || (sum == bestSum && cols > bestCols))
                    { bestSum = sum; bestRows = rows; bestCols = cols; }
                }
                return (bestRows, bestCols, total);
            }
            // 非自适应：列 = 手填 columns、行 = 手填 rows（≥1）。列数上限 7（V2.14.15 与自适应一致，
            // 见 SettingsForm.nudCols.Maximum）——这里钳位是双保险：即使配置被手改成超限值，
            // 主窗体矩阵也最多 7 列，两种模式的列上限恒一致。手填 rows×columns 若放不下全部窗口，
            // 自动补行到 ceil(总数/列)（多余格子留空），保证"窗口↔相机点位一一对应"与自适应一致。
            int cols2 = Math.Max(1, Math.Min(7, manualCols));
            int rows2 = Math.Max(1, manualRows);
            if (rows2 * (long)cols2 < total)                 // 手填行列放不下全部窗口 → 自动补行
                rows2 = (int)Math.Ceiling(total / (double)cols2);
            return (rows2, cols2, total);
        }

        /// <summary>
        /// 计算自适应布局（V2.12.0，等价于 ResolveLayout(autoFit:true)）：根据相机列表 + 当前产品型号，
        /// 返回 (行, 列, 窗口总数)。窗口总数 = 各相机 ProgramsFor(型号) 点位表条目数之和（至少 1）。
        /// 主窗体 BuildWindowGrid / 设置窗体预览 / 协调器等处统一调用，保证各层看到的行列一致。
        /// </summary>
        public static (int rows, int cols, int windowCount) AutoFitLayout(
            IEnumerable<CameraConfig> cameras, string productModel)
        {
            return ResolveLayout(cameras, productModel, true, 1, 1);
        }

        /// <summary>
        /// 计算自适应下"每台相机的窗口起始序号"（V2.12.0）：第 i 台相机的点位表条目排在
        /// 窗口区间的起点（前缀和）。用于"前上后下分组"定位某个点位对应哪个窗口编号；
        /// 也供 WindowPointForm 按相机标注每个窗口的归属与点位描述。
        /// 返回列表长度=相机数；元素=该相机点位表首条目的窗口编号（1 起），下一台是其前缀和+1。
        /// </summary>
        public static List<int> AutoFitCameraStarts(IEnumerable<CameraConfig> cameras, string productModel)
        {
            var starts = new List<int>();
            int acc = 1;                                                 // 窗口编号从 1 开始
            foreach (var cam in cameras ?? new List<CameraConfig>())
            {
                starts.Add(acc);
                if (cam != null) acc += cam.ProgramsFor(productModel).Count;
            }
            return starts;
        }

        /// <summary>
        /// 生成某型号的【默认窗口↔点位映射】（V2.13）：按"前上相机后下相机、各相机点位表
        /// 条目顺序"铺排——窗口 1 起，依次取相机 0 点位表第 1、2…条，再取相机 1 点位表第 1、2…条。
        /// 返回列表长度 = 该型号窗口总数（各相机点位表条目和，≥1，见 WindowCountFor）；
        /// 若各相机点位表全空则兜底给一条"相机·点位1"（防空列表，与 WindowCountFor 的 ≥1 一致）。
        /// 【V2.13.4 关联键升级】条目存 CameraId（CameraConfig.CameraId，真编号）；相机 ID 为 0
        /// （旧配置）时用列表行序兜底，保证唯一可反查。
        /// 与 V2.12.1 的"窗口=相机点位表条目"隐式铺排等价——不编辑时默认映射=这个，行为零变化。
        /// </summary>
        public static List<WindowPointItem> DefaultWindowPointMap(
            IEnumerable<CameraConfig> cameras, string productModel)
        {
            var list = new List<WindowPointItem>();
            int ci = 0;                                          // 相机列表下标（仅铺排顺序，非关联键）
            foreach (var cam in cameras ?? new List<CameraConfig>())
            {
                if (cam == null) { ci++; continue; }             // 空安全：跳过被手改成 null 的元素，下标照常累加
                var table = cam.ProgramsFor(productModel);
                if (table != null)
                {
                    int camId = cam.CameraId > 0 ? cam.CameraId : ci + 1;   // 相机ID（0 回退行序）
                    foreach (var it in table)
                    {
                        if (it == null) continue;                // 空安全：点位表里混入 null 条目时跳过
                        list.Add(new WindowPointItem { CameraId = camId, StationNo = it.StationNo });
                    }
                }
                ci++;
            }
            if (list.Count == 0) list.Add(new WindowPointItem { CameraId = 1, StationNo = 1 }); // 兜底≥1
            return list;
        }

        /// <summary>
        /// 解析"某型号当前的窗口↔点位映射"（V2.13，主窗体/协调器/点位窗体/设置页统一调用）：
        ///   ① WindowPointMaps 里找到与指定型号同名（大小写不敏感）且长度=该型号窗口总数的表
        ///      → 返回该表（用户手动编辑/交换过的映射）；
        ///   ② 型号没配表 / 表长度不对（相机点位表改动后数量变了）→ 回退默认铺排
        ///      （DefaultWindowPointMap），保证运行时窗口与点位一一对应、永不越界。
        /// 【为什么长度必须校验】窗口总数 = 相机点位表条目和，若映射表长度与它不一致
        ///   （点位表增删点位后旧的映射没跟着变），反查会越界/找不到窗口，必须回退默认。
        /// </summary>
        public static List<WindowPointItem> ResolveWindowPointMap(
            IEnumerable<CameraConfig> cameras, string productModel,
            List<ModelWindowPointMap> maps)
        {
            var def = DefaultWindowPointMap(cameras, productModel);
            if (maps == null || string.IsNullOrWhiteSpace(productModel)) return def;
            foreach (var m in maps)
            {
                if (m == null || m.Points == null) continue;
                if (string.Equals(m.ModelName, productModel, StringComparison.OrdinalIgnoreCase))
                {
                    // V2.13.10：长度一致【且】所有条目的 CameraId 都指向真实存在的相机，
                    // 才返回用户手动编辑过的映射；否则回退默认铺排。
                    // 【为什么必须校验 CameraId】改号/删相机后，旧映射里的 CameraId 变成孤儿——
                    // 它 >0 又 ≠ 任何相机的真编号，旧的"CameraId<=0 即旧格式"判定抓不到；
                    // 若运行时用了孤儿表，TryResolveActiveWindow 按 (孤儿ID,点位) 永远反查不到
                    // 窗口 → 该相机全部点位被当作"不归本相机"返回假、结果写 3（整台相机罢工）。
                    // 本校验与 ConfigStore.EnsureWindowPointMaps（加载/保存清理）共用同一套规则，
                    // 双保险保证运行时拿到的映射永不包含孤儿 ID。
                    if (m.Points.Count == def.Count && PointMapValidForCameras(cameras, m.Points))
                        return m.Points;   // 长度与 ID 都合法 → 用用户映射
                    return def;            // 数量变了或含孤儿 ID → 回退默认
                }
            }
            return def;
        }

        /// <summary>
        /// 校验"窗口↔点位映射表"所有条目的 CameraId 是否都指向真实存在的相机（V2.13.10 引入）。
        /// 【为什么需要】映射条目存 CameraId（相机真编号，见 WindowPointItem）；若某台相机的
        ///   CameraId 被改号（设置页"相机ID"列或手改 json）/相机被删除，旧映射里的 CameraId 就
        ///   变成"孤儿 ID"——它 >0 却又 ≠ 任何相机的真编号，而旧的"CameraId<=0 即旧格式"判定
        ///   抓不到它（旧格式是缺失字段=0，改号后的孤儿是"值仍在但已无对应相机"）。
        /// 【判定】收集当前相机列表的"有效 ID 集合"：CameraId>0 用真编号、0 用行序兜底
        ///   （与 DefaultWindowPointMap / ProductionCoordinator.CameraIdFor 同一套兜底规则，保证
        ///   "编辑候选"、"默认铺排"、"运行时反查"用同一把钥匙）；映射里只要有一个条目的
        ///   CameraId 不在集合中 → 判为无效（return false），调用方应重置该型号为默认铺排。
        /// 【调用方】ConfigStore.EnsureWindowPointMaps（加载/保存时清理孤儿表）与
        ///   ResolveWindowPointMap（运行时解析防御）统一走本方法，两端规则绝不漂移。
        /// </summary>
        /// <param name="cameras">当前相机列表（可含 null 元素，空安全跳过）</param>
        /// <param name="points">要校验的窗口↔点位映射表（null 视为无效）</param>
        /// <returns>true=所有条目均指向存在的相机；false=含孤儿/空条目</returns>
        public static bool PointMapValidForCameras(
            IEnumerable<CameraConfig> cameras, IEnumerable<WindowPointItem> points)
        {
            if (points == null) return false;
            var ids = new HashSet<int>();          // 当前相机列表的有效 ID 集合（全局唯一判定基准）
            int ci = 0;                            // 行序兜底下标（与 DefaultWindowPointMap 一致）
            foreach (var cam in cameras ?? new List<CameraConfig>())
            {
                if (cam != null) ids.Add(cam.CameraId > 0 ? cam.CameraId : ci + 1);
                ci++;                              // null 元素也累加下标，保行序与铺排一致
            }
            foreach (var p in points)
                if (p == null || !ids.Contains(p.CameraId)) return false;
            return true;
        }

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
        /// 窗口→存图点位映射（【V2.12.1 已退役】，仅保留作历史字段兼容旧配置，运行时不再读取）。
        ///
        /// V2.12.1 统一模型：点位由【相机点位表】唯一决定（各相机 "点位→程序号" 表里的 StationNo，
        /// 上下相机点位号各自从 1 起、会重复），窗口只是把相机点位条目"前上相机后下相机"顺序铺排的
        /// 格子（窗口编号 = 相机起始窗口 + 表内位置）。因此：
        ///   - 运行时"PLC 点位 → 窗口"解析不再查本表（见 ProductionCoordinator.TryResolveActiveWindow）；
        ///   - WindowPointForm 的格子标注、存图文件名 {点位} 均改用相机点位号 + {相机} 目录隔离，
        ///     不再使用本表的"全局唯一点位"；
        ///   - 本表仅由 ConfigStore.EnsureStationMap 按窗口总数对齐留着（点位=窗口编号），避免旧 json
        ///     带着自定义映射导致加载解析混乱，但不参与任何存图/显示/切程序逻辑。
        /// </summary>
        public List<int> WindowStationMap { get; set; } = new List<int>();

        /// <summary>
        /// 窗口是否启用（V1.12.28 新增，长度=窗口总数，见 WindowCountFor）。
        /// 第 i+1 号窗口的点位坏了/停用时把对应元素置 false：
        ///   - 主界面矩阵"完全移除"该格子，剩余窗口重新紧凑排列（窗口编号保留原值）；
        ///   - PLC 拍照请求写到该点位时，上位机不触发相机、不显示、不存图、不计数，
        ///     直接把结果写成 3（跳过，等同告诉 PLC"本点位未检测，请走下一工位"）。
        /// 编辑入口：系统设置 →"窗口/点位配置…"里右键格子或点"禁用/启用选中窗口"。
        /// 长度由 ConfigStore.EnsureStationMap 在加载/保存时自动对齐（缺的补 true，多的截断）。
        /// </summary>
        public List<bool> WindowEnabled { get; set; } = new List<bool>();

        /// <summary>
        /// 【V2.13】窗口↔点位独立映射（按产品型号分表，JSON `windowPointMaps`）。
        /// 【背景】V2.12.1 统一模型：窗口总数 = 各相机按型号点位表条目和，存图点位=相机点位号
        ///   （上下相机各自从 1 起、会重复，靠 {相机} 目录层隔离）。在此基础上现场仍需要手动
        ///   【调整窗口与点位的对应】——例如"窗口 3 想显示上相机点位 5"、"两个窗口内容互换"、
        ///   "一键恢复出厂铺排"，故引入本独立映射，让窗口↔(相机,点位) 的对应可编辑。
        /// 【结构】每台产品型号一张表（ModelWindowPointMap）：Points 长度 = 该型号窗口总数
        ///   （各相机点位表条目和），Points[i] = 窗口 i+1 对应的（相机列表下标, 相机点位号）。
        /// 【默认值】默认铺排 = "前上相机后下相机、各相机点位表顺序"（见 DefaultWindowPointMap），
        ///   即不编辑时与 V2.12.1 行为完全一致；只有手动编辑/交换后才偏离默认。
        /// 【约束】同一"（相机, 点位）"只能分配给一个窗口（运行时 PLC 请求点位需反查唯一窗口，
        ///   见 ProductionCoordinator.TryResolveActiveWindow），编辑点位时界面做唯一性校验。
        /// 【对齐】ConfigStore.EnsureWindowPointMaps 在加载/保存时：缺型号表补默认铺排、
        ///   长度不对按默认铺排对齐（点位由相机点位表决定，长度必须跟随窗口总数）。
        /// 【运行时】协调器/主窗体/点位窗体统一走 ResolveWindowPointMap 解析（缺表回退默认），
        ///   与 ResolveLayout/WindowCountFor 是同一套"四层共用"的配套。
        /// </summary>
        public List<ModelWindowPointMap> WindowPointMaps { get; set; } = new List<ModelWindowPointMap>();

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

        /// <summary>标题栏左侧固定文案（产品型号说明），如"产品型号"</summary>
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

        /// <summary>
        /// 主界面各显示窗口【右下角 OK/NG 徽标】是否显示（V2.10.3）。
        /// 徽标为自绘矩形框 + 框内文字（OK 绿、NG 红，颜色随 OkColorName/NgColorName），
        /// 叠加在每格相机画面上方。V1.9.5 曾因"现场嫌占画面"整块移除，
        /// 现把显隐改为可配置：默认 false（与移除后现状一致，保持画面干净），
        /// 现场需要醒目 OK/NG 时在系统设置里勾选"窗口徽标"即可。
        /// </summary>
        public bool WindowOkNgVisible { get; set; } = false;

        /// <summary>
        /// 主界面各显示窗口【左上角窗口编号】是否显示（V2.10.4）。
        /// 默认 true（现状：每格左上角悬浮半透明白底 + 深蓝灰字的编号，辅助现场定位第几路）。
        /// 现场嫌编号碍眼时可在系统设置"窗口点位"行勾掉"显示窗口编号"，保存后即时生效。
        /// </summary>
        public bool WindowIndexVisible { get; set; } = true;

        /// <summary>
        /// 主界面各显示窗口【悬停气泡提示】是否显示（V2.10.8）。
        /// 默认 true：鼠标放到任一显示窗口内停留片刻，气泡提示"双击放大（全屏查看）；再双击还原"，
        /// 方便新手操作员发现双击功能。现场觉得气泡挡画面时可在系统设置里勾掉
        /// "窗口点位"行的"悬停提示"，保存后即时生效。
        /// </summary>
        public bool WindowToolTipVisible { get; set; } = true;

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
    /// 默认结构（V2.12.1 起按相机分目录，上下相机同号点位互不干扰）：
    ///   根目录 / 年月日(2026年08月11日) / SN号 / OK|NG / 相机 / 点位号文件
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
        /// 默认（V2.12.3 按现场最终确认的顺序定稿，{相机} 在 {OKNG} 前）：
        ///   ["{年月日}","{SN}","{相机}","{OKNG}"] → 根/2026年08月11日/SN-0001/上相机/OK/1.jpeg
        /// 说明：
        ///   {年月日} 是一个整体目录名，展开成"2026年08月11日"（不是年/月/日三级）；
        ///   {相机}   V2.12.1 新增：展开成相机名（如"上相机"/"下相机"）。**存图点位用的是相机
        ///     点位号，上下相机同号会重复，必须靠 {相机} 这一层目录把两家隔开**，否则文件互相覆盖。
        ///     建议保留；若现场确不需要按相机分目录（只有一台相机），可在"配置目录结构…"里删掉这层。
        ///   {OKNG}   按本次判定展开成 OK 或 NG 两个并列目录之一，满足现场分开放习惯；
        ///   点位号进文件名（见 FileNameTemplate），不作为目录层级。
        /// </summary>
        public List<string> SubDirs { get; set; } = new List<string> { "{年月日}", "{SN}", "{相机}", "{OKNG}" };

        /// <summary>
        /// 文件名模板（不含扩展名，jpeg/iv4p 双格式统一按此命名）。支持的占位符（其余文字原样保留）：
        ///   {点位}   相机点位号（本相机点位表 StationNo；上下相机各自从 1 起，靠目录里的 {相机} 层区分）
        ///   {相机}   相机名（如"上相机"/"下相机"，也可放文件名里，默认放目录层）
        ///   {时间}   精确到毫秒的时间戳 yyyyMMdd_HHmmss_fff（多张同点位防重名用）
        ///   {SN}     序列号（若文件名也要带 SN 可加）
        ///   例：默认 "{点位}" → 1.png
        /// ⚠️ V2.14.11：**双格式归档（SaveImageFilePair）已不再使用本模板**——现场定稿
        ///   归档文件名 = 相机源文件名 + "_" + 时间戳（如 0084_20260814_102030_123.jpeg），
        ///   本字段仅旧版 TCP/BR 取图（SaveImage/SaveImageBytes）仍按模板命名，属历史兼容。
        /// </summary>
        public string FileNameTemplate { get; set; } = "{点位}";

        /// <summary>
        /// 存图文件名是否追加时间戳后缀（V1.12.18）。
        /// true(默认)：双格式归档（SaveImageFilePair）最终文件名 =
        ///   相机源文件名 + "_" + 时间戳(yyyyMMdd_HHmmss_fff)（V2.14.11 起源文件名不再套模板）；
        ///   旧版 SaveImage 则是 模板渲染结果 + "_" + 时间戳。
        ///   防止"同点位重复拍照/重复触发"时覆盖旧图——现场同点位可能被多次触发，
        ///   每张图都要保留（旧版仅靠 _2/_3 递增兜底，命名不清）。
        /// false：保持原名（模板带 {时间} 时基本不重名，此开关仅作保险）。
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
        ///   "Tcp"   ：基恩士 SR 系列扫码枪以太网 TCP/IP 无协议通讯（默认，现场实测）——上位机作
        ///              TCP 客户端连扫码枪，扫码枪读到条码后主动推送文本行，本程序按行切分；
        ///   "Serial"：串口 RS-232 扫码枪（扫完发一行条码+CR/LF，需在设置页显式选串口表配置）。
        /// 【V2.13.8 默认值由 "Serial" 改为 "Tcp"】现场扫码枪就是 TCP/IP 协议；旧配置/新建的
        ///   ScanConfig 缺 mode 字段时，反序列化用本默认值决定走哪个实现——改前缺字段会误走串口
        ///   （连 COM3，表现为"网络可达却连不上"），改后缺字段走 TCP 收码，贴合现场。
        /// </summary>
        public string Mode { get; set; } = "Tcp";

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