# CommandCenter

相机 + PLC 现场命令中心上位机（.NET Framework 4.7.2 WinForms）。

上位机作为命令中心，按"PLC 告知相机到位 → 上位机触发相机拍照 → 取像完成保存图片 → 通知 PLC 完成 → PLC 走到下一点位"的节奏运转，同时负责操作员交互（切换配方、查看来料）。

## 技术栈

- .NET Framework 4.7.2 WinForms（C# 7.3）
- NModbus 3.0.83 —— 汇川 PLC Modbus TCP 通讯（本地 `libs/` 引用，离线可编译）
- Newtonsoft.Json —— 配置 / 配方序列化（本地 `libs/` 引用）
- 相机：基恩士 IV4-500CA，TCP/IP 无协议通信触发拍摄（T1/T2/RT/BR）+ 取图双通道
  （FTP 推图 / TCP-BR 直读取图，逐台相机可选，见 `CameraConfig.ImageSource`）

## 目录结构

```
CommandCenter/
├── Views/           界面（MainForm + MainForm.Designer / SettingsForm + SettingsForm.Designer /
│                    DirTreeEditForm 目录结构可视化配置 / WindowPointForm 窗口点位可视化配置 /
│                    LoginForm 账号登录（管理员+开发者） / DevTestForm 功能测试（开发者专用））
│                    ※ MainForm/SettingsForm 静态布局（标题栏、状态栏、矩阵容器、设置表控件）
│                      在对应 .Designer.cs 里可视化维护；动态部分（相机灯、窗口矩阵内容）
│                      在业务文件里运行时生成
├── Controls/        自绘/辅助控件（CameraDisplayControl / OkNgBadge）
├── Services/        通讯与业务编排（PlcService / ConnectionMonitor / KeyenceIV4Camera /
│                    ImageStore / ProductionCoordinator / RecipeManager /
│                    ScannerService / ScannerTcpService）
├── Models/          配置模型（AppConfig / RecipeConfig / WindowData）
├── Utils/           ConfigStore（JSON 读写）/ LogHelper（按天日志）
├── libs/            本地引用的第三方 DLL（NModbus / Newtonsoft.Json）
├── docs/            通讯接入说明
└── Config/          运行时配置（appconfig.json / recipes.json，不入库）
```

## 构建

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  CommandCenter/CommandCenter.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
```

产物：`CommandCenter\bin\Debug\CommandCenter.exe`。

## 通讯对接

相机（TCP 触发 + 取图双通道：FTP 推图 / TCP-BR 直读）与 PLC（Modbus TCP 保持寄存器）的
详细握手与寄存器表，见 **`docs/通讯接入.md`**。

## 可配置项

所有参数集中在运行时生成的 `Config/appconfig.json`：

- 相机：**两台相机列表**（V1.9.8 现场 IP 已写死为默认：相机1 `19.87.6.212`、相机2 `19.87.6.213`，
  每台另含触发端口、FTP上传目录、触发指令、超时、取图方式；配置文件缺失或留空时自动用这两台），
  一次"到位"对所有相机**并行触发**拍照（V1.8.3 起，总耗时 ≈ 最慢一台相机，节拍快不漏检）。
  **取图来源双通道（V1.7.0）**，每台相机在 `ImageSource` 里独立二选一：
  - `Ftp`（默认，成熟）：相机作 FTP 客户端推图到上位机目录，上位机 FileSystemWatcher 监听新图；
  - `Tcp`：触发成功后同一连接紧接发 `BR,m`（指令/参数见 `ReadImageCommand`/`ReadImageMode`，
    默认 `BR`/`1`）同步读回相机最新 24bit 位图，免 FTP 服务器落盘中转；
    Tcp 模式该相机不注册 FTP 监听，避免历史文件被误当新图。
- PLC：IP、端口、站号、到位/开始/完成/配方/计数寄存器 D 地址
- 显示：窗口行数 × 列数、标题栏字段开关、OK/NG 颜色名、**窗口→存图点位映射**
  （默认点位=窗口编号，可在设置窗体"窗口/点位配置..."里可视化自定义/交换窗口位置），
  **标题栏 OK/NG 色块高亮开关**（`TitleOkNgHighlight`，在设置窗体"OK/NG显示"行配置），
  **标题栏"系统设置"按钮显隐开关**（`ShowSettingsButton`，默认 true；生产现场写 false
  可隐藏按钮防误操作，布局自动紧凑，改回只需改 json）
- 图像：保存根目录、**存图目录结构**（可视化逐级配置，默认 年月日/SN号/OK|NG，点位号进文件名，
  在设置窗体点"配置目录结构..."编辑）、FTP 监听目录。
  ⚠️ **FTP 取图依赖上位机自行部署 FTP 服务器**：程序只监听目录、不自带 FTP 服务，
  没装 FTP 服务器则 Ftp 模式的图永远不到（会等图超时记取像失败）；Tcp 取图模式无此依赖。
  **存图重名防覆盖（V1.8.3）**：同 SN/判定目录里同点位二次拍照自动追加 `_2/_3…` 序号，不丢历史图。
- 扫码枪：**多台扫码枪列表**（每台：启用开关、方式 串口/以太网无协议 Tcp、串口参数、
  IP/端口、**触发指令**），设置窗体"扫码枪列表"表格可视化增删改；任何一台扫到的条码更新当前序列号
  （按项目约定：停止位存 "1"/"15"/"2"，校验位存标准枚举名）。表格**按"方式"自动显隐列**
  （V1.12.2）：全 Tcp 只显示 IP/端口/触发指令，全 Serial 只显示串口参数，混用则全列显示。
  **触发指令（V1.12.1，仅 Tcp 模式）**：基恩士 SR 无协议模式下上位机连上后需先发一条
  `TriggerCommand`（默认 `LON`，发送时自动补 `\r\n` 帧结束符）扫码枪才进入读码状态；
  每次连接/断线重连后自动发送，留空则不发送（对应扫码枪设成"上电自动读码"模式）。
  默认地址为现场实测 `19.87.6.100:9004`。
- 管理员：**登录开关**（`Security.AdminEnabled`，默认 true）、用户名（`AdminUser`，默认
  `admin`）、密码 **SHA-256 哈希**（`AdminPasswordHash`，不存明文；默认出厂密码 `admin123`）。
  启用后点"系统设置"每次都要登录管理员账号才放行；改密码在**登录对话框**里完成（登录界面
  自带"修改密码"入口：验证原密码 → 新密码两次一致且 ≥6 位 → 保存即时生效），系统设置窗体
  保持纯业务配置不掺账号管理。另提供**"记住密码"**勾选框：勾选后登录成功把用户名+密码用
  Windows DPAPI 加密存到 `%LOCALAPPDATA%\CommandCenter\remembered_login.dat`（绑定当前
  Windows 用户，拷走文件也解不开），下次打开登录框自动回填、点登录即可；取消勾选或改密码
  时自动清理/同步
- **开发者账号（V1.12.0，功能测试登录）**：`Security.DevEnabled`（默认 true）、
  `DevUser`（默认 `dev`）、`DevPasswordHash`（默认出厂密码 `dev123` 的哈希）。
  用开发者账号登录"系统设置"按钮后，进的是**功能测试窗体 `DevTestForm`** 而不是系统设置：
  只做 PLC/相机/扫码枪通讯手动验证（相机 T1 仅触发 / T2 触发+读判定、扫码枪读码实时展示、
  PLC 读地址/写地址测试 + 协议偏移量配置 + 到位/触发/完成/配方信号），**复用主窗体已建好的
  连接、不新建连接、不产生配置改动**；开发者密码不支持在登录框里改（改密码面板仅服务管理员），
  改哈希需手动算后写配置。
  典型场景：PLC 业务逻辑未写完时，用 dev/dev123 单独验证相机、扫码枪与 PLC 通讯链路是否通。

换机型/换配方现场只改 JSON，复用同一份程序。