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
- **UI 线程禁做网络 IO（V1.0.1 血泪）**：轮询/连接/读写 PLC 与相机一律放后台线程（`System.Threading.Timer`），TCP 连接必须 `BeginConnect + WaitOne` 强制超时。禁止在 UI 线程同步 `TcpClient.Connect` 或 `ReadHoldingRegisters`——对不可达 IP 会冻结整个界面（表现为"点按钮半天才响应"）。
- **显示窗口矩阵用 TableLayoutPanel 百分比等分**：窗口数量由 `display.rows/columns` 配置，所有窗口尺寸由容器等分自动保持一致，禁止写死像素布局。
- **PLC 寄存器约定**：配置里一律存 **D 地址**（NModbus `ReadHoldingRegisters(start,…)` 的 start 即 D 地址，无需 +40001）。改动 PLC 或相机通讯必须同步 `docs/通讯接入.md`。

## 关键文件导航

| 文件 | 作用 |
| --- | --- |
| `CommandCenter/Views/MainForm.cs` | 主窗体：标题栏 + 窗口矩阵 + 事件接线 |
| `CommandCenter/Services/ProductionCoordinator.cs` | 生产流程编排（到位→触发→等图→上报循环），业务核心 |
| `CommandCenter/Services/ConnectionMonitor.cs` | 连接健康监控：后台心跳 + 断连自动重连 + 边沿日志（对齐 AgingTestSystem） |
| `CommandCenter/Services/PlcService.cs` | 汇川 PLC Modbus TCP 读写（NModbus 3.0.83） |
| `CommandCenter/Services/KeyenceIV4Camera.cs` | 基恩士 IV4 TCP 无协议触发 + 读取判定（T1/T2/RT指令） |
| `CommandCenter/Services/ImageStore.cs` | 相机 FTP 推图监听 + 图片归档 |
| `CommandCenter/Models/AppConfig.cs` | 全部可配置项模型（相机/PLC/显示/图像/扫码） |
| `CommandCenter/Utils/ConfigStore.cs` | appconfig.json 读写（小驼峰序列化） |
| `CommandCenter/Controls/CameraDisplayControl.cs` | 相机显示窗 + 右下角自绘 OK/NG 徽标（主界面不显示点位标识，点位只走设置界面查询） |
| `CommandCenter/Views/DirTreeEditForm.cs` | 图片存储目录结构可视化配置（逐级目录 + 文件名规则 + 实时预览） |
| `CommandCenter/Views/WindowPointForm.cs` | 窗口→存图点位可视化配置（格子矩阵：编辑点位/交换位置/恢复默认） |
| `docs/通讯接入.md` | 相机/PLC 对接流程与寄存器表 |
| `CHANGELOG.md` | 版本改动记录（最新在前） |

## 构建与验证命令

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  CommandCenter/CommandCenter.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m /m
```

- 构建成功标准：输出 `CommandCenter -> ...\bin\Debug\CommandCenter.exe` 且无 error。
- 无单元测试框架；以构建通过 + 冒烟测试为验证手段（`Start-Process` 启动 exe，等几秒确认进程存活再 `Stop-Process`）。

## 文档同步（每次任务完成必做，逐条核对）

- **`CHANGELOG.md`**：顶部新增/更新当前版本小节，写明"改动范围、为什么这么改、优化点"三部分（参考既有 V1.x 小节格式），改动再小也记。
- **`README.md`**：目录结构、核心业务流、构建方式有变化时同步更新。
- **`docs/通讯接入.md`**：寄存器地址 / 相机指令 / 通讯流程等通讯类改动，必须同步并写明版本号。
- **代码注释**：改动处注释详细到小白能看懂；新文件/新方法写清头部说明；中文保持 UTF-8。
- **提交前自检**：`git status` + `git diff` 确认改动范围与文档同步都完成后再交付；用户不要求 commit 时只留工作区改动即可。