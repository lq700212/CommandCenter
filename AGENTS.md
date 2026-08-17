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
- **代码混淆（V2.14.31）**：发布版必须过混淆再部署（防反编译拿类名/方法名/IP/寄存器号/相机指令），工具 `CommandCenter/tools/Obfuscar/Obfuscar.Console.exe`（离线单体 exe，策略同 libs 离线），配置 `CommandCenter/obfuscar.xml`（全量重命名 + HideStrings 字符串加密），一键脚本 `CommandCenter/build-obfuscated.ps1`（Release 构建 → 混淆 → 补 dll/config → 冒烟），产物 `bin/Obfuscated/`。**混淆豁免红线：配置模型 `CommandCenter.Models*` 命名空间必须 SkipNamespace 跳过**——`ConfigStore.Save` 用小驼峰序列化，属性名=appconfig.json 字段名，混淆属性名会让现场旧配置读不回、新配置字段错乱（改 obfuscar.xml 前先想这条）。新增/修改 P/Invoke（`DllImport`）必须显式写 `EntryPoint="..."`（混淆改方法名后默认按方法名找导出函数会 DllNotFound）。混淆版 PDB 失配不能断点，排查用 Debug 版 + 日志。**改动混淆相关逻辑必须同步 `docs/CommandCenter.md` 第八部分与 `AGENTS.md` 本节**。

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
- **扫码枪触发指令（V1.12.1，基恩士 SR 无协议）**：Tcp 模式下扫码枪**不是连上就回数据**，上位机须先发一条打开激光/开始读取的指令（`ScanConfig.TriggerCommand`，默认 `LON`）才读码。`ScannerTcpService.TryConnect` 每次连接/重连成功后**自动发送一次**（发送时自动补 `\r\n` 帧结束符），配置留空则不发送。`IScanner.SendTrigger()` 供界面手动重发。串口扫码枪上电即读码、无需触发（串口实现 SendTrigger 为空操作）。**默认 TCP 而非串口（V2.13.8，现场基恩士=TCP/IP）**：`ScanConfig.Mode` 默认值=`"Tcp"`，只要没显式配 `"Serial"`（设置页串口表）一律走 TCP 实现；`ConfigStore.ApplyDefaults` 对 `scanners` 为 null **或空列表**都兜底一台启用 TCP 枪（`19.87.6.100:9004/LON`，与设置页 TCP 模板行一致）——排查"Test-NetConnection 通但程序不连扫码枪/测试页无扫码枪选项"先查这两处。**读码失败文本过滤（V2.14.30，重要）**：基恩士 SR 无协议模式读码失败会把错误字符串（`ERROR`/`ER,READ,00`/`NG`）当条码推送，收码层按 `ScanConfig.IgnoreScanTexts` 名单（默认 `ERROR,ERR,NG,NOREAD`，逗号/中文逗号/分号/顿号分隔，忽略大小写，`*` 结尾=前缀匹配不误伤同前缀真码，`IsIgnoredScanText`）过滤：命中即**不触发 `SerialNumberScanned`**（序列号框/协调器都收不到、不存 `{SN}=ERROR` 脏目录），改触发 `IScanner.ScanFailed` 事件 → 协调器 `OnScannerFail` 置 `_serialErrorSeen` → StepScanChannel 等 SN 阶段见失败信号**立即把扫码结果写 2 通知 PLC**、不等 ScanWaitMs 超时（真码 `_serialReceived` 仍优先于失败信号；事件 hook/unhook 在 AttachScanners 成对维护）。**扫码结果"2=死等补录"协议（V2.14.33，PLC 侧已配合）**：产品必须有 SN，PLC 拿到 40004=2 会**死等人工补录**——不复位请求、不判 NG，直到上位机把 2 覆盖成 1 才走 OK 流程。`StepScanChannel` 步骤1（等 PLC 复位）里检查 `_scanResultWritten==2 && _serialReceived`（操作员补录完成，`SetManualSerial` 置位）→ 立即 `WriteScanResult(1)` 覆盖，否则 PLC 永远等不到 1 流程卡死（V2.14.33 修复前的根因：补录只置标志、没改写结果寄存器）。**弹窗提醒（V2.14.32，UI 职责分离）**：MainForm 在 `SubscribeRuntimeEvents` 里同样订阅每台枪的 `ScanFailed`（`sc.ScanFailed += OnScannerFailPrompt`）→ 切回 UI 线程弹 `ScannerFailForm`（外观对齐 LoginForm：蓝横幅+白面板+【人工补录】蓝主按钮/【稍后处理】白次按钮）——【人工补录】返回 OK 顺手调 `PromptManualSerial()` 接手本件；【稍后处理】**不是放行本件**（PLC 死等 2），操作员稍后经主界面【人工补录】按钮补录、协调器覆盖成 1 流程才继续。**弹窗只做"人看的提醒"，绝不参与业务判定（业务判定仍是协调器那套）**；**必须节流**：`_lastScannerFailPromptUtc` 记上次真实弹窗时刻，`ScannerFailPromptThrottle(30s)` 内重复失败只进日志不再弹（持续坏枪防刷屏）。**今日不再提醒（同版本增强）**：弹窗内 `chkMuteToday` 复选框勾选后，MainForm 记 `_scannerFailMuteDate=DateTime.Today`，当日后续失败一律不再弹（跨弹窗实例全局生效、次日自动恢复，业务判定与日志照常）——改弹窗逻辑先想"UI 屏蔽"与"业务判定"两层要分离。**读到真码自动关闭（V2.14.48）**：`ScannerFailForm` 弹窗与 `SerialInputForm` 补录框构造新增可选参数 `scanners`（`IEnumerable<IScanner>`），打开期间订阅每台枪的 `SerialNumberScanned`——读到真码（说明扫码枪已恢复、扫码路径已把 40004 从 2 覆盖成 1）即自动以"非 OK"语义关闭（不触发人工补录流程），省操作员一次点击；**订阅/退订必须成对维护**（`FormClosed` 退订防泄漏）、`_autoClosed` 标志防连续推码重复关闭、事件在工作线程触发须 `BeginInvoke` 回 UI 线程（模态 `ShowDialog` 的消息循环会处理）。设置窗体扫码枪 TCP/串口表均有"忽略文本"列。改动扫码枪通讯必须同步 `docs/CommandCenter.md` 的"扫码枪"章节与默认配置。
- **UI 线程禁做网络 IO（V1.0.1 血泪）**：轮询/连接/读写 PLC 与相机一律放后台线程（`System.Threading.Timer`），TCP 连接必须 `BeginConnect + WaitOne` 强制超时。禁止在 UI 线程同步 `TcpClient.Connect` 或 `ReadHoldingRegisters`——对不可达 IP 会冻结整个界面（表现为"点按钮半天才响应"）。
- **显示链路图片一律后台解码 + 缩略图（V2.13.2 血泪）**：禁止在 UI 线程同步"读盘 + GDI+ 解码 + 向 PictureBox 给全尺寸大图"——基恩士原图可达 2592×1944，每次刷新都卡界面。显示图片必须：① 走 `ProductionCoordinator.LoadThumbnailSafe`（`FileShare.ReadWrite` + 等比降采样到最大边 1280）把解码/缩放放到后台线程（如 `Task.Factory.StartNew`），完成后把小图 `BeginInvoke` 回 UI 赋值；② 计数/标题等轻量更新与图片加载分开投递（图片加载不得拖慢写回 PLC 结果的协调器线程）；③ 窗口被重建/关窗竞态时原地 `Dispose` 新加载的缩略图防句柄泄漏。**显示不等归档（同版本）**：FTP 取图时"jpeg 一到 FTP 目录 → 显示"与"归档复制（含 iv4p）+ 删 FTP 源"解耦——协调器在 `SaveImageFilePair` 之前就用 `LoadThumbnailSafe(jpeg)` 提前加载内存预览图塞进 `WindowData.PreviewImage` 随事件带走，UI 直接赋值；预览失败回退按 `ImagePath`（归档副本）后台加载。别再让显示等 iv4p 复制/重试。相机取图/归档本身仍走既有后台链路，别为显示去改归档逻辑。**半截文件防丢图（V2.14.49 血泪）**：jpeg 由 watcher Created 事件唤来（"先建文件再写入"），事件到达时文件可能**仍半截**，GDI+ 解码必失败——此时 UI 回退也救不回（回退用 `ImagePath`=FTP 源 jpeg 路径，归档成功后立即被删）。**后台补发（不阻塞节拍）**：预览加载只试一次、失败【不】在协调器同步等待（同步 Sleep 会拖慢 `_taskDone`→通道释放→下一拍受理）；改为显示事件照抛（计数/徽标照常）→ 归档 → 归档成功后后台 Task 读【完整归档副本】（补等 iv4p 后才归档，jpeg 已写完）解码补发新事件 `DisplayImageAvailable(windowIndex, thumb, isOk)`（只更新窗口图/徽标、不重复计数；换代检查 `_disposed` + 订阅者空判，无则 Dispose 缩略图），UI 侧 `OnDisplayImageAvailable` BeginInvoke 回 UI 更新、窗口重建则 Dispose。让 UI 永远走内存预览图路径、不依赖会被删除的源文件。
- **显示窗口矩阵用 TableLayoutPanel 百分比等分**：窗口数量由 `display.rows/columns` 配置，所有窗口尺寸由容器等分自动保持一致，禁止写死像素布局。**V2.14 起矩阵外层包 `pnlWindowScroll`（AutoScroll=true）滚动宿主；V2.14.15 起"铺满/滚动"判定阈值按显示区高度动态算**：`MainForm.ApplyGridScrollLayout` 每行最小高度 = 显示区高/10（下限 60px），因设置窗体行数上限 10，**行数 ≤10 恒走铺满（行高=显示区高/行数，与自适应铺满效果一致，"设置几行就几行平分显示区"）**；仅行数 >10（如手填列太少自动补行、极端窗口数）才切"滚动模式"（grid Dock=Top + 定高，右侧出竖直滚动条、滚轮/滑块翻看，标题栏/状态栏不随滚动）。**列数两种模式统一上限 7**（自适应天然 ≤7；非自适 `SettingsForm.nudCols.Maximum=7`，`DisplayConfig.ResolveLayout` 非自适应分支再钳位 `Math.Min(7,manualCols)` 双保险）。改动主窗体网格必须先过这套"铺满/滚动"判定。
- **显示窗口矩阵统一模型（V2.12.1，取代并合并 V2.12.0"自适应"；V2.14 改自适应铺排规则；V2.14.18 非自适窗口总数=行×列）**：
  **窗口总数 = `DisplayConfig.ResolveLayout(cameras, model, display).windowCount`（全链路统一唯一口径，
  主窗体 BuildWindowGrid / 设置页预览 / 协调器 / WindowPointForm / ConfigStore 对齐共用，禁止各层各写一套）**：
  自适应 = 各相机按型号点位表 `ProgramsFor(型号)` 条目和；**非自适 = 手填行×列（放不下点位自动补行）**，
  **点位不够多出的格子 = 【空窗口】（映射 null 条目）**——主界面照样建窗占满显示区、只是不接图，
  不是"点数少就留空白"。各型号点位不同（U171=24、Z121=29…），每个型号按自身点数算窗口总数/空窗口位置。
  **自适应自动铺排（V2.14）**=列最多 7，遍历列 1..min(7,总数)、行=ceil(总数/列)，取"行列和最小、并列列多者优先"
  ——效果接近方形、缺格集中在最后一行且最少、窗口尽量放大占满（1→1×1、2→1×2、3→1×3、4→2×2、5→2×3、
  6→2×3、7→2×4、28→4×7），`AutoFitCameraStarts` 返回各相机窗口起始序号
  （"前上相机后下相机"分组），主窗体 BuildWindowGrid / 设置页预览 / 协调器 / WindowPointForm **共用同一套
  计算，禁止各层再各写一套**。**勾选"自适应"只决定行列是否自动算**：`AutoFit=true` 时行/列输入框置灰
  （行列按上述规则自动铺排）；不勾时手填行×列**即窗口总数**（填满显示区、所见即所得），两模式区别只在
  "行列怎么来"。**空窗口约定（V2.14.18）**：默认铺排 = `ResolveWindowPointMap(cameras, windowCount, model, maps)`
  前 N（=点位数）张按"前上相机后下相机"填 `WindowPointItem`、**尾部 null=空窗口**；空窗口可【交换位置】
  （把点位搬进空窗口的唯一入口，含空↔空无效果），**不支持【编辑点位】【禁用/启用】**（WindowPointForm 选中
  空窗口时按钮自动置灰 + 方法内防御提示），WindowEnabled/WindowStationMap/WindowPointMaps 全部按行列乘积
  占位对齐；空窗口不参与协调器 PLC 轮询/点位匹配（null 条目天然跳过）。**存图点位统一 = 相机点位号（StationNo）**（上下相机各自从 1 起会重复，靠
  ImageStore 归档子目录 **`{相机}` 层隔开**——`SubDirs` 默认含 `{相机}`，旧配置加载自动补，绝不拿
   WindowStationMap/windowIndex 当存图点位）；手动点位编辑（编辑点位/交换位置/恢复默认）在 WindowPointForm
  里两种模式**都可编辑（V2.13 恢复）**：结果按型号分表存 `DisplayConfig.WindowPointMaps`
   （`WindowPointItem{CameraId,StationNo}`（V2.13.5 起，此前 V2.13 为 `CameraIndex` 列表下标），
   默认=前上相机后下相机铺排、不编辑行为与旧版零差异；
  `ResolveWindowPointMap` 按型号查表、长度≠窗口总数时回退默认；`ConfigStore.EnsureWindowPointMaps`
  加载/保存自动对齐）。**编辑规则（V2.14.2 修订）**：编辑点位候选=当前型号各相机点位表**全部**点位
  （被其他窗口占用的项标"当前窗口N，选中即互换"、选中自动与该窗口【互换点位】保持唯一性；
  V2.14.2 前实现是"排除已被占用组合"，但窗口总数=点位表条目和、一一对应下候选恒剩自己一个、
  换不了点位——已修；同"相机+点位"仍只对应一个窗口，`ProductionCoordinator.TryResolveActiveWindow`
  据此反查唯一窗口）；**交换位置任意两窗口可互换（含跨相机，V2.13.1 放开）**——窗口↔点位映射本来就是
   "归属相机+点位号"二元组（`WindowPointItem{CameraId,StationNo}`），上相机·点位3 与下相机·点位3
  是不同点位，反查键=(相机,点位) 在两窗口互换后仍唯一（值集合不变），故跨相机交换不会让反查混乱；
  交换只改"窗口↔点位"对应（写回 WindowPointMaps），**不改各相机点位表/程序映射 ModelStationPrograms**；
  恢复默认=重置该型号出厂铺排+全部窗口重新启用。
  **禁用状态跟随点位迁移（V2.14.20）**：交换位置（SwapCells）与编辑点位触发自动互换（EditSelectedPoint）
  时，**禁用标志 `_enabled[a]/_enabled[b]` 必须随点位条目一起互换**——禁用语义="该窗口对应的点位停了"，
  点位搬到哪扇窗、禁用就跟到哪扇窗，绝不能互换后禁用还留在旧窗口（否则旧窗口换来的点位照常拍、
  新窗口搬入的被禁点位却还在跑）。存储层 WindowEnabled 仍是"窗口序号→布尔"、与 WindowPointMaps 同长
  对齐，交换时成对搬移即等效"禁用跟点位走"；两处交换都带下标越界防御。
  **格子高亮三态配色（V2.14.21，UI 视觉约定）**：WindowPointForm 的格子有三种高亮颜色、互不混淆——
  普通选中=浅黄（`SelectedColor`，禁用/编辑按钮定位）、交换模式下第一次点选的起点=天蓝（`SwapStartColor`）、
  交换完成（SwapCells / EditSelectedPoint 自动互换）后参与互换的两扇窗=绿色（`SwapDoneColor`，现场 OK=绿
  成功语义）闪烁约 1.6s 后熄灭（`_swapFlash` + `_flashTimer`，FormClosed 手动 Dispose 防句柄泄漏）。
  高亮判定统一走 `HighlightFor`（优先级：交换完成绿 > 交换起点天蓝 > 普通选中浅黄），渲染统一走
  `ApplyCellHighlight`；**被禁用的格子高亮时保持灰底、只加同色粗边框，绝不能换底色**（否则丢失
  "已禁用"视觉语义）。改高亮渲染先读这两处，禁止各分支各写一套。
  **编辑副本深拷贝红线（V2.14.2 血泪）**：WindowPointForm 的 `_windowPointEdits` 必须用 `ClonePoints`
  深拷贝 `WindowPointMaps` 里持久化的 Points 列表当编辑副本——**绝不许直接引用目标列表对象**（此前
  引用导致交换/编辑/恢复默认立刻污染 `_cfg`，用户点【取消】也生效、后续保存照落盘）；点【确定】OnOk
  才把副本整体赋回目标，与 WindowEnabled/`_programEdits` 的"副本式编辑"一致。设置页勾选自适应仅置灰行/列输入框并弹 ToolTip
  明示"自适应只影响行列形状、不影响点位编辑"。
   **默认型号（V2.12.3）**：`AppConfig.ProductModel` 默认 **"U171"**（非空），无配置文件首次启动也
   按该型号点位表铺出对应窗口（U171=上20+下4=24 窗），不会因型号空串把窗口塌成 1 个（此前 `Load()`
   无文件分支直接 new AppConfig() 连相机列表也是空的，窗口=0→兜底 1 个的回归根因）；`ConfigStore.Load`
   把"空段兜底+数组对齐"抽成 `ApplyDefaults`，有/无配置文件统一走。
- **PLC 握手协议（V2.7 定稿，从站模式）**：现场 PLC(汇川)做主站、上位机做从站监听本机 502；
  **"请求-结果-复位"三拍握手**，寄存器固定 40001~40012（完整协议见 `docs/CommandCenter.md` §5.5）：
  请求区（PLC只写）：`40001 扫码请求`(0/1)、`40002 上相机拍照请求`(1~255=点位)、`40003 下相机拍照请求`；
  结果区（PLC只读，上位机写）：`40004 扫码结果`(0/1/2)、`40005 上相机`、`40006 下相机`(0/1/2，相机结果
  另支持 **3=点位禁用跳过**）；型号区：**`40007`=型号序号（V2.14.13，查 `PlcConfig.ModelIndexes`
  映射，默认 Z121=1、U171=2）+ `40008~40012`=型号 ASCII 字符串**（每寄存器 2 字符 ASCII、高字节
  在前、不足补 0x00、最多 10 字符，超长从 40013 向后扩展；PLC 优先用 40007 序号区分型号）。**三拍流程**：PLC 写请求≠0 → 上位机处理完
  写结果≠0 → PLC 读走并复位请求=0 → 上位机看请求清零再复位结果=0 → 进入下一拍。**复位确认改"边沿记忆"（V2.14.41 前期段，通道释放被拖住；"NG 收不到"残留窗口治柄见 V2.14.42）**：三拍中的"复位请求=0"是**即逝中间态**——PLC 读到结果≠0 后把请求写 0、随即立刻写下一拍的新请求（≠0），PLC 扫描/轮询周期越短"0"窗口越窄；而上位机相机通道释放又多卡在 `_taskDone`（FTP 取图+归档 3~5s，远晚于 PLC 复位），旧"**当前**读到请求==0"这种瞬态判定必被错过（读到的是 PLC 已提前写入的下一个请求≠0）→ `_activeCh` 永锁相机ID、后续请求一律不受理 → PLC 拿不到 OK/NG。修复：`StepScanChannel` 步骤1/`StepCameraChannel` 步骤2 改"复位边沿记忆"（观察到一次请求==0 即永久记下 `_scanReqResetSeen`/`_reqResetSeen`，PLC 写 0 本身就是"已读走本拍结果"的回执）+ 相机通道加**点位推进兜底**（请求变成另一个点位≠本拍 `_pendStation` → PLC 必然已"读结果→复位→进下一拍"，V2.14.42 起**不再要求 `_taskDone`**）；still==0/==本拍点位（同点位连拍）时继续等真实复位，**绝不提前清零**（V2.12.5 丢结果红线不回退）、`_taskDone` 闸门保留（V2.13.7 防同相机并发取图不回退）。**"结果尽早清零"（V2.14.42，现场"每个点位都拍了、log 每次写了 1/2、但 PLC 收不到 NG（OK 正常）"根治）**：判定即写把 1/2 落进结果寄存器后，旧实现要等 `_taskDone`（FTP 取图+归档 3~5s）才清 0 → 结果寄存器存在 3~5s **残留窗口**，PLC 快轮询时下一拍 <1s 就来读走上一拍残留值、把本拍 NG（基恩士 NG 判定比 OK 更久、写入更晚）提前消费掉 → PLC 侧丢 NG。修复：`StepCameraChannel` 步骤2 拿到"PLC 已读走本拍结果"回执（`_reqResetSeen`）就**立即 `WriteCameraResult(0)`**（新增 `_resultCleared` 只清一次），残留窗口 3~5s 缩到 ~100ms；`_taskDone && _resultCleared` 才释放通道，清 0 与释放解耦、互不拖累。**【V2.14.44 保护版收紧（2026-08-16，现场"PLC 读不到 40005"疑似被"点位推进推断立即清 0"抢清）】**"立即清 0"改为**只认真回执 `_reqResetSeen`**（PLC 把请求写 0 = 真已读走本拍结果）；原"点位推进推断"（still>0 且 != `_pendStation`）拆到新增 `_reqAdvancedSeen`，**只作通道释放兜底、不再触发立即清 0**——防 PLC"先推进请求、后读走结果"（不同拍）时上位机拿推断当回执、抢在 PLC 读之前把本拍 1/2 清成 0（DevTest 读 0 / PLC 读不到）。推断放行时结果寄存器残留的上一拍值，由**下一拍 `BeginCameraChannel` 受理时统一 `WriteCameraResult(cfg, 0)` 清掉**（此刻本拍尚未写结果，清的是上拍残留，绝不误清新拍）。通道释放条件改 `_taskDone && (_resultCleared || _reqAdvancedSeen)`。恢复 V2.14.42 原行为=把"立即清 0"条件改回 `_reqResetSeen || _reqAdvancedSeen`（见 StepCameraChannel 步骤2 与 `_reqAdvancedSeen` 字段注释）。改动握手复位判定先读本段与 `docs/CommandCenter.md` §5.3/5.4。改动握手复位判定先读本段与 `docs/CommandCenter.md` §5.3/5.4。**【V2.14.46 协议红线（2026-08-17，现场日志坐实"PLC 读不到结果"的第一拍断点）】**上位机日志缺失上相机 2,4,6,8,10,12,14,16,19 九个点位 + 点位18 后 30s 空白，用户确认缺失点位 = **PLC 写 40002 后立刻清零（脉冲式请求）**——请求保持 < 上位机轮询周期(100ms)，上位机轮询抓不到 → 无触发日志 → 点位缺失（收不到就没日志，只看日志误以为正常）；"有的点位收到、有的缺失" = 轮询相位碰运气。**所有权澄清：请求寄存器 40002/40003 由 PLC 写、PLC 清；上位机只写/清结果寄存器（40005/40006）**——"写入后立刻清零"只可能来自 PLC 梯形图，别往上位机头上安。**要求（写给 PLC 工程师，与 V2.14.45"读后复位"合并即完整协议）**：PLC 写请求后**必须保持该值**，直到读到对应结果≠0 才清零并推进下一拍；**禁止"写入请求后立刻清零"**。上位机侧无需改（它从不写请求寄存器）。改动握手复位判定先读本段与 docs §5.3/5.5。**新一轮清窗（V2.14.11）**：`ProductionCoordinator.BeginScanChannel`（收到 40001=1 扫码请求、本轮生产启动）触发 `RoundStarted` 事件，MainForm 订阅后清空各窗口图片（`SetImage(null)` 回深灰空态）——新的一轮第一个动作就是扫码，上一轮图片已过时，提前清掉避免与新结果混淆，**同时徽标一并重置为默认绿 OK（V2.14.47——上一轮判定随旧件作废，避免旧件的 NG 红框残留误导）**；**标题栏计数随扫码清零（V2.14.28）**：同一事件里 `_total/_ok/_ng` 归 0 并 `RefreshTitle()`——扫码=新的检测件到位，总数/OK/NG 统计的是**当前一个工件**的检测点，完成一件清一件、不跨件累加；事件在轮询后台线程触发、UI 侧 BeginInvoke 回 UI 线程再遍历 `_windowControls` 清空。**上电/开机初始化复位（V2.13.8）**：PLC 与上位机各把各的结果寄存器先写 0，防断电重启残留旧值（上次 1/2/3）被误当新结果——PLC 侧由梯形图上电清 0；上位机侧 `PlcService.EnsureConnected` 每次成功建站（软件启动/断线重建/热更重建）都自动调 `ResetResultRegisters()` 把扫码结果（`ScanResultAddress`）+ 各相机结果（`PlcResultAddress`，MainForm.BuildServices 经 `SetCameraResultAddresses` 注册，0=未配置跳过）清 0，日志见"上电初始化：上位机结果寄存器已全部复位为 0"。**热更重建必须释放旧从站网络（V2.14.23 血泪，改动重建/释放必须遵守）**：热更重建或 `Dispose` PlcService 时，除 `_cts.Cancel()`/`_listener.Stop()` 外**必须调用 `_network?.Dispose()`**（NModbus 3.0.83 `ModbusTcpSlaveNetwork` 实现了 IDisposable，其 Dispose() 会停止 TcpListener 并逐个关闭所有已连入的 master TCP 会话 `ModbusMasterTcpConnection`；只 Stop listener 已 accept 的 master socket 会残留 → SettingsForm 保存后 PLC 主站认为连接还活着、不重连新从站 → 主界面 PLC 灯卡黄、PLC 发请求上位机收不到，必现）。三处清理点统一补：`EnsureConnected` 重建前/catch 分支、`ResetConnection`（被 Dispose 复用）与 `Dispose` 锁外强停分支。**相机结果"判定即写"（V2.13.7 定稿，重要）**：相机 OK/NG 判定在 T2 触发+读判定返回时就已知，`DoCameraShot` 判定一返回**立即** `WriteCameraResult` 落 PLC 结果（1/2），**不等 FTP 取图+归档**（那会让 PLC 陪跑数百 ms~2s 图传输）——取图/归档/显示降级为纯异步补充，图中途没到/归档失败只记日志+报警、**结果不回退**（以相机判定为准，图缺失只影响显示/存图）；同时通道释放必须等"PLC 已复位请求 **且** `_taskDone`（拍照 Task 完全结束）"（`StepCameraChannel` step2 闸门），否则判定即写让 PLC 提前复位请求、通道过早释放，下一拍请求进来会开新 Task 造成**同相机并发取图/删源混图**——改动握手流程必须同步本段与 `docs/CommandCenter.md` §5.3/5.4。**地址约定（V2.12.3 定稿）**：配置里统一存** DataStore 索引**（PLC 协议号 = 索引 + 40000，如协议 40002 上相机请求 → 索引 2，就是汇川 D2/D3/D5 这类数字，填 2 就是 2）；现场实测 PLC 写 40002 → 从站 DataStore[2]（曾误以为"零偏移直接用协议号"导致读 DataStore[40002] 永远读不到请求；V2.12.2 曾做"协议号-40000 换算"中间方案，V2.12.3 起按"改就改干净"删掉 `ProtocolToIndex`，业务层【零换算】）；地址全部收进 `PlcConfig`（`ScanRequestAddress/ScanResultAddress/
ProductModelIndexAddress/ProductModelAddress/ProductModelLen/ModelIndexes`）+ 顶层 `ProductModel`
   （**V2.14.24 当前型号入口唯一化：删掉设置页"产品型号"下拉（lblModel+cmbModel，与"产品型号配置…"
   弹窗功能重复）——当前型号只在主界面标题栏型号下拉 `cmbModel`（V2.8，操作员日常切型号用，候选恒
   预置 U171/Z121 ∪ 顶层 `ProductModels`）：`SwitchModel` 更新 ProductModel + 写盘 + 只重建协调器**
   （PLC/相机/扫码枪复用、设备不断连，**切型号即写型号区（V2.14.14）：`_plc.SetCurrentModel` +
   `WriteProductModel` 立即下发新型号**，按新型号查 `modelStationPrograms` 切程序）；
   **型号集合（增删 + 型号→PLC 序号映射）统一在"产品型号配置…"按钮弹窗（ModelIndexEditForm，
   V2.14.14）里表格维护（两列：序号/型号名称，前几行预载已有映射，确定写回 `plc.modelIndexes`、
   取消关闭不落盘）**——取代 V2.14.13 的"型号序号"框 nudModelIndex；`ConfigStore.EnsureModelIndexes`
   加载/保存时**双向对齐**：ProductModels ∪ 当前型号缺序号自动补一条映射（当前最大序号+1）、
   **ModelIndexes 里弹窗新增的型号名自动回流进 ProductModels（候选列表）**——保证"产品型号配置…"
   加的新型号在主界面标题栏型号下拉/窗口点位配置里可选可用。设置窗体 SettingsForm 用 `_currentModel`
   （= 打开时主界面标题栏选中值 > 配置 ProductModel > 预置第一候选）做自适应铺排计算与 WindowPointForm
   初始型号，WindowPointForm 里切型号经 modelLink 回调更新它、点【保存】才写 `_cfg.ProductModel`。
   **型号写入时机（V2.14.14）**：① 每次扫码 `ProductionCoordinator` 调 `PlcService.WriteProductModel`
   写 40007=型号序号 + 40008~40012=型号字符串；② **从站建站成功（EnsureConnected）即写当前型号**
   （`PlcService.SetCurrentModel` 缓存型号，建站成功自动写，PLC 不触发扫码也读得到）。版本化流程：
`ProductionCoordinator` 是**三通道状态机**（通道①扫码 40001/40004、通道②第1台相机 40002/40005、
   通道③第2台相机 40003/40006），**V2.13.5 起活跃通道=相机 ID**：`_activeCh = cameraId`，
   `PollNewRequest` 按相机表顺序轮询每台相机显式配置的请求通道，读到请求即 `BeginCameraChannel(cameraId,…)`
   （相机 ID → `IndexOfCamera` 反查下标取相机对象与点位表）。曾踩坑：把通道号当下标用（上相机通道=1
   当下标 1）导致"上相机触发误取下相机表、下相机触发越界，有效点位全回 3"（V2.12.5）。**相机通道地址
   （V2.12.6 起）收进相机表 `cameras[].plcRequestAddress/plcResultAddress`**（DataStore 索引，
   **V2.13.5 起 0=未配置通道、不参与轮询，废除"0=按相机序号自动"**：默认上=请求2/结果5、下=请求3/结果6，
   由旧配置迁移自动固化；**新增相机/第3台起必须与 PLC 协商后显式填地址**，未填则不参与 PLC 轮询）。
   相机触发前按窗口映射解析点位 → 按"当前型号→点位"查本相机映射表
  （先 `ModelStationPrograms` 型号表、型号没配表回退 `StationPrograms` 默认表）`PW` 切程序。
扫码枪列表经 `_coordinator.AttachScanners()` 注入（协调器比扫码枪先创建，用方法注入不用构造）。
   **轮询回调必须带重入互斥（V2.14.35）**：`PositionTimer_Tick` 入口用 `Interlocked` 令牌（`_polling`）
   串行化——`System.Threading.Timer` 不等待回调结束，某次回调执行超 100ms（PLC 从站断线重建/负载高/
   日志卡顿）会并发重入，两个并发回调同时看到"空闲(`_activeCh==ChNone`)+ 同一 PLC 请求≠0"会把一个
   请求触发拍两遍（开多个 Task 连发 T2、同点位并发取图/删源混图）；令牌一次只放行一个回调、重入的直接
   跳过（PLC 请求握手期间保持、跳过的拍下次还读得到，用"丢一拍"换"绝不重入双触发"）。改轮询入口
   先读这段；删除该互斥前先想"有没有谁在依赖重入行为"。
   **协调器重建换代守卫 + 启动磨合期（V2.14.36，与 V2.14.35 互补的另一条独立路径；V2.14.40 补三道修复）**：切型号
   `SwitchModel`/热更 `ApplyRuntimeConfig` 会 `Dispose` 旧协调器后瞬时新建 `Start`，旧协调器在途的
   `DoCameraShot` Task **没有取消机制停不掉**，而 PLC 请求在握手期间保持旧值——新协调器读完会对同一
   请求**二次触发相机**（连发 T2 + 两套并发取图/归档/删源互删对方源文件）。修复必须守住三层（改协调器
   重建/相机触发链路先读这一段，禁止只守一层）：① 新建协调器启动磨合期 `_startUtc`/`_startDrainMs`/
   `_drained`：`PollNewRequest` 在构造后 `_startDrainMs` 内一律不认领新请求，给旧 Task 写完 PLC 结果、PLC 读走并
   复位请求留窗口（磨合期后请求仍保持=上一代真没处理完，正常受理不丢请求）；② `DoCameraShot` try 开头
   与发 T2/T1 前检查 `_disposed`：已换代即收尾、**绝不把触发指令发出去**（相机不重复拍）；③ **判定即写照
   落 PLC 保留、但已换代时取图/归档/删源/显示全部跳过**（判定即写保留=PLC 能读完结果复位→磨合期满后不再
   触发——这是防二次触发另一半关键；取图归档跳过=防旧 Task 与新 Task 并发取图、互删 FTP 源、图发到已重建
   窗口矩阵，宁可这半拍无存档图）。换代与重入是【两道互补防线】：V2.14.35 防单协调器内回调重入、
   V2.14.36 防协调器重建丢认领，缺一不可。
   **V2.14.40 补三道修复（仍需同时遵守，缺一仍会双重触发）**：① **`DoCameraShot` 失败路径收口写 NG**——
   旧实现多个失败 return 路径（SetOutputFormat/SwitchProgram/TriggerAndRead 失败、防线1/2 _disposed）都不写
   PLC 结果，换代后旧 step1 不调，PLC 结果保持 0 → PLC 不复位请求 → 新协调器磨合期满读到请求仍保持 → 再发
   T2 → 双重触发。修复：局部标志 `plcResultWritten`——成功路径"判定即写"置 true；`finally` 里检测
   `!plcResultWritten` 补写一次 NG(2) 收口，任何失败/换代路径 PLC 都有结果可读、能复位请求（不检查
   _disposed，换代后 _plc 是 MainForm 复用同一实例仍可写；"写脏新协调器结果"由动态磨合期兜住）。
   ⚠️ 禁止回到"入口抢先写 2"方案：PLC 主站周期轮询结果、读到 ≠0 即复位请求，入口写 2 会让 OK 件在
   T2 判定中就被 PLC 误读成 NG 复位（正常拍一件 NG 一件）。② **磨合期改为动态值 `_startDrainMs = max(1200, 各相机 ResponseTimeoutMs 最大值) + 1000`**——
   原 1200ms < T2 超时 5s，磨合期满时旧 Task 还在等 T2 应答（T2 早已发出），新协调器受理即双重触发。动态值
   确保磨合期 > 旧 Task 最长耗时；③ **`KeyenceIV4Camera.TriggerAndRead`/`SendTrigger` 加 `Func<bool> stillAlive`
   回调**——V2.14.36 防线2 与"实际发 T2"间有竞态窗口（检查通过后、发 T2 前协调器被 Dispose，T2 仍发出）。
   `stillAlive` 在 EnsureConnected 之后、发 T2 前最后一刻调用，返回 false 放弃触发、不写 T2 字节进 TCP 流。
   `DoCameraShot` 调用处传 `() => !_disposed`，窗口收敛到最小。DevTestForm 不传（默认 null=旧行为）。仍非完全
   原子（volatile 读 + 后续 Write），彻底根治需 CancellationToken（改动量大未实施，见 TriggerAndRead 注释）。
   改动 PLC 或相机通讯或握手流程必须同步 `docs/CommandCenter.md`。
- **相机 FTP 双文件归档 + 点位程序号（V1.12.18；V1.12.24 起取图改"扫目录取最新"，V2.14.37 起"事件路径优先"）**：现场方案是"一台相机=一个 FTP 目录、所有点位图混放"——FTP 目录只当**中转暂存区**：基恩士每次拍照生成 jpeg+iv4p 两个文件（**文件名不保证恒为 `0000`（现场实测有 `0084` 等任意编号），上位机不写死文件名**），上位机等图 = `WaitForFtpImage`：**V2.13.6 起事件信号加速 + 轮询兜底双保险**：
   MainForm.BuildServices 为每台相机 `ImageStore.AddMonitor` 启动 FileSystemWatcher，相机一推图触发
   `FtpFileArrived` → **① 记住"事件实际到达的文件路径"（`_ftpArrivedPath[相机下标]`，V2.14.37）
   ② 置位该相机 `ManualResetEventSlim`** → 等图流程立即醒来（消除纯轮询最长 200ms 的被动延迟）；
   事件漏报/失效靠 200ms 轮询 + `ImageWaitMs` 超时重扫兜底，不失图）。**取图（V2.14.37）改为"事件路径
   优先"：`WaitForFtpImage` 先 `TryResolveArrivedPair`（用事件文件主名找同名 jpeg+iv4p 配对 + jpeg
`IsNewerThanTrigger` mtime 校验新于本枪触发时刻）命中即取**——外源高频推图堆叠时照片不再被
    `ImageStore.FindLatestPair(dir)`"按目录修改时间取最新"顶成别的枪/旧图（现场实测取到 ≈18 枪前的图）、
    照片与 PLC 判定配对错乱；**事件路径为空/配对缺失/早于触发时刻才回退扫目录取图
    （V2.14.39 红线：jpeg 优先 + 同主名随附）**：基恩士每次触发生成的 jpeg+iv4p **必然同主名**
    （`0084.jpeg`+`0084.iv4p` 一对），`FindLatestPair` **必须先取写时间最新的一张 jpeg，再按同主名配对
    .iv4p、jpeg 一到立即返回**——两线缺一不可：① 旧版"jpeg 组取最新 + iv4p 组取最新、不要求同名"在目录
    堆叠多拍文件时把**不同两次触发硬凑成一队**（现场实测触发33 归档 `00032.jpeg | 00031.iv4p`，跨拍错配
    +误删配套 iv4p）；② 若先等 iv4p 才返回，jpeg 先到 iv4p 迟到的现场画面会被拖到超时（显示要的是
    jpeg、不是 iv4p）——iv4p 迟到/缺失时返回 iv4p=null + WARN，只少归档复盘副本、不丢图主体。
    改动取图配对逻辑先读这段再动，禁止各层各写一套。
   调 `ImageStore.SaveImageFilePair` 双格式原样归档
   （jpeg 显示/归档主体、iv4p 基恩士私有格式原样复制；**归档文件名（V2.14.11 定稿）= 相机源文件名 +
   "_" + 时间戳**，如 FTP 里的 `0084.jpeg/iv4p` → 归档 `0084_20260814_164022_461.jpeg` + 同名 `.iv4p`，
   **不再用 FileNameTemplate 模板渲染**，模板仅旧版 TCP/BR 取图兼容）后 **`DeleteSourceFile` 删除"实际归档的那对"
   FTP 源文件**（处理即删防同点位重复触发新旧图混淆；**超时兜底时只要目录里有图照样归档**，不再
   "有图不存"）。**存图定期清理（V2.14.12）**：`ImageConfig.KeepDays`（默认 30，0=不自动清理，
   在 DirTreeEditForm"存图保留天数"处可调）控制存图目录保留天数，`MainForm.BuildServices` 建完
   ImageStore 即调 `StartPeriodicCleanup()` 起后台定时器（启动 30 秒后首次、每天一次，线程池线程不卡 UI）；
   `RunCleanupOnce` 只扫 `SaveRootDir` 顶层目录：**快速路径**第一层目录名是标准日期
   （`{年月日}` 渲染的"2026年08月11日"或"20260811"）按目录名直接判定过期即整棵删；
   **通用路径**目录名非日期时递归查整棵子树**所有文件**都早于阈值才删、还有新图保留（防误删）；
   单目录失败记日志跳过；**存图根目录是盘符根（如 `E:\`）直接放弃清理并告警，绝不删盘根子目录**。
   清理只动保存根目录过期目录、绝不动相机 FTP 取图目录；`Dispose` 停定时器（热更/关窗自动停）。
   改动存图清理逻辑必须同步本段与 docs/CommandCenter.md（§4.3 ⑨/第六部分）。**SubDirs 层级禁止"完整路径当一层"（V2.14.22 血泪）**：`ImageConfig.SubDirs`
   每项只能是【一层目录名/规则】（如 `{年月日}`、`{SN}`、`{相机}`、`OK`、`NG`），**绝不允许把整条绝对
   路径模板（如 `E:\Images\{年月日}\{SN}\{相机}\{OKNG}`）粘成一个层级**——否则 `ImageStore` 整串带 `\`
   路径被 `Path.Combine` 直接拼接成"一层套一层"超长嵌套目录（实测 `年月日\SN\相机\NG` 重复 4 层），
   且随配置保存越叠越深；三层防御防线：① `ConfigStore.NormalizeSubDirs`（ApplyDefaults 加载/保存时，
   在 `EnsureCameraSubDir` **之前**）把含 `\`/`/` 的项按分隔符拆层、剥掉"盘符+根目录段"前缀（已现
   `E:\Images` 被粘成 `E:\Image` 少个 s 的拼写错误）、忽略大小写去重，脏配置不手改即自愈；②
   `ImageStore.RenderSubDirsToSegments`（SaveImage/SaveImageFilePair 共用）渲染后同样拆段/丢盘符/丢
   根目录末段重名前缀再拼接，运行时配置被改脏也不出嵌套；③ `DirTreeEditForm.OnOk` 保存前拦截——
   任一层级含 `\` 或 `/` 弹窗"每级只能是一层名字"并中止保存（从源头杜绝。改动 SubDirs 相关逻辑
   必须先读这三处，禁止各层各写一套。⚠️ **ImageStore 归 MainForm 所有，协调器 Dispose【不得】关它**（V2.13.6 修复：
   此前协调器 Dispose 调 `_imageStore.Dispose()`，SwitchModel 只重建协调器、复用同一 ImageStore，
   切型号后监听就被关掉、信号加速失效）；热更（ApplyRuntimeConfig）与关窗（FormClosing）由 MainForm
   显式 Dispose，旧 watcher 不泄漏。**现场相机映射（V1.12.22 定稿；V2.13.3 修正 FTP 目录；V2.13.4 相机编号=独立 cameraId 字段）**：列表第1台=**上相机**=`19.87.6.213`→FTP 取图目录 `D:\IV存图\2`（**真编号 `cameraId`=2**）；列表第2台=**下相机**=`19.87.6.212`→`D:\IV存图\1`（**真编号=1**）——编号=存图目录号（上→\2=相机2、下→\1=相机1）。⚠️ **相机真编号独立存 `CameraConfig.CameraId`，不能靠交换列表顺序实现**（V2.13.5 起 PLC 通道地址
   全部显式配在各相机 `PlcRequestAddress/PlcResultAddress`、与列表位置无关，交换不影响通道；但真编号
   与存图目录号绑定，靠交换列表会让"相机ID=列表行序"的旧兜底显示错乱，仍禁止交换）；修改编号只动
   设置页相机表"相机ID"列或 json `cameraId` 字段。**设置页相机表按 CameraId 升序【展示】/保存（V2.13.8 引入、V2.13.9 修正）**：`LoadCameraRows` 按 CameraId 升序排表格（1,2,3,…、未填编号排最后），但 **`CollectCamerasFromGrid` 保存时按行 Tag（`CameraRowTag.OriginalIndex`）恢复【原始配置顺序】落盘、新增行排最后**——排序只影响"展示顺序"、不影响"持久化顺序"。⚠️ **为什么保存顺序必须保持原始顺序（V2.13.9 血泪）**："前上相机后下相机"默认铺排（`DisplayConfig.DefaultWindowPointMap`/`AutoFitCameraStarts`）依赖相机【列表顺序】；若把排序后的行序（[下,上]）写回 `_cfg.Cameras`，任何触发"重新生成默认铺排"的路径（WindowPointForm 点"恢复默认"、相机点位表增删行/新增相机加点位致映射长度变化、ConfigStore.EnsureWindowPointMaps 重置）都会生成"先下后上"的翻转铺排 → 窗口编号语义翻转、`WindowEnabled` 禁用错位（运行时 `(CameraId,StationNo)` 反查仍唯一不崩，但窗口与点位/禁用的对应错）。**`EnsureCameraIdentity` 补 CameraId 与 PLC 通道地址一律按 IP 匹配 `DefaultCameras()`（V2.13.9 起不再按下标 `defaults[i]`——列表顺序可能被手改 json/排序打乱，下标会张冠李戴）**；**相机ID全局唯一与孤儿映射校验（V2.13.11）**：相机 ID 是"窗口↔点位"反查的唯一关联键，**① 唯一性**——`EnsureCameraIdentity` 三遍分配（先收集已固定 ID→IP 匹配默认相机优先取真编号 213→2/212→1、不能被自定义相机抢→自定义相机取"第一个未被占用正整数"，替换行序 `i+1`，杜绝与默认真编号撞号），`SettingsForm.OnSave` 最开头拦截"两相机同号"（弹窗中止本次保存，0=未填不算重复），**`Save` 也补调 `EnsureCameraIdentity`**（新增相机 ID=0 写盘前统一补号）；**② 孤儿映射**——`DisplayConfig.PointMapValidForCameras`（有效 ID=`CameraId>0` 真编号、0 行序兜底，与铺排/反查同一把钥匙）全校验映射条目 CameraId 能否在相机列表反查到，`EnsureWindowPointMaps` 重置条件与 `ResolveWindowPointMap` 运行时防御双端共用（加载/保存清理+运行双保险），**改号/删相机后旧映射（旧 ID 变孤儿）自动重置该型号默认铺排、即写即用新编号**，禁止新增第三方手写"只看长度不看 ID"的映射校验（R1：改号后该相机全部点位跳 3 罢工）。⚠️ **存量配置顺序迁移（V2.13.9）**：V2.13.8 期间已保存过（cameras 已落成 `[下,上]`）的 json 光靠"保存恢复原始顺序"救不了，`ConfigStore.EnsureDefaultCameraOrder` 在**加载时**检测"两台默认相机恰好颠倒（IP 212 在 213 前）"自动换回 `[上, 下]`（自定义相机/单台默认/已正确顺序不干预）。相机字段本身仍只以 CameraId 为关联键（点位映射/PLC 通道/存图目录），排序是"整理展示顺序"，不是"改编号"，相机编号仍只能改相机ID列或 json。`CameraConfig.Name/FtpUploadDir` + `DefaultCameras()` 一处改。⚠️ 上/下相机的 **FTP 取图目录与安装位置相反配对**（上相机推到 `\2`、下相机推到 `\1`，现场实测相机推图目标如此）；旧版本曾写成"上→\1、下→\2"导致"触发相机正确、取图拿到对面相机"的错位 bug（V2.13.3 修复），**改相机 IP/目录仍只动 `DefaultCameras()` 与 `appconfig.json` 的 cameras 段**。**V2.8 型号映射预置也在 `DefaultCameras()`（与默认配置一致，U171 上相机点位 V2.14.16 扩至 1~20**：上相机点位 14/15/18/19→P010、16→P011、17→P012、2/20→P001，余 1→0、3~6→2、7→3、8→4、9→5、10→6、11→7、12→8、13→9）**：上相机 U171=P000~P012 / Z121=P013~P028、下相机 U171=P000~P003 / Z121=P005~P007；型号候选预置 `ProductModels=["U171","Z121"]`，改型号映射/加新型号优先走界面（设置页"产品型号"下拉 + WindowPointForm 型号下拉），不手改 json。**点位区分靠程序号（V1.12.25 起按相机分表，V1.12.26 支持任意台相机+下拉选择，V2.8 起按产品型号分表，重要）**：现场是"28 个窗口点位对应两台相机分工拍摄"（不是每台相机拍全部点位），且各相机程序库互相独立，所以点位→程序号映射**必须每相机一张表**，**且同一台相机在不同产品型号下程序号/点位归属不同**（如"上相机"型号 U171 用 P000~P012、型号 Z121 用 P013~P028），故再**按型号分表**：`CameraConfig.StationPrograms`（`List<StationProgramItem>`，`{stationNo,programNo}`，JSON `stationPrograms`）作"默认/不区分型号"表 + `ModelStationPrograms`（`[{modelName,programs:[{stationNo,programNo},…]}]`，JSON `modelStationPrograms`）每型号一张；运行时 `ResolveProgramForStation` 先查当前型号同名表（大小写不敏感）、型号没配表回退默认表、仍无该点位就不切换。型号候选走顶层 `ProductModels`（预置 U171/Z121，设置页可手输加入）。设置入口与"窗口↔存图点位"矩阵**同页混排在 `WindowPointForm`**（**相机 + 型号双下拉**（V2.12.4 起型号下拉**只列真实产品型号、默认选中与主界面标题栏型号一致**；"默认（不区分型号）"项已移除，`StationPrograms` 默认表仅作型号没配表时的运行时回退、界面不再编辑，只编辑对应型号的 `ModelStationPrograms`）+ **点位/程序号两列下拉选择**（V1.12.26）：**点位列候选=窗口映射点位（数量=窗口数，点位默认=窗口编号、互换/个别调整仍用同一集合）**、**程序号候选="不切换"+0~127（0 合法；程序数量与具体编号由相机程序库决定、与窗口数无关，现场动态选）**；点位不拍直接删行、"不切换"=保持相机当前程序）。**新增相机也自动有自己的独立映射表**：SettingsForm 相机表加一行即新相机（`LoadCameraRows` 把来源配置挂行 Tag、`OnSave` 经 `CollectCamerasFromGrid` 复用 Tag 对象保留 `StationPrograms`+`ModelStationPrograms`，映射配好后点保存不会丢；新增行 Tag=null→保存时建空表）。触发切程序在 `ProductionCoordinator.TriggerOneCamera`：先按"本轮该相机要填的窗口"（`_nextWindowIndex + idx`，与 FinishAll 环形窗口分配一致）经 `ResolveStation` 得点位 → `ResolveProgramForStation` 查**本相机当前型号表** → 命中先 `SwitchProgram`（`PW,nnn`，**`ProgramNo >= 0` 才发，0 也是合法程序号，失败即中止该相机**）再 `T2`、**未命中不切换**（不再读固定 `CameraConfig.ProgramNo`——该字段 V1.12.25 起废弃，仅旧配置兼容）。**PW 同程序号跳过（V2.14.19 节拍优化）**：`KeyenceIV4Camera.SwitchProgram` 缓存"上次成功切到的程序号"（`_lastProgramNo`，锁内读写），目标程序与缓存一致直接 `return true`、**不再重发 PW**——现场相邻点位常是同一程序（U171 上相 点3~6→P002、点14/15/18/19→P010），一次 PW 往返+相机切换实测 200~390ms（比 T2 还久）纯浪费，跳过即省；⚠️ **连接重建（断电/断线/超时重连）必须在 `EnsureConnected` 连接成功处把缓存重置为 -1**，否则相机恢复默认程序后缓存骗过跳过、会拿默认程序错拍（该点位拍出无意义图）——改 PW 相关逻辑先检查这条防线；DevTest 手动切程序走同一实例会刷新缓存，不冲突。`.SetOutputFormat`（`OF,nn`，配置非空才发、失败即中止）在切程序之前；注意 **`OutputFormat` 必须恰好 2 位数字**（"00"~"03"），配置非法会让该相机触发直接失败（`SetOutputFormat` 校验长度/数字后 false）；`SwitchProgram` 程序号越界会**自动夹到 0~127**（配置 128+ 不报错而是切到 127）。V2.7 起点位来源 = PLC 请求 `40002`/`40003` 里带的点位编号（触发前再按窗口映射确认归属相机，不再有单独的点位寄存器）。存图文件名默认加时间戳后缀（`ImageConfig.FileTimestampSuffix`）。**取图方式仅保留 Ftp**（Tcp/BR 代码留作旧配置兼容、设置窗体不再提供 Tcp 选项）。改动相机通讯/归档流程必须同步 `docs/CommandCenter.md` 第四部分与默认配置。
- **相机应答↔指令匹配（V2.14.34 红线，"NG 被误报 OK"根治）**：基恩士无协议帧**不带"应答哪条指令"
  的标识**，只能靠上位机按顺序等；而相机应答是异步的（T2 拍照+判定 200ms~数秒），节拍压紧时上一拍
  滞留的旧应答（可能正是 `RT,...,OK` 帧）会顶着当前指令的真应答。**读相呼应答必须校验"本条指令的
  应答"而非"读到什么用什么"**——`KeyenceIV4Camera.SendCommandAndReadLine` 三层防御（新增/改动相机
  通讯如读应答相关，必须先对齐这三点，禁止各层各写一套）：
  ① **发指令前非阻塞排空 `_stream` 已缓冲旧帧**（`while (_stream.DataAvailable)` 读掉，只读"此刻已可读"
   不等待、节拍无损；排在发送前，流里只可能有旧残留、不可能吞掉本条应答）；
  ② **读到的行校验期望前缀**才返回（各指令：T2/RT→`RT`，PW→`PW`，PR→`PR`，OF→`OF`，T1→指令名，
   `ER` 一律视为合法应答——它是本指令错误回执，必须交给调用方判失败，**不能当残留丢弃**）；读到不
   匹配行（残留/串位帧）记 Warn 丢弃、继续读；5 个调用点（TriggerAndRead/SwitchProgram/
   ReadProgramNo/SetOutputFormat/SendTrigger）已全传前缀，**新增调用点必须带**；期望前缀 null/空=
   退回"读到一行用一行"旧行为（仅供旧代码兼容）。
  ③ 读不到匹配行而超时 → 返回失败 + 断连标记，走既有"失败=NG"路径。
  **`ParseResult` 判定同步红线**：详细格式扫描响应**全部**字段，任一明文 `NG` 即判 NG（复合工具
  判定"任一工具不良=整体不良"，绝不因只认第 2 字段漏 NG）；无 NG 且有明文 OK 才判 OK；完全无明文
OK/NG 才回退标准格式逐位判定。改动相机读应答/判定逻辑必须同步 `docs/CommandCenter.md` 第四部分
   "应答-指令匹配与残留排空"段落与默认配置说明。
   **触发计数跳变检测（V2.14.38 排查辅助约定）**：详细格式 `RT,计数,OK,...` 第 1 字段=相机合计
   触发编号（每次实际触发 +1，含外部触发源）。`TriggerReadOutcome.TriggerNo` 透出该值，`TriggerAndRead`
   对照每相机 `_lastTriggerNo`——跳变 >1 记 WARN"检测到触发计数跳变：X→Y（两次T2之间被外部额外触发
   N次）"、计数回退记 WARN（断电重启/复位），正常每次 +1 不刷日志（节拍无感，仅现场远程定位"外部
   第二触发源"用）。改动此检测须同步 docs 第四部分与 CHANGELOG。

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
| `CommandCenter/Views/MainForm.cs` | 主窗体：标题栏 + 窗口矩阵 + 事件接线；**标题栏型号下拉 cmbModel（V2.8，操作员直接切型号，见 SwitchModel/InitModelCombo；配置对话框内切型号只同步设置页型号下拉、不实时切主界面——保存后 ApplyRuntimeConfig 统一刷新，延迟生效）**；序列号框 **lblSerial 只读展示（V2.14.7 由 TextBox 换回历史 Label 框）** + 右侧"人工补录"按钮 btnManualSerial（V2.14.6 恢复 V1.12.17 弹窗交互，替代 V1.12.19 框内直录，见 SetupSerialEditor/PromptManualSerial；**V2.14.7 起取消 txtSerial 双击弹窗与悬停 ToolTip**，手动补录仅靠 btnManualSerial 按钮入口，扫码枪收码 OnSerialScanned 仍直接覆盖只读框文本，两条通道隔离不互抢） |
| `CommandCenter/Services/ProductionCoordinator.cs` | 生产流程编排（两阶段状态机：扫码到位→扫SN→相机到位→拍到图→上报→循环），业务核心 |
| `CommandCenter/Services/ConnectionMonitor.cs` | 连接健康监控：后台心跳 + 断连自动重连 + 边沿日志（对齐 AgingTestSystem） |
| `CommandCenter/Services/PlcService.cs` | 汇川 PLC Modbus TCP 读写（NModbus 3.0.83） |
| `CommandCenter/Services/KeyenceIV4Camera.cs` | 基恩士 IV4 TCP 无协议触发 + 读取判定（T1/T2/RT/PW/OF 指令，V1.12.18 加切程序） |
| `CommandCenter/Services/ImageStore.cs` | 相机 FTP 推图监听 + 图片归档（SaveImageFilePair 双格式 jpeg+iv4p） |
| `CommandCenter/Models/AppConfig.cs` | 全部可配置项模型（相机/PLC/显示/图像/扫码/安全） |
| `CommandCenter/Utils/ConfigStore.cs` | appconfig.json 读写（小驼峰序列化） |
| `CommandCenter/Utils/I18n.cs` | 界面国际化（V2.15.0）：`I18n.T("中文","English")` 双参内联翻译，`I18n.Language`（zh-CN/en-US，默认中文，setter 触发 LanguageChanged 由 MainForm 热刷新）；日志保持中文，OK/NG/PLC/IP/SN 专有名词不翻译。新增界面文本一律用 `I18n.T` 双语。**切换入口在主界面标题栏【系统设置】按钮右侧的 `btnToggleLanguage`（V2.15.1 起，点击即切即存）** |
| `CommandCenter/Utils/SecurityUtil.cs` | 管理员密码 SHA-256 哈希 + 记住密码 DPAPI 加解密（登录/改密码/回填共用） |
| `CommandCenter/Views/LoginForm.cs` | 账号登录对话框（管理员 admin / 开发者 dev 双账号，按角色分流进设置或功能测试，V1.9.0/V1.12.0） |
| `CommandCenter/Views/DevTestForm.cs` | 功能测试窗体（开发者专用：相机 T1/T2 触发（T2 取图闪图存图，V1.12.24）+ PLC 寄存器交互 + 扫码枪读码展示/发触发指令，复用主窗体连接，V1.12.0） |
| `CommandCenter/Views/SerialInputForm.cs` | 手动输入序列号对话框（V2.14.6 恢复；V2.14.7 外观改到 Designer 分部文件 SerialInputForm.Designer.cs，可用 VS 设计器拖拽微调 UI，本类只留业务：预填全选/回车确定/Esc取消/空提交拦截；V2.14.48 构造新增可选 `scanners` 参数，打开期间读到真码自动关闭）：外观对齐 LoginForm（顶部蓝色横幅+白面板+蓝主按钮），点标题栏"人工补录"按钮弹出（V2.14.7 起双击序列号框入口已取消） |
| `CommandCenter/Views/ScannerFailForm.cs` | 扫码枪异常提醒对话框（V2.14.32）：扫码枪读码失败（推 ERROR 等错误文本）时弹窗，提醒检查扫码枪或【人工补录】接手本件；外观对齐 LoginForm（蓝横幅+白面板+【人工补录】蓝主按钮/【稍后处理】白次按钮），回 OK 由 MainForm 调 PromptManualSerial 接手；【稍后处理】**不是放行本件**（V2.14.33：PLC 死等 2，需稍后补录覆盖成 1）；**☐ 今日不再提醒 复选框（V2.14.32 增强）**：勾选后 MainForm 记 `_scannerFailMuteDate=Today`，当日后续失败不再弹窗（次日自动恢复，业务照常）；**打开期间读到真码自动关闭（V2.14.48）**：构造新增可选 `scanners` 参数，订阅每台枪 `SerialNumberScanned`，读到真码自动以"稍后处理"语义关闭（免人工补录） |
| `CommandCenter/Views/ModelIndexEditForm.cs` | 产品型号配置对话框（V2.14.14，按钮入口在设置窗体 PLC 区"产品型号配置…"）：表格维护型号↔PLC序号(40007)映射（V2.14.25 起三列=选中勾选列/序号/型号名称，全居中；右上角【新增】/【删除选中】按钮，勾选多行批量删；**V2.14.29 起新增行必须点【新增】按钮（禁用 DataGridView 自带"* 新行"，防"无法删除未提交的新行"异常）；Delete 键与删除按钮共用同一 DeleteRows 逻辑（优先勾选行、无勾选退选中行），编辑单元格中按 Delete 仅删字符不删行**），前几行预载已有映射、确定才写回 `plc.modelIndexes`（编辑副本深拷贝、取消不影响原配置），由设置窗体【保存】统一写盘；外观对齐 LoginForm 横幅+白面板+蓝主按钮 |
| `CommandCenter/Controls/CameraDisplayControl.cs` | 相机显示窗 + 右下角自绘 OK/NG 徽标（主界面不显示点位标识，点位只走设置界面查询）；左上角窗口编号显隐由配置 `DisplayConfig.WindowIndexVisible` 控制（V2.10.6）。**窗口徽标显隐只随开关走（V2.14.26 还原 V2.14.24 前逻辑）**：`DisplayConfig.WindowOkNgVisible`（默认 true）开着就显示（未判定时默认绿 OK、拿到判定按结果变色），不再受"该窗口是否已拿到相机结果"限制——现场反馈空窗口/未接图时徽标也应在，图异步没取到不能把徽标整个藏掉 |
| `CommandCenter/Views/DirTreeEditForm.cs` | 图片存储目录结构可视化配置（逐级目录 + 文件名规则 + 实时预览 + 时间戳后缀开关 + 存图保留天数，V2.14.12） |
| `CommandCenter/Views/WindowPointForm.cs` | 窗口↔存图点位 + 点位↔相机程序号 可视化配置（格子矩阵编辑点位/交换/恢复默认，V2.13 恢复编辑并按型号存 WindowPointMaps + 相机下拉点位程序表，V1.12.25 同页混排、V1.12.26 两列改下拉选择、V2.12.0 自适应下按相机表铺排矩阵/格子标"相机名·点位号"；**相机↔型号单向联动（V2.14.5）**：cmbCamera 恒列所有相机、选定后 cmbModel 只列该相机有点位的型号、切型号不再反过滤相机——见 `ModelCandidatesFor`/`SyncModelForCamera`/`ApplySelections`） |
| `docs/CommandCenter.md` | **项目文档（V2.10 合并版）**：① 用户使用说明（操作手册）② 系统总览与设备清单 ③ 扫码枪对接 ④ 相机对接 ⑤ PLC 通讯对接与对外协议定义（§5.5）⑥ 计数与结果流转 ⑦ IP/参数速查 ⑧ 版本演进 |
| `docs/上位机通讯封装范式.md` | 通讯架构技术总结（连接/心跳/重连/UI 解耦范式，跨项目可复用，独立保留） |
| `CommandCenter/tools/Obfuscar/Obfuscar.Console.exe` | 代码混淆器本体（V2.14.31，离线单体 exe，发布时由 build-obfuscated.ps1 调用） |
| `CommandCenter/obfuscar.xml` | 混淆配置（全量重命名 + 字符串加密；**Models 命名空间必须跳过**，见技术栈红线） |
| `CommandCenter/build-obfuscated.ps1` | 一键混淆发布脚本（Release 构建→混淆→补 dll→冒烟），产物 bin/Obfuscated/ |
| `CHANGELOG.md` | 版本改动记录（最新在前） |

## 构建与验证命令

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  CommandCenter/CommandCenter.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m /m
```

- 构建成功标准：输出 `CommandCenter -> ...\bin\Debug\CommandCenter.exe` 且无 error。
- 无单元测试框架；以构建通过 + 冒烟测试为验证手段（`Start-Process` 启动 exe，等几秒确认进程存活再 `Stop-Process`）。

### 混淆发布命令（V2.14.31，部署前必跑）

```powershell
& ".\CommandCenter\build-obfuscated.ps1"
```

- 产物：`CommandCenter\bin\Obfuscated\`（混淆后 exe + 第三方 dll + config + Mapping.txt），整个目录拷去现场。
- 混淆不改功能；日常 Debug 构建（上面那条）不受影响。混淆版 PDB 失配不能断点，排查用 Debug 版 + 日志。

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