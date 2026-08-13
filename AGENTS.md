# AGENTS.md — CommandCenter 项目指南

> 本文件是 AI 助手在操作本项目前的**强制前置阅读**。开工前先读本文件，明确角色、约定与红线。
> 优先级：本文档 > 项目已有代码风格 > 通用最佳实践。

## 项目角色

你是本项目（Windows 窗体 C#/.NET Framework 应用）的**资深开发/维护工程师**，负责按用户需求改代码、修 bug、沉淀约定。改动必须**可编译、可运行、风格统一**，并在关键改动后更新 `CHANGELOG.md`。

## 技术栈

- .NET Framework **4.7.2** WinForms（非 .NET Core/.NET 5+，勿引入其语法/API），C# 语言版本 `LangVersion=7.3`
- 通讯：**NModbus 3.0.83**（汇川 PLC Modbus TCP）；相机走基恩士 TCP 无协议通信（自写 TcpClient）
- 序列化：**Newtonsoft.Json**（配置/型号）
- **依赖策略（重要）**：第三方库拷在 `CommandCenter/libs/` 目录由 csproj `<Reference HintPath>` 直接引用，**不依赖 NuGet restore**，离线可编译。新增第三方库请同样"拷 dll 进 libs 再引用"。

## 铁律（违反即返工）

1. **文件编码 UTF-8**。禁止 `Add-Content`/`Out-File` 默认编码写中文（会成 GBK）。写文件用 write 工具；新增中文文件后自查：`[IO.File]::ReadAllText(path, UTF8).Contains("预期中文")` 要能命中。
2. **不提交运行时数据与机密**：`Config/*.json`（appconfig 等运行时生成）、`Logs/`、`bin/`、`obj/` 一律 gitignore，绝不入库。
3. **改动后必须构建验证**（命令见下），禁止提交编译不过的代码。
4. **不主动 commit/push**，除非用户明确要求；提交前先 `git status` + `git diff` 确认只包含预期改动。
5. **代码注释要详细，让小白能看懂**：关键方法/流程/边界/配置依赖写清"做什么 + 为什么 + 怎么改"，杜绝 `i++ // 自增` 式废话。参考本仓库 `Services/ProductionCoordinator.cs` 与 `Models/AppConfig.cs` 头部注释风格。

## 代码约定

- 类/方法/属性 PascalCase；私有字段 `_camelCase`；接口前缀 `I`。
- 控件命名匈牙利前缀：`lbl`/`btn`/`txt`/`nud`/`cmb`/`pnl`/`grid`。
- **界面文件头注释必须带 ASCII 布局图**（`Views/*.cs`、`Dialogs/*.cs` 类 XML 注释里，用 `┌─┐│└┘` 画），框内标注控件名与关键交互点，必须与实际布局一致。AI 无法看图，全靠这张文本图。
- **串口/枚举配置值的存储约定**：停止位存字符串 `"1"`/`"15"`/`"2"`；校验位存标准枚举名 `None/Odd/Even/Mark/Space`。读写两端大小写兼容。参考 `Services/ScannerService.StopBitsFromString` / `ParityFromName`。
- **OK/NG 现场习惯（必须）**：**OK = 绿色、NG = 红色**（矩形框 + 文字同色），颜色名可在 `appconfig.json` 的 `display.okColorName/ngColorName` 里配。
- **管理员登录（V1.9.0）**：点"系统设置"每次都要登录管理员账号（`Security.AdminEnabled=true` 时，MainForm.OpenSettings 校验），**密码只存 SHA-256 哈希、不存明文**（`Utils/SecurityUtil.HashPassword`）。账号维护全部在**登录对话框**里完成：登录面板校验，改密码面板（验证原密码 → 新密码两次一致且 ≥6 位 → 保存写盘）；**系统设置窗体不放管理员区**，保持纯业务配置。**"记住密码"用 Windows DPAPI 加密存 `%LOCALAPPDATA%\CommandCenter\`**（绑定当前 Windows 用户，拷走无效；`SecurityUtil.Save/Load/ClearRememberedLogin(bool isDev, …)`，`isDev=false` 存管理员文件 `remembered_login.dat`、`isDev=true` 存开发者文件 `remembered_login_dev.dat`），**管理员/开发者记录互斥**：登录任一账号成功会把另一角色的记住文件一并清除（`LoginForm.BtnLogin_Click`），改密码也清开发者记录，防止跨角色回填残留。绝不在配置文件里存可回填的明文密码。新增安全类配置走 `SecurityConfig`，勿引入明文密码字段。
- **开发者账号 + 功能测试（V1.12.0）**：除管理员外还有开发者账号（`SecurityConfig.DevEnabled/DevUser/DevPasswordHash`，默认 `dev`/`dev123`）。MainForm.OpenSettings 登录后按 `login.Role` 分流：`Admin` → 系统设置 SettingsForm，`Developer` → 功能测试 DevTestForm。**功能测试窗体约定（必须遵守）**：① 只做通讯手动验证、不产生任何配置改动；② **复用主窗体传入的 `_plc`/`_cameras`/`_scanners` 实例、绝不新建 TcpClient/串口/连接**（内部 EnsureConnected 惰性建连缓存复用；扫码枪为设备主动推码，只订阅 `SerialNumberScanned` 事件收码、不重复 Open，可调 `SendTrigger()` 手动重发触发指令），关闭时不 Dispose 这些服务；③ 所有网络 IO 走后台线程 + SafeInvoke 回 UI（红线同 UI 禁 IO）；④ 开发者密码不支持界面修改（改密码面板仅服务管理员）。新增测试入口若需连设备，先找 MainForm 是否已有该服务实例，有了就传引用复用。**T2 取图存图（V1.12.24）**：`btnTriggerRead`（"触发+判定T2（取图存图）"）触发成功后复用主窗体传入的 `_imageStore` 与相机配置（`FtpUploadDir`）扫该相机 FTP 目录取最新 jpeg+iv4p → `picTestShot` 闪图 → `SaveImageFilePair` 存进主窗体配置存图目录（**点位固定 1**、判定 OK/NG、打开窗体时 SN 快照），结果/路径进日志；点 T1 只验证触发链路不取图存图。
- **扫码枪触发指令（V1.12.1，基恩士 SR 无协议）**：Tcp 模式下扫码枪**不是连上就回数据**，上位机须先发一条打开激光/开始读取的指令（`ScanConfig.TriggerCommand`，默认 `LON`）才读码。`ScannerTcpService.TryConnect` 每次连接/重连成功后**自动发送一次**（发送时自动补 `\r\n` 帧结束符），配置留空则不发送。`IScanner.SendTrigger()` 供界面手动重发。串口扫码枪上电即读码、无需触发（串口实现 SendTrigger 为空操作）。改动扫码枪通讯必须同步 `docs/CommandCenter.md` 的"扫码枪"章节与默认配置。
- **UI 线程禁做网络 IO（V1.0.1 血泪）**：轮询/连接/读写 PLC 与相机一律放后台线程（`System.Threading.Timer`），TCP 连接必须 `BeginConnect + WaitOne` 强制超时。禁止在 UI 线程同步 `TcpClient.Connect` 或 `ReadHoldingRegisters`——对不可达 IP 会冻结整个界面（表现为"点按钮半天才响应"）。
- **显示窗口矩阵用 TableLayoutPanel 百分比等分**：窗口数量由 `display.rows/columns` 配置，所有窗口尺寸由容器等分自动保持一致，禁止写死像素布局。
- **显示窗口矩阵统一模型（V2.12.1，取代并合并 V2.12.0"自适应"）**：窗口总数**恒** = 各相机按型号
  点位表 `ProgramsFor(型号)` 条目和（`DisplayConfig.WindowCountFor` / `ResolveLayout` 统一计算，
  列=min(7,总数)、行=ceil(总数/列)、点数≤7 单行铺满），`AutoFitCameraStarts` 返回各相机窗口起始序号
  （"前上相机后下相机"分组），主窗体 BuildWindowGrid / 设置页预览 / 协调器 / WindowPointForm **共用同一套
  计算，禁止各层再各写一套**。**勾选"自适应"只决定行列是否自动算**：`AutoFit=true` 时行/列输入框置灰
  （行列自动铺排）；不勾时手填行/列只当"排列宽度/期望行数"，放不下**自动补行**，窗口总数仍=点位和，
  两种模式所见完全一致。**存图点位统一 = 相机点位号（StationNo）**（上下相机各自从 1 起会重复，靠
  ImageStore 归档子目录 **`{相机}` 层隔开**——`SubDirs` 默认含 `{相机}`，旧配置加载自动补，绝不拿
  WindowStationMap/windowIndex 当存图点位）；手动点位编辑（编辑点位/交换位置/恢复默认）在 WindowPointForm
  里两种模式**都锁定置灰**（点位由相机点位表唯一决定，按钮禁用+方法内双保险），设置页勾选自适应即置灰
  行/列输入框并弹 ToolTip 明示"自适应下哪些功能不可用"、相关控件 ToolTip 同步说明。
   **默认型号（V2.12.3）**：`AppConfig.ProductModel` 默认 **"U171"**（非空），无配置文件首次启动也
   按该型号点位表铺出对应窗口（U171=上18+下4=22 窗），不会因型号空串把窗口塌成 1 个（此前 `Load()`
   无文件分支直接 new AppConfig() 连相机列表也是空的，窗口=0→兜底 1 个的回归根因）；`ConfigStore.Load`
   把"空段兜底+数组对齐"抽成 `ApplyDefaults`，有/无配置文件统一走。
- **PLC 握手协议（V2.7 定稿，从站模式）**：现场 PLC(汇川)做主站、上位机做从站监听本机 502；
  **"请求-结果-复位"三拍握手**，寄存器固定 40001~40011（完整协议见 `docs/CommandCenter.md` §5.5）：
  请求区（PLC只写）：`40001 扫码请求`(0/1)、`40002 上相机拍照请求`(1~255=点位)、`40003 下相机拍照请求`；
  结果区（PLC只读，上位机写）：`40004 扫码结果`(0/1/2)、`40005 上相机`、`40006 下相机`(0/1/2，相机结果
  另支持 **3=点位禁用跳过**）；型号区：`40007~40011`（上位机写固定产品型号，每寄存器 2 字符 ASCII、高字节
  在前、不足补 0x00、最多 10 字符，超长从 40012 向后扩展）。**三拍流程**：PLC 写请求≠0 → 上位机处理完
  写结果≠0 → PLC 读走并复位请求=0 → 上位机看请求清零再复位结果=0 → 进入下一拍。**地址约定（V2.12.3 定稿）**：配置里统一存** DataStore 索引**（PLC 协议号 = 索引 + 40000，如协议 40002 上相机请求 → 索引 2，就是汇川 D2/D3/D5 这类数字，填 2 就是 2）；现场实测 PLC 写 40002 → 从站 DataStore[2]（曾误以为"零偏移直接用协议号"导致读 DataStore[40002] 永远读不到请求；V2.12.2 曾做"协议号-40000 换算"中间方案，V2.12.3 起按"改就改干净"删掉 `ProtocolToIndex`，业务层【零换算】）；地址全部收进 `PlcConfig`（`ScanRequestAddress/CamUpRequestAddress/…/
ProductModelAddress/ProductModelLen`）+ 顶层 `ProductModel`（**两处可改，同一个值**：① 设置窗体 PLC 区
   "产品型号" **cmbModel 可编辑下拉**（候选=预置三型号 ∪ 顶层 `ProductModels`，手输新型号保存自动加入）；
   ② **主界面标题栏型号下拉 `cmbModel`（V2.8，操作员日常切型号用，候选恒预置 U171/U172/Z121 不依赖配置
   文件）：`SwitchModel` 更新 ProductModel + 写盘 + 只重建协调器**（PLC/相机/扫码枪复用、设备不断连，
   新型号随下次扫码写 PLC、按新型号查 `modelStationPrograms` 切程序）），
   每次扫码 `ProductionCoordinator` 调 `PlcService.WriteProductModel` 写 40007~40011）。版本化流程：
  `ProductionCoordinator` 是**三通道状态机**（通道①扫码 40001/40004、通道②上相机 40002/40005、
  通道③下相机 40003/40006），相机触发前按窗口映射解析点位 → 按"当前型号→点位"查本相机映射表
  （先 `ModelStationPrograms` 型号表、型号没配表回退 `StationPrograms` 默认表）`PW` 切程序。
  扫码枪列表经 `_coordinator.AttachScanners()` 注入（协调器比扫码枪先创建，用方法注入不用构造）。
  改动 PLC 或相机通讯或握手流程必须同步 `docs/CommandCenter.md`。
- **相机 FTP 双文件归档 + 点位程序号（V1.12.18；V1.12.24 起取图改"扫目录取最新"）**：现场方案是"一台相机=一个 FTP 目录、所有点位图混放"——FTP 目录只当**中转暂存区**：基恩士每次拍照生成 jpeg+iv4p 两个文件（**文件名不保证恒为 `0000`（现场实测有 `0084` 等任意编号），上位机取图一律 `ImageStore.FindLatestPair(dir)` 扫目录取"修改时间最新"的一对、不写死文件名**），`ProductionCoordinator.OnFtpFileArrived` 按扩展名配对（`PendingCamera.FtpJpegPath/FtpIvpPath`）只作**信号加速**（两个都到齐即 `IsSnapped` 提前收尾，事件漏报也靠收尾重扫兜底），`FinishAll` 归档时经 `TryResolveFtpSources` 重新扫目录取最新对、扫描失败才回退事件路径，调 `ImageStore.SaveImageFilePair` 双格式原样归档（jpeg 显示/归档主体、iv4p 基恩士私有格式原样复制）后 **`DeleteFtpSource` 删除"实际归档的那对"FTP 源文件**（处理即删防同点位重复触发新旧图混淆；**超时兜底时只要目录里有图照样归档**，不再"有图不存"）。**现场相机映射（V1.12.22 定稿，与默认配置一致）**：相机1=**上相机**=`19.87.6.213`→FTP 取图目录 `D:\IV存图\1`；相机2=**下相机**=`19.87.6.212`→`D:\IV存图\2`（`CameraConfig.Name/FtpUploadDir` + `DefaultCameras()` 一处改）。改相机 IP/目录只动 `DefaultCameras()` 与 `appconfig.json` 的 cameras 段。**V2.8 型号映射预置也在 `DefaultCameras()`（与默认配置一致）**：上相机 U171=P000~P012 / U172=P013~P028、下相机 U171=P000~P003 / Z121=P005~P007；型号候选预置 `ProductModels=["U171","U172","Z121"]`，改型号映射/加新型号优先走界面（设置页"产品型号"下拉 + WindowPointForm 型号下拉），不手改 json。**点位区分靠程序号（V1.12.25 起按相机分表，V1.12.26 支持任意台相机+下拉选择，V2.8 起按产品型号分表，重要）**：现场是"28 个窗口点位对应两台相机分工拍摄"（不是每台相机拍全部点位），且各相机程序库互相独立，所以点位→程序号映射**必须每相机一张表**，**且同一台相机在不同产品型号下程序号/点位归属不同**（如"上相机"型号 U171 用 P000~P012、U172 用 P013~P028），故再**按型号分表**：`CameraConfig.StationPrograms`（`List<StationProgramItem>`，`{stationNo,programNo}`，JSON `stationPrograms`）作"默认/不区分型号"表 + `ModelStationPrograms`（`[{modelName,programs:[{stationNo,programNo},…]}]`，JSON `modelStationPrograms`）每型号一张；运行时 `ResolveProgramForStation` 先查当前型号同名表（大小写不敏感）、型号没配表回退默认表、仍无该点位就不切换。型号候选走顶层 `ProductModels`（预置 U171/U172/Z121，设置页可手输加入）。设置入口与"窗口↔存图点位"矩阵**同页混排在 `WindowPointForm`**（**相机 + 型号双下拉**（型号="默认（不区分型号）"+候选；"默认"编辑旧 `StationPrograms`、其余编辑对应 `ModelStationPrograms`）+ **点位/程序号两列下拉选择**（V1.12.26）：**点位列候选=窗口映射点位（数量=窗口数，点位默认=窗口编号、互换/个别调整仍用同一集合）**、**程序号候选="不切换"+0~127（0 合法；程序数量与具体编号由相机程序库决定、与窗口数无关，现场动态选）**；点位不拍直接删行、"不切换"=保持相机当前程序）。**新增相机也自动有自己的独立映射表**：SettingsForm 相机表加一行即新相机（`LoadCameraRows` 把来源配置挂行 Tag、`OnSave` 经 `CollectCamerasFromGrid` 复用 Tag 对象保留 `StationPrograms`+`ModelStationPrograms`，映射配好后点保存不会丢；新增行 Tag=null→保存时建空表）。触发切程序在 `ProductionCoordinator.TriggerOneCamera`：先按"本轮该相机要填的窗口"（`_nextWindowIndex + idx`，与 FinishAll 环形窗口分配一致）经 `ResolveStation` 得点位 → `ResolveProgramForStation` 查**本相机当前型号表** → 命中先 `SwitchProgram`（`PW,nnn`，**`ProgramNo >= 0` 才发，0 也是合法程序号，失败即中止该相机**）再 `T2`、**未命中不切换**（不再读固定 `CameraConfig.ProgramNo`——该字段 V1.12.25 起废弃，仅旧配置兼容）。`SetOutputFormat`（`OF,nn`，配置非空才发、失败即中止）在切程序之前；注意 **`OutputFormat` 必须恰好 2 位数字**（"00"~"03"），配置非法会让该相机触发直接失败（`SetOutputFormat` 校验长度/数字后 false）；`SwitchProgram` 程序号越界会**自动夹到 0~127**（配置 128+ 不报错而是切到 127）。V2.7 起点位来源 = PLC 请求 `40002`/`40003` 里带的点位编号（触发前再按窗口映射确认归属相机，不再有单独的点位寄存器）。存图文件名默认加时间戳后缀（`ImageConfig.FileTimestampSuffix`）。**取图方式仅保留 Ftp**（Tcp/BR 代码留作旧配置兼容、设置窗体不再提供 Tcp 选项）。改动相机通讯/归档流程必须同步 `docs/CommandCenter.md` 第四部分与默认配置。
- **删除/清理旧代码的自检纪律（必须遵守，2026-08 血泪总结）**：删除"旧配置兼容/冗余判断"这类代码时，先分清两类再动手：
  - **真·旧配置兼容**（可删）：为"旧版本缺字段/旧格式"写的兜底，项目未上线时是死代码；
  - **防 NRE 的空安全**（不可删，否则留坑）：`obj.Prop.Trim()`、`obj.Method()` 这类链式调用，删掉外层判空后，配置被手改成 null/空值时直接崩溃。
  - 删除后**必须逐处校验**：① 被删判空保护的对象在"所有调用路径"是否恒非 null（尤其 json 手改、跨窗体传参、列表元素）；② 用 `?.Trim()...==true` 这类空安全写法替代裸链式调用（语义不变、只防崩溃），**而不是**加回旧兜底逻辑；③ 构建 + 冒烟测试必须跑，另做一次"故意破坏输入"推演（如把配置里字段手写成 null/空串，代码是否还会崩）。改完自问三遍："删掉的这段保护，有没有谁还在依赖它？"

- **WinForms 鼠标事件"命中与冒泡"红线（2026-08 血泪，做"双击/点击生效"先读这条）**：
  判断某控件上"点击/双击有没有反应"，先想清两个问题，否则白改：
  ① **真实命中目标是谁**：鼠标双击落在**最内层的子控件**上（如图像区 PictureBox 用 Dock=Fill 占满整窗，双击必落它），不会"自动落到父 UserControl"；
  ② **事件冒不冒泡**：WinForms 中带 `Mouse` 前缀的（`MouseClick`/`MouseDoubleClick`/`MouseDown`…)**会**沿父链冒泡；不带前缀的（`Click`/`DoubleClick`）**不冒泡**。
  - 要做"整窗口都响应双击"最稳写法：**直接订阅最内层子控件（PictureBox）的 `MouseDoubleClick`**（参考 `CameraDisplayControl.HandleDoubleClick`），因为它在真实命中点、必然触发、不依赖冒泡。别用父控件 `OnDoubleClick` 重写（不冒泡→没反应），也别赌父 `MouseDoubleClick` 冒泡（部分环境不稳定）。
  - **headless / 无桌面交互会话下，合成鼠标事件（`mouse_event`、`SendMessage WM_LBUTTONDBLCLK`）无法触发 WinForms 双击**——WinForms 对双击有内部状态/计时免疫，合成事件被吞，不能用来验证"是否生效"。
  - 要验证"双击→放大→还原"这类 UI 行为，用**进程序 harness 反射调用 `protected OnMouseDoubleClick`** 注入到真实命中控件（PictureBox），再反射读私有字段断言结果（`_fullScreenForm` 是否非空、`_windows[?]` 是否同一），这是本项目经过验证的可靠手段（见临时验证脚本思路）。

## 关键文件导航

| 文件 | 作用 |
| --- | --- |
| `CommandCenter/Views/MainForm.cs` | 主窗体：标题栏 + 窗口矩阵 + 事件接线；**标题栏型号下拉 cmbModel（V2.8，操作员直接切型号，见 SwitchModel/InitModelCombo）**；序列号框 txtSerial 点击直录（Enter 提交/Esc 还原/失焦非空提交，V1.12.19，见 SetupSerialEditor） |
| `CommandCenter/Services/ProductionCoordinator.cs` | 生产流程编排（两阶段状态机：扫码到位→扫SN→相机到位→拍到图→上报→循环），业务核心 |
| `CommandCenter/Services/ConnectionMonitor.cs` | 连接健康监控：后台心跳 + 断连自动重连 + 边沿日志（对齐 AgingTestSystem） |
| `CommandCenter/Services/PlcService.cs` | 汇川 PLC Modbus TCP 读写（NModbus 3.0.83） |
| `CommandCenter/Services/KeyenceIV4Camera.cs` | 基恩士 IV4 TCP 无协议触发 + 读取判定（T1/T2/RT/PW/OF 指令，V1.12.18 加切程序） |
| `CommandCenter/Services/ImageStore.cs` | 相机 FTP 推图监听 + 图片归档（SaveImageFilePair 双格式 jpeg+iv4p） |
| `CommandCenter/Models/AppConfig.cs` | 全部可配置项模型（相机/PLC/显示/图像/扫码/安全） |
| `CommandCenter/Utils/ConfigStore.cs` | appconfig.json 读写（小驼峰序列化） |
| `CommandCenter/Utils/SecurityUtil.cs` | 管理员密码 SHA-256 哈希 + 记住密码 DPAPI 加解密（登录/改密码/回填共用） |
| `CommandCenter/Views/LoginForm.cs` | 账号登录对话框（管理员 admin / 开发者 dev 双账号，按角色分流进设置或功能测试，V1.9.0/V1.12.0） |
| `CommandCenter/Views/DevTestForm.cs` | 功能测试窗体（开发者专用：相机 T1/T2 触发（T2 取图闪图存图，V1.12.24）+ PLC 寄存器交互 + 扫码枪读码展示/发触发指令，复用主窗体连接，V1.12.0） |
| `CommandCenter/Controls/CameraDisplayControl.cs` | 相机显示窗 + 右下角自绘 OK/NG 徽标（主界面不显示点位标识，点位只走设置界面查询）；左上角窗口编号显隐由配置 `DisplayConfig.WindowIndexVisible` 控制（V2.10.6） |
| `CommandCenter/Views/DirTreeEditForm.cs` | 图片存储目录结构可视化配置（逐级目录 + 文件名规则 + 实时预览） |
| `CommandCenter/Views/WindowPointForm.cs` | 窗口↔存图点位 + 点位↔相机程序号 可视化配置（格子矩阵编辑点位/交换/恢复默认 + 相机下拉点位程序表，V1.12.25 同页混排、V1.12.26 两列改下拉选择、V2.12.0 自适应下按相机表铺排矩阵/格子标"相机名·点位号"/锁定点位编辑） |
| `docs/CommandCenter.md` | **项目文档（V2.10 合并版）**：① 用户使用说明（操作手册）② 系统总览与设备清单 ③ 扫码枪对接 ④ 相机对接 ⑤ PLC 通讯对接与对外协议定义（§5.5）⑥ 计数与结果流转 ⑦ IP/参数速查 ⑧ 版本演进 |
| `docs/上位机通讯封装范式.md` | 通讯架构技术总结（连接/心跳/重连/UI 解耦范式，跨项目可复用，独立保留） |
| `CHANGELOG.md` | 版本改动记录（最新在前） |

## 构建与验证命令

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  CommandCenter/CommandCenter.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m /m
```

- 构建成功标准：输出 `CommandCenter -> ...\bin\Debug\CommandCenter.exe` 且无 error。
- 无单元测试框架；以构建通过 + 冒烟测试为验证手段（`Start-Process` 启动 exe，等几秒确认进程存活再 `Stop-Process`）。

## 文档同步（铁律：每次任务必须主动完成，不许等用户提醒）

> 文档同步与代码改动同等重要，是任务"完成"的判定标准之一。做完代码改动后**主动逐条核对下表**，
> 全部更新完毕才算任务结束，无需用户提醒"记得更新文档"。遗漏文档同步 = 任务未完成，返工。

- **`CHANGELOG.md`**：顶部新增/更新当前版本小节，写明"改动范围、为什么这么改、优化点"三部分（参考既有 V1.x 小节格式），改动再小也记。
- **`README.md`**：目录结构、核心业务流、构建方式有变化时同步更新；**新增可配置项时必须在"可配置项"一节补充说明**（含字段名、默认值、用途）。
- **`docs/CommandCenter.md`**：寄存器地址 / 相机指令 / 通讯流程等通讯类改动，必须同步（对应第四/第五部分）并写明版本号（放第八部分"版本"）。
- **`docs/CommandCenter.md` 第一部分**：用户可见的操作变化（按钮/流程/新功能/排查项）同步更新，保持操作员手册与代码一致。
- **`AGENTS.md`**：若本次改动引入了新的项目约定（红线/约定/命令/文件导航变化），同步更新本文件。
- **代码注释**：改动处注释详细到小白能看懂；新文件/新方法写清头部说明；中文保持 UTF-8。
- **提交前自检**：`git status` + `git diff` 确认改动范围与文档同步都完成后再交付；用户不要求 commit 时只留工作区改动即可。