# AGENTS.md — CommandCenter 项目指南

> 本文件是 AI 助手在操作本项目前的**强制前置阅读**。开工前先读本文件，明确角色、约定与红线。
> 优先级：本文档 > 项目已有代码风格 > 通用最佳实践。

## 项目角色

你是本项目（Windows 窗体 C#/.NET Framework 应用）的**资深开发/维护工程师**，负责按用户需求改代码、修 bug、沉淀约定。改动必须**可编译、可运行、风格统一**，并在关键改动后更新 `CHANGELOG.md`。

## 技术栈

- .NET Framework **4.7.2** WinForms（非 .NET Core/.NET 5+，勿引入其语法/API），C# 语言版本 `LangVersion=7.3`
- 通讯：**NModbus 3.0.83**（汇川 PLC Modbus TCP）；相机走基恩士 TCP 无协议通信（自写 TcpClient）
- 序列化：**Newtonsoft.Json**（配置/配方）
- **依赖策略（重要）**：第三方库拷在 `CommandCenter/libs/` 目录由 csproj `<Reference HintPath>` 直接引用，**不依赖 NuGet restore**，离线可编译。新增第三方库请同样"拷 dll 进 libs 再引用"。

## 铁律（违反即返工）

1. **文件编码 UTF-8**。禁止 `Add-Content`/`Out-File` 默认编码写中文（会成 GBK）。写文件用 write 工具；新增中文文件后自查：`[IO.File]::ReadAllText(path, UTF8).Contains("预期中文")` 要能命中。
2. **不提交运行时数据与机密**：`Config/*.json`（appconfig/recipes 等运行时生成）、`Logs/`、`bin/`、`obj/` 一律 gitignore，绝不入库。
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
- **开发者账号 + 功能测试（V1.12.0）**：除管理员外还有开发者账号（`SecurityConfig.DevEnabled/DevUser/DevPasswordHash`，默认 `dev`/`dev123`）。MainForm.OpenSettings 登录后按 `login.Role` 分流：`Admin` → 系统设置 SettingsForm，`Developer` → 功能测试 DevTestForm。**功能测试窗体约定（必须遵守）**：① 只做通讯手动验证、不产生任何配置改动；② **复用主窗体传入的 `_plc`/`_cameras`/`_scanners` 实例、绝不新建 TcpClient/串口/连接**（内部 EnsureConnected 惰性建连缓存复用；扫码枪为设备主动推码，只订阅 `SerialNumberScanned` 事件收码、不重复 Open，可调 `SendTrigger()` 手动重发触发指令），关闭时不 Dispose 这些服务；③ 所有网络 IO 走后台线程 + SafeInvoke 回 UI（红线同 UI 禁 IO）；④ 开发者密码不支持界面修改（改密码面板仅服务管理员）。新增测试入口若需连设备，先找 MainForm 是否已有该服务实例，有了就传引用复用。
- **扫码枪触发指令（V1.12.1，基恩士 SR 无协议）**：Tcp 模式下扫码枪**不是连上就回数据**，上位机须先发一条打开激光/开始读取的指令（`ScanConfig.TriggerCommand`，默认 `LON`）才读码。`ScannerTcpService.TryConnect` 每次连接/重连成功后**自动发送一次**（发送时自动补 `\r\n` 帧结束符），配置留空则不发送。`IScanner.SendTrigger()` 供界面手动重发。串口扫码枪上电即读码、无需触发（串口实现 SendTrigger 为空操作）。改动扫码枪通讯必须同步 `docs/通讯接入.md` 的"扫码枪"章节与默认配置。
- **UI 线程禁做网络 IO（V1.0.1 血泪）**：轮询/连接/读写 PLC 与相机一律放后台线程（`System.Threading.Timer`），TCP 连接必须 `BeginConnect + WaitOne` 强制超时。禁止在 UI 线程同步 `TcpClient.Connect` 或 `ReadHoldingRegisters`——对不可达 IP 会冻结整个界面（表现为"点按钮半天才响应"）。
- **显示窗口矩阵用 TableLayoutPanel 百分比等分**：窗口数量由 `display.rows/columns` 配置，所有窗口尺寸由容器等分自动保持一致，禁止写死像素布局。
- **PLC 寄存器约定（V1.12.11 起从站模式）**：现场 PLC(汇川)做 Modbus 主站、上位机做从站监听本机 502；配置里一律存 **D 地址**（NModbus 从站 `SlaveDataStore.HoldingRegisters.ReadPoints/WritePoints(start,…)` 的 start 即 D 地址，0-based，与原主站 `ReadHoldingRegisters` 一致，无需 +40001）。握手寄存器沿用 D100~D112、读写方向反转（PLC 写到位进来，上位机写完成/计数/配方出去给 PLC 读），配方下发用 D108 标志位握手（上位机写自己区+PLC 轮询拉取+写 0 回执）。**两阶段流程（V1.12.16）**：产线是"先扫码、后拍照"——`ProductionCoordinator` 是状态机（等"扫码到位"→触发扫码等 SN →等"相机到位"→拍图→上报→回扫码阶段）；"扫码枪到位信号"寄存器字段 `PlcConfig.ScanMoveDoneAddress`（占位 D99，**地址待现场定稿，现场只需改 json 数值**）由 `PlcService.ReadScanMoveDone/ClearScanMoveDone` 读写；扫码枪列表经 `_coordinator.AttachScanners()` 注入（协调器比扫码枪先创建，用方法注入不用构造）。改动 PLC 或相机通讯或两阶段流程必须同步 `docs/通讯接入.md`。
- **相机 FTP 双文件归档 + 点位程序号（V1.12.18）**：现场方案是"一台相机=一个 FTP 目录、所有点位图混放"——FTP 目录只当**中转暂存区**：基恩士每次拍照生成 `0000.jpeg`+`0000.iv4p` 两个文件（文件名恒定），上位机 `ProductionCoordinator.OnFtpFileArrived` 按扩展名配对（`PendingCamera.FtpJpegPath/FtpIvpPath`）、**两个都到齐才算 IsSnapped**，`FinishAll` 调 `ImageStore.SaveImageFilePair` 双格式原样归档（jpeg 显示/归档主体、iv4p 基恩士私有格式原样复制）后 **`DeleteFtpSource` 删除 FTP 源文件**（处理即删防同点位重复触发新旧图混淆）。**现场相机映射（V1.12.22 定稿，与默认配置一致）**：相机1=**上相机**=`19.87.6.213`→FTP 取图目录 `D:\IV存图\1`；相机2=**下相机**=`19.87.6.212`→`D:\IV存图\2`（`CameraConfig.Name/FtpUploadDir` + `DefaultCameras()` 一处改）。改相机 IP/目录只动 `DefaultCameras()` 与 `appconfig.json` 的 cameras 段。**点位区分靠程序号**：`TriggerOneCamera` 触发前先 `SetOutputFormat`（`OF,nn`，配置非空才发、失败即中止）再 `SwitchProgram`（`PW,nnn`，**`ProgramNo >= 0` 才发（0 也是合法程序号），失败即中止该相机**）最后 `T2`；注意 **`OutputFormat` 必须恰好 2 位数字**（"00"~"03"），配置非法会让该相机触发直接失败（`SetOutputFormat` 校验长度/数字后 false）；`SwitchProgram` 程序号越界会**自动夹到 0~127**（配置 128+ 不报错而是切到 127）。PLC 点位号寄存器 `PlcConfig.PointInfoAddress`（占位 D113，**TODO 待 PLC 程序定稿**）定稿前点位由窗口映射 `WindowStationMap` 决定。存图文件名默认加时间戳后缀（`ImageConfig.FileTimestampSuffix`）。**取图方式仅保留 Ftp**（Tcp/BR 代码留作旧配置兼容、设置窗体不再提供 Tcp 选项）。改动相机通讯/归档流程必须同步 `docs/通讯接入.md` §2.2/§2.3 与默认配置。
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
| `CommandCenter/Views/MainForm.cs` | 主窗体：标题栏 + 窗口矩阵 + 事件接线；序列号框 txtSerial 点击直录（Enter 提交/Esc 还原/失焦非空提交，V1.12.19，见 SetupSerialEditor） |
| `CommandCenter/Services/ProductionCoordinator.cs` | 生产流程编排（两阶段状态机：扫码到位→扫SN→相机到位→拍到图→上报→循环），业务核心 |
| `CommandCenter/Services/ConnectionMonitor.cs` | 连接健康监控：后台心跳 + 断连自动重连 + 边沿日志（对齐 AgingTestSystem） |
| `CommandCenter/Services/PlcService.cs` | 汇川 PLC Modbus TCP 读写（NModbus 3.0.83） |
| `CommandCenter/Services/KeyenceIV4Camera.cs` | 基恩士 IV4 TCP 无协议触发 + 读取判定（T1/T2/RT/PW/OF 指令，V1.12.18 加切程序） |
| `CommandCenter/Services/ImageStore.cs` | 相机 FTP 推图监听 + 图片归档（SaveImageFilePair 双格式 jpeg+iv4p） |
| `CommandCenter/Models/AppConfig.cs` | 全部可配置项模型（相机/PLC/显示/图像/扫码/安全） |
| `CommandCenter/Utils/ConfigStore.cs` | appconfig.json 读写（小驼峰序列化） |
| `CommandCenter/Utils/SecurityUtil.cs` | 管理员密码 SHA-256 哈希 + 记住密码 DPAPI 加解密（登录/改密码/回填共用） |
| `CommandCenter/Views/LoginForm.cs` | 账号登录对话框（管理员 admin / 开发者 dev 双账号，按角色分流进设置或功能测试，V1.9.0/V1.12.0） |
| `CommandCenter/Views/DevTestForm.cs` | 功能测试窗体（开发者专用：相机 T1/T2 触发 + PLC 寄存器交互 + 扫码枪读码展示/发触发指令，复用主窗体连接，V1.12.0） |
| `CommandCenter/Controls/CameraDisplayControl.cs` | 相机显示窗 + 右下角自绘 OK/NG 徽标（主界面不显示点位标识，点位只走设置界面查询） |
| `CommandCenter/Views/DirTreeEditForm.cs` | 图片存储目录结构可视化配置（逐级目录 + 文件名规则 + 实时预览） |
| `CommandCenter/Views/WindowPointForm.cs` | 窗口→存图点位可视化配置（格子矩阵：编辑点位/交换位置/恢复默认） |
| `docs/通讯接入.md` | 相机/PLC 对接流程与寄存器表 |
| `docs/使用说明.md` | 用户操作手册（操作员视角：启动/登录/日常操作/设置/功能测试/排查） |
| `docs/现场设备IP清单.md` | 现场设备 IP/端口/触发指令速查与 appconfig 对照 |
| `docs/上位机通讯封装范式.md` | 通讯架构技术总结（连接/心跳/重连/UI 解耦范式，跨项目可复用） |
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
- **`docs/通讯接入.md`**：寄存器地址 / 相机指令 / 通讯流程等通讯类改动，必须同步并写明版本号。
- **`docs/使用说明.md`**：用户可见的操作变化（按钮/流程/新功能/排查项）同步更新，保持操作员手册与代码一致。
- **`AGENTS.md`**：若本次改动引入了新的项目约定（红线/约定/命令/文件导航变化），同步更新本文件。
- **代码注释**：改动处注释详细到小白能看懂；新文件/新方法写清头部说明；中文保持 UTF-8。
- **提交前自检**：`git status` + `git diff` 确认改动范围与文档同步都完成后再交付；用户不要求 commit 时只留工作区改动即可。