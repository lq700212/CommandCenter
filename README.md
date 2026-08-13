# CommandCenter

相机 + PLC 现场命令中心上位机（.NET Framework 4.7.2 WinForms）。

上位机作为命令中心，按"机器人带扫码枪到位 → 扫得产品 SN → 机器人带相机到位 → 上位机触发拍照 →
取像完成保存图片 → 显示在对应点位窗口并通知 PLC 完成 → PLC 走下一工位"的两阶段节奏运转，
同时负责操作员交互（切换配方、查看来料）。

> 完整业务流与寄存器占位表见 **`docs/通讯接入.md` §3.3/§3.2**（当前 PLC 寄存地址为占位待定稿，
> 与现场程序定稿后替换；"扫码到位"占位 D99 已代码接入，只改 json 数值即可）。V1.12.16 起
> `ProductionCoordinator` 已实现**两阶段**状态机：先等"扫码到位"扫得 SN，再等"相机到位"拍照，
> 保证"先有 SN、后拍照"、存图目录带本次 SN。

## 技术栈

- .NET Framework 4.7.2 WinForms（C# 7.3）
- NModbus 3.0.83 —— 汇川 PLC Modbus TCP 通讯（本地 `libs/` 引用，离线可编译）
- Newtonsoft.Json —— 配置 / 配方序列化（本地 `libs/` 引用）
- 相机：基恩士 IV4-500CA，TCP/IP 无协议通信触发拍摄（T1/T2/RT，V1.12.18 加 PW 切程序/OF 输出格式），
  取图走 FTP 推图（`0000.jpeg`+`0000.iv4p` 双文件配对归档，V1.12.18 起唯一取图方式，Tcp/BR 已下线）

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
├── docs/            文档中心（使用说明 / 通讯接入 / 现场设备IP清单 / 上位机通讯封装范式）
└── Config/          运行时配置（appconfig.json / recipes.json，不入库）
```

## 构建

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  CommandCenter/CommandCenter.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
```

产物：`CommandCenter\bin\Debug\CommandCenter.exe`。

## 通讯对接

相机（TCP 触发 + FTP 推图双文件归档，V1.12.18 起唯一取图方式）与 PLC（Modbus TCP 保持寄存器）的
详细握手与寄存器表，见 **`docs/通讯接入.md`**。

## 文档中心

| 文档 | 用途 |
| --- | --- |
| `docs/使用说明.md` | **用户操作手册**：启动、主界面、日常操作、账号、系统设置、功能测试、常见问题排查 |
| `docs/通讯接入.md` | **通讯设计**：相机/PLC/扫码枪协议、寄存器表、指令格式、两阶段业务流程、版本演进 |
| `docs/现场设备IP清单.md` | **设备 IP 统计**：网络拓扑、设备清单、扫码枪触发指令细节、与配置对照 |
| `docs/上位机通讯封装范式.md` | **技术总结**：连接管理/心跳重连/后台轮询与 UI 解耦等通讯架构范式（跨项目可复用） |

## 可配置项

所有参数集中在运行时生成的 `Config/appconfig.json`：

- 相机：**两台相机列表**（V1.9.8 现场 IP 已写死为默认：相机1 `19.87.6.212`、相机2 `19.87.6.213`，
   每台另含触发端口、FTP上传目录、触发指令、超时、取图方式、**程序号**；配置文件缺失或留空时自动用这两台），
   一次"到位"对所有相机**并行触发**拍照（V1.8.3 起，总耗时 ≈ 最慢一台相机，节拍快不漏检）。
  **取图方式（V1.12.18 起仅 `Ftp`，Tcp 下线）**：相机作 FTP 客户端推图（目录由相机侧配置），
  上位机 FileSystemWatcher 监听新图；Tcp/BR 直读代码保留仅作旧配置兼容，设置窗体不再提供该选项。
  **程序号（V1.12.18）**：`ProgramNo`（默认 -1=不切换；**>=0 都会切换，0 也是合法程序号**），
  触发前先发 `PW,nnn` 切到该点位相机程序再触发、切失败即中止——一台相机拍多个点位时每个点位
  一个程序号，靠它区分点位。
  存图文件名默认追加时间戳后缀（`FileTimestampSuffix`）防同点位重复触发覆盖。
- PLC（V1.12.11 起从站模式）：监听绑定 IP（`0.0.0.0`=所有网卡）、监听端口、从站 UnitId、
  到位/开始/完成/配方/配方标志/计数寄存器 D 地址。现场 PLC(汇川)做 Modbus 主站，上位机做从站监听
  502 等主站连入读写寄存器区；配方下发用 D108 标志位握手（上位机写自己区+PLC 轮询拉取+写 0 回执）。
- 显示：窗口行数 × 列数、标题栏字段开关、OK/NG 颜色名、**窗口→存图点位映射**
  （默认点位=窗口编号，可在设置窗体"窗口/点位配置..."里可视化自定义/交换窗口位置），
  **标题栏 OK/NG 色块高亮开关**（`TitleOkNgHighlight`，在设置窗体"OK/NG显示"行配置），
  **标题栏"系统设置"按钮显隐开关**（`ShowSettingsButton`，默认 true；生产现场写 false
  可隐藏按钮防误操作，布局自动紧凑，改回只需改 json）
- 图像：保存根目录、**存图目录结构**（可视化逐级配置，默认 年月日/SN号/OK|NG，点位号进文件名，
  在设置窗体点"配置目录结构..."编辑）、FTP 监听目录。
  ⚠️ **FTP 取图上位机零配置（现场确认）**：FTP 推图由基恩士工程师在相机软件（IV Navigator）里
  配置好，上位机**无需部署/安装任何 FTP 服务器**（FileZilla 等一概不用），程序只监听推图落盘目录
  （FileSystemWatcher）；图不到先联系基恩士核对相机侧推图。
  **双文件归档（V1.12.18）**：基恩士每次拍照生成 `0000.jpeg`+`0000.iv4p` 两个文件，程序按扩展名
  配对、都到齐才算图到位，**归档到正式目录后删除 FTP 源文件**（FTP 目录只当中转暂存区，处理即删）；
  iv4p 为基恩士私有复盘格式、原样复制保留，jpeg 为显示/归档主体。
  **存图重名防覆盖（V1.8.3）**：同 SN/判定目录里同点位二次拍照自动追加 `_2/_3…` 序号；
  **V1.12.18 起文件名另加时间戳后缀**（`FileTimestampSuffix`）双保险，不丢历史图。
- 扫码枪：**多台扫码枪列表**（每台：启用开关、方式 串口/以太网无协议 Tcp、串口参数、
  IP/端口、**触发指令**），设置窗体"扫码枪列表"可视化增删改；任何一台扫到的条码更新当前序列号
  （按项目约定：停止位存 "1"/"15"/"2"，校验位存标准枚举名）。**V1.12.8 起 TCP 与串口拆为两张表**
  （`gridScannersTcp` / `gridScannersSerial`）：TCP 表只配 IP/端口/触发指令，串口表只配串口名/波特率/
  停止位/校验位，方式由"所在的表"决定、不再有"方式"下拉列——解决同一张表行间切 Tcp/Serial 导致
  整列显隐混乱的 bug。两张表各带一行默认值（TCP=19.87.6.100:9004/LON，串口=COM3/115200/1/None），
  打开即可直接改。**V1.12.9 起 TCP 表默认行与"添加一台"默认勾选"启用"**（与代码默认接入的
  以太网扫码枪一致——主程序对 Mode=Tcp 建 `ScannerTcpService`，连上即发 `LON` 自动收码）；
  串口表默认行不勾选（代码默认不用串口枪，要接入再手动勾）。内容超出窗体高度时右侧自动出竖滚动条，
  保存/取消固定底部不随滚动。
  **触发指令（V1.12.1，仅 Tcp 模式）**：基恩士 SR 无协议模式下上位机连上后需先发一条
  `TriggerCommand`（默认 `LON`，发送时自动补 `\r\n` 帧结束符）扫码枪才进入读码状态；
  每次连接/断线重连后自动发送，留空则不发送（对应扫码枪设成"上电自动读码"模式）。
  默认地址为现场实测 `19.87.6.100:9004`。
- 管理员：**登录开关**（`Security.AdminEnabled`，默认 true）、用户名（`AdminUser`，默认
  `admin`）、密码 **SHA-256 哈希**（`AdminPasswordHash`，不存明文；默认出厂密码 `admin123`）。
  启用后点"系统设置"每次都要登录管理员账号才放行；改密码在**登录对话框**里完成（登录界面
  自带"修改密码"入口：验证原密码 → 新密码两次一致且 ≥6 位 → 保存即时生效），系统设置窗体
  保持纯业务配置不掺账号管理。另提供**"记住密码"**勾选框：勾选后登录成功把用户名+密码用
   Windows DPAPI 加密存到 `%LOCALAPPDATA%\CommandCenter\`（绑定当前
   Windows 用户，拷走文件也解不开），下次打开登录框自动回填、点登录即可；取消勾选或改密码
   时自动清理/同步。**V1.12.21：开发者账号也可记住密码**（存 `remembered_login_dev.dat`，
   管理员存 `remembered_login.dat`），且两角色记录**互斥**——登录任一方成功即清除另一方的
   记住文件，避免跨角色回填残留（管理员登录后不再保留 dev 免密入口，反之亦然）。
- **开发者账号（V1.12.0，功能测试登录）**：`Security.DevEnabled`（默认 true）、
  `DevUser`（默认 `dev`）、`DevPasswordHash`（默认出厂密码 `dev123` 的哈希）。
  用开发者账号登录"系统设置"按钮后，进的是**功能测试窗体 `DevTestForm`** 而不是系统设置：
  只做 PLC/相机/扫码枪通讯手动验证（相机 T1 仅触发 / T2 触发+读判定、扫码枪读码实时展示、
  PLC 读地址/写地址测试 + 协议偏移量配置 + 到位/触发/完成/配方信号），**复用主窗体已建好的
  连接、不新建连接、不产生配置改动**；开发者密码不支持在登录框里改（改密码面板仅服务管理员），
  改哈希需手动算后写配置。
  典型场景：PLC 业务逻辑未写完时，用 dev/dev123 单独验证相机、扫码枪与 PLC 通讯链路是否通。

换机型/换配方现场只改 JSON，复用同一份程序。