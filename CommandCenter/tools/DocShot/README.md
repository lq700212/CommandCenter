# DocShot 截图 harness 说明（培训文档配图专用）

`DocShot.cs` 独立编译成临时 exe，直接 `new` 真实产品窗体逐张 PrintWindow，
配图落 `docs/images/`。以后改了界面，重跑一遍即换图。

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
- 主界面拍前固定 1400×820（`OnShown` 会先铺满，拍前改回，保证文档图统一）。
- Show 后 sleep 700ms 等首帧；主界面另等 2.5s 让建站/连接超时结束、状态灯稳定。

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
