# 版本改动记录

## V1.12.27（2026-08-13）功能测试 T2 取图存图时序修复：触发后等待 FTP 推图 + 预览加载失败不静默

> 现场反馈：测试界面点"触发+判定T2（取图存图）"后图像显示失败。排查确认与 programNo/切程序
> **无关**（该按钮本就直接 T2、用相机当前程序拍照，从不切程序）。两个真实问题：
> ① **取图时序**：T2 触发成功只代表"相机已拍照"，图推到 FTP 取图目录还有网络/存储延迟，旧逻辑
> 立即扫目录 → 要么取到旧图、要么目录空（"FTP 取图目录里没有 jpeg"）。主流程靠 FileSystemWatcher
> 事件等图到达，测试窗体没有事件机制。② **预览静默失败**：`ShowTestImage` 里 `LoadImageSafe`
> 返回 null 时给 `picTestShot.Image=null`，图像区留白但路径标签仍显示绿色"最近存图"，无任何提示。

### 改动范围
- **`Views/DevTestForm.cs`**：
  - `BtnTriggerRead_Click`：T2 触发成功后**轮询等待**该相机 FTP 取图目录最多 5 秒
    （`FtpWaitAfterTriggerMs`），认"修改时间不早于触发时刻（-1s 容差）"的 jpeg 为本次新图
    （`IsNewerThanTrigger`）；超时后目录有旧图残留则取最新对兜底，无图才报"相机已触发但未推图"。
  - `ShowTestImage`：改为返回 bool，加载失败返回 false，调用方把路径标签改红色并追加日志
    （"存图成功但预览加载失败（文件可能被占用）"），不再静默留白。

### 为什么这么改
- 触发后立即取图是"凭运气"：相机推图延迟现场实测可达数百 ms~数秒，立即扫目录大概率取不到本张。
  轮询等待让"触发→推图→取图→归档→闪图"的链路与主流程"事件等图"语义对齐，联调结果稳定。
- 预览加载失败必须可见：存了图但显示不了会让现场误判"取图存图失败/没拍到"，红字提示可定向排查。

### 验证
- Debug 构建通过；冒烟启动进程存活。取图等待在后台线程轮询（Sleep 不阻塞 UI、不占 UI 线程），
  **注意**：相机 IP 关机时 T2 触发会先报"相机连接失败"，属预期（现场需先开机相机再测）。

## V1.12.26（2026-08-13）点位→相机程序号映射增强：支持任意台相机 + 下拉选择

> 现场反馈/澄清：V1.12.25 的"点位→相机程序号"映射表要**每台相机都有自己的表**，新增相机也要能配；
> 且配置时希望**用下拉选择点位和程序**，而不是手输数字。同时澄清了候选语义：**点位数量=窗口数量**
> （点位默认=窗口编号，滚换/个别调整后集合仍与窗口一致）；**相机程序的数量与编号是相机程序库定的、
> 与窗口数无关**，要在全集里动态选。本次补齐：
> ① 修复"配好映射→在设置页点保存→映射全丢"的隐患（保存时不再 `new` 重建相机对象）；
> ② 新增/未保存的相机也能立刻配它自己的映射表（表格行 Tag 绑定原配置对象）；
> ③ 映射表点位、程序号两列改成**下拉选择**（点位列=窗口点位、程序号列="不切换"+0~127）。

### 改动范围
- **`Views/SettingsForm.cs`**：
  - `LoadCameraRows`：每行把来源 `CameraConfig` 挂到 `Tag` 上（新增行 Tag=null）。
  - 新增 `CollectCamerasFromGrid()`：从表格行收集相机（复用行 Tag 对象→保留 `StationPrograms`，
    新增行新建对象并**回绑 Tag**），`OnSave` 与打开映射页**共用**同一批相机对象，映射不丢。
  - 打开 WindowPointForm 时改传 `CollectCamerasFromGrid()`（含未保存新增行），新相机立即能配表。
- **`Views/WindowPointForm.cs` + Designer**：`colStation`/`colProgram` 由文本列改为 **下拉列**
  （`DataGridViewComboBoxColumn`）。**点位候选以窗口映射点位为准**（数量=窗口数，仅兜底追加
  "已配但窗口里没有"的存量点）；**程序号候选＝"不切换"+0~127**（0 合法，程序数和编号跟相机
  程序库走、与窗口数无关，现场动态选）；`FlushProgramGrid` 读回："不切换"/空/非法→-1（不切换），
  int→原值；`ReloadProgramGrid`：程序号 -1 显示"不切换"。

### 为什么这么改
- 映射表必须**跟相机走**：新增相机是一台新相机、它自己的程序库是空的，理所当然要有自己的空映射表。
- 保存丢映射是 V1.12.25 的隐性缺陷：`OnSave` 用 `new CameraConfig{...}` 重建对象，配好的
  `StationPrograms` 全丢——现场第一反应"配了没用"。Tag 复用对象后修掉。
- 下拉选择避免手输错号（点位敲错、程序号敲成 10 却想发 P10 这种歧义），选项天然限制在合法范围。

### 验证
- Debug 构建通过；冒烟启动进程存活。
- harness 验证（22/22 通过）：保存往返保留每台相机映射（引用同一）；新增行 Tag 绑定、有自己的空表、
  配好映射后再次保存仍在；下拉候选覆盖全部点位/0~127/"不切换"；Reload -1 显示"不切换"、
  Flush 读回"不切换"→-1、int→原值、空点位行跳过、程序未选→-1。

## V1.12.25（2026-08-13）点位→相机程序号映射（设置页同页混排）；功能测试 T2 取图存图后删除 FTP 源图

> 现场"28 个窗口点位对应两台相机"：上相机拍一部分点位、下相机拍另一部分，**不是**每台相机都拍全部
> 点位；而每台相机的程序库各自独立（上相机的 P000 是上相机自己的程序），所以点位→程序号映射必须
> **按相机分表**。本次把固定 `CameraConfig.ProgramNo` 升级为 `StationPrograms`（点位→程序号映射表），
> 并把设置入口与"窗口↔存图点位"矩阵放进**同一个对话框同页编辑**（`WindowPointForm`）：
> 每台相机在表里配了哪些点位 == 这台相机负责拍哪些点位，触发时按本轮点位查表切程序，**未命中不切换**
> （避开不归本相机拍的点位）。同时功能测试页 T2 取图存图成功后，**同步删除对应 FTP 源图**，与主流程
> "中转暂存区处理即删"保持行为一致，避免测试把相机目录越堆越多。

### 改动范围
- **`Models/AppConfig.cs`**：新增 `StationProgramItem`（点位→程序号单条，`StationNo`/`ProgramNo`），
  `CameraConfig.StationPrograms`（每台相机一张映射表，JSON：`stationPrograms:[{stationNo,programNo},…]`）；
  `ProgramNo` 注释改为"V1.12.25 起废弃，由 StationPrograms 接管，仅保留旧配置兼容"。
- **`Services/ProductionCoordinator.cs`**：`TriggerOneCamera` 触发前改查映射表——按"本轮本相机要填的窗口"
  （`_nextWindowIndex + idx`，与 FinishAll 窗口环形分配一致）解析点位号 → `ResolveProgramForStation` 查
  本相机表 → 命中先 `PW` 切程序再触发、**未命中不切**（不再读固定 `ProgramNo`）；
  新增 `ResolveProgramForStation`（表空/为空点位返回 -1；校验 `ProgramNo >= 0` 故 0 是合法程序号）。
- **`Views/WindowPointForm.cs` + Designer**：窗体调大（640×664），下部同页混排新增"相机程序映射"区：
  相机下拉 `cmbCamera` + 点位/程序号可编辑表格 `dgvPrograms` + 新增映射/删除选中行按钮；
  编辑逻辑：切相机先 `FlushProgramGrid` 再 `ReloadProgramGrid`，点位留空=不拍、程序号留空/非法=-1（不切）；
  确定时窗口映射写到 `WindowStationMap`、各相机映射写到各自 `CameraConfig.StationPrograms`。
- **`Views/SettingsForm.cs`**：打开 WindowPointForm 时传入 `_cfg.Cameras`（同引用，点确定写回、点保存落盘）；
  相机表**移除废弃的"程序号"列**（`ProgramNo` 不再读/写，避免现场在旧入口填了却不生效——点位程序号
  必须去"窗口/点位配置…"下区配）。
- **`Views/DevTestForm.cs`**：T2 取图存图（V1.12.24）成功归档后再调 `ImageStore.DeleteSourceFile` 删除
  该相机的 jpeg+iv4p FTP 源图（后台线程执行、方法内吞异常，失败不影响测试结果）。

### 为什么这么改
- 旧固定 ProgramNo 是"一刀切"：一台相机只认一个程序，无法表达"同一台相机在不同点位用不同程序"，
  更无法表达"某点位不归本相机拍"；映射表按相机分组后两个问题一起解决。
- 映射与窗口点位同页编辑：一次到位信号、拍哪个点、切哪个相机程序是强关联配置，放同一个对话框
  一起改最直观，也避免设置页入口越堆越多。
- 测试页删图：功能测试会频繁重复拍照，不删源图会导致相机 FTP 目录越堆越多、且干扰下次"取最新"。

### 验证
- Debug 构建通过；冒烟启动进程存活。
- harness 验证（22/22 通过）：StationPrograms JSON 序列化/反序列化（程序号 0 保留）；
  `ResolveProgramForStation` 命中/未命中/-1/空表/null 各分支；WindowPointForm 同页写回窗口映射与
  两台相机映射（含点位留空跳过、程序号 -1 保留占位且查询不命中、切相机后各自写回）；
  `ImageStore.DeleteSourceFile` 删 jpeg/iv4p、幂等与 null 安全。

> 基恩士相机推图文件名不一定恒为 `0000.jpeg`/`0000.iv4p`（现场实测可能是 `0084.jpeg`/`0084.iv4p`
> 等任意编号）。旧实现虽用 FileSystemWatcher 事件记路径（兼容任意文件名），但收尾归档仍依赖事件
> 路径，**事件漏报/错过就会取不到图**。本次做"放错机制"：收尾时统一**扫相机 FTP 取图目录取修改时间
> 最新的一对 jpeg+iv4p**（`ImageStore.FindLatestPair`），不写死任何文件名；事件路径仅作目录扫描
> 失败时的兜底。同时把"图到齐才算 IsSnapped"的判定从归档必需改为信号加速——**超时兜底时只要
> 目录里有图照样能归档**（此前超时兜底有图也不存）。另在功能测试页把 T2 按钮升级为"触发+判定+
> 取图闪图存图"闭环（点位固定 1，存进主窗体配置的存图目录）。

### 改动范围
- **`Services/ImageStore.cs`**：新增 `FindLatestPair(string dir)`（扫目录按扩展名分组，各取
  `LastWriteTimeUtc` 最新的一对，支持 `.jpeg`/`.jpg` 与 `.iv4p`；目录不存在/无文件返回空结果不抛异常）；
  新增配套结果类 `LatestPairResult`。
- **`Services/ProductionCoordinator.cs`**：
  - `FinishAll` FTP 归档分支重写：优先 `TryResolveFtpSources`（扫目录取最新对），扫描失败回退事件路径；
    `hasImage` 判定不再绑定 `IsSnapped`/事件路径，目录里有图即归档；删除源文件改为删除"实际归档的那对"；
  - 新增 `TryResolveFtpSources` / `FtpDirFor`（目录解析逻辑与 Start 注册监听一致）；
  - `OnFtpFileArrived` / `PendingCamera` 注释同步为"事件只作信号加速，归档重扫目录"。
- **`Views/DevTestForm.cs` + Designer**：T2 按钮（`btnTriggerRead`）升级为"触发+判定T2（取图存图）"——
  触发成功即从该相机 FTP 目录取最新图 → `picTestShot` 闪图 → `SaveImageFilePair`（点位固定 1、判定 OK/NG、
  打开窗体时 SN 快照）存到主窗体配置的存图目录，结果/路径进日志；新增右侧图片预览框 + 存图路径标签；
  构造参数追加 `imageStore`/`cameraConfigs`/`serialSnapshot`（复用主窗体实例，不新建连接）。
- **`Views/MainForm.cs`**：`OpenSettings` 里 DevTestForm 构造传新参数。

### 为什么这么改
- 现场相机命名不可控 + FileSystemWatcher 事件偶发不可靠，双保险（事件加速 + 目录扫描兜底）才能真正
  保证"拍了就有图归档"；删源文件后下一轮取"最新"天然避重。
- 功能测试页此前只验证触发链路，无法验证"取图→闪图→存图"；升级 T2 后用一口气验证完相机→取图→归档，
  现场联调少一个来回。

### 验证
- Debug 构建通过；冒烟启动进程存活。
- harness 验证（8/8 通过）：`0084.jpeg`/`0084.iv4p` 混在 `0003.jpeg` 等旧文件中取到最新对；
  `.jpg` 兼容；目录不存在不抛异常；`SaveImageFilePair` 双格式归档到 `{年月日}/{SN}/{OKNG}/1_*.jpeg|iv4p`、
  不删源文件；仅 jpeg 无 iv4p 也可归档。

## V1.12.23（2026-08-13）相机列表加"序号"列=相机ID；主界面相机显示"有名称显名称、无名称为相机N"

> 为解决主界面"相机1/相机2"与设置表、现场名字（上相机/下相机）对应不直观：设置窗体相机表最前
> 加只读**序号**列（=1 起的列表位置，即相机 ID），主界面相机下拉/相机灯/状态明细统一改为
> **有配置名称（`CameraConfig.Name`）则显示名称（上相机/下相机），无名称才回退"相机N"**。
> 序号就是程序内部数组下标+1，保存仍按列表顺序写回配置，序号列不落盘、只作展示 ID。

### 改动范围
- **`Views/SettingsForm.cs`**：相机表新增首列"序号"（只读、窄列）；加载/添加/删除后
  `RenumberCameraSeq()` 重排（删中间某台后编号自动前移，保持 1 起连续）；ASCII 布局图同步。
- **`Views/MainForm.cs`**：新增 `CamDisplayName(i)` 统一出口（有名称显名称、无名称显"相机N"）；
  相机灯文字、相机下拉 `CamOverviewLabel`、悬停状态明细、标题栏注释全部改走该出口。
- **`Views/DevTestForm.cs`**（V1.12.22 已符合，无改动）：功能测试相机下拉已是此规则。
- 序号列=ID 的依据：`_cameras` 数组下标+1 即设置表序号列，`QuitReason`/`CameraIndex`
  等内部逻辑以数组下标关联，主界面 "相机N" 的 N 就是序号列数值，一处配置多处一致。

### 验证
- Debug 构建通过；序号列在保存循环中按列名读取，不涉及序号列，行为不变。

## V1.12.22（2026-08-13）相机映射定稿：上/下相机 IP 与 FTP 取图目录一一对应

> 基恩士工程师已把两台相机的 FTP 推图目录配置到本机，上位机只需按对应关系监听取图：
> **相机1=上相机=`19.87.6.213`→FTP 取图目录 `D:\IV存图\1`**；
> **相机2=下相机=`19.87.6.212`→`D:\IV存图\2`**。
> 注意此映射与旧默认**相反**（旧：相机1=.212、相机2=.213），本次一并改对。

### 改动范围
- **`Models/AppConfig.cs`**：
  - `CameraConfig` 新增 `Name` 字段（"上相机"/"下相机"，界面/日志展示用，纯展示不影响通讯）；
  - `IpAddress` 模型默认改为 `19.87.6.213`（相机1=上相机）；
  - `DefaultCameras()` 工厂方法改为上述一一对应（含 `FtpUploadDir=D:\IV存图\1/2`）——
    空配置/设置窗体默认行/主窗体兜底/添加相机全部走这里，一处改全场生效。
- **`Services/KeyenceIV4Camera.cs`**：新增 `DisplayName`（取 `CameraConfig.Name`，空为""），
  供下拉框/状态灯显示上/下相机。
- **`Views/SettingsForm.cs`**：相机表格新增"相机名称(上/下)"列（列序：名称/IP/端口/FTP目录/
  取图方式/程序号），加载/保存/添加行全链路带上 Name 与默认 FTP 目录。
- **`Views/MainForm.cs`**：标题栏相机下拉文案改为"上相机  IP"（无名称退回"相机N IP"）。
- **`Views/DevTestForm.cs`**：功能测试相机下拉同样显示上/下相机名称。
- **文档同步**：`AGENTS.md`（相机映射约定）、`README.md`（可配置项）、
  `docs/现场设备IP清单.md`（拓扑/清单/json 对照）、`docs/通讯接入.md`（§2.2a/§2.2b/§2.3/版本记录）、
  `docs/使用说明.md`（§五 FTP 说明/§七排查表）、`CHANGELOG.md`。

### 为什么这么改
- 相机推图目录是"相机侧配好、上位机只管监听"，但目录必须与相机索引一一对应，
  否则一张图的相机归属分不清（`OnFtpFileArrived` 按 `cameraIndex` 配对 pending）。
  把 IP/目录/名称收敛到 `DefaultCameras()` 一个出口，改现场只需动一处。

### 验证
- Debug 构建通过；冒烟启动进程存活。
- 空配置下 `DefaultCameras()` 返回两台：上相机 213→`D:\IV存图\1`、下相机 212→`D:\IV存图\2`；
  设置窗体默认两行、添加相机一行均与此一致。

## V1.12.21（2026-08-13）开发者账号也支持记住密码，双角色记录互斥

> 此前"记住密码"只对管理员生效（开发者登录不回填、也不写文件）。现场反馈：开发者也常在
> 功能测试页往返，每次都要敲密码麻烦。本次让**开发者同样可勾选记住密码**；同时为避免
> "这台机器同时记得 admin 和 dev 两个免密账号、登录框随机回填一个"的跨角色残留，
> 规定**管理员与开发者记住记录互斥**：登录任一角色成功，都会把另一角色的记住文件清掉，
> 机器上只保留"最近一次登录的那个角色"的记忆。

### 改动范围
- **`Utils/SecurityUtil.cs`**：记住密码方法全部加 `bool isDev` 角色参数，按角色分文件：
  - `Save/Load/ClearRememberedLogin(bool isDev, …)`；`isDev=false` → `remembered_login.dat`
    （保留旧文件名，兼容升级前已记住的管理员记录）；`isDev=true` → `remembered_login_dev.dat`。
  - 类注释补充双账号互斥说明（互斥清除由调用方 LoginForm 实现）。
- **`Views/LoginForm.cs`**：
  - 构造回填：先看管理员记住记录（匹配 AdminUser），否则看开发者记录（DevEnabled 且匹配
    DevUser），两侧都不匹配才保留默认 admin —— 开发者记住后打开登录框会回填 dev 账号密码；
  - `BtnLogin_Click`：admin 登录成功 → 勾选存/未勾选清**管理员**文件，并**顺带清开发者文件**；
    dev 登录成功 → 勾选存/未勾选清**开发者**文件，并**顺带清管理员文件**（两处都是先处理
    本角色记录、再删对方记录，保证"本角色记忆"与"对方清除"在同一登录动作内完成）；
  - `BtnSavePwd_Click`（改密码，管理员操作）：同步管理员记住文件的同时也清开发者文件。
  - 类注释与代码注释同步双账号记住逻辑。
- **`Views/LoginForm.Designer.cs`**：`chkRemember` 注释同步（不再写死"仅管理员"）。
- **文档同步**：`AGENTS.md`（记住密码约定）、`README.md`（可配置项与登录说明）、
  `docs/使用说明.md`（§四 记住密码）、`CHANGELOG.md`。

### 为什么这么改
- 开发者遍历功能测试页是高频操作，记住密码可省去重复输入；但若 admin/dev 记忆文件共存，
  登录框回填逻辑必须二选一，容易造成"明明想登 admin 却回填了 dev 密码"的困惑。
  互斥清除让行为可预期：**最近登谁、就记住谁**，与"登录框回填的用户名"永远一致。
- 改密码走的是管理员身份验证，视作管理员操作，同样清开发者记忆（防止改完密码后
  残留开发者免密入口）。

### 验证
- Debug 构建通过；冒烟启动进程存活。
- 用 csc 编译临时 harness 直测 `SecurityUtil` 三个场景：① admin 记住 → admin 文件存在、
  dev 文件不存在；② dev 登录 → admin 文件被清、dev 文件写入且密码可解密回填；③ 取消勾选
  dev → dev 文件被清。全部符合预期。

## V1.12.20（2026-08-13）功能测试窗体新增相机程序切换/读取按钮（联调验证用）

> 基恩士现场配置了多个相机程序（P000/P001/P002…正在调试），下午工程师撤离前要快速验证
> "上位机切程序"链路是否通。在功能测试窗体（开发者）相机区新增三个按钮：
> **读当前程序号（PR）**、**切换程序→P001（PW,001）**、**切换程序→P002（PW,002）**，
> 直接复用 `KeyenceIV4Camera.SwitchProgram/ReadProgramNo`（已存在，V1.12.18 实现）。

### 改动范围
- **`Views/DevTestForm.Designer.cs`**：相机测试区新增一行（读程序号按钮 + 当前程序号标签 +
  两个切程序按钮），grpCamera 加高至 212，下方 grpScanner/grpPlc/grpLog 相应下移 42、
  窗体高度 890→932；文件头注释同步新行布局。
- **`Views/DevTestForm.cs`**：
  - `BtnReadProgramNo_Click`：PR 读当前程序号，显示 `P000/P001/P002…`（读回失败显示红字）；
  - `BtnSwProg1_Click` / `BtnSwProg2_Click` → 统一走 `SwitchCameraProgram(no, display)`：
    后台线程发 PW 切换，成功后**顺带 PR 读回确认**（读回超时不影响主结果，日志提示可再
    手动读）；失败（未连接/相机回 ER）显示红字；
  - `SetBusy` 纳入新按钮禁用（防连点并发）。
- **文档同步**：`docs/使用说明.md`（§六 功能测试相机条目补切程序说明）、`CHANGELOG.md`。

### 为什么这么改
- 前期只需验证"切程序指令发过去相机有响应、程序号真变"，先不触发拍照（要连拍验证就
  先切程序再点 T2）。读回确认比只看 PW 回显更可信（相机侧程序号/主控模式切换可能异步生效）。

### 验证
- Debug 构建通过；冒烟启动进程存活、无崩溃。
- 事件接线与既有 T1/T2 按钮同构（SelectedCamera 取下拉选中相机、Task.Run 后台 + SafeInvoke），
  复用已验证的通讯层方法（SwitchProgram/ReadProgramNo）。

## V1.12.19（2026-08-13）序列号点击直录，不再弹窗

> 上版（V1.12.17）手动输入序列号要"双击标题栏序列号框 → 弹录入对话框"，现场反馈多一步
> 弹窗点击累赘。本次把序列号显示框从只读 Label 升级为**可点击即编辑的 TextBox**（外观不变：
> 白底 + 单线边框 + 同字号），鼠标点击直接出现输入光标，输入后回车/失焦提交、Esc 还原，
> 删掉不再使用的 `SerialInputForm` 弹窗。与扫码枪收码等效（`SetManualSerial` 可推进"等 SN"阶段）。

### 改动范围
- **`Views/MainForm.Designer.cs`**：`lblSerial`（Label）→ `txtSerial`（TextBox）；保留原视觉
  （固定宽度 `AutoSize=false`、`FixedSingle` 单线边框、微软雅黑 11 Bold、深蓝灰字、白底）；
  头部布局图同步换文案。
- **`Views/MainForm.cs`**：
  - 删除 `PromptManualSerial` / `SerialInputForm` 弹窗路径，改为 `SetupSerialEditor` 一次性接线：
    `KeyUp`（Enter 提交 / Esc 还原）+ `Leave`（失焦非空提交）；
  - 新增 `CommitSerialEdit`（trim 非空才 `SetManualSerial`，空输入还原显示防误清空）与
    `RestoreSerialDisplay`（还原为协调器当前 SN）；
  - `RelayoutTitleBar` 排布数组/宽度计算改用 `txtSerial`（TextBox 走"固定宽度"分支，不 cast Label）；
  - `InitTitleBarFields`/`ApplyConfigVisibility`/`OnSerialScanned` 的显示与显隐同步改 `txtSerial`。
- **`Views/SerialInputForm.cs`（删除）**：弹窗不再使用，源码与 csproj `<Compile>` 登记一并移除。
- **文档同步**：`docs/使用说明.md`（§3.2 手动输入改"点击直录 + Enter/失焦/Esc 交互"、主界面速览图），
  `CHANGELOG.md`。

### 为什么这么改
- 现场是流水线节拍环境，弹窗"弹出→输入→点确定→关窗"四步操作在手动补录频繁时非常拖节奏；
  框内直录点一下就能打字，回车即生效，操作员心智负担最低。
- 保留"空输入不提交 / Esc 还原"的兜底：扫码收到的 SN 不应被一次误编辑或空串清掉。

### 验证
- Debug 构建通过（无 error）；冒烟启动进程存活、无崩溃。
- 交互逻辑走查：Enter 非空→`SetManualSerial`；Esc→还原；失焦非空→提交、空→还原；扫码
  `OnSerialScanned` 直接覆盖 `txtSerial.Text`（扫码优先级最高）；`SetupSerialEditor` 仅构造时
  订阅一次（热更不重建该控件，无重复订阅）。

## V1.12.18（2026-08-13）相机单 FTP 目录混图方案 + 点位程序号切换（PW）+ 双文件归档

> 现场新方案：**一台相机 = 一个 FTP 服务器，所有点位拍的图混放在同一目录**（文件名为固定
> `0000.jpeg`+`0000.iv4p`，由基恩士工程师在相机软件里配置推图、上位机零配置）。上位机不再按
> "相机→目录"区分点位，改为：**FTP 目录只当中转暂存区**——监听新图 → 按扩展名配对双文件 →
> 复制归档到正式存图目录 → **删除 FTP 源文件**（处理即删，防同点位重复触发新旧图混淆）。
> 同时**每个点位对应相机的一个程序**：触发前先 `PW` 切程序再 `T2` 触发，多点位靠程序号区分。

### 改动范围
- **`Models/AppConfig.cs`**：
  - `CameraConfig` 新增 **`ProgramNo`**（触发前 PW 切换的相机程序号，默认 `-1`=不切换）与
    **`OutputFormat`**（判定输出格式 `OF` 指令，默认 `"00"` 标准）；
  - `ImageConfig` 新增 **`FileTimestampSuffix`**（存图文件名追加时间戳后缀防同点位重复触发覆盖，
    默认 `true`）；
  - `PlcConfig` 新增 **`PointInfoAddress`**（PLC 到位时携带的点位号寄存器，占位 D113，
    **TODO 待现场 PLC 程序定稿**，定稿后据点位号切程序）。
- **`Services/KeyenceIV4Camera.cs`**：新增 **`SwitchProgram(int)`**（`PW,nnn[CR]`，nnn=程序号
  3 位补零，响应 `PW[CR]` 成功 / `ER,PW,03|22` 失败）、**`ReadProgramNo()`**（`PR[CR]` →
  `PR,nnn[CR]`）、**`SetOutputFormat(string)`**（`OF,nn[CR]`）；类注释指令表同步更新。
- **`Services/ImageStore.cs`**：新增 **`SaveImageFilePair(jpegPath, iv4pPath, stationNo, isOk,
  serial)`**——双格式原样复制归档（jpeg 为显示/归档主体、iv4p 为基恩士私有复盘格式原样保留），
  复用模板渲染/目录层级，按 `FileTimestampSuffix` 追加 `_yyyyMMdd_HHmmss_fff` 时间戳；
  配套 **`CopyWithRetry`**（`FileShare.ReadWrite` + 失败短重试，容忍 FTP 事件先于写完到达）。
- **`Services/ProductionCoordinator.cs`**（核心）：
  - `PendingCamera` 改为双文件快照 `FtpJpegPath`/`FtpIvpPath`（替代原 `FtpPath`）；
  - `OnFtpFileArrived` 按扩展名分派配对，**两个文件都到齐才算 `IsSnapped`**；
  - `FinishAll` 归档改走 `SaveImageFilePair`，成功后 `DeleteFtpSource` 删除 FTP 源目录里的
    `0000.jpeg`+`0000.iv4p` 源文件；
  - `TriggerOneCamera` 触发前先 `SetOutputFormat` + `SwitchProgram`（`ProgramNo>0` 时），
    切失败即中止该相机并记取像失败（防止用错程序拍无意义图）；
  - 删除已无引用的旧 `ArchiveImage` 方法（FtpPath 已不存在，双文件归档由 `SaveImageFilePair` 承担）。
- **`Views/SettingsForm.cs`**：相机表格新增**"程序号"列**（读/写 `ProgramNo`）；
  取图方式下拉**移除 `Tcp` 选项**（现场只保留 Ftp，Tcp/BR 代码留作旧配置兼容）。
- **文档同步**：`docs/通讯接入.md`（§2.2 新增 PW/PR/OF 指令条目、§2.2 FTP 双文件约定与处理即删、
  §2.2b 速查表加 `programNo`/`outputFormat`、§3.2 寄存表加 D113 点位信息占位、§3.3 时序、
  §3.4 点位→程序号联动 TODO、版本表 V1.12.18）、`README.md`、`CHANGELOG.md`。

### 为什么这么改
- 现场方案从"一台相机拍固定点位"升级为"一台相机负责多个点位、图全部混放"：文件名恒定且会
  被相机覆盖，因此**必须"处理即删"**（先归档到正式目录再删源），否则同点位第二次触发的新图
  会被误当旧图重复归档；iv4p 是基恩士复盘格式，现场要留底，故双文件一并原样归档。
- 点位区分靠"程序号"：不同点位在相机里是不同程序（不同视觉工具/参数），触发前 `PW` 切到
  对应程序，保证判定与图像属于正确点位；`OF` 输出格式与 T2 判定解析解耦，现场调试用详细格式、
  程序解析只认标准格式。

### 验证
- Debug 构建通过（无 error）；harness 验证 `SaveImageFilePair`：双格式落盘、iv4p 内容原样、
  时间戳防重名、iv4p 缺失不崩、jpeg 源缺失返回 null（7 项断言全过）；
  冒烟启动进程存活、无崩溃。
- **代码-文档核对（已固化到 docs/通讯接入.md §2.1a，排障必读）**：触发顺序 OF→PW→T2 固定、
  `ProgramNo>=0` 都切换（0 合法、-1 才不切）、`OutputFormat` 必须恰好 2 位数字否则触发直接失败、
  `SwitchProgram` 越界自动夹 0~127、双文件到达顺序不保证但必须齐才算到位、归档成功后才删 FTP 源、
  归档失败不删源文件回退显示、`SaveImageFilePair` 只复制不删除（删除归协调器）。

## V1.12.17（2026-08-13）手动输入序列号 + FTP 取图描述更正 + 文档中心整理

> ① 现场无扫码枪 / 扫码枪没读到码时，操作员此前无法手动录入产品 SN（代码里只留了注释、
> 没有实际入口）；本次实现"双击标题栏序列号框 → 弹框手动录入"。② FTP 取图此前文档误写成
> "上位机须部署 FTP 服务器"，**现场与基恩士工程师确认：FTP 推图由基恩士工程师在相机软件
> （IV Navigator）里全部配置好，上位机零配置、无需安装任何 FTP 服务器（FileZilla 等不用）**。
> ③ 调试近尾声，docs 从 4 篇收敛为 4 篇高质量文档（使用说明 / 通讯接入 / IP清单 / 技术范式）。

### 改动范围
- **`Services/ProductionCoordinator.cs`**：新增 **`SetManualSerial(string code)`**——手动输入 SN
  与扫码枪收码等效：① 更新 `LatestSerialNumber`（标题栏 + 存图 {SN} 目录）；② 置
  `_serialReceived=true`，若正处于"等 SN"阶段（PhaseScanPending）下一轮轮询即推进到等相机阶段；
  其它阶段该标志会在下次扫码到位时被重置，无副作用。
- **`Views/SerialInputForm.cs`（新增）**：手动输入序列号对话框（纯代码构造，无 Designer）。
- **`Views/MainForm.cs`**：序列号框 `lblSerial.MouseDoubleClick` → `PromptManualSerial()` 弹框，
  确定后调 `SetManualSerial` 并刷新标题栏；类注释补手动输入说明。
- **`CommandCenter.csproj`**：登记新窗体 `SerialInputForm.cs`。
- **`docs/通讯接入.md`**：① 相机 2.2b 新增"相机联调配置字段速查表"（原 `docs/联调清单.md` 精华）；
  ② 2.2/2.3 FTP 描述更正为"上位机零配置，FTP 由基恩士工程师在相机软件里配置、无需另装 FTP 服务器"。
- **`docs/使用说明.md`（新增）**：用户操作手册（启动/主界面/日常操作/账号/系统设置/功能测试/排查表）；
  手动输入 SN 操作与 FTP 取图说明同步更正。
- **`docs/联调清单.md`（删除）**：前期验证类记录，精华已并入通讯接入.md。
- **`docs/现场设备IP清单.md` / `README.md`**：FTP 描述更正为"上位机零配置、基恩士侧全配置"。
- **`AGENTS.md`**：关键文件导航补齐 docs 四件套；文档同步铁律新增 `docs/使用说明.md`。

### 为什么这么改
- 手动输入 SN：产线节拍下产品未贴码/扫码枪漏读时，操作员需要一条手动补录通道，否则 SN 沿旧值、
  存图目录归档错乱；双击序列号框最直观（与窗口双击放大交互一致）。
- FTP 描述更正：现场与基恩士工程师确认 FTP 推图由相机软件（IV Navigator）全配置、上位机零配置，
  此前"装 FileZilla"的表述误导维护。

### 验证
- Debug 构建通过（无 error）；harness 验证：`SetManualSerial` 置 SN 且 `_serialReceived=true`、
  `SerialInputForm` 确定取到输入值 / 取消返回 null；冒烟启动进程存活、无崩溃。
## V1.12.16（2026-08-12）打通"两阶段"业务流程：先扫码得 SN、再相机拍照 + 寄存地址占位

> 与现场核对完整产线节奏后的流程实现：**机器人带扫码枪到位 → 上位机扫码得 SN → 机器人带相机到位
> → 上位机触发拍照 → 取像保存并显示在对应点位窗口 → 通知 PLC 完成 → PLC 走下一工位**。
> 在保住原有"相机到位→拍照→等图→上报"闭环不变的前提下，把流程串成"先扫码、后拍照"两阶段。
> PLC 通讯寄存地址尚未定稿，全部沿用现代码占位（新增"扫码枪到位信号"用 D99 占位、已代码接入），
> 现场地址定了只改 json 数值即可。

### 改动范围
- **`Models/AppConfig.cs`**（`PlcConfig`）：新增 **`ScanMoveDoneAddress`（PLC→上位机"扫码枪运动到位"
  信号，占位 D99）**，注释标明"占位待定稿、现场定稿后只改此值"。
- **`Services/PlcService.cs`**：新增 **`ReadScanMoveDone()` / `ClearScanMoveDone()`**，读写自己
  DataStore 的扫码到位寄存器（与现有 ReadMoveDone/ClearMoveDone 同风格、同锁，从站模式无外部请求）。
- **`Services/ProductionCoordinator.cs`**（核心，两阶段状态机）：
  - 新增 `AttachScanners(IEnumerable<IScanner>)` 注入扫码枪、`HookScannerEvents/UnhookScannerEvents`
    订阅退订 `SerialNumberScanned`（置"SN 已到"标志，不重复维护文本）、阶段常量与 `_phase` 状态；
  - `PositionTimer_Tick` 改为按 `_phase` 分发 **①等"扫码到位"(D99)→ 复位+SendTrigger 触发扫码 →
    ②等 SN（`_serialReceived` 推进；超时 `ScanWaitMs`=30s 兜底不卡流程）→ ③等"相机到位"(D100)→
    并行触发拍照（原逻辑原样保留）**；无扫码枪时扫码到位即视为通过、直接等相机；
  - `FinishAll` 收尾后 `_phase` 复位回"等扫码到位"、状态文案同步改为"等待 PLC 扫码枪到位信号"；
  - `Dispose` 退订扫码枪事件（防热更/关闭悬挂）。
- **`Views/MainForm.cs`**：`BuildServices` 建完扫码枪后调用 `_coordinator.AttachScanners(_scanners)`。
- **文档同步**：`docs/通讯接入.md`（§3.2 寄存表加扫码到位、§3.3 完整时序、§3.4 实现说明、版本表）、
  `README.md`、`CHANGELOG.md` 更新。

### 为什么这么改
- 现场真实节奏是"扫码枪与相机分两个机构、分两段到位"，不是单一"相机到位"信号；先拿到 SN 再
  拍照，存图目录才能按 SN 归档、判定结果才能与产品一一对应。
- 地址未定稿，故扫码到位用占位 D99 接入、其余沿用占位，只留"明天定地址改 json 数值"一个动作，
  避免地址定了再改代码判断逻辑。

### 验证
- Debug 构建通过（`CommandCenter.exe` 正常生成、无 error）；冒烟启动进程存活、无崩溃。

## V1.12.15（2026-08-12）PLC 状态文案对齐从站语义 + 显示窗口双击放大/还原

> 两个现场体验优化：
> ① 从站模式（V1.12.11 起 PLC 做主站）下，主界面左下角状态栏仍显示旧的"等待 PLC 到位信号"，
>    与右上角三态灯（主站已连入/监听就绪/监听失败）语义不一致，改为"等待 PLC 主站到位信号"；
> ② 客户想看某一路相机的画面细节，此前只能看小格窗。新增"鼠标左键双击任一显示窗口 → 全屏放大，
>    再次双击 / 按 Esc → 还原"，放大期间画面仍随检测实时刷新。

### 改动范围
- **`Services/ProductionCoordinator.cs`**：状态文本"等待 PLC 到位信号"→"等待 PLC 主站到位信号"
  （4 处：Start / Resume / 到位异常 / 收尾复位），对齐从站模式下"到位信号由 PLC 主站写入 D100"。
- **`Controls/CameraDisplayControl.cs`**：新增 `WindowDoubleClicked` 事件 + `HandleDoubleClick`，
  **直接订阅图像子控件（PictureBox / 编号 Label）的 `MouseDoubleClick`**（左键双击，UI 线程）。
  【根因修正·两轮血泪】首版重写 `OnDoubleClick`：`Control.DoubleClick` 事件不支持冒泡，双击落在
  占满整窗的 PictureBox 上时不触发 → 没反应；第二版改为订阅本 UserControl 的 `MouseDoubleClick`
  （依赖冒泡），实测部分环境冒泡仍不稳定，依旧没生效。最终改为**直接订阅 PictureBox 的
  MouseDoubleClick**——因为 PictureBox 用 Dock=Fill 占满整窗、双击必落其上，完全不依赖冒泡、
  **必然命中**（harness 对 PictureBox 注入双击确认为全屏）。
- **`Views/MainForm.cs`**：
  - 窗口矩阵每格订阅 `WindowDoubleClicked` → `OnWindowDoubleClicked`；
  - 新增 `EnterFullScreenWindow`/`RestoreFullScreenWindow`：用无边框、置顶、覆盖整屏（含任务栏）
    的独立 Form 承载该窗口（同一控件实例，Dock=Fill 铺满），**移动控件而非复制图片**，保证全屏时
    主流程 `SetImage` 刷新照常生效；进入前记录原单元格（TableLayoutPanelCellPosition），还原时放回原位；
  - 双击另一窗口时先还原当前全屏、再放大新窗口（双击放大/再双击还原 + 直接切换）；
  - Esc 兜底还原（无边框窗体无关闭按钮）；`BuildWindowGrid`/`FormClosing` 幂等收尾全屏窗体
    （防热更/关窗时孤儿顶级窗体残留导致无法退出）；
  - 状态栏注释（"等待PLC主站到位"、双击放大说明）同步。
- **`CHANGELOG.md`**：本小节记录。

### 为什么这么改
- 状态栏文案与三态灯语义一致，现场一看即知"上位机是从站、等 PLC 主站发到位信号"；
- 全屏用独立 TopLevel 窗体 + 移动控件实例：布局最简单（不碰主窗体 Dock 布局），并让全屏画面
  跟随检测实时刷新（复制图片的方案会停住）。

### 验证
- Debug 构建通过，冒烟启动进程存活、无崩溃。

> 现场联调：功能测试页写 D101，汇川主站直接读地址 101 即见，与 NModbus 从站
> `PointSource.ReadPoints/WritePoints(start)` 的 0-based 起始地址天然对齐，**无需任何换算**。
> 关闭 V1.12.13 遗留的"待现场确认"项（D 地址↔协议地址 0/1-based 偏移）。

### 改动范围
- **`docs/通讯接入.md`**：PLC 章节 3.1 补充"地址一一对应、零偏移"的实测结论，
  明确"写 D101 读 101 即见，不要做 +40001/±1 换算"；寄存器表说明同步更新。
- **`CHANGELOG.md`**：本小节记录实测结论。
- 代码无改动：`PlcService.ReadLocal/WriteLocal` 现状（D 地址直接作 start）即正确，
  符合实测结果，无需调整。

### 为什么这么改
- V1.12.13 审查时留了悬念：若汇川把 D100 映射到协议 40100（start=99）则全表错位一位，
  需要统一在 `ReadLocal/WriteLocal` 加偏移。现场实测证明无偏移，结论关闭，不必加任何补偿。

### 现场实测细节
- 上位机从站监听 502，UnitId=1，绑定 0.0.0.0（所有网卡）；
- PLC 主站连入上位机 IP:502 后，读地址 101 读到上位机写入 D101 的值，一一对应。

## V1.12.13（2026-08-12）从站通讯逻辑审查修复：配方/通用读写状态如实化

> 上一版修好"监听起不来"后，全面审查了 PLC 通讯逻辑（含并发安全、DataStore 容量、
> 寄存器偏移）。确认核心无隐患（DataStore 默认覆盖 0~65535、业务 `_lock` + NModbus
> `PointSource._syncRoot` 双层并发保护、配方先写号再置标志位无中间态），但发现三处
> "界面/测试信息失真"问题并修复——从站模式写本地 DataStore 恒成功，导致旧的返回值
> 语义（true=已下发给 PLC）失效，界面会误报成功。

### 改动范围
- **`Services/PlcService.cs`**：
  - `WriteRecipe` 返回真实状态：DataStore 未就绪→false（真没写进去）；写入成功但
    `HasMasterConnected=false`（主站未连入）→false，界面如实提示"已缓存待拉取"；
  - `ReadRegister`/`WriteRegister`：DataStore 未就绪时返回 false（此前恒 true，功能测试
    在从站没起来时也会误报"成功/读到 0"）；
  - `WriteRecipe` 的 RecipeLen 按 1~20 截断（防异常配置分配超大数组+写越界被静默吞）。
- **`Views/MainForm.cs`**：`SwitchRecipe` 失败分支改黄色提示"配方已缓存，PLC 主站未连入
  （连入后自动拉取）"，不再误报红色"下发 PLC 失败"。
- **`Views/DevTestForm.cs`**：配方下发日志文案同步区分"已写入且可拉取 / 已缓存待主站拉取"。

### 为什么这么改
- 从站模式下"写本地寄存器区"永远成功，旧 bool 返回值误导操作员（以为配方已切到 PLC）；
  结合 `HasMasterConnected` 让状态与 PLC 三态灯语义一致（黄=等待主站）。

### 审查确认无问题（不修）
- DataStore 默认容量 0~65535，D100~D112 不会越界；
- 业务 `_lock` 与 NModbus `PointSource._syncRoot` 双层锁：业务侧、PLC 网络线程并发读写安全；
- 从站监听启动/重建/Dispose 清理链完整，有 `_disposed` + 限时抢锁兜底。

### 待现场确认
- **D 地址 ↔ 协议地址 0/1-based 偏移**：当前 `D100→start=100→协议 40101`；若汇川映射为
  `D100→40100`（start=99）全表错位一位，联调首件事验证 D100 写读对齐。
- 联调注意：DevTestForm 与主流程共享同一 PlcService 与同一握手寄存器区（D100~D112），
  测试页操作握手区会与后台 Coordinator 轮询争抢（清 D100 可能吞真实到位信号），
  业务流程运行时不要在测试页操作 D100~D112。

## V1.12.12（2026-08-12）修复从站监听启动失败 + PLC 灯三态显示主站连入状态

> 现场反馈：从站模式（V1.12.11）下界面一直获取不到 PLC 连接信息，PLC 灯恒红。
> 排查定位两处同类 bug：`new ModbusSlave(unitId, dataStore, null)` 与
> `new ModbusTcpSlaveNetwork(listener, factory, null)` 的 `handlers`/`logger` 参数
> 在 NModbus 3.0.83 中要求非 null，传 null 抛 `ArgumentNullException`，导致**从站监听从未
> 启动成功**（日志"PLC 从站监听启动失败 0.0.0.0:502，原因：值不能为 null。参数名: handlers"）。
> 与网络无关（ping PLC 通只代表网络层通）；监听没起来，PLC 主站自然连不进来。
>
> 修复后顺手补齐从站模式的"主站连入"信息：从站不能主动连 PLC，`IsConnected` 只表示监听
> 就绪，无法看出主站是否真的在通讯。利用 NModbus 从站网络的 `Masters`（已连入的 TCP 主站
> 列表）做 1s 轮询，把 PLC 灯升级为三态，界面可直接看出"主站是否已连入"。

### 改动范围
- **`Services/PlcService.cs`**：
  - 修复：`_slave`/`_network` 改用 `factory.CreateSlave(unitId, dataStore)` /
    `factory.CreateSlaveNetwork(listener)` 创建——工厂内部自动挂载默认功能服务（03/06/10/15/16）
    与非 null logger，不再手写 `new ...(…, null)`；
  - 新增：`HasMasterConnected` 属性 + `MasterConnectionChanged` 事件，后台 1s 轮询
    `_network.Masters.Count > 0` 做边沿检测（`MasterPollTick`），监听启动后启用、重建/Dispose 停止。
- **`Views/MainForm.cs`**：PLC 灯改三态（红=监听失败 / 黄=监听就绪等待主站 / 绿=主站已连入），
  悬停 ToolTip 说明当前状态含义与排查方向（端口占用/防火墙/PLC 主站程序与指向）；
  `UpdatePlcStatus` 统一刷新（订阅 ConnectionChanged + MasterConnectionChanged）。
- **`Views/DevTestForm.cs`**：PLC 状态区同步三态文案（主站已连入 / 监听就绪等待主站 / 监听失败），
  订阅 MasterConnectionChanged 实时刷新。

### 为什么这么改
- 直接 new NModbus 从站对象时对"不可空参数传 null"是本库常见坑，改用工厂方法是最简正确姿势；
- 从站模式下"监听就绪"与"主站连入"是两回事，三态灯把两者分开，联调时能一眼定位问题在
  上位机侧（红/黄）还是 PLC 侧（黄且 PLC 不连）。

### 待现场联调确认
- 监听 502 需 Windows 防火墙放行入站；若 PLC 主站连入后灯仍黄，检查 PLC 主站程序与指向
  （应指向本机 IP:502、UnitId=1）。

## V1.12.11（2026-08-12）PLC 通讯角色反转：上位机做 Modbus TCP 从站

> 现场确认：汇川 PLC 做 Modbus TCP 主站，上位机做从站。原方案上位机做主站主动
> ReadHoldingRegisters/WriteSingleRegister 读写 PLC 寄存器，现全部反转为上位机监听本机 502、
> 等主站连入并读写上位机自己的 SlaveDataStore 寄存器区。因 PlcService 保留全部对外方法签名，
> 调用方(Coordinator/MainForm/DevTestForm/ConnectionMonitor)代码零改动，仅语义从"连上 PLC"
> 变为"从站监听已就绪"。配方下发因从站不能主动发消息，改为 D108 标志位握手中转
> （上位机写自己区+PLC 轮询拉取+写 0 回执）。

### 改动范围
- **`Services/PlcService.cs`**（重写）：从 `TcpClient + CreateMaster` 改为 `ModbusTcpSlaveNetwork +
  ModbusSlave + SlaveDataStore` 从站；监听在后台线程承载 `ListenAsync(CancellationToken)`，
  停止靠 `Cancel + listener.Stop()`；DataStore 读写用 `HoldingRegisters.ReadPoints/WritePoints`
  （0-based，与原 ReadHoldingRegisters 一致）。对外方法签名全部保留，底层改读写自己 DataStore。
- **`Models/AppConfig.cs`**：`PlcConfig` IpAddress 语义改为"监听绑定 IP"（默认 0.0.0.0），新增
  `RecipeFlagAddress`=D108 配方握手标志位，寄存器注释更新方向反转。
- **`Services/ProductionCoordinator.cs`/`ConnectionMonitor.cs`**：仅注释更新（角色反转说明），代码零改动。
- **`Views/SettingsForm.Designer.cs`**：PLC IP/端口 ToolTip 文案改为"从站监听绑定 IP/监听端口"。
- **`Views/DevTestForm.cs`**：PLC 操作区注释更新（改为读写上位机自己 DataStore）。
- **`docs/通讯接入.md`/`docs/现场设备IP清单.md`/`README.md`/`AGENTS.md`**：PLC 角色与寄存器约定同步。

### 为什么这么改
- 现场实际架构就是 PLC 主站/上位机从站，原代码按"上位机主站"实现与现场不符；
- 保留对外签名零改动调用方，把架构反转的风险锁在 PlcService 内部，便于回归验证。

### 待现场联调确认
- NModbus DataStore 地址偏移（当前按 0-based 与原主站一致；若 PLC 侧偏移不同，统一在
  `PlcService.ReadLocal/WriteLocal` 调整，业务层无感）。
- 上位机监听 502 需 Windows 防火墙放行入站；若 502 被占用，改 `PlcConfig.Port`。
- 配方"型号→配方号"映射待现场约定后填配置（当前预留占位）。

## V1.12.10（2026-08-12）现场资料更正：汇川 PLC 为主站

> 现场确认：汇川 PLC 在系统中是**主站**角色（此前文档/界面文案按"从站"描述有误）。
> 本次把文档与设置界面提示文案中的 PLC 角色更正为"主站"，并新增 `docs/现场设备IP清单.md`
> （整理现场设备 IP：上位机 19.87.6.230 / PLC 19.87.6.1 / 相机1 19.87.6.212 / 相机2 19.87.6.213 /
> 扫码枪 19.87.6.100:9004 触发指令 LON）。

### 改动范围
- **`docs/现场设备IP清单.md`**（新增）：现场设备 IP 速查文档——网络拓扑图 + 设备清单 +
  扫码枪触发指令细节（以代码为准：`LON\r\n`，十六进制 `4C 4F 4E 0D 0A`，现场验证 OK）+
  appconfig.json 字段对应 + 核对注意事项。
- **`docs/通讯接入.md`**：顶部总览表 PLC 描述"Modbus TCP（从站）"→"Modbus TCP（现场为主站）"。
- **`Views/SettingsForm.Designer.cs`**：PLC IP 输入框悬停提示文案"汇川，Modbus TCP 从站"→"汇川，现场为主站"。

### 为什么这么改
- PLC 主站角色以现场确认为准，文档与界面文案保持一致，避免后续联调/核对时因"从站"表述产生误导。

### 优化点
- 现场设备 IP 信息集中成独立文档，后续核对/交接直接看 `docs/现场设备IP清单.md`。

## V1.12.9（2026-08-12）设置窗体扫码枪默认启用，与代码默认接入的扫码枪一致

> 现场反馈：系统设置页面的扫码枪列表默认模板行"启用"都是未勾选状态，而主程序代码默认实际
> 接入的是现场实测的以太网无协议扫码枪（`19.87.6.100:9004`，触发指令 `LON`，`ScannerTcpService`），
> 两者不一致——现场打开设置看到默认没勾启用，容易误以为要手动加枪/勾选才生效。本次把设置界面
> 的默认行为对齐代码：**TCP 表模板行与"添加一台"默认勾选"启用"，串口表模板行保持不勾选**
> （代码默认不用串口枪，要接入再勾），并在界面上直接体现出来。

### 改动范围
- **`Views/SettingsForm.cs`**：
  - `LoadScannerRows()`：TCP 表默认模板行"启用"由 `false` 改 `true`（串口表保持 `false`），
    并更新方法注释说明"默认启用 = 代码默认接入的那把以太网扫码枪"；
  - `WireButtonEvents()`："添加一台（TCP 扫码枪）"追加的默认行同步改 `true`，新加的枪默认启用；
  - `OnSave()`：两张表都删空时的兜底条目由 `new ScanConfig()`（Mode=Serial、未启用）改为
    TCP 现场默认枪且 `Enabled=true`，与界面模板行展示一致，避免"删空保存再打开"出现界面与配置不符。
- **`README.md`**：扫码枪可配置项说明补充"TCP 表默认行默认勾选启用"。

### 为什么这么改
- 设置界面是现场配枪的入口，默认展示应与程序实际默认行为一致，减少"明明默认就该用、
  界面却没勾选"的认知偏差；
- 只把 TCP（以太网）枪设为默认启用：代码/现场默认接入的就是这把枪（`MainForm.BuildScanner`
  对 `Mode=Tcp` 建 `ScannerTcpService`，连上即发 `LON` 自动收码）；串口枪不是默认设备，不勾选，
  需要时再手动勾。

### 优化点
- 新增 TCP 扫码枪时默认勾选启用，现场"加一把枪直接用"的路径更顺；
- 删空兜底与界面模板对齐，任何路径下界面显示与保存落盘的配置都一致。

## V1.12.8（2026-08-12）设置窗体扫码枪列表拆表 + 默认值 + 滚动条

> 现场反馈系统设置页面的扫码枪列表有 bug：同一张 DataGridView 里第一行选 TCP、第二行选 Serial
> 后表格变得异常——根因是 DataGridView 列可见性是**整列**属性，无法逐行显隐，混用时只能全显
> 所有列导致视觉混乱。本次将扫码枪列表拆为 TCP 表 + 串口表两张独立表格，各行用首列"启用"勾选
> 控制接入，方式由"所在的表"决定，彻底消除列显隐切换问题。同时给两张表各填一行默认值
> （TCP=现场实测 19.87.6.100:9004/LON，串口=COM3/115200/1/None），打开设置即可看到模板行直接改。
> 另外因两张表加上原有内容超出窗体高度，新增右侧竖滚动条，保存/取消按钮固定底部不随滚动。

### 改动范围
- **`Views/SettingsForm.Designer.cs`**（重写设计器）：
  - 删除旧的单个 `gridScanners` / `btnAddScanner` / `btnDelScanner` / `lblScanners`；
  - 新增 `gridScannersTcp`（4 列：启用/IP/端口/触发指令）+ `gridScannersSerial`（5 列：启用/串口名/波特率/停止位/校验位），
    各带独立的"添加一台"/"删除选中"按钮与加粗标题（"扫码枪列表(TCP):" / "扫码枪列表(串口):"）；
  - 新增 `pnlScroll`（Panel, AutoScroll=true, Dock=Fill）包裹所有配置控件——超出可视高度自动出竖滚动条；
  - 新增 `pnlBottom`（Panel, Dock=Bottom, 浅灰背景）固定放"保存"/"取消"按钮，不随内容滚动；
  - 窗体 ClientSize 由 720×790 调整为 740×700，适配滚动布局。
- **`Views/SettingsForm.cs`**（上一 Agent 已改好，本次仅更新注释）：
  - `SetupScannerGridColumns()` 给两张表分别建列；`LoadScannerRows()` 按配置 Mode 分流填表，
    空表各补一行默认值；`OnSave()` 合并两张表为一个 `Scanners` 列表（TCP→Mode="Tcp"，串口→Mode="Serial"）；
  - 类注释 ASCII 布局图同步为两张表 + 滚动条 + 固定底栏。
- **`README.md`**：扫码枪可配置项说明同步为"TCP 表 + 串口表拆分"。
- **`docs/通讯接入.md`**：扫码枪章节同步拆表说明与版本号。

### 为什么这么改
- DataGridView 的列可见性是整列范围的，无法"第一行只显示 TCP 列、第二行只显示串口列"，
  混用状态下全列显示既混乱又容易填错参数。拆成两张表后每张表的列固定、语义清晰，用户不会再混淆；
- 默认值让现场打开设置就能看到一行 TCP 扫码枪模板（IP/端口/触发指令已填好），直接改即可，
  不用从空白行开始逐个填；
- 滚动条解决"两张表 + 原有内容总高超出窗体"的问题，保存/取消固定底部始终可见可点。

### 优化点
- 两张表各自独立的"添加/删除"按钮，操作范围明确不会误删另一协议的行；
- 滚动面板与底部按钮栏分离（Dock=Fill + Dock=Bottom），样式与整体一致、不突兀；
- ToolTip 气泡覆盖新增的 6 个控件（2 标题 + 4 按钮），现场悬停即知用途。

## V1.12.7（2026-08-12）主界面标题改名"上位机控制中心"

> 现场客户对主窗体标题命名有要求。原标题"CommandCenter - 相机/PLC 命令中心"改为"上位机控制中心"，
> 更贴合本软件"现场上位机统一控制"的定位。

### 改动范围
- **`Views/MainForm.Designer.cs`**：主窗体标题 `Text` 由 `CommandCenter - 相机/PLC 命令中心`
  改为 `上位机控制中心`（任务栏/窗口标题栏显示）。
- **`Views/MainForm.cs`**：类注释"命令中心主窗体。"同步为"控制中心主窗体。"

### 为什么这么改
- 现场要求主界面名称直接叫"上位机控制中心"，不体现具体设备型号，避免与 PLC/相机品牌混淆。

### 优化点
- 纯文案/注释改动，无行为变化；窗口标题与软件定位一致。

## V1.12.6（2026-08-12）主界面标题栏新增扫码枪连接状态灯

> 现场主界面之前只能看 PLC/相机的连接状态，扫码枪连没连接无直观体现（要进功能测试窗体看）。
> 本次在标题栏右上角 PLC 状态灯右侧新增"● 扫码枪"状态灯——**样式与 PLC/相机灯完全一致**
> （圆点+名称，绿=已连接、红=未连接，初始灰色），复用 V1.12.5 给扫码枪加的
> `ConnectionChanged` 事件实时刷新，不用轮询。

### 改动范围
- **`Views/MainForm.Designer.cs`**：标题栏新增 `lblScannerStatus`（Dock.Right，紧跟 `lblPlcStatus`
  之后 Add → 位于 PLC 灯右侧、相机灯左侧），文本固定"● 扫码枪"、96px、初始灰色
  （与 PLC 灯设计器默认一致）；类注释 ASCII 布局图同步（●PLC | ●扫码枪 | ●相机N）。
- **`Services/ScannerTcpService.cs`**：`TryConnect` 连接失败（超时/被拒/异常）也触发
  `SetConnected(false)`——对齐 PLC/相机"连不上就变红"。此前 TCP 扫码枪一直连不上时
  （IP 不可达/端口被调试助手占用）从不触发 `ConnectionChanged`，主界面灯停在初始灰不变，
  颜色显示逻辑与 PLC/相机不一致。边沿检测保证状态没变不重复发事件，无事件风暴。
- **`Views/MainForm.cs`**：
  - `SubscribeRuntimeEvents` 订阅每台扫码枪的 `ConnectionChanged` → `RefreshScannerStatus()`，
    并在订阅末尾做一次初始刷新（构造/热更后立即上色）；
  - 新增 `RefreshScannerStatus()`：只切颜色不改文本，**颜色显示逻辑与 PLC/相机灯完全一致**
    ——绿=已连接、红=未连接（色值与 `UpdateDeviceStatus` 相同），只要有一台"启用"的扫码枪
    未连接即红，全部启用都已连接才绿；禁用（`Enabled=false`）不参与判定；
    **没有任何启用的扫码枪时显示灰色**（同 PLC/相机灯"无设备/未判定"的初始灰，不表示故障）；
    事件工作线程触发，统一 BeginInvoke 回 UI。
- **CHANGELOG/docs 同步**（本节）。

### 为什么这么改
- 扫码枪是"设备主动推码 + 后台自动重连"模型，连接随时可能断开/恢复，主界面需要一个一眼可见、
  实时准确的连接状态位（此前只能靠日志或功能测试窗体判断，现场不便）；
- 聚合规则与相机 ≥3 台的聚拢语义一致（全部连接才绿），多台扫码枪最多时行为可预期；
- **样式与 PLC/相机灯完全统一**：一排"●PLC | ●扫码枪 | ●相机N"圆点灯，操作员不用区分两种
  表达方式（最初做成了"扫码枪：已连接/未连接"文字式，与右侧圆点灯不协调，已按现场反馈统一）。

### 优化点
- 主界面即可确认扫码枪通断，配合 V1.12.5 的断开边沿日志，现场排查"扫码枪没反应"更快。

## V1.12.5（2026-08-12）扫码枪连接状态实时化 + 断线缓存清理

> 现场排查扫码枪"提示断连但调试助手能通"：根因是**基恩士 SR 无协议 TCP 模式下扫码枪只接受
> 一个客户端**——调试助手占用连接时上位机连上即被踢（表现为持续断连），关掉调试助手重启后即通。
> 但排查中还暴露两个代码缺陷：① `IScanner` 没有连接状态事件，功能测试窗体的扫码枪状态灯只
> 打开时刷新一次、永远停"断连"，即使后台已自动连上界面也显示断连，放大了"连不上"的误判；
> ② TCP 扫码枪断线重连时不清半条码缓存，条码读到一半断线会与重连后的新条码拼接成脏数据。
> 本次一并修复，并核对默认扫码枪地址全链路一致为 `19.87.6.100:9004 / LON`。

### 改动范围
- **`Services/ScannerService.cs`（IScanner 接口 + 串口实现）**：
  - 接口新增 `event EventHandler<bool> ConnectionChanged`（对齐 PLC/相机的 `ConnectionChanged`
    语义：边沿触发、工作线程抛、UI 需 Invoke），串口/TCP 两实现统一暴露，上层只依赖接口；
  - 串口实现：`Open()` 成功触发 true、`Dispose()` 触发 false（内部 `SetConnected` 边沿检测，
    状态没变不发事件）。
- **`Services/ScannerTcpService.cs`**：
  - 连接成功（`TryConnect`）触发 `ConnectionChanged(true)`；断流（`MarkDown`）触发 false——
    功能测试窗体状态灯随真实连接实时转绿/转红；
  - `MarkDown` 断线时清空半条码缓存 `_line`（防"读到一半断线→残留半截与新条码拼接"污染）；
  - 断线边沿日志：从"已连接"首次变"断开"打一条 `扫码枪(TCP)连接断开，3s 节流后自动重连`，
    不再静默（对齐 ConnectionMonitor 断连边沿提示约定）；从未连上则不刷（连接失败日志已降噪）。
- **`Views/DevTestForm.cs`**：`WireEvents` 订阅每台扫码枪的 `ConnectionChanged`，连接/断线
  即时 `SafeInvoke` 刷新状态灯（此前只有初始一次 + PLC/相机事件顺带刷新）。

### 为什么这么改
- 扫码枪是"设备主动推码 + 后台自动重连"的模型，连接随时可能断开/恢复（被调试助手占用、
  拔网线、重启设备），没有连接事件，界面永远展示打开瞬间的旧状态，误导排查方向；
- 半条码缓存残留是真实边界 bug：断点恰好落在条码中间时，下一条码会带上前半截脏数据，
  存图目录名/标题栏序列号都会错。

### 优化点
- 功能测试窗体扫码枪状态灯现在是"真状态"，连上即绿、断开即红，与 PLC/相机状态灯行为一致；
- 现场再遇到"断连"可直接看日志区分：`连接失败（后台持续重连）`= 一直连不上（多半端口占用/
  地址不对）；`连接断开，3s 节流后自动重连`= 已连上又被踢（扫码枪单客户端，检查是否被占用）。

## V1.12.4（2026-08-12）沉淀"删除旧代码自检纪律"到 AGENTS.md

> 删除扫码枪旧配置兼容逻辑时，曾误删"防 NRE 的空安全"（`Mode.Trim()` 遇手改 null 会崩），
> 用户提醒后修复。为避免此类问题重复发生，把"删除/重构旧代码前先分清两类、删后逐处校验"的自检纪律
> 固化到 AGENTS.md"代码约定"区，作为强制前置纪律。

### 改动范围
- **`AGENTS.md`**：新增"删除/清理旧代码的自检纪律"条目——
  - 先分清"真·旧配置兼容"（可删）与"防 NRE 的空安全"（不可删）；
  - 删后逐处校验调用路径恒非 null、用 `?.` 空安全写法替代裸链式调用（而非加回旧兜底）；
  - 构建 + 冒烟 + "故意破坏输入"推演三件套，删完自问"有没有谁还依赖这段保护"。

### 为什么这么改
- 删代码是"负改动"，删错比不改更糟：旧配置兼容是死代码，但空安全是活防线，两者删除风险天差地别；
- 靠用户每次提醒代价太高，沉淀成规则后 AI 自动执行、自我校验。

### 优化点
- 后续所有删除/清理类改动自带检查清单，降低引入新 bug 的概率。

## V1.12.3（2026-08-12）清理扫码枪旧配置兼容逻辑

> 项目未上线，不保留任何"旧版本配置"的兼容兜底。本次把扫码枪链路里为旧配置
> （Mode 缺失/为空、scan 单对象）写的冗余判空与"按 Serial 兜底"全部删掉，逻辑只认当前模型。

### 改动范围
- **`Views/SettingsForm.cs`**：扫码枪表格加载行（`LoadScannerRows`）不再对空 Mode 按 "Serial"
  兜底显示，直接填 `s.Mode`；保存（OnSave）不再把空 Mode 兜底为 "Serial"，直接取表格值。
- **`Views/MainForm.cs`**：`BuildScanner` 去掉 `scan != null` 与 `!IsNullOrWhiteSpace(Mode)` 判空，
  直接按 `Mode.Trim()=="Tcp"` 二选一（ConfigStore 已保证 Scanners 元素非 null）。
- **`Views/DevTestForm.cs`**：`ScannerLabel` 去掉 Mode 判空，直接判断是否 Tcp。
- **`Models/AppConfig.cs`**：`ScanConfig.Mode` 注释去掉"其他值按 Serial 兜底"旧话术。

### 为什么这么改
- 项目未上线、配置全部以当前模型为准（ConfigStore 原则），为旧格式写的兜底是死代码，影响可读性；
- 删干净后"方式列"值即真相，界面显隐与实例创建行为单一、可预期。

### 优化点
- 扫码枪相关代码不再有"可能为空按串口"的分支，逻辑更直白；为 V1.12.2 的列显隐功能提供了干净基础。

## V1.12.2（2026-08-12）扫码枪设置：按"方式"自动隐藏无关配置列

> 设置窗体扫码枪表格里，串口参数（串口名/波特率/停止位/校验位）与网络参数
> （IP/端口/触发指令）原本一直全列平铺。现场选择 Tcp 后串口配置项还挂着，
> 显得杂乱且容易误填。改为按"方式"列自动显隐，选 Tcp 只留下网络参数列。

### 改动范围
- **`Views/SettingsForm.cs`**：
  - 新增 `ApplyScannerModeColumns()`：扫描表格所有行的"方式"列值，按约定显隐列组——
    **全 Tcp → 只显示 IP/端口/触发指令，隐藏串口列；全 Serial → 只显示串口列；混用或空表格 → 全列显示**
    （DataGridView 列可见性是整列属性、不是单行，故混用时只能全显，否则某行会看不到自己需要的列）；
  - `SetupScannerGridColumns()` 挂 `CurrentCellDirtyStateChanged`：ComboBox 列值要 CommitEdit
    才会触发 CellValueChanged，故在"单元格变脏"时主动提交并刷新列显隐；
  - `LoadScannerRows()` 填完行后刷新一次；"添加一台/删除选中"后也刷新（新行方式已知、删行后方式组合可能变化）。

### 为什么这么改
- 本项目现场扫码枪就是 Tcp 网口，串口配置列纯属干扰，隐藏后界面只留可用的网络参数，降低误填风险；
- 保留 Serial 全套参数，将来换串口扫码枪切一下"方式"下拉即可，不需要改代码。

### 优化点
- 选择即所见：点"方式"列下拉切到 Tcp，串口列立刻消失，交互直觉；
- 混用/空表格兜底全显，不会出现"某台扫码枪的配置列被藏起来没法填"的情况。

## V1.12.1（2026-08-12）扫码枪触发指令（LON）+ 现场默认地址

> 现场实测基恩士 SR 系列无协议模式：上位机连上 TCP 后**不会自动读码**，需先发一条
> `LON`（打开激光）指令、帧尾补 `\r\n`，扫码枪才进入读码状态。据此完善扫码枪通讯链路，
> 并把默认地址改为现场实测值，方便直接联调。

### 改动范围
- **`Models/AppConfig.cs`（ScanConfig）**：
  - 新增 `TriggerCommand` 配置（默认 `LON`，仅 Tcp 模式用）——连接成功后发送的触发指令，
    发送时自动补 `\r\n` 帧结束符；留空则不发送（对应扫码枪设成"上电自动读码"模式的场景）；
  - 默认 IP/端口由 `192.168.1.110:9005` 改为现场实测 `19.87.6.100:9004`。
- **`Services/ScannerTcpService.cs`**：`TryConnect` 每次连接（含断线重连）成功后自动发送
  `TriggerCommand`；新增公开 `SendTrigger()`（实现 `IScanner.SendTrigger`），供界面手动重发，
  触发指令配置为空时跳过发送。
- **`Services/ScannerService.cs`（IScanner）**：接口新增 `SendTrigger()` 声明；
  串口扫码枪上电即读码、无需触发指令，串口实现为空操作返回 true。
- **`Views/SettingsForm.cs`**：扫码枪表格新增"触发指令"列（`TriggerCommand`），
  默认行/新增行默认值同步为 `Tcp / 19.87.6.100 / 9004 / LON`。
- **`Views/DevTestForm.cs` + `.Designer.cs`**：扫码枪测试区新增
  **"发送触发指令"按钮**（`btnScannerTrigger`）——扫码枪突然不读时点一下手动重发 LON；
  网络写入走后台线程 + SafeInvoke 回 UI，`_busy` 期间禁用（对齐红线）。

### 为什么这么改
- 基恩士 SR 无协议模式下光连上不收码（现场实测），必须先发触发指令才会推条码，
  否则功能测试窗体/主窗体扫码枪区会一直"读不到码"，误以为设备坏了；
- 触发指令做成可配置项，现场若改成其它指令（LOF/LON 组合等）无需改代码。

### 优化点
- 连接/重连自动重发触发指令，扫码枪始终处于可读状态，无需人工干预；
- 功能测试窗体可手动重发，排查"扫码枪突然不读"更直接。

## V1.12.0（2026-08-12）新增开发者账号 + 功能测试窗体（相机/PLC 通讯手动验证）

> PLC 业务逻辑（到位→触发→等图→上报）尚未写完，需要先单独验证"相机↔上位机"
> "PLC↔上位机"两条链路是否通。为此新增开发者账号 dev，登录后进入功能测试窗体，
> 复用主窗体已建好的连接做手动触发/读写，不新建连接、不碰业务配置。

### 改动范围
- **`Models/AppConfig.cs`（SecurityConfig）**：新增开发者账号配置——
  `DevEnabled`（默认 true）、`DevUser`（默认 `dev`）、`DevPasswordHash`
  （默认出厂密码 `dev123` 的 SHA-256 哈希）。与管理员同规则：只存哈希不存明文；
  开发者密码暂不支持登录框内修改（改密码面板仅服务管理员），改哈希需手动算后写配置。
- **`Views/LoginForm.cs`**：
  - 新增 `LoginRole` 枚举（None/Admin/Developer）与 `LoginForm.Role` 属性，
    调用方 ShowDialog 返回 OK 后据此分流；
  - 登录校验支持双账号：先比对管理员，再比对开发者（DevEnabled=false 时开发者不认）；
  - 记住密码只对管理员账号生效（开发者登录不回填、不清除管理员记录）；
  - "修改密码"面板加入开发者账号拦截提示（dev 不支持界面改密码）；
  - 横幅/标题文字由"管理员登录"改为通用的"账号登录"。
- **`Views/DevTestForm.cs` + `DevTestForm.Designer.cs`**（新增）：
  - **相机区**：相机下拉框（列出主窗体全部相机）、连接状态灯、
    `仅触发拍照 T1`（SendTrigger）、`触发+读判定 T2`（TriggerAndRead），结果 OK=绿/NG=红/失败=灰；
  - **扫码枪区（V1.12.0 追加）**：扫码枪下拉框（TCP 显示 IP:端口 / 串口显示 COM口号+波特率）、
    连接状态灯、**最近读到条码大字实时展示** + 操作提示。扫码枪（基恩士 SR 系列 TCP 无协议 /
    串口）为"设备主动推码"模式：主窗体已 Open 并持续监听，测试窗体只订阅
    `SerialNumberScanned` 事件展示条码，**不重复 Open、不新建连接**；
  - **PLC 区（V1.12.0 增强）**：连接状态灯、**协议偏移量配置**（`txtOffset`，默认 0，
    实际读写地址 = 输入地址 + 偏移量，用于协议地址与 D 地址不一致的换算）、
    **读地址测试**（读地址 + 偏移 → 读寄存器值）、**写地址测试**（写地址 + 偏移 + 写值 → 写寄存器）、
    读/清到位信号、触发信号置 1/0、完成信号写 0/1/2、下发配方（WriteRecipe）；
    **PLC 区每行控件按"行中心线"垂直居中对齐**（btn 行顶、txt 行顶+4、lbl 行顶+7，
    见 DevTestForm.Designer.cs 文件头说明）；
  - **日志区**：全部操作带时间戳滚动记录；
  - **连接复用（关键）**：本窗体不新建任何 TcpClient/串口，直接使用 MainForm 传入的
    `_plc`/`_cameras`/`_scanners` 实例（内部 EnsureConnected 惰性建连缓存复用、ConnectionMonitor
    仍由主窗体统一管）；关闭窗体不 Dispose 这些服务（属主窗体，由主窗体统一释放）；
  - **线程（红线）**：所有网络 IO 一律 Task.Run 后台线程 + SafeInvoke 回 UI，
    绝不在 UI 线程同步读写；`_busy` 标志防连点并发读写同一连接。
- **`Services/PlcService.cs`**：新增通用 `ReadRegister(dAddress, out value)` /
  `WriteRegister(dAddress, value)` 公开方法（内部复用既有 SafeRead/SafeWrite 与已建连接），
  及 `IpLabel` 属性（IP:端口标签）。
- **`Views/MainForm.cs`**：`OpenSettings` 登录后按 `login.Role` 分流——
  Admin → 系统设置 SettingsForm（原行为）；Developer → 功能测试 DevTestForm，
  测试窗体关闭不触发保存/热更（测试不产生配置改动）。DevTestForm 构造追加传入
  `_scanners` 与 `_config.Scanners`（扫码枪服务实例 + 配置，测试窗体复用与打标签用）。
- **`CommandCenter.csproj`**：注册 DevTestForm 两个文件。

### 为什么这么改
- 联调期用管理员账号登录风险高（进的是改配置窗体，误点保存会改坏现场配置）；
  开发者账号独立成角色，登录即进"只测不配"的功能测试窗体，角色职责清晰、权限隔离；
- 复用主窗体连接避免测试窗体各自建连占满相机 2 连接上限，且与主窗体共享同一连接状态。

### 优化点
- dev/dev123 开箱即用，现场联调零配置；
- 功能测试不产生任何配置写入，测试完直接关窗回到主界面，无需重启。

## V1.11.0（2026-08-12）主窗体默认全屏 + 禁用缩放（保留边框，客户可正常关闭）

> 现场要求程序启动即全屏、不允许缩成小窗，但仍要保留系统边框与关闭按钮
> （否则客户关不了软件）。原 MainForm 是普通可缩放窗（1400x820 居中），可随意拉伸。

### 改动范围
- **`Views/MainForm.Designer.cs`**：
  - `FormBorderStyle` 改为 `FixedSingle`：固定单线边框，**窗口边缘不可拖拽缩放**
    （Sizable 边框才带拖拽句柄），配合 `MaximizeBox=false` 塞死所有缩放入口；
  - `MaximizeBox=false`：中间的"最大化/还原"按钮禁用（变灰不可点）；
  - `MinimizeBox=true`：最小化按钮保留可用；
  - 关闭按钮完整保留，客户可正常退出；
  - `WindowState=Maximized`：启动即铺满屏幕（保留任务栏）。
- **`Views/MainForm.cs`**：
  - `OnShown` 直接调用 `RelayoutTitleBar()` 完成标题栏紧凑重排；
  - **新增 `WndProc` 拦截 `WM_NCHITTEST`（双保险）**：FixedSingle 边框仍挡不住
    Windows 10/11 对"最大化窗口"的系统级边缘拖拽缩放，现将窗口左/右/上/下/四角
    的调整大小热区（HTLEFT..HTBOTTOMRIGHT，命中码 10~17）一律改写为 HTCLIENT
    （客户区），Windows 不再进入缩放拖拽流程；
  - **关键：铺满屏幕改用手动 Bounds=WorkingArea，不再用 WindowState.Maximized**
    ——Maximized 状态会被 Windows 强制切换成"可调整边框"，边缘拖拽缩放照常开放，
    拦截也挡不住；Normal + FixedSingle 的边框才真正固定，无拖拽句柄。
    最小化/关闭按钮（HTMINBUTTON/HTCLOSE）与标题栏拖动（HTCAPTION）不受影响。

### 为什么这么改
- 初版用 `FormBorderStyle.None`（无边框）铺满整屏，但失去关闭按钮，客户无法退出；
- 第二版改 `FixedDialog`（无最小化/最大化按钮），客户要求"最小化也要有"；
- 第三版用 `Sizable` + `MaximizeBox=false`，但 Sizable 边框仍可**拖拽边缘缩放**；
- 第四版改 `FixedSingle` + `Maximized`，但**Maximized 会让 Windows 把边框切换成
  可调整样式**，边缘拖拽依然生效；
- 终版 `FixedSingle` + `Normal` + 手动铺满工作区 + WndProc 拦截：
  **按钮缩放、常规拖拽缩放、最大化窗口边缘拖拽缩放三条通道全部失效**，
  仅保留最小化/关闭按钮，既默认全屏、又不留任何缩小窗口的通道。

### 优化点
- 启动即全屏，任意分辨率自动铺满，窗口矩阵/状态栏等分布局始终完整；
- 固定边框不能拖拽/还原，杜绝"缩成小窗"误操作，同时保留关闭按钮可退出。

## V1.10.0（2026-08-12）相机连接指示灯按台数分模式：≤2台每台一灯，≥3台聚拢成下拉列表

> 现场相机台数增多后，标题栏右侧"每台相机 96px 一个灯"的灯阵会越占越宽，
> 把左侧字段/系统设置按钮越挤越靠左。改为：相机 ≤2 台保持既有"每台一个灯"，
> ≥3 台聚拢成一个"总连接状态标签 + 相机下拉列表"，标题栏宽度占用大幅收敛。

### 改动范围
- **`Views/MainForm.cs`**：
  - `BuildCameraStatusLights` 按 `_cameras.Count` 分两种模式：
    - **≤2 台**：保持原逻辑，每台一个 `● 相机N` 指示灯（绿=连、红=断），零行为变化；
    - **≥3 台**：生成两个 Dock.Right 控件——`_lblCamAggregate` 总状态标签
      （**只有所有相机都连接才显示绿色，任一断连就红色**）+ `_cmbCamOverview`
      下拉列表（`DropDownList` 防误改，`OwnerDrawFixed` 每项自绘"状态圆点+相机名IP"，
      绿=OK、红=断连），可点开逐台查看名字与连接状态。
  - 新增 `RefreshCameraAggregateStatus()`：从相机 `ConnectionChanged` 事件回调中刷新
    总状态标签颜色与悬停明细（ToolTip 列出每台"相机N IP：已连接/断连"），并重绘
    下拉框状态圆点。小台数模式下 `_lblCamAggregate==null` 直接返回，不影响原灯。
- `SubscribeRuntimeEvents` 的相机连接事件：≤2 台走原 `UpdateDeviceStatus` 更新对应灯；
      ≥3 台改走 `RefreshCameraAggregateStatus` 聚合刷新，两模式互不干扰。
- 对齐微调（V1.10.0 追加）：≥3 台模式的总状态标签 + 下拉框装进 `_pnlCamOverview`
  容器（Dock.Right）统一垂直居中——ComboBox 直接 Dock.Right 会被拉满 48px 高、文字偏上，
  与左侧"● PLC"标签（MiddleRight 居中）不对齐；总标签字体改为与 PLC 标签一致的
  "微软雅黑 10F Bold"（此前 Microsoft YaHei 10F Bold，fontname 写法不同易致观感偏差）。
- 移除"空间不足隐藏字段让位"逻辑（V1.10.0 追加）：早期版本放不下时按优先级
  （产品→配方→序列号→计数→分隔线）逐个隐藏低价值字段，**配方下拉框 cmbRecipe
  位列隐藏优先级首位，相机多/窗口窄时会被直接藏掉，现场"配方控件显示不出来"由此而来**。
  V1.10.0 相机区已聚拢成"总标签+下拉框"固定宽容器，右侧不再随台数膨胀，
  无需再隐藏任何字段；RelayoutTitleBar 现改为所有可见字段一律完整排布，
  越过右边界即停止（不隐藏），cmbRecipe 永远显示。
- 相机下拉列表只显示 IP（V1.10.0 追加）：`KeyenceIV4Camera` 新增 `IpAddressOnly`
  属性（仅 IP 不带端口），`CamOverviewLabel` 与悬停明细均改用该属性，标题栏
  相机项显示"相机N  IP"，不再带端口号（`IpLabel` 保留给日志区分用）。
- 右上角相机总标签不显示台数（V1.10.0 追加）：`_lblCamAggregate` 文本由
  "● 相机 N 台" 简化为 "● 相机"，只保留圆点+相机字样（台数在相机数量配置里看）。
- 验证：构建通过 + 冒烟启动存活正常。

### 为什么这么改
- 相机台数 1~2 时逐台灯直观、一眼看到每台状态，保留不动；
- 台数 ≥3 后"每台 96px"的灯阵与左侧字段争抢标题栏宽度，聚拢成"总状态 + 下拉"后
  固定只占约 270px，标题栏在任何相机台数下都稳定，不再越挤越左；
- 总状态"全连才绿、一断即红"与现场 OK/NG 的绿红习惯一致，断连时一眼可判，
  下拉明细 + 悬停列表定位到具体哪台断了。

### 优化点
- 标题栏宽度占用与相机台数解耦：新增相机不再挤占左侧字段，系统设置按钮位置稳定；
- 断连定位更快：悬停总标签即看每台连/断，无需逐个确认。

## V1.9.9（2026-08-12）修复"默认两台相机被反序列化叠加成四台" + 标题栏灯多挤压字段

> V1.9.8 把默认相机收敛为两台后，实测程序启动会创建 **4 台相机**（212、213 各重复一次），
> 连接日志里同一毫秒打出多条相同 IP 的失败。根因不是缓存，是 Newtonsoft 反序列化叠加。
> 同时修复相机灯多时标题栏右侧灯区把"系统设置"按钮等字段压住/盖住的布局问题。

### 改动范围
- **`Models/AppConfig.cs`**：`Cameras` 属性初始化器由 `= CameraConfig.DefaultCameras()`
  （建 2 台）改回 `= new List<CameraConfig>()`。**这是四台 bug 的根因修复**：
  Newtonsoft 反序列化对"属性已有实例的集合"默认是**复用该实例并向其 Add json 元素**
  （不是整体替换），于是初始化器的 2 台 + json 里的 2 台叠成了 4 台（实测
  `AppConfig.Cameras.Count == 4`）。初始化器给空列表后，默认两台相机统一由
  ConfigStore.Load / MainForm / SettingsForm 的"空列表兜底"补齐，行为不变。
- **`Utils/ConfigStore.cs`**：`Load` 对相机的兜底条件从"仅 null"放宽到"null **或空列表**"，
  补 `CameraConfig.DefaultCameras()`——因初始化器已改空列表，json 没写相机时靠这里兜底，
  否则会拿到空列表、一台相机都不触发。
- **`Views/MainForm.cs`**：
  - `RelayoutTitleBar` 感知右侧 Dock 区（PLC 灯 + 每台相机灯）总宽：左侧字段最大 X 限制为
    `标题栏宽 - 右内边距 - 右侧灯区宽`，不再把字段推进灯区被盖住；空间不足时按优先级
    （产品→配方→序列号→计数→分隔线）逐个隐藏低价值字段再重排，**系统设置按钮始终可见**。
  - 把配置可见性判定抽成 `ApplyConfigVisibility()`（InitTitleBarFields 与 RelayoutTitleBar
    共用），重排前先按配置恢复被临时隐藏的字段，避免窗口拉大后字段"消失不回"。
  - 新增 `Resize += RelayoutTitleBar`：窗口缩放时重新压缩/恢复标题栏字段。
- 修复验证：构建 + 冒烟，启动日志 `BuildServices 共创建 2 台相机`，连接失败每台仅 1 条。

### 为什么这么改
- **四台问题**：属性初始化器 + Newtonsoft 复用集合的叠加是隐蔽坑，改"初始化器空列表 +
  加载兜底"是让集合内容只由一处（配置/兜底）决定的干净解；
- **标题栏问题**：左侧绝对坐标与右侧 Dock 两套布局互不知道对方，必须让排布侧显式
  扣除 Dock 区宽度，并允许按优先级让位，否则相机一多按钮就被灯盖住。

### 优化点
- 相机连接失败日志从"同 IP 同毫秒重复多条"恢复到"每台一条"，现场排查干扰大幅减少；
- 标题栏在任意相机台数/窗口宽度下都能保证操作按钮可见，布局不再错乱。

## V1.9.8（2026-08-12）相机 IP 写死为现场两台 + 默认配置内置两台相机

> 现场两台相机 IP 已确定，无需再配置：相机1=19.87.6.212、相机2=19.87.6.213。
> 把"默认两台相机"作为程序出厂配置，无配置文件 / 配置留空 / 设置窗体空表格
> 全部自动带这两台，开箱即用，避免现场忘了填 IP 导致一台不触发。

### 改动范围
- **`Models/AppConfig.cs`**：
  - `CameraConfig.IpAddress` 默认值由 `192.168.1.100` 改为现场相机1 `19.87.6.212`；
  - 新增统一出口 `CameraConfig.DefaultCameras()`：返回现场两台相机（相机1 `19.87.6.212`、
    相机2 `19.87.6.213`，其余参数用模型默认）。**三处需要"默认两台相机"的地方全部收敛到
    这一个方法**——无配置时 `AppConfig.Cameras` 初值、主窗体空配置兜底、设置窗体空表格
    默认行/添加行，现场换相机会只改这一处 IP 即可，不会漏改。
  - `AppConfig.Cameras` 初值改为 `CameraConfig.DefaultCameras()`（此前空列表）。
- **`MainForm.BuildServices`**：空配置兜底由"补一台 `new CameraConfig()`"改为
  "补两台默认相机"——原来空配置只触发一台相机，漏了第二台；现在与出厂默认一致触发两台。
- **`Views/SettingsForm.cs`**：
  - 相机表格空时按默认两台相机填两行（此前只填一行 `192.168.1.100` 示例）；
  - "添加一台"按钮默认 IP 用现场相机1（此前 `192.168.1.1` 占位）；
  - 保存后相机全删光时的兜底改为默认两台（此前兜底一台空 IP 相机）。
- **`Utils/ConfigStore.cs`**：`Load` 对 `cameras` 为 null 的兜底由空列表改为
  `CameraConfig.DefaultCameras()`，保证 json 里显式写空 `cameras: null` 也会用默认两台。
- **`docs/通讯接入.md`**：相机章节写明默认两台 IP 及 `DefaultCameras()` 统一出口，
  校准清单第一条勾选"IP 已定稿"并把网段确认改为 19.87.6.x。
- **`README.md`**：可配置项-相机一节改为"两台相机列表"，记录默认 IP。

### 为什么这么改
- 现场相机 IP 已经确认固定，不再需要现场改 IP；把默认值直接写死能减少一次配置错误的机会；
- "默认两台"在多处使用，收敛成单一工厂方法（`DefaultCameras`）是防止"改一台 IP 漏另一处"
  的关键——此前每处各自硬编码，现场换 IP 极易只改设置窗体、忘了改空配置兜底。

### 优化点
- 开箱即有 2 台相机：首次运行、配置损坏回退、设置表格清空后保存，行为完全一致；
- 改动全部走既有"缺字段用模型默认"约定，存量配置（已配 1 台/多台）不迁移、行为不变。

## V1.9.7（2026-08-12）登录界面改密码提示文字居中

### 改动范围
- `Views/LoginForm.Designer.cs`：改密码面板"新密码至少 6 位…"提示（lblPwdHint）
  纵坐标 140→135，使与上方"确认密码框"底部、下方按钮行顶部间距相等（各 6px），居中显示。

## V1.9.6（2026-08-12）新增相机通讯联调清单文档

> 现场需快速调通基恩士相机通讯，新增一份可直接转发给工程师的联调确认清单。

### 改动范围
- 新增 `docs/联调清单.md`：按"必须确认/建议确认"分级列出相机联调需向现场确认的信息
  （型号/固件、IP、无协议通信端口、T1/T2/RT 指令实测回显、判定工具与字符含义、取图方式），
  并附 `appconfig.json` 相机节点字段对照表与快速联调路径。

## V1.9.5（2026-08-12）主界面窗口去掉右下角 OK/NG 徽标

> 现场反馈：窗口画面上右下角 OK/NG 标签占画面、不美观，去掉。

### 改动范围
- `Controls/CameraDisplayControl.cs`：删除右下角自绘 OK/NG 徽标（OkNgBadge 实例、
  创建/定位/Resize 刷新逻辑）；`SetOkNgStatus`/`IsOk` 接口保留（主流程仍记录判定状态，
  只是不再叠加显示在画面上）。

### 为什么这么改
- 判定结果仍由标题栏 OK/NG 计数色块体现，窗口内重复徽标意义不大且挡画面；
  保留接口便于将来需要时一键恢复。

## V1.9.4（2026-08-12）登录界面高度调小

> 管理员登录界面下方空白区域过大，压缩窗体高度。

### 改动范围
- `LoginForm`：窗体高度 280→256（ClientSize），两个面板（登录/改密码）随 Dock=Fill 自动变矮，
  内容控件（按钮行最高点 ~196）在 208 高面板内仍有余量，不会裁切。

## V1.9.3（2026-08-12）标题栏 OK/NG 色块继续加宽

> V1.9.2 加宽后客户仍嫌不够醒目，再次加大。

### 改动范围
- `MainForm.StyleCountBadge`：色块左右 padding 14→22、上下 3→5，进一步放大；
  AutoSize 与标题栏布局逻辑不变。

## V1.9.2（2026-08-12）标题栏 OK/NG 色块加宽

> 客户反馈标题栏 OK/NG 高亮色块不够醒目，加宽放大。

### 改动范围
- `MainForm.StyleCountBadge`：色块左右 padding 6→14、上下 2→3，整体更宽更高更醒目；
  AutoSize 逻辑不变，宽度仍随计数位数自动伸缩，标题栏布局（RelayoutTitleBar）不受影响。

### 优化点
- 只调色块本体，不动布局公式与配色逻辑，改动面最小。

## V1.9.1（2026-08-12）BR 取图响应字段修正 + 登录界面默认账号

> ① 用户核对《IV4 通信、连接指南》后指出：BR 读图响应里 nnnnnnnnnn 是**合计触发编号**、
> ddddddd 才是**图像数据长度**，V1.9.0 之前的实现正好理解反了（拿触发编号当字节数去读图，
> 几乎必然读错/读崩）。② 登录界面账号框默认填管理员账号，去掉"当前账号"冗余提示。
> ③ 明确 CR（回车符）作为数据包结束标记的通讯语义（T1 回显确认）。

### 改动范围
- **`Services/KeyenceIV4Camera.cs`**（ReadImage）：
  - 响应解析顺序按手册修正：**阶段1=合计触发编号 → 阶段2=图像数据长度 → 阶段3=读 N 字节**；
    此前把触发编号当长度（防御性长度校验几乎必失败）、把长度当属性透出，方向完全颠倒；
  - `ReadImageOutcome.DataAttr` 更名为 `DataTriggerNo`（合计触发编号，仅日志/现场对照）；
  - 无最新图像时报错场景说明：前缀校验不过即判失败并走断连重连，不误取错数据；
  - 指令 m 参数注释明确：0=无压缩、1=1/2 压缩（默认 1）。
- **`Views/LoginForm.cs` / `Designer.cs`**：账号框默认填 `AdminUser`（通常 admin）并全选，
  删除"当前账号: admin"灰色提示控件（无意义，占位又啰嗦），按钮行上移填补空白。
- **`docs/通讯接入.md`**：BR 响应字段含义纠正（触发编号/数据长度）、补 CR 结束标记语义
  与 T1 回显确认说明；AppConfig.cs 注释同步。
- **`Services/ProductionCoordinator.cs`**：TCP 取图成功日志字段名随 DataTriggerNo 更新。

### 为什么这么改
- 字段颠倒属"手册没读细"的硬伤：拿触发编号当长度，长度校验（>0 且 ≤64MB）在相机计数
  较大时直接判非法，较小值时按错误长度读图导致数据错位/截断，Tcp 取图模式会持续失败；
  按手册顺序解析后语义正确，触发编号仅透出、长度精确决定读取字节数。
- 登录账号就一个（admin），默认填上省去每次输入；"当前账号"提示与输入框内容重复，删除。

### 优化点
- ReadImage 各阶段读数字、防御校验、断连标记逻辑保留不动，只纠正字段顺序与命名，
  改动面最小、回归风险低；
- CR 语义写进类头注释与文档，后续维护不再误判"响应到底回不回显、要不要补 \r"。

## V1.9.0（2026-08-12）管理员登录控制系统设置使用权限

> 现场要求：不希望操作员随意进系统设置改配置。V1.8.5 只能"隐藏按钮"，
> 一旦隐藏管理员想改配置也得手改 json。本次增加管理员登录：点"系统设置"必须先
> 登录管理员账号，验证通过才放行；改密码整合进登录对话框（登录界面自带"修改密码"
> 入口，不占系统设置空间），密码以 SHA-256 哈希存配置，不存明文。

### 改动范围
- **`Models/AppConfig.cs`**：新增 `SecurityConfig`（`AppConfig.Security`）：
  - `AdminEnabled`（默认 true）：是否启用登录校验，false 则点系统设置直接打开（旧版行为）；
  - `AdminUser`（默认 `admin`）：管理员用户名，登录时大小写不敏感比对；
  - `AdminPasswordHash`（默认 = `admin123` 的 SHA-256）：密码只存哈希不存明文。
  - 旧配置缺该字段时用模型默认，无需迁移（对齐项目"缺字段用模型默认"约定）。
- **新增 `Utils/SecurityUtil.cs`**：`HashPassword` 计算 SHA-256 hex 小写（用 .NET 自带
  `SHA256`，零第三方依赖），登录比对与改密码共用同一哈希逻辑；另加"记住密码"三方法
  （见下）。
- **新增 `Views/LoginForm.cs`**（+ Designer）：管理员登录对话框，登录 + 改密码两块面板：
  - **登录面板**：用户名/密码 + **记住密码勾选框** + 当前账号提示 + 链接式"修改密码" +
    蓝色主按钮"登录"（回车=登录、ESC=取消关闭），校验通过返回 DialogResult.OK；
  - **修改密码面板**：原密码（须验证正确）+ 新密码 + 确认密码 + 链接式"返回登录" +
    蓝色主按钮"保存修改"（新密码两次一致且 ≥6 位，保存后写盘、下次登录用新密码）；
  - 顶部蓝色横幅标题随面板切换（"管理员登录"/"修改密码"），主按钮与密码框圆点显示。
- **`Views/MainForm.cs`**：`OpenSettings` 在 `Security.AdminEnabled=true` 时先弹 LoginForm
  （传整个 _config，改密码可直接写盘），登录失败/取消直接 return，不进入系统设置——
  **每次点都校验**，无"记住登录状态"可钻空子。
- **`Utils/ConfigStore.cs`**：`Load` 对 `Security` 空段兜底 `new SecurityConfig()`。
- **记住密码（DPAPI 加密，`CommandCenter.csproj` 引用 `System.Security`）**：
  - 勾选"记住密码"登录成功后，把"用户名+密码"用 Windows **DPAPI**（`ProtectedData`）
    加密存到 `%LOCALAPPDATA%\CommandCenter\remembered_login.dat`（绑定当前 Windows 用户，
    换机器/换用户解不开，拷走文件也无效）；下次打开登录框自动回填，用户仍可看到圆点密码、
    点登录即可；
  - 取消勾选登录成功时删除旧记录；改密码保存后若勾选则把记住文件同步成新密码，
    否则清掉——保证回填的永远是当前生效密码，不会因为改密后回填旧密码而登录失败；
  - 回填前校验记住的用户名与当前 `AdminUser` 一致才生效，换账号不串记录。

### 为什么这么改
- 改密码是"管理员账号维护"，放进系统设置会和设备/相机/存图等业务配置混在一起，
  界面混乱；整合进登录对话框后，账号相关操作（登录、改密码）集中在一处，
  系统设置保持纯业务配置，职责清晰；
- 权限控制目标是"防现场误操作"而非"防敌意破解"：SHA-256 哈希足以防止"配置文件被看一眼
  就拿到密码"，加盐/慢哈希对本地单机上位机没有实际收益，实现保持最直白；
- 每次点都要求登录而非"登录一次长期有效"：现场设置是高风险入口，无记忆状态则权限边界
  最清晰，操作员也不可能因为忘退出留下漏洞。

### 优化点
- 默认启用但带出厂默认 admin/admin123，首次登录后改掉即可，不存在"启用就进不去"的死锁；
- 关闭 `AdminEnabled` 完全等价旧版行为，现场不需要防护时可一键回退；
- 登录界面重设计（蓝色横幅 + 主辅按钮分区），比传统灰框对话框清爽，密码框圆点防窥视；
- 密码一致性/长度在界面层校验，配置层只收哈希，逻辑单点（SecurityUtil）两处复用；
- "记住密码"用系统托管密钥（DPAPI）而非自造密钥/明文，兼顾"自动回填"体验与安全红线。

## V1.8.5（2026-08-12）"系统设置"按钮显隐可配置

> 现场要求：生产运行时不希望操作员随意进系统设置改配置。本次在显示配置里加一个开关，
> 默认显示；`display.showSettingsButton=false` 即可隐藏标题栏"系统设置"按钮，布局自动紧凑。

### 改动范围
- **`Models/AppConfig.cs`**：`DisplayConfig` 新增 `ShowSettingsButton`（默认 `true`）。
  - 旧配置缺该字段时用默认值 true，无需迁移、行为不变（对齐项目"缺字段用模型默认"约定）。
- **`Views/MainForm.cs`**：`InitTitleBarFields` 按配置设置 `btnSettings.Visible`
  （构造与"设置保存热更"都会调用，改 json 后热生效无需重启）。
  - 隐藏时 `RelayoutTitleBar` 已按 `Visible` 跳过不占位，标题栏自动紧凑，无需改布局逻辑。
- **`README.md`**：可配置项"显示"一节补充 `ShowSettingsButton` 说明（字段名、默认值、用途）。
- **`AGENTS.md`**：文档同步升级为铁律（任务完成标准之一，主动核对、不需用户提醒）。

### 为什么这么改
- 隐藏入口是"防现场误操作"的最直接手段；保留 json 开关意味着管理员仍可改回来，
  不锁死灵活度。

### 优化点
- 零接口破坏、零布局改动，纯配置驱动；隐藏后仅入口消失，其余标题栏项不受影响。

## V1.8.4（2026-08-12）设置窗体删除空白行误报修复

> 现场反馈：系统设置里相机/扫码枪列表，点击"末尾空白行"（`AllowUserToAddRows` 附加的 * 占位行，
> 用于直接输入新增）再点"删除选中"，会误弹"请先选中要删除的行"。
> 原因：该"新行"不在 `SelectedRows` 集合里，也无法用 `Rows.Remove` 删除，旧代码对
> "SelectedRows 为空"一律弹提示。本次把两处删除逻辑合并为共用方法，正确处理三种场景。

### 改动范围
- **`Views/SettingsForm.cs`**：
  - 新增共用方法 `DeleteSelectedRows(grid, rowName)`，相机/扫码枪删除按钮统一调用。
    三段式处理：① 删 SelectedRows 里的真实行（整行高亮场景）；② 无整行选中但光标停在
    真实行时按"当前行"删除（点单元格即选中行）；③ 光标停在末尾新行时，临时
    `AllowUserToAddRows=false` 再恢复 true，使空白占位行随之为空重建，等效"删除空白行"，
    不再误报"未选中行"。
  - 两个删除按钮事件改为调用共用方法；真实数据行删光时表格自然只留新行，保存侧对空行有兜底。

### 为什么这么改
- 空白占位行本意是"输入即新增"，不是真实数据，删除它的正确语义就是"放弃该占位行"；
  旧实现把它当作"未选中行"提示，现场会误以为程序判断错误；
- 合并共用方法消除相机/扫码枪两套重复逻辑，将来行为调整只改一处。

### 优化点
- 删除交互更贴合直觉：点击任意单元格即视为选中该行（与 FullRowSelect 视觉一致）；
- 构建通过 + 冒烟测试通过（启动 6s 进程存活）。

## V1.8.3（2026-08-12）通讯层健壮性全面加固（11 项修复）

> 上线前按"逐文件代码审查"排查出 11 项潜在缺陷，本次全部修复：
> 数据安全 1 项（存图重名覆盖丢历史图）、现场稳定 3 项（BR 断连残留/关窗卡 2s/FTP 图读早）、
> 性能 1 项（多相机串行触发改并行）、健壮性 6 项。**不涉及任何寄存器地址/相机指令/配置格式变更**，
> 旧配置可直接沿用，行为完全兼容。

### 改动范围（按文件）
- **`Services/ImageStore.cs`（2 项）**
  - **存图重名防覆盖（高危）**：默认文件名模板 `{点位}` 下，同一 SN/判定目录里同点位二次拍照
    必然重名，原来直接覆盖丢历史图。现自动检测重名追加 `_2/_3…` 序号兜底（模板带 `{时间}` 时基本
    不重名，纯保险、不改任何存图规则）。
  - **FTP 目录监听判重规范化**：比较时去掉尾斜杠并忽略大小写，避免 `D:\x` 与 `D:\x\`/`d:\X`
    被当成两个目录重复监听造成重复取图。
- **`Services/KeyenceIV4Camera.cs`（1 项）**：`ReadImage`(BR) 响应解析各阶段（前缀/长度/属性/图像数据）
  遇对端关闭（ReadByte 返回 -1 / Read 返回 0）时一律 `MarkDisconnected`——此前只判失败不标记，
  坏流残留导致下一次动作复用已关闭连接持续失败（V1.7.2 修过的"假活连接"同款 bug 在 BR 路径漏了）。
- **`Services/ScannerTcpService.cs`（2 项）**
  - **Dispose 改限时抢锁 + 锁外强断网**：此前无超时 `lock`，Worker 持锁做 `TryConnect`（最多 2s）时
    UI 关窗会卡最多 2s；现对齐 PlcService/相机的 300ms 限时抢锁，拿不到锁就锁外 Close socket
    （让持锁线程的 BeginConnect 立刻结束并收敛异常），关窗永不阻塞。
  - **Open 检查 `Enabled`**：未启用的 TCP 扫码枪不再起后台连接线程（对齐串口实现行为）。
- **`Services/ScannerService.cs`（1 项）**：串口收码 `_buffer` 加 512 长度上限（对齐 TCP 版），
  防异常/噪声数据无限增长撑爆内存。
- **`Services/PlcService.cs`（1 项）**：`SafeWrite` 改返回 bool，`ReportCounts` 逐个收集成功与否，
  任一计数写失败记 Warn——此前三连写不校验返回值，任一个失败都静默（现场台账会悄悄少记数）。
- **`Utils/ConfigStore.cs`（1 项）**：保存配置优先 `File.Replace` 原子替换（目标存在时），
  避免"先删旧再移新"窗口期 Move 失败导致配置丢失回退默认；Replace 受限时 fallback 原逻辑。
- **`Services/ProductionCoordinator.cs`（3 项）**
  - **多相机并行触发（性能）**：此前 for 串行每台"触发+取图"同步阻塞（T2+BR 每台最坏累加），
    N 台相机总耗时线性累加，现场节拍快时会漏检到位信号。改为每台相机一个 Task 并行触发、
    `WaitAll` 同步等待，总耗时 ≈ 最慢一台相机的时间；各任务只写自己的快照互不干扰。
  - **FTP 图读早重试**：`Created/Renamed` 事件有时先于文件写完到达，`Image.FromFile` 抛
    "文件被占用"导致图丢失；改为 `FileShare.ReadWrite` 打开 + 复制到内存解码 + 最多 3 次
    短延迟重试（共约 1.2s）。
  - **`PendingCamera.IsSnapped` 加 volatile + `LoadImageSafe` 改 `FileShare.ReadWrite`**：
    消除 FTP 线程写/收尾线程读的极小可见性窗口；显示图在文件仍被写时也能读到。

### 为什么这么改
- 前三项（存图覆盖/BR 断连残留/关窗卡死）分别是"数据丢失、持续假失败、界面卡顿"三类现场最怕的问题，
  且都发生在通讯与归档热路径上，风险最集中；
- 并行触发是明确的节拍收益点：多相机现场单次检测耗时从"逐台累加"降为"最慢一台"，直接降低漏检风险；
- 其余为一致性加固，让服务间行为约定（限时抢锁/断连即清/长度上限）全项目对齐，后人在此基础上扩展不易再踩同类坑。

### 优化点
- 全部修复零配置变更、零接口破坏（`SafeWrite` 返回值被忽略的调用点无需改动），旧配置/旧行为完全兼容；
- 构建通过 + 冒烟测试通过（启动 6s 进程存活，PLC/相机连接失败日志为无真实设备的预期输出）。

## V1.8.2（2026-08-12）现场调试文档补强

> 上线前逐条核查全部通讯链路，发现两处"填地址之外"的关键依赖现场必踩坑，补进
> `docs/通讯接入.md` 校准清单：① FTP 取图模式依赖上位机自行部署 FTP 服务器
> （程序只监听目录、不自带 FTP 服务，没装 FTP 则图永远不到、等图超时记取像失败）；
> ② 扫码枪 TCP 端口 9005 是猜的常见值、且 V1.8.1 起配置是 `scanners` 数组，
> 手改旧 `scan` 段不会生效——两处均在清单里标注"先用调试工具实测/用设置窗体配置"。

### 改动范围
- `docs/通讯接入.md`：相机 2.3 校准清单补 **FTP 服务器依赖**（含部署说明、失败现象）；
  扫码枪 1.4 校准清单补 **端口实测确认** 与 **scanners 数组配置提醒**。
- 无代码改动，纯文档（现场校准逐条可勾）。

### 为什么这么改
- 通讯代码（PLC/相机/扫码枪/存图）此前已逐条模拟验证通过，真正的剩余风险全在
  "设备侧配置 + 上位机环境"这些代码之外的事；把坑写进清单，明天现场照着勾即可。

### 优化点
- 校准清单从"结果导向"改为"先实测再配"（先确认端口/帧格式，再进设置窗体填值），
  把排查顺序理顺，减少现场来回试。

## V1.8.1（2026-08-12）扫码枪支持多台 + 系统设置可视化配置

> 上一版（V1.8.0）扫码枪只有代码级接入、配置只能手改 json。现场反馈：扫码枪可能不止一把
> （不同工位/不同门各一把），且希望像相机一样在设置窗体里直接配。本次：
> ① 配置模型 `ScanConfig` 单对象 → `Scanners` 列表（对齐 `Cameras` 多台风格）；
> ② 设置窗体新增"扫码枪列表"表格（启用勾选 + 串口/TCP 参数列 + 添加/删除）。
> 配置里留一台的用法不变，加行即多台，任何一台扫到的条码都更新当前序列号。

### 改动范围
- **模型 `AppConfig.Scan → Scanners`**：`List<ScanConfig>`，每台一个独立配置
  （启用开关、方式 Serial/Tcp、串口参数、网络参数）。`ConfigStore.Load` 空列表兜底，
  旧 json 里的 `scan` 单对象字段被忽略、加载不报错（模型字段名变了，旧配置该段不迁移）。
- **`MainForm` 多台实例**：`_scanner`（单实例）→ `_scanners`（`List<IScanner>`），
  `BuildServices` 遍历 `_config.Scanners` 逐台 `BuildScanner`；`SubscribeRuntimeEvents`、
  `ApplyRuntimeConfig`、`FormClosing` 全部改为遍历；每台扫到的条码都进 `LatestSerialNumber`
  与标题栏（互不干扰，断连自愈各自独立）。
- **`SettingsForm` 新增"扫码枪列表"**（对齐相机表格风格）：
  - 列：`启用`（勾选） / `方式`（Serial/Tcp 下拉） / `串口名` / `波特率` / `停止位` /
    `校验位` / `IP` / `端口`——串口模式配串口四参数、TCP 模式配 IP/端口；
  - 按钮：`添加一台` / `删除选中`，与相机同交互；保存时逐行回写 `Scanners`，
    空行自动剔除、全空则兜底一条默认（未启用）。
  - 窗体加高到 790，扫码枪表格位于相机表格下方；ASCII 布局图、ToolTip 同步更新。
- **模拟测试**：新增两台假扫码枪并行验证——各自收码不串扰、一台断线另一台不受影响、
  断线那台节流重连恢复收码，48 项断言全过。

### 为什么这么改
- 与相机（`Cameras` 列表 + 设置表格）完全同构：现场会一种就会另一种，维护零学习成本；
- 多台扫码枪是明确的现场需求（不同工位扫码），列表模型是最小且可扩展的表达；
- 断连自愈、收码切行逻辑全部复用 `IScanner` 实现类，多台只多几个实例，无重复代码。

### 优化点
- 设置窗体即可完成扫码枪的全部接入配置（含启用/方式/串口/TCP），不再需要手改 json；
- 一台的旧用法不变：`Scanners` 列表只留一行即等效原单台。

## V1.8.0（2026-08-12）扫码枪接入（基恩士 SR 系列，串口/以太网 TCP 无协议二选一）

> 客户确认扫码枪也是基恩士的。基恩士 SR 系列支持串口 RS-232 与以太网 TCP/IP 无协议两种
> 通讯。本次把现有（从未接线的）串口扫码枪服务真正接入主流程，并新增以太网无协议实现，
> `scan.mode` 配置二选一。

### 改动范围
- **新增 `ScannerTcpService`（基恩士 SR 以太网 TCP/IP 无协议）**：
  - 上位机作 TCP 客户端连扫码枪（`ScanConfig.IpAddress:Port`，基恩士无协议默认端口现场确认），
    扫码枪读到条码主动推送文本行，本服务按行切分（一行=一条码，CR/LF/CRLF 兼容）；
  - 自持后台线程做"连接 + 阻塞读流"：连接 `BeginConnect+WaitOne` 强制超时（2s）、
    断线按 3s 节流**静默自动重连**、收码在专用读线程（绝不碰 UI 线程，对齐项目铁律）；
  - 防御：单条条码最长 512 字符、Dispose 时 Close 打断读线程等待。
- **新增 `IScanner` 统一接口**：串口 `ScannerService` 与 `ScannerTcpService` 同接口，
  主窗体只依赖接口，按 `ScanConfig.Mode` 决定实例化。
- **`ScanConfig` 扩展**：`Mode`（"Serial" 默认 / "Tcp"）、Tcp 的 `IpAddress`/`Port`。
- **`MainForm` 接入（修复"扫码枪未接线"的遗留问题）**：
  - `BuildServices` 创建扫码枪、`SubscribeRuntimeEvents` 订阅扫码事件并启动；
  - 扫到的条码 → `_coordinator.LatestSerialNumber` + 标题栏"序列号:"刷新（Invoke 回 UI 线程）；
  - FormClosing / ApplyRuntimeConfig（热更）中一并释放与重建。
- **`docs/通讯接入.md`**：新增"二、扫码枪"章节（配置示例、Tcp/Serial 说明、现场校准清单）；
- **模拟测试**：新增假基恩士 SR 扫码枪 TCP 服务器，验证连接/收码（CRLF 与 LF）/
  断线自动重连，41 项断言全过。

### 为什么这么改
- 旧代码里有 `ScannerService`（串口）但从未实例化，扫码枪形同虚设——本次真正接通；
- 客户扫码枪是基恩士，以太网无协议是其主流通讯方式（与相机同思路），且不占串口资源，
  现场插网线即用；串口实现保留兜底。

### 优化点
- 扫码枪断连自愈独立在实现类内（3s 节流重连），不占 ConnectionMonitor；
- 扫码数据直达序列号链路：标题栏显示 + 存图 `{SN}` 目录自动生效。

## V1.7.2（2026-08-12）通讯层修复（模拟测试验证通过）

> 去现场前用"假 PLC + 假相机"模拟服务器对通讯层做了全链路实测，
> 揪出几个会在现场导致"对好 IP 也连不通 / 时好时坏 / 误判"的 bug 并修复。

### 改动范围
- **相机 `KeyenceIV4Camera` 三个关键修复**：
  1. **CRLF 残留**：IV4 响应以 CRLF 结尾，旧实现读到 `\r` 就停，把 `\n` 留在流里，
     下一次动作先读到残留 `\n` 判"无响应"——现场表现为"第一次触发正常、第二次判定失败"
     交替出现。现在读响应行前先跳过前导 CR/LF/NUL（同时容忍相机响应前发的空行）。
  2. **假活连接复用**：读超时/断流后 TCP 的 Connected 属性仍可能为 true，旧实现不清理连接，
     下次动作复用坏流、永远失败且不重连。现在超时/断流一律 MarkDisconnected，下次强制重建。
  3. **判定解析**：`RT,`（判定内容为空）此前会因无字符可判而误判 OK，现场若相机未配判定
     会把不良直接放行；现判失败。同时兼容 `RT, 00000000`（逗号后带空格）避免误判 NG。
- **PLC `PlcService`**：读写失败只 SetConnected(false) 不清理连接，同样会造成"假活复用"
  （Modbus 协议已错位还继续用旧 master）。新增 ResetConnection 在失败时清引用强制重建；
  EnsureConnected 快速路径补 `_master != null` 校验。
- **`ProductionCoordinator` 收尾时机（V1.7.0 引入的缺陷）**：Tcp 取图模式的图在触发时就
  同步读回（IsSnapped 已置位），但触发循环结束只判断"全部触发失败"，没有"到图即收尾"检查，
  导致 Tcp 模式要白等满 ImageWaitMs 才上报完成。补上该检查后到图立即收尾。
- **`SetState` 状态去重**：忙时到位轮询每 200ms 抢占失败都会打一条相同日志，会刷爆日志；
  按文本去重后只在状态真正切换时记录。
- **`ImageStore`**：删除未使用的 `JoinDirSegments` 死代码与孤立注释。

### 为什么这么改
- CRLF 残留 + 假活连接是两个"现场连不通"的头号隐患，都源于"读一行"边界与
  连接失效检测不彻底，模拟测试（假相机回 CRLF、假 PLC 回 Modbus）直接复现并验证修复。
- 判定内容为空判失败是质量红线：宁缺毋滥，绝不放行可能的不良。

### 优化点
- 新增模拟测试（`bin\Debug\SimServer.cs` / `CommTest.cs`，不入库）：假汇川 PLC（Modbus TCP
  03/06/16）+ 假基恩士 IV4（T1/T2/RT/BR + CRLF 回帧），覆盖相机类级、PLC 类级、
  Coordinator 全流程（到位→触发→判定→BR 取图→归档→Done 信号）共 36 项断言全过；
- 修复后行为：T2 连续触发稳定、BR 图可正常解码归档、PLC 读写稳定、
  Tcp 模式到图即收尾（不再等满超时）。

## V1.7.1（2026-08-12）标题栏相机灯顺序调整为从左到右 相机1..相机N

> 现场反馈：右上角连接指示灯排序反了——相机1 跑到最右边，不符合"相机1 在相机2 左边、
> 相机3 继续往相机2 右边排"的习惯。

### 改动范围
- **`MainForm.BuildCameraStatusLights`**：相机灯由"倒序 Add"改为"正序 Add"。
  Dock.Right 布局是"先 Add 的靠左、后 Add 的靠右"，正序循环得到 PLC 灯右侧依次排
  相机1、相机2、相机3…（之前倒序循环是相机3..相机2..相机1，相机1 在最右）；
- 同步更新 MainForm.cs 顶部 ASCII 布局图与 MainForm.Designer.cs 说明注释。

### 为什么这么改
- 用户习惯按编号从左到右递增查看相机状态，加新相机（相机3）就自然追加到最右，无需再调整。

### 优化点
- 顺序改动只影响标题栏指示灯，连接状态对应关系（按相机下标）不受影响；
- 设置热更（保存配置）后灯顺序同样按新规则重建。

## V1.7.0（2026-08-11）相机取图双通道：新增 TCP/BR 直读取图

> 需求背景：客户提出除了相机 FTP 推图，是否能用 TCP 直接从基恩士 IV4 读图。
> IV4 手册确认 TCP/IP 无协议通信支持"读取图像数据"指令（`BR,m`，读最新 24bit 位图），
> 响应为 `BR,nnnnnnnnnn,ddddddd,图像数据`。因尚未现场调试哪种方式更稳，本次**两种取图
> 方式并存**，配置可逐台切换，明天现场实测后定。

### 改动范围
- **`CameraConfig` 新增取图来源配置**：
  - `ImageSource`（"Ftp"/"Tcp"，默认 Ftp，大小写不敏感，其他值按 Ftp 兜底）——旧配置零迁移；
  - `ReadImageCommand`（指令名，默认 "BR"）+ `ReadImageMode`（BR 参数 m，默认 "1"）→ 拼成 `BR,1` 发送；
- **`KeyenceIV4Camera` 新增 `ReadImage()`**（V1.7.0 核心）：
  - 发送 `BR,m[CR]`，用**逐字节状态机**解析响应头（前缀 `BR,` → 长度字段 → 属性字段），
    再按长度字段**精确分块读取 N 字节**图像数据——图像数据是二进制可能含 0x0D/0x0A，
    绝不能按"读一行"解析；
  - 对响应做防御校验：长度必须 >0 且 ≤64MB、BMP 头 `BM` 轻校验（不符记日志，等现场实测确认格式）；
  - 与 `TriggerAndRead` 共用 `EnsureConnected` 短连接缓存：同一次流程里 T2（触发+判定）
    紧接 BR（取图）走同一条 TCP 连接，不挤占相机 2 路连接上限；
  - 新增结果载体 `ReadImageOutcome`（成功/字节/大小/属性/失败原因）；
- **`ProductionCoordinator` 接入 Tcp 取图分支**：
  - 触发循环：T2/T1 触发成功后，若该相机 `ImageSource=="Tcp"` 立即同步 `ReadImage()`，
    读回即置 `IsSnapped`（等效 FTP 模式"新图已到"），取图失败该点位按失败处理；
  - `FinishAll` 归档：TCP 模式走 `ImageStore.SaveImageBytes`（内存解码归档，不落 FTP 中转文件），
    FTP 模式保持文件归档；归档失败各自兜底；
  - `Start()`：Tcp 模式相机不注册 FTP 监听（避免历史文件被误当新图）；
  - `PendingCamera` 增加 `ImageBytes`/`ImageSource` 字段；
- **`ImageStore` 新增 `SaveImageBytes()`**：把 BR 读回的字节解码成 Bitmap 后按既有目录/文件名
  模板归档（期望完整 BMP；非标准格式解码失败返回 null 不落盘坏文件，由调用方报错）；
- **`SettingsForm`**：相机表格新增"取图方式"下拉列（Ftp/Tcp），现场保存即切换；
- **`docs/通讯接入.md`**：新增 BR 指令说明、两种取图来源、现场校准清单、版本 V1.7.0。

### 为什么这么改
- 手册确认 BR 可读最新图像后，Tcp 取图能砍掉"FTP 服务器落盘 + FileSystemWatcher 监听"两个中间层，
  链路更短、取帧节奏由上位机掌控；
- 但 BR 读的是"最新图像"，节拍快/有外部触发源时可能与本次触发拍摄串帧，且相机回传的格式
  （是否带 BMP 文件头、属性字段含义）只能现场实测确认——所以两种方式并存、逐台可切，不押宝；
- 状态机解析是必须的：图像数据是二进制，若按文本行读取会被像素里的 0x0D/0x0A 提前截断。

### 优化点
- 取图双通道：Ftp（成熟兜底）与 Tcp（短链路）现场实测二选一，或混合部署；
- BR 解析对长度/前缀/数据完整性做防御，异常一目了然（日志带已收字节数）；
- 配置零迁移：旧配置缺 ImageSource 自动按 Ftp 走，行为与 V1.6.0 完全一致。

## V1.6.0（2026-08-11）系统设置保存后即时生效（免重启）

> 现场反馈：每次在系统设置里改完配置都要"重启程序"才生效，体验差。
> 通讯本来就是后台心跳 + 断连自动重连，改完 IP 只需按新配置走一遍断开重连即可，
> 没必要重启。本次实现"保存即生效"。

### 改动范围
- **`MainForm` 新增热生效入口 `ApplyRuntimeConfig()`**（V1.6.0 核心）：
  - 保存后停掉旧服务层（监控器/协调器/PLC/各相机，Dispose 均有"限时抢锁 + 锁外强断网"兜底，
    即使后台连接任务正忙也不阻塞界面）；
  - 用新配置**全量重建**服务层与界面：PLC、相机服务（按新 IP/端口/指令/超时）、
    ImageStore（按新 FTP 目录/存图目录重建监听）、ProductionCoordinator（按新窗口总数/
    点位映射/相机台数）、ConnectionMonitor，然后重新订阅事件并启动流程；
  - 服务连接是惰性的（EnsureConnected 才建连），重建后由后台心跳/到位轮询按新 IP 自动重连，
    等效"按新配置断开重连"，完全复用既有断连重连机制；
  - `MainForm` 只保留主窗体状态（统计 _total/_ok/_ng、序列号、配方切换版本号），热更时透传给新协调器；
- **可重入重构**（支撑热更，构造与热更共用）：
  - `InitTitleBarRuntime` 拆出 `InitTitleBarFields`（字段/可见性/OK-NG 色块/相机灯，可重入）
    与 `BuildCameraStatusLights`（相机灯整套重建，先移除旧灯）；
  - `SubscribeEvents` 拆出 `SubscribeRuntimeEvents`（业务事件重新订阅，可重入）；
    FormClosing 释放服务的 lambda 引用字段、热更后自动指向新实例，只挂一次；
  - `BuildWindowGrid` 重建前先 Dispose 旧窗口，防止热更时旧窗口 PictureBox 图片句柄泄漏；
- **`SettingsForm`**：保存提示与 ToolTip 文案由"需重启程序生效"改为"保存后即时生效"，
  并说明设备会短暂断连后自动连回。

### 为什么这么改
- 客户痛点：改个 IP/端口就要重启，停产等待体验差；通讯层已有成熟的后台心跳 + 自动重连，
  新配置只需"断连重连"即可接上，重启完全多余；
- 全量重建而非局部热更：PLC/相机寄存器、FTP 目录、窗口行列、相机台数相互牵连，
  局部替换容易留旧引用（coordinator 持相机列表与窗口总数、ImageStore 持 FTP 监听），
  全量重建逻辑简单、不易出错；
- 副作用仅是"保存后设备短暂断连、几秒内自动连回"，心跳静默，现场可接受。

### 优化点
- 系统设置保存即生效，免重启、免停线，现场体验大幅提升；
- 任何配置（IP/端口/寄存器/相机台数/窗口行列/存图目录/颜色/开关）保存后立即落地；
- 服务层重建完全复用既有 Dispose/重连/锁机制，无新增网络 IO，不违反"UI 线程禁网络"铁律。

## V1.5.0（2026-08-11）标题栏 OK/NG 计数色块高亮

> 现场反馈：主界面 OK/NG 只显示一个带颜色的数字不够醒目，客户要求高亮。
> 本次把**标题栏**的 `OK:0` / `NG:0` 计数做成"实心彩色色块 + 白字"高亮；
> 窗口右下角的 OK/NG 徽标经现场确认保持原版样式（白底彩框彩字）不变。

### 改动范围
- **标题栏 OK/NG 计数高亮**：
  - `lblOk` / `lblNg` 由"11F 彩色文字"改为"**实心彩色色块 + 白色加粗字**"
    （OK=OkColor 绿底白字、NG=NgColor 红底白字，字号 11F→12F、加 6px 左右内边距）；
  - `MainForm.StyleCountBadge` 封装样式化逻辑，`InitTitleBarRuntime` 按配置应用；
    色块 AutoSize 保持 true，宽度随数字自动伸缩，`RelayoutTitleBar` 的紧凑重排与垂直居中照常；
  - 新增配置 `DisplayConfig.TitleOkNgHighlight`（默认 true），关闭回退旧版彩色文字。
- **`SettingsForm` 新增"OK/NG显示"配置行**：`OK/NG显示: [√标题栏高亮]`，
  读取/保存挂在 LoadFromConfig/OnSave，新增 ToolTip 说明；相机列表及以下控件整体下移 34px，
  窗体高度 598→632，界面 ASCII 布局图同步更新。
- **窗口右下角徽标保持原版**：`OkNgBadge`/`CameraDisplayControl` 未改动，
  仍为"白底 + 彩色边框 + 彩色文字"（52x24），OK 绿、NG 红，颜色走既有
  `OkColorName`/`NgColorName` 配置。

### 为什么这么改
- 客户担心"带颜色的数字不够醒目"：光靠颜色区分在远处/暗处不直观，**色块底 + 白字**
  对比度最强，标题栏的累计 OK/NG 一眼可辨；
- 标题栏计数是操作员最常看的汇总数据，优先高亮它；窗口单次结果仍用原版徽标，
  现场已习惯，不动。

### 优化点
- 标题栏 OK/NG 一眼可辨，满足客户"高亮显示"诉求；
- 开关可配（`TitleOkNgHighlight`），不需要色块可回退彩色文字；
- 无网络 IO，不违反"UI 线程禁网络"铁律。

## V1.4.0（2026-08-11）序列号显示框 + 主界面去掉点位标识 + 设置页布局空隙修正

> 现场反馈：① 主界面序列号栏不要写死"待扫码"，要一个固定宽度的显示框；
> ② 窗口上不要显示"点位 N"，点位归属只通过设置界面的"窗口/点位配置..."查询比对，
> 且点位默认跟随拍照触发顺序；③ 设置页"配置目录结构..."按钮与下方文件名模板行贴太近，
> 上下空隙要一致；④ 两对按钮间距要拉开、保存/取消靠右并对齐上方控件；
> ⑤ 各处的 "?" 问号标记不好看，全部删除，只保留控件鼠标悬停 ToolTip 提示。

### 改动范围
- **主界面序列号（需求①⑤）**：
  - 删除构造时 `_coordinator.LatestSerialNumber = "待扫码"` 的写死赋值；
  - 序列号拆成两部分：**标题 `lblSerialTitle`（"序列号:"，框外左侧）+ 显示框 `lblSerial`**：
    框固定宽度（`AutoSize=false`、宽 220、单线边框 FixedSingle、`TextAlign=MiddleLeft`），
    框内只放序列号值，没有则留空框（不写"待扫码"）；
  - 标题栏所有控件上下垂直居中：`RelayoutTitleBar` 统一按 `y=(48-控件高度)/2` 计算
    （按钮30/下拉27/显示框24/普通标签19 各自居中，视觉上全部居中对齐）；
  - 设计器初始文本同步改为空串；
- **主界面去掉点位标识（需求②）**：
  - `CameraDisplayControl` 移除右上角"点位 N"标签（`_stationLabel`、`SetStationNo`、`StationNo` 属性），
    控件只保留左上角窗口编号与右下角 OK/NG 徽标；
  - `MainForm.BuildWindowGrid` 移除 `SetStationNo` 调用；
  - 点位逻辑确认无改动：`ProductionCoordinator.ResolveStation` 仍优先取 `WindowStationMap` 映射、
    缺/越界兜底"点位=窗口编号"，即**第一个拍照位=窗口1=点位1**，环行走位；配置调整后按调整后的来；
  - **顺带修复潜在 bug**：`_nextWindowIndex` 初始值从 0 改为 1，避免首个拍照位点位落在 0（窗口0 不存在）
    导致第一次检测的窗口/点位错位；
- **设置页布局（需求③④）**：
  - 文件名模板行（`lblFile`/`txtFileNameTpl`）及其后各行整体下移 12px，
    使"配置目录结构..."按钮下方空隙与上方一致（12px），不再紧贴；
  - 按钮行下移 12px 对齐新布局，窗体高度 586→598；
  - "添加一台"(20) 与 "删除选中"(150) 拉开到 30px；"保存"(490)/"取消"(610) 移到最右侧，
    取消右边缘与上方控件右缘对齐（x=700），保存/取消间隙同为 30px；
- **删除问号标记（需求⑤）**：
  - 删除 `Controls/TipMarker.cs` 文件及 csproj 引用；
  - `SettingsForm` / `DirTreeEditForm` 移除 `TipMarker.AttachAll` 调用、`SettingsForm` 移除
    `TipMarker.Sync` 调用；动态"当前目录结构"提示仍挂在按钮的 ToolTip 上（悬停即可看）；
  - ToolTip 悬停提示本身全部保留（V1.3.1 的交互不变），只是不再显示 "?" 小图标；
- 同步更新 `SettingsForm.cs` / `SettingsForm.Designer.cs` 顶部 ASCII 布局图注释。

### 为什么这么改
- 序列号由扫码枪驱动（`ScannerService` 预留），上位机侧当前不写死默认值，空态就该显示空白；
  固定宽度 + 边框让"显示框"看得见、前后字段位置稳定；
- 点位是"存图归属"概念，现场通过设置界面查询比对即可，窗口上显示"点位 N"属于冗余信息；
- 问号标记现场反馈"不好看"：悬停提示（ToolTip）已足够，多余小图标反而显得杂乱，直接删除；
- 按钮拉开间距 + 保存/取消靠右对齐，视觉更整齐、不再拥挤。

### 优化点
- 主界面更干净：序列号有固定显示框、窗口只剩编号+结果徽标；
- 设置页控件不再挤在一起，两对按钮间距一致、右缘对齐；
- 界面不再有问号小图标，只剩标准的悬停气泡提示，更简洁；
- 首个拍照位点位从 0 修正为 1，与现场"第一个拍照位=点位1"的预期一致。

## V1.3.2（2026-08-11）ToolTip 悬停提示增加"?"问号标识

> 现场反馈：V1.3.1 把说明文字全改成了悬停气泡，但气泡平时不可见，操作员不知道
> "哪个控件悬停有说明"，等于提示没生效。本次给所有带 ToolTip 的控件统一补一个
> 蓝色 "?" 小标记——问号是 Windows 帮助/提示的标准符号，一看就知道"这里悬停有说明"。

### 改动范围
- 新增 `Controls/TipMarker.cs`：静态辅助类，自动遍历窗体里所有已挂 ToolTip 的控件，
  在控件旁边找一个不越界、不与其它控件重叠的空位（依次试 右侧→左侧→上方→下方）
  放一个 16×16 蓝色 "?" 问号标记，悬停问号同样显示对应说明。
- `DirTreeEditForm`：构造末尾调用 `TipMarker.AttachAll(this, tip)`。
- `SettingsForm`：构造末尾调用 `TipMarker.AttachAll(this, tip)`；
  因 "配置目录结构..." 按钮的提示文本是**动态**的（含当前目录结构），
  `RefreshDirPreview` 每次更新后追加 `TipMarker.Sync` 让问号标记同步新文本。
- `DirTreeEditForm` 预览区：`tvPreview` 在 GroupBox 内部四周都放不下问号，
  故预览说明同时挂到外层 `gbPreview`（悬停预览区即显示），问号标记放 GroupBox 右侧。

### 为什么这么改
- 纯 ToolTip 是"悬停才出现"的隐藏信息，无任何常驻线索，对操作员不可发现；
  "?" 是全球通用的帮助符号（系统设置、VS 选项对话框等均用），符合 Windows 惯例，
  在紧凑界面里是最轻量、最不占地方的可视提示。
- 用统一辅助类而不是逐个手工加 Label：以后新增 ToolTip 或新窗体，一行 `AttachAll` 自动覆盖，
  位置自动避让，不会挤坏现有布局。

### 优化点
- 界面不增新常驻文字、不加图片资源，仅一个 16px 问号字符，风格统一、离线可编译；
- 位置算法自动避让（不越界、不与控件重叠），按钮密集排布时也能找到空位。

## V1.3.1（2026-08-11）界面说明改为 ToolTip 气泡提示

> 现场反馈：设置页和目录配置页的灰色说明文字（lblNote / lblDirPreview / lblHelp / lblPointsHelp）
> 常驻界面、占地方又不够优雅。改为行业惯例的悬停气泡：鼠标悬停相关按钮/标题/输入框即出提示。

### 改动范围
- 删除 4 个常驻说明标签，全部改为 `ToolTip` 气泡：
  - `DirTreeEditForm.lblNote`（占位符说明）→ 悬停文件名模板/层级输入框/占位符下拉与插入按钮显示；
  - `SettingsForm.lblHelp`（模板占位符速查）→ 悬停文件名模板输入框显示；
  - `SettingsForm.lblPointsHelp`（点位操作说明）→ 悬停"窗口/点位配置..."按钮显示；
  - `SettingsForm.lblDirPreview`（当前目录结构预览）→ 悬停"配置目录结构..."按钮显示，且内容**动态**：
    每次打开/关闭目录配置对话框都会把最新目录结构刷进该按钮的气泡（`RefreshDirPreview`）。
- 悬停参数按 Windows 工具提示标准：`InitialDelay=500ms`（行业惯例 0.4~0.7s）、`ReshowDelay=100ms`、
  `AutoPopDelay=8000ms`（停留 8 秒自动消失，不挡界面）、`ShowAlways=true`（窗体未激活也显示）。
- 布局顺带优化：`DirTreeEditForm` 预览区上移、窗体高度缩小 60px 更紧凑；
  `SettingsForm` 文件名模板输入框加宽到窗体右缘（与根目录框对齐）。

### 为什么这么改
- 说明文字是"低频查看"信息，常驻界面反而挤占视觉空间、显得凌乱；气泡提示在需要时悬停即出，
  界面更干净，信息又都在手边，是 Windows/Web 桌面软件的通行做法。

### 优化点
- 界面上少 3 行灰字、对话框矮一截，主流程控件更醒目；
- 动态"当前目录结构"不丢失：悬停目录配置按钮即可核对，改完立即刷新。

### 修复（V1.3.1 回归）
- 点击"系统设置"闪退"值不能为 null。参数名:cont"：两个窗体的 `components` 容器字段
  此前从未初始化（null），而 `new ToolTip(this.components)` 的构造函数对 null 容器会抛
  `ArgumentNullException`。修复：在 `InitializeComponent` 开头先 `this.components = new Container();`
  （VS 设计器标准写法），ToolTip 挂到容器上统一自动释放。已验证 SettingsForm / DirTreeEditForm
  均能正常打开。

## V1.3.0（2026-08-11）窗口存图点位可视化配置 + 目录配置交互优化

> 按现场反馈：①目录结构配置页补"插入到下方"；②文件名说明文字自动换行；③存图点位默认=显示窗口编号，
> 并支持可视化自定义与窗口位置互换。

### 改动范围
- **窗口→存图点位映射**（核心）：`DisplayConfig` 新增 `WindowStationMap`（第 i+1 号窗口的存图点位）。
  默认点位=窗口编号，存图文件名 `{点位}` 改用该映射；相机不再配置点位，
  `CameraConfig` 移除旧 `StationNo` 字段（项目未上线，不保留旧配置兼容）。
- **可视化配置对话框**（新窗体 `Views/WindowPointForm`）：格子矩阵与主界面布局一致，每格显示
  "固定编号 + 存图点位"。点格子选中 → "编辑点位"改存图号（例如 1 号窗口存图名改成 2.png）；
  "交换位置"把两个窗口内容互换而编号固定（不管谁被换到第一格，永远是 1 号）；
  "恢复默认"一键回到"点位=窗口编号"。改动先在本地副本上做，点"确定"才写回配置。
- **主界面窗口右上角**新增"点位 N"标注：现场直接看到该窗口的图将来存成"几.png"
  （窗口固定编号仍显示在左上角，二者解耦）。
- **设置窗体**：相机表格去掉"点位号"列（点位统一由窗口映射管理），新增"窗口/点位配置…"入口，布局下移。
- **目录结构配置页**：目录层级操作按钮区新增"插入到下方"（与"插入到上方"对称，插入到选中层级之后）；
  文件名规则下方的占位符说明改为固定宽自动换行，长文本不再右侧显示不全。
- **点位映射对齐**：`ConfigStore` 加载/保存时自动把 `WindowStationMap` 对齐到窗口总数（Rows×Columns），
  缺的补"点位=窗口编号"、多的截断，保证运行时取 map 永不越界。
- **修复**："窗口/点位配置…"打开时行列数误用已保存的 `_cfg.Display.Rows/Columns`，
  用户刚在设置页改完行/列还没保存就打开点位配置，格子矩阵仍是旧窗口数（与主界面不一致）。
  现改为取界面 `nud` 上的最新值，改行列后再配点位所见即所得。

### 为什么这么改
- 现场要求"点位号默认=对应主页显示窗口的编号"，但每个窗口的点位要能自定义、且要可视化操作；
  用户还希望窗口位置能调（编号固定跟随格子）。此前点位藏在相机表格的数字里、与显示窗口无关，
  既不直观也不符合"点位=窗口"的现场直觉。
- 项目未上线：顺手清掉全部旧配置兼容/迁移逻辑（旧 `camera` 单对象、`subDirTemplate`、
  `subDirByDate`/`filePrefix` 旧命名、相机 `StationNo`），配置缺省一律用模型默认值。

### 优化点
- 可视化格子矩阵"所见即所得"：改点位/交换/恢复默认即时刷新文字，选中格与待交换格浅黄高亮；
- 配置模型更干净：无任何迁移分支，`ImageStore` 直接用 `SubDirs` 建目录，缺省兜底明确；
- 对话框"编辑副本 + 确定才写回"，误操作取消不污染配置。

## V1.2.0（2026-08-11）图片存储目录结构可视化配置

> 按现场需求把存图目录从"单行字符串模板"升级为"逐级可视化配置"。

### 改动范围
- **数据模型**：`ImageConfig` 新增 `SubDirs` 目录层级列表（每级一个目录名或生成规则），
  新增 `{年月日}` 占位符（输出"2026年08月11日"这样的【一个】目录名，非年/月/日三级）；
  `FileNameTemplate` 默认改为 `{点位}`（点位号进文件名，不再建目录）。
- **可视化配置对话框**（新窗体 `Views/DirTreeEditForm`）：
  - 逐级列表编辑目录层级，支持添加/插入/删除/上移/下移；
  - 每级名字/规则就地改，占位符下拉框一键插入；
  - 文件名规则单独配置；底部实时预览 OK/NG 两条完整落盘路径；
  - 保存根目录可浏览选择。
- **运行时落盘**：`ImageStore.SaveImage` 改按 `SubDirs` 逐级渲染建目录；列表为空时回退旧字符串模板。
- **配置兼容**：`ConfigStore.Load` 自动把旧 `subDirTemplate` 字符串拆解成 `SubDirs` 列表，升级无需重配。

### 为什么这么改
- 现场要求的最终结构是 `根目录/2026年08月11日/SN号/OK|NG/点位号.png`，目录只有年月日/SN/OK|NG 三级，
  点位号是文件名；旧"年/月/日 三级目录 + 模板字符串"无法直观表达，改可视化逐级编辑后现场直接改界面即可。

### 优化点
- 目录层级"所见即所得"：配置区下方是**实时目录树预览**（TreeView），按示例数据（今天日期/SN-0001/点位1）
  把每一级渲染成文件夹节点、末尾挂文件名，`{OKNG}` 展开成 OK/NG 两个并列子树，一眼看清最终落盘结构；
  文本输入走 300ms 防抖刷新，打字不卡 UI、不每键重建树。
- 兼容旧配置零迁移成本，老模板自动展开成新列表。

## V1.1.0（2026-08-11）相机 + PLC 命令中心正式版

> 本版为从零搭建到当前的完整交付（期间未发布过，所有改动合并为一个版本）。

### 改动范围
- **工程骨架**：.NET Framework 4.7.2 WinForms 分层结构（Views/Controls/Services/Models/Utils），
  第三方库 Newtonsoft.Json、NModbus 拷入 `libs/` 直接引用，离线可编译；`docs/通讯接入.md` 沉淀
  相机/PLC 对接流程与寄存器表。
- **通讯接入**：
  - PLC（汇川）Modbus TCP 保持寄存器读写；相机（基恩士 IV4）TCP 无协议触发 + FTP 推图；
  - 相机真实指令：`T1` 触发、`T2` 触发+回读判定、`RT` 读结果，判定以相机回帧为准（全 0 → OK，
    含非合格位 → NG），`ReadResultFromCamera` 开关可退回"图到即 OK"旧逻辑；
  - **连接自愈 + 心跳**（ConnectionMonitor）：后台心跳 + 断连识别（边沿日志）+ 节流静默自动重连；
    无回调式连接根治 `EndConnect` NRE；Dispose 限时抢锁防关窗卡死。
- **多相机动态配置**：`AppConfig.Camera` → `Cameras` 列表，配置列几台就建几台相机服务与指示灯
  （●PLC ●相机1 ●相机2…），一次到位对每台依次触发，各相机独立点位号/FTP 目录，图到齐整体收尾。
- **存图目录模板化**：`SubDirTemplate`/`FileNameTemplate` 支持 `{年}{月}{日}{SN}{OKNG}{点位}{时间}`
  占位符，默认结构 = 根目录/年/月/日/SN/OK|NG/点位号.png；SettingsForm 里可视化配置。
- **配方**：产品型号 = 配方（前缀 Label + 配方下拉框合一）；切换在后台 `Task.Run` 异步下发 PLC
  （带版本号丢弃过期结果），成功绿色/失败红色状态提示；UI 线程零网络 IO。
- **界面**：MainForm / SettingsForm 控件全部 Designer 化（静态布局进 .Designer.cs，动态部分
  ——相机灯、窗口矩阵内容——运行时代码生成）；标题栏信息字段按 ShowXxx 开关紧凑重排；
  窗口矩阵 TableLayoutPanel 百分比等分（默认 4×7）；OK=绿 / NG=红。
- **配置兼容**：`ConfigStore.Load` 自动把旧 json 单对象 `camera` 迁移为 `cameras[0]`，升级无需重配。

### 为什么这么改
- **通讯编排与界面解耦**：ProductionCoordinator 只发业务事件、MainForm 只订阅刷新，换界面可复用业务；
- **卡顿根治**：轮询/连接/读写全部移出 UI 线程 + BeginConnect 强制超时，对不可达 IP 界面仍即时响应；
- **现场节拍**：判定以相机回帧为准而不是"图到了就当 OK"；一次到位触发多台相机，点位号不再随窗口漂移；
- **可维护**：静态控件可视化拖拽（Designer）、动态数量由配置驱动，字段/窗口/相机台数 JSON 即配即生效。

### 优化点
- UI 线程不做任何网络 IO（红线），连接超时/失败降频重试 + 日志去重；
- 多相机每台独立指示灯、独立存图目录，断连一眼分清是哪台；
- 配方切换秒回 + 防过期覆盖，实测 PLC 不可达（2s 超时）下 UI 仍即时响应；
- 设计器改造后标题栏布局与改造前逐像素一致（产品型号 272 / 下拉框 363 / 序列号 555 / 总数 712 /
  OK 786 / NG 852 / 系统设置 952，PLC 灯 1456 / 相机灯 1552 / 状态栏 274,902）。
