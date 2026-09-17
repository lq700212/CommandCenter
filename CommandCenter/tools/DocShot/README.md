# DocShot 截图 harness 说明（培训文档配图专用）

`DocShot.cs` + `RealShot.cs` 配合出图，落 `docs/images/`。以后改了界面，
两脚本各重跑一遍即换图。

**管线分工（V2.16.8 定稿，以后不要改）**：
- 静态对话框（登录/设置/点位/目录/型号/补录/异常/开发者模式）→ `RealShot.cs`
  真进程实拍（地面真相）。harness 直 new 的对话框恒为 classic 边框（外框小一圈），
  根因未查明，直接绕过。
- 动态态（主界面 OK/NG 完成态，无硬件演不出）→ `DocShot.cs` harness 演出。
  主界面全屏无边框问题，不受影响。

## RealShot（真进程驱动）

```powershell
# 编译（仓库根）与运行（workdir=CommandCenter\bin\Debug）见 RealShot.cs 头部注释
.\cc_realshot.exe E:\Project\CommandCenter\docs\images
```

- 另起真实 exe，Win32 消息像用户一样开窗：只开窗截图 + 取消，
  绝不点保存/删除/启动/触发/写入类真动作。前提：开发机账号是出厂默认
  （先读配置哈希比对，不瞎试密码）。
- **V1 事故铁律**：开窗点击一律 `PostMessage` + WaitTitle（`SendMessage` 点弹模态框的
  按钮会卡死发送线程，现场每个弹窗都得人工关）；登录框按坐标排序一次填对；
  看门狗只关非目标小弹窗；FAIL 截全屏；单实例预检；全英文日志；finally 验退。
- 跑完删 exe/pdb；`Logs\` 全程备份、跑完还原（真进程启动即写当天日志），
  控制台打 "logs restored" 确认行，不污染开发机日志。

## DocShot（harness 演出，主界面专用）

## 编译（仓库根，产物跑完即删，不污染 bin）

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe" `
  /nologo /target:exe /platform:AnyCPU /codepage:65001 `
  /out:CommandCenter\bin\Debug\cc_docshot.exe `
  /r:CommandCenter\bin\Debug\CommandCenter.exe `
  /r:CommandCenter\bin\Debug\Newtonsoft.Json.dll `
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
  CommandCenter\tools\DocShot\DocShot.cs
```

注意：源码含中文，csc 必须加 `/codepage:65001`，否则中文注释乱码编译失败。

## 运行（工作目录必须是 `CommandCenter\bin\Debug`）

```powershell
.\cc_docshot.exe E:\Project\CommandCenter\docs\images
```

- 读真实 `Config\appconfig.json`（仅显示不保存，各窗体传 JSON 深拷贝副本）。
- `Logs\` 全程备份、跑完还原（控制台打"Logs 已还原"确认行），不污染开发机日志。
- 跑完删 `cc_docshot.exe`（+pdb），bin 里不留非交付物。
- 全程无人值守约 1~2 分钟（等 PLC 建站 + 相机/扫码枪连接超时 + 11 张×0.7s）。

## R1 数据路径（确定性演出，无随机）

- 主界面完成态：程序生成 `docshot_ok/ng.png`（自带"演示图片 DEMO-SN-0001"字样，
  用完即删）→ `ProductionCoordinator.LoadThumbnailSafe` 真实解码 →
  反射调 `MainForm.OnInspectionFinished`（该方法注释明示支持 harness 直连，
  UI 线程同步路径，计数/徽标/图片全是真实方法刷的）。先喂 3 个 OK 拍
  `main_ok`，再喂 2 个 NG 拍 `main_ng`（总数 5、OK 3、NG 2）。
- 其余 8 窗：真实构造 + 真实配置 clone；`SerialInputForm` 预填 `DEMO-SN-0001`、
  `ScannerFailForm` 带 `ERROR` 失败文本、`DeveloperModeForm` 传 null 服务+空列表
  （只截图不点按钮，无需设备）。

## R2 截图方式

- 全部标准窗体 → PrintWindow 整窗（含标题栏，文档要看到窗口名）。
- **视觉样式（V2.16.8 血泪）**：`Main()` 开头必须调
  `Application.EnableVisualStyles()` + `SetCompatibleTextRenderingDefault(false)`
  （与产品 `Program.Main` 一致），否则 ComboBox/滚动条/表格头/按钮按 Classic
  渲染、与真实软件颜色风格不一致（A/B 实测：标题栏区逐像素 diff mean≈20）。
- 主界面铺满工作区（与真实 `OnShown` 一致），不再固定尺寸——窄尺寸会挤掉
  标题栏按钮（V2.16.6 图曾把"系统设置"挤成"系统设"、语言/主题按钮消失）。
- 拍前 `Activate()`（非激活标题栏灰蓝）、Show 后 sleep 700ms 等首帧；
  主界面另等 2.5s 让建站/连接超时结束、状态灯稳定。
- A/B 保真验证法：另起真实 exe 跑稳后同手法 PrintWindow 抓一张，
  与 harness 图逐像素 diff 分区看（标题栏/内容/状态栏），mean<10 即达标。

## R3 非空校验（阈值已校准，勿沿用别项目）

- `distinct>=12 && dark>=15`（内容区去标题栏，步长 7 采样）。
- 校准表（MetricProbe 2026-09-17 实测，本机 WinForms 有边框窗）：

| 窗体 | distinct | dark | 结论 |
| --- | --- | --- | --- |
| blank 对照 | 7 | 35 | 空白（dark 含系统边框，虚高） |
| serialinput | 19 | 23 | 最稀疏真图 |
| scannerfail | 51 | 68 | 真图 |
| login | 28 | 127 | 真图 |
| main | 39 | 32775 | 真图（深灰矩阵） |

- FAIL 重拍一次，再 FAIL 存 `*.FAIL.png` 转人工，退出码 1（不整批陪葬发生在窗体级，
  名单打终端）。

## 防卡住配置

- 看门狗线程 300ms 扫本进程意外模态框（`#32770` 或有 Owner 的 WinForms 窗，
  且不在 known 名单）：否/取消优先点，否则确定/是，都没有则 WM_CLOSE，并打印日志。
- 全程不点"保存/删除/启动/触发"类按钮（截图不需要真动作）；关窗只 `Dispose`。

## 已知坑

1. `MainForm` 构造监听 502：若本机 502 被占（旧进程没关），构造抛异常整批 FAIL——
   跑前确认无残留 `CommandCenter.exe` 进程。
2. 相机/扫码枪指向现场 IP，从开发机连不上是正常的（红灯 + WARN 日志），等超时即可，
   不要改配置去"修"它——截图要的就是真实状态，文档里会注明演示机未接设备。
3. `DeveloperModeForm` 构造传 null PLC：只截图不点任何按钮；点了会 NRE（预期内，
   harness 不点）。
4. 无头/锁屏会话 PrintWindow 仍可工作（与屏幕位置无关），CopyFromScreen 不行——
   本 harness 只用 PrintWindow。
