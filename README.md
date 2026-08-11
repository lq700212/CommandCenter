# CommandCenter

相机 + PLC 现场命令中心上位机（.NET Framework 4.7.2 WinForms）。

上位机作为命令中心，按"PLC 告知相机到位 → 上位机触发相机拍照 → 取像完成保存图片 → 通知 PLC 完成 → PLC 走到下一点位"的节奏运转，同时负责操作员交互（切换配方、查看来料）。

## 技术栈

- .NET Framework 4.7.2 WinForms（C# 7.3）
- NModbus 3.0.83 —— 汇川 PLC Modbus TCP 通讯（本地 `libs/` 引用，离线可编译）
- Newtonsoft.Json —— 配置 / 配方序列化（本地 `libs/` 引用）
- 相机：基恩士 IV4-500CA，TCP/IP 无协议通信触发拍摄 + FTP 推图

## 目录结构

```
CommandCenter/
├── Views/           界面（MainForm + MainForm.Designer / SettingsForm + SettingsForm.Designer /
│                    DirTreeEditForm 目录结构可视化配置 / WindowPointForm 窗口点位可视化配置）
│                    ※ MainForm/SettingsForm 静态布局（标题栏、状态栏、矩阵容器、设置表控件）
│                      在对应 .Designer.cs 里可视化维护；动态部分（相机灯、窗口矩阵内容）
│                      在业务文件里运行时生成
├── Controls/        自绘/辅助控件（CameraDisplayControl / OkNgBadge）
├── Services/        通讯与业务编排（PlcService / KeyenceIV4Camera / ImageStore /
│                    ProductionCoordinator / RecipeManager / ScannerService）
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

相机（TCP 触发 + FTP 推图）与 PLC（Modbus TCP 保持寄存器）的详细握手与寄存器表，见 **`docs/通讯接入.md`**。

## 可配置项

所有参数集中在运行时生成的 `Config/appconfig.json`：

- 相机：**多台相机列表**（每台：IP、触发端口、FTP上传目录、触发指令、超时），
  一次"到位"对所有相机各触发一次拍照
- PLC：IP、端口、站号、到位/开始/完成/配方/计数寄存器 D 地址
- 显示：窗口行数 × 列数、标题栏字段开关、OK/NG 颜色名、**窗口→存图点位映射**
  （默认点位=窗口编号，可在设置窗体"窗口/点位配置..."里可视化自定义/交换窗口位置）
- 图像：保存根目录、**存图目录结构**（可视化逐级配置，默认 年月日/SN号/OK|NG，点位号进文件名，
  在设置窗体点"配置目录结构..."编辑）、FTP 监听
- 扫码枪：是否启用、串口参数（按项目约定：停止位存 "1"/"15"/"2"，校验位存标准枚举名）

换机型/换配方现场只改 JSON，复用同一份程序。