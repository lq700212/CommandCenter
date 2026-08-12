---
name: winforms-ui-debug
description: 调试本项目（AgingTestSystem，WinForms/.NET Framework 4.x + SunnyUI）的界面渲染问题：标题行竖线、颜色叠加、边框/裁剪、DPI 缩放残影、控件错位等"像素级"视觉 bug。核心是"指哪打哪"——用户说哪个窗体/页面，就编译一个独立 harness 直接启动该窗体（绕过登录/主流程），再配合 PrintWindow 截图 + 像素扫描 + 反射探查来定位根因、验证修复。用户提到具体页面/窗体名（如设置窗口、SettingsForm、工位网格、WorkstationGridView）或"界面竖线/横线/颜色不对/叠色/滚动了/错位"等词时触发。
---

# WinForms 界面像素级调试（指哪打哪）

本技能沉淀"编译并直接启动指定窗体 + 像素探针"的调试套路。这是本仓库 UI bug 定位最快的方式：**不改主程序流程、不进登录页，直接 new 出目标窗体来观察**。适用于所有 WinForms/SunnyUI 渲染问题（分割线、颜色、裁剪、DPI、滚动条、对齐）。

> 开工前先读 `AGENTS.md`（文件编码 UTF-8、改后必构建、文档同步等红线全部适用）。

## 一、总流程

```
1. 构建项目（拿到最新的 exe + DLL）
2. 写独立 harness（.cs）→ 用 csc 编译成一个小 exe
3. 运行 harness：直接 new 目标窗体并 Show
4. harness 内用反射读私有字段、打印几何、像素扫描 / PrintWindow 截图
5. 定位根因 → 改代码 → 重新构建 → 重跑 harness 对比 before/after 像素
6. 冒烟测试主程序 + 更新 CHANGELOG.md（+ 必要时 README/docs）
```

## 二、构建与 harness 编译命令

### 1. 构建主项目

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" AgingTestSystem/AgingTestSystem.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
```

构建产物在 `AgingTestSystem/bin/Debug/`（exe + 全部依赖 DLL）。**每次改完代码都必须重新构建再跑 harness**，否则 harness 引用的是旧 exe。

### 2. 编译 harness（关键命令，可复用）

```powershell
$bin = "E:\Project\AgingTestSystem\AgingTestSystem\bin\Debug"
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe" `
  /nologo /t:exe "/out:$bin\UiProbe.exe" "C:\Users\ADMINI~1\AppData\Local\Temp\opencode\UiProbe.cs" `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll `
  "/r:$bin\AgingTestSystem.exe" "/r:$bin\SunnyUI.dll" "/r:$bin\SunnyUI.Common.dll" `
  "/r:$bin\NModbus.dll" "/r:$bin\NModbus.Serial.dll" "/r:$bin\Newtonsoft.Json.dll" `
  "/r:$bin\DocumentFormat.OpenXml.dll" "/r:$bin\DocumentFormat.OpenXml.Framework.dll"
```

要点：
- 编译报错只看 `error CS` 行（`2>&1 | Select-String -Pattern "error CS"`）。
- `$?` 为真才运行 exe：`if ($?) { Push-Location $bin; & ".\UiProbe.exe"; Pop-Location }`。
- 若 `Roslyn\csc.exe` 找不到，用 `Get-ChildItem 'D:\Program Files\Microsoft Visual Studio' -Recurse -Filter csc.exe` 定位。
- **harness 源码放临时目录**（`C:\Users\ADMINI~1\AppData\Local\Temp\opencode\`），产物放 bin/Debug（gitignore，不污染仓库）。调试完记得删掉 `bin/Debug` 里生成的 `UiProbe*.exe`。

## 三、harness 通用骨架（直接改目标窗体）

```csharp
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

static class UiProbe
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        SetProcessDPIAware();          // 关键！不设的话渲染结果 ≠ 真实程序（DPI 会不同）
        Application.EnableVisualStyles();
        var config = new AgingTestSystem.Models.DeviceConfig();
        var form = new AgingTestSystem.Dialogs.SettingsForm(config);  // ① 换成目标窗体
        form.StartPosition = FormStartPosition.CenterScreen;
        form.Show();
        Application.DoEvents();
        System.Threading.Thread.Sleep(400);   // 等首帧画完
        Application.DoEvents();

        // ② 反射拿私有字段（UIDataGridView 等）
        var f = typeof(AgingTestSystem.Dialogs.SettingsForm)
            .GetField("_grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var grid = f.GetValue(form) as DataGridView;

        // ③ 打印几何（帮理解布局/坐标系）
        Console.WriteLine("Form=" + form.ClientSize + " Grid=" + grid.ClientSize + " DisplayRect=" + grid.DisplayRectangle);
        foreach (DataGridViewColumn col in grid.Columns)
            Console.WriteLine("Col " + col.Name + " W=" + col.Width + " Auto=" + col.AutoSizeMode);

        form.Close();
    }
}
```

### 如何"指哪打哪"启动任意窗体
- 打开目标窗体的 `.cs`，看构造函数签名（例：`SettingsForm(DeviceConfig)`、`RecipeManagerForm()`）。
- 构造所需依赖：大部分窗体直接 `new`；若依赖 `DeviceConfig` / `UserManager` / 主窗体引用，用反射或 `new` 现造的实例传进去。
- 不是主窗体的对话框类，直接 `new + Show()` 就能独立运行。

## 四、三大调试工具（本技能核心）

### 1. PrintWindow 整窗截图（最可靠，与屏幕位置无关）

`Screen Capture / CopyFromScreen` 在"窗体被居中到非标准分辨率 / 半屏外"时会得到错位、裁剪、甚至不可复现的像素数据（本会话踩过坑）。**整窗截图一律用 PrintWindow**，它渲染完整窗口（含标题栏/边框），输出位图坐标 = 窗口坐标，稳定可复现：

```csharp
[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
struct RECT { public int Left, Top, Right, Bottom; }

static Bitmap CaptureWindow(Form form)
{
    RECT r; GetWindowRect(form.Handle, out r);
    var bmp = new Bitmap(r.Right - r.Left, r.Bottom - r.Top);
    using (var g = Graphics.FromImage(bmp))
    {
        IntPtr hdc = g.GetHdc();
        PrintWindow(form.Handle, hdc, 0);
        g.ReleaseHdc(hdc);
    }
    return bmp;
}
```

### 2. grid 客户区坐标 → PNG 位图像素坐标映射

DPI 感知进程里 `GetCellDisplayRectangle`/`GetRowDisplayRectangle` 返回的可能是**物理像素**，且窗口有边框偏移，不能直接当 PNG 坐标用。正确换算：

```csharp
static Point GridToPng(Form form, DataGridView grid, Bitmap bmp, int gx, int gy)
{
    RECT win; GetWindowRect(form.Handle, out win);
    Point clientOrigin = form.PointToScreen(Point.Empty);   // 客户区原点(屏幕)
    Point gridOrigin  = grid.PointToScreen(Point.Empty);    // grid 客户区原点(屏幕)
    Rectangle scrGrid = grid.RectangleToScreen(grid.ClientRectangle);
    double scale = (double)scrGrid.Width / grid.ClientSize.Width;   // DPI 缩放（150% 时 = 1.5）
    int px = (int)Math.Round((gridOrigin.X - clientOrigin.X) + gx * scale) + (clientOrigin.X - win.Left);
    int py = (int)Math.Round((gridOrigin.Y - clientOrigin.Y) + gy * scale) + (clientOrigin.Y - win.Top);
    return new Point(px, py);
}
```

### 3. 像素扫描找"线"（定位竖线/横线/叠色的颜色与坐标）

沿一条水平线逐像素 `bmp.GetPixel(x,y)`，按颜色特征找线：
- 先打印整段色值（`x:R,G,B`），**用颜色值反推来源**：不同颜色 = 不同绘制者（例：cell border=GridColor `104,173,255`，滚动条左线=`80,160,255`，标题蓝=`48,119,238`）。
- 再扫多行 y（行 top/mid/bot），确认是"整行贯穿"还是"局部"，区分竖线/横线/文字像素。
- 用已知控件属性值做色值字典（`Color.FromArgb(...)` 与屏幕采样比对），快速锁定是谁画的。

### 4. 改像素前先读几何（一次命中的关键，本次 V1.54j 的做法）

遇到"某处要不要有竖线/横线、线画在哪"这类问题，**先反射打印布局几何 + 扫描色值，用几何推导出绘制坐标，再改代码**，往往一稿就过。比"改了→看→再改"的试错快得多。**而且一定要打印真实尺寸，别信代码注释里的硬编码值**（踩坑 8）。

拿"数据行最右边缘要有竖线、分组标题行不要"为例，定位候选 X 的思路链：

```csharp
// 1) 容器与边界真实几何（反射私有字段）
Console.WriteLine("pnlScroll.Padding=" + pnlScroll.Padding);          // 实测 Left=18，注释写 12 是错的！
Console.WriteLine("DisplayRect=" + grid.DisplayRectangle);           // 内容可视区
Console.WriteLine("CellBorderStyle=" + grid.CellBorderStyle + " GridColor=" + grid.GridColor);
// 2) 找盖在表格右边缘的滚动条子控件，这是"视线终点"
var sc = grid.Controls.OfType<Sunny.UI.UIScrollBar>().FirstOrDefault();
Console.WriteLine("UIScrollBar Bounds=" + sc.Bounds + " ShowLeftLine=" + sc.ShowLeftLine); // X=1377 W=27
// 3) 最后一列逻辑右边缘（即"想画线的位置"）
var cr = grid.GetCellDisplayRectangle(grid.Columns.Count - 1, 0, false);
Console.WriteLine("lastCol cell0=" + cr);                            // Right=1378
// 4) 推导：scrollBar 从 1377 起盖住 1377~1404，所以 cell.Right(1378) 不可见，
//    数据行"可见的最右像素"= scrollBar.Bounds.Left - 1 = 1376 ← 竖线就画这
// 5) 扫描该行带前后各 10px 确认底色（这一步是"验证候选坐标"而不是"找 bug"）
```

**通用推导口诀**：被子控件遮住的像素别去画——`可见最右 x = 子控件.Bounds.Left - 1`，`可见最下 y = 子控件.Bounds.Top - 1`。拿到这个 x 再决定画线的颜色（跟 `GridColor` 或对应 cell border 一致，避免另造新色）。

## 五、本仓库踩过的坑（下次直接避）

1. **Child 控件永远画在父内容之上**：SunnyUI `UIDataGridView` 内置一个 `UIScrollBar` **子控件**盖在表格右边缘（最后一列右边界 ~ 表格右边界）。它默认 `ShowLeftLine = True`，在 X=918 画一条 `80,160,255` 竖线贯穿每一行——分组标题行最右侧的"竖线"真凶就是它。**任何 RowPostPaint / CellPainting / 覆写 OnPaint 都压不过子控件**；正解是找到子控件改属性（`grid.Controls.OfType<Sunny.UI.UIScrollBar>().First().ShowLeftLine = false;`）。
2. **RowPostPaint 的 Graphics 被裁剪在行显示矩形内**：`e.Graphics.ClipBounds` 打印确认。想覆盖 cell border 画到 `_grid.Width+1` 是没用的——超出裁剪的部分画不上。别在"覆盖宽度差 1px"上反复纠结，先确认那个"线"到底是谁画的。
3. **逻辑像素 vs 物理像素（DPI）**：150% 缩放下 1 逻辑像素对应 1.5 物理像素，border 落在半像素上会被渲染到某个物理像素，颜色可能与你预期不同。**harness 必须 `SetProcessDPIAware()`**，否则渲染路径都不一样，探针结果不可信。
4. **Screen Capture 不可靠，用 PrintWindow**：`CopyFromScreen` + 居中窗口在多显示器/窗体超屏时像素错位、时有时无，浪费大量时间。整窗截图一律 PrintWindow。
5. **Color 值是最强的线索**：同一列边框是 `104,173,255`、滚动条左线是 `80,160,255`、滚动条轨道 `243,249,255`、表格背景 `237,243,253`、数据行背景 `243,249,255`。像素颜色不匹配 ≠ 你改的那个对象，先反查绘制来源再动手。
6. **验证 DPI 与"行是否真的被扫到"**：探针 y 取错位置会得出"没线"的假结论。用 `grid.GetRowDisplayRectangle(i,false)` + `RectangleToScreen` 映射后再取 y，别凭肉眼估。
7. **cell 逻辑右边缘 ≠ 可见最右像素**：SunnyUI UIDataGridView 的 `UIScrollBar` 子控件（Bounds.X=1377, Width=27）会盖住最后一列（colValue）右边缘 1~27px。所以 `GetCellDisplayRectangle(...).Right`（=1378）那个像素你是画不上/看不见的；想给数据行画"表格右边界竖线"，X 要取 `scrollBar.Bounds.Left - 1`（=1376）。**别把线画进子控件地盘**。
8. **代码注释里的硬编码坐标可能是错的，以实测为准**：例如 `Grid_RowPostPaint` 注释写"pnlScroll.Padding.Left=12"，实测却是 **18**；注释写"滚动条线在 X=918"，那只是当时窗口宽度的值。调试/改布局时，先反射打印 `Padding`/`Bounds`/`DisplayRectangle` 等真实值，再决定画在哪。自己写注释时也标注"实测"值。
9. **"某行有、某行没有"这类需求，用行号集合分流，不要另开事件**：需在 RowPostPaint 里给普通数据行画线、给分组行不画时，直接复用已有的 `_groupRows`（行号集合）做分支：`if (!_groupRows.Contains(e.RowIndex)) { 数据行逻辑; return; } else { 分组行逻辑; }`。既不动行样式、也不用区分事件，一次画绘里处理两类，可读性好。
10. **善用"批量纵向扫描多行"一次性确认规则**：改完"按行分支画线"后，harness 里循环前 N 行（`for r in 0..13`）对固定 X 取色，对照行号集合打印 `行号 [GROUP|data] 色值`，能一眼确认"分组行=浅蓝、数据行=线色"全部命中，比单看两行更稳。
11. **纯代码窗体的 AutoScale 不生效 = 99% 缺 `SuspendLayout()`**（V1.58.4 血泪）：只设 `AutoScaleMode.Font` + `AutoScaleDimensions` 不够，未挂起布局时逐次 `Controls.Add` 会把 AutoScaleDimensions 固化当前 DPI 值（144 DPI 下 6×12→9×18），缩放因子恒 1。验证时先看 `form.AutoScaleDimensions` 构造后是不是还是 6×12，被改成 9×18 就是缺 SuspendLayout。
12. **测 AutoScale 缩放必须用 PerMonitorV2 harness**：只 `SetProcessDPIAware()` 是 System-aware，AutoScaleDimensions 自动按当前 DPI 初始化，永远测不出缩放（假结论）。csc 编译带 `/win32manifest:pmv2.manifest` + bin 里放同名 `.exe.config`（`DpiAwareness=PerMonitorV2` 开关）才走真路径；对照 Designer 窗体（SettingsForm）能缩放即证明环境正确。
13. **禁窗口缩放：`Maximized` 是最大的坑，改用手动铺满 `WorkingArea`**（V1.11.0 CommandCenter 血泪）：想"默认全屏 + 禁止拖拽缩放 + 保留最小化/关闭按钮"时，**绝不能用 `WindowState.Maximized`**——Windows 会在最大化瞬间把 `FixedSingle` 边框强制切换成"可调整"样式，边缘拖拽缩放照常开放，`WndProc` 拦截 `WM_NCHITTEST`（HTLEFT..HTBOTTOMRIGHT 命中码 10~17 改 HTCLIENT）在最大化状态下也挡不住系统这一层。正解组合：`FormBorderStyle=FixedSingle`（Normal 下无拖拽句柄）+ `MaximizeBox=false` + `MinimizeBox=true` + `WindowState=Normal` + 在 `OnShown` 里手动 `Bounds = Screen.FromControl(this).WorkingArea` 铺满屏幕（等效全屏、保留任务栏）。按钮缩放、常规拖拽、最大化边缘拖拽三条通道全关，最小化/关闭按钮不受影响。
14. **WinForms 点击/双击生效，先问"真实命中谁 + 冒不冒泡"**（V1.12.15 CommandCenter 三轮血泪，做"双击放大/还原/整窗响应双击"先读这条）：判断某控件上"点击/双击有没有反应"，先想清两件事，否则白改：
   - **① 真实命中目标是谁**：鼠标双击落在**最内层子控件**上（如图像区 `PictureBox` 用 `Dock=Fill` 占满整窗，双击必落它），**不会"自动落到父 UserControl"**。红线：双击 BI 控件时只有被点的那个控件收到消息。
   - **② 事件冒不冒泡**：WinForms 中带 `Mouse` 前缀的（`MouseClick`/`MouseDoubleClick`/`MouseDown`…）会沿父链冒泡；不带前缀的（`Click`/`DoubleClick`）**不冒泡**。
   - **最稳写法，直接背**：直接订阅最内层子控件（PictureBox）的 `MouseDoubleClick`（参考 `CommandCenter/Controls/CameraDisplayControl.cs` 的 `HandleDoubleClick`），它在真实命中点、必然触发、不依赖冒泡。**别用**父控件 `OnDoubleClick` 重写（不冒泡→没反应，第一版就这么挂的）；**也别赌**父控件 `MouseDoubleClick` 冒泡（部分环境不稳定，第二版也挂）。
   - **验证不能用合成鼠标**：headless / 无桌面交互会话下，`mouse_event`、`SendMessage WM_LBUTTONDBLCLK` 都**触发不了 WinForms 双击**——WinForms 对双击有内部状态/计时免疫，合成事件被吞，发多少遍都不生效，别拿它当验证依据（在这上面空转了很久）。
   - **可靠的验证手段**：进程序 harness 反射调用**真实命中控件（PictureBox）的 `protected OnMouseDoubleClick`** 注入双击（不依赖消息/计时），再反射读私有字段断言结果（如 `_fullScreenForm` 是否非空、放大的 `_windows[?]` 是否同一、`RestoreFullScreenWindow` 后是否置 null）。这是本项目验证"双击→放大→还原"类行为的可靠手段；状态文本类就反射读 `Label.Text`。

## 五·五、窗口全屏 / 禁缩放 / 边框行为专项（V1.11.0 CommandCenter 沉淀）

"开机全屏、禁缩放、但保留最小化/关闭按钮"是工控界面的常见需求，也是 WinForms 最容易反复翻车的点。核心原则：**让"固定"成为真实状态，而不是看起来固定**。下表是各方案的实测结果（客户现场逐版验证过）：

| 方案 | 结果 | 结论 |
| --- | --- | --- |
| `FormBorderStyle=None` + 铺满屏 | 无边框、无任何系统按钮 | 关不了软件，客户投诉 |
| `FixedDialog` | 边框固定、**无最小化/最大化按钮** | 客户要最小化 |
| `Sizable` + `MaximizeBox=false` | 按钮禁用，但**边框仍可拖拽缩放** | 拖拽漏网 |
| `FixedSingle` + `WindowState=Maximized` | **Windows 把边框强制切成可调整样式，边缘拖拽照常开放** | 最隐蔽的坑 |
| ✅ `FixedSingle` + `Normal` + 手动铺 `WorkingArea` + `MaximizeBox=false` + `MinimizeBox=true` | 全屏等效、无拖拽句柄、最小化/关闭都在 | 正解 |

### 关键机制：为什么 `Maximized` 不能用
`WindowState=Maximized` 时 Windows 会临时把 `FormBorderStyle` 当作可调整边框处理（为了支持最大化窗口边缘拖拽调整），**与你 Designer 里写的 `FixedSingle` 无关**。此时即使 `WndProc` 拦截 `WM_NCHITTEST` 把四边热区改 HTCLIENT 也挡不住——系统在最大化状态下直接走自己的缩放通道。

### 正确组合（照抄即可）
```csharp
// Designer / InitializeComponent：
this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle; // Normal 下无拖拽句柄
this.MaximizeBox = false;   // 中间的"最大化/还原"按钮变灰
this.MinimizeBox = true;    // 最小化按钮保留
this.WindowState = System.Windows.Forms.FormWindowState.Normal; // 千万别用 Maximized！

// OnShown：手动铺满工作区（等效全屏，保留任务栏）
var work = Screen.FromControl(this).WorkingArea;
Bounds = new Rectangle(work.Location, work.Size);
```

### 双保险（可选，防系统残留热区）
`WndProc` 拦截 `WM_NCHITTEST`（0x0084），把命中码 10~17（HTLEFT..HTBOTTOMRIGHT 四边四角）一律改写为 1（HTCLIENT），Windows 不再进入缩放拖拽流程；最小化（8）/关闭（20）/标题栏拖动（2）不受影响。Normal + FixedSingle 下其实已够，这条是兜底。

### 排查口诀
- 用户说"拖拽还是能缩放" → 先查 `WindowState` 是不是 `Maximized`（十有八九是它）；
- 用户说"最小化按钮没了" → 查 `FormBorderStyle` 是不是 `FixedDialog`/`None`；
- 用户说"关不了软件" → 查是不是 `FormBorderStyle=None`；
- 想全屏 → **别用 Maximized，用 OnShown 手动铺 Bounds**；否则 `FixedSingle` 白设。

## 五·五·一、高 DPI 适配专项（V1.55 沉淀）

用户报"高分辨率屏幕界面显示不正常"时，先分清两类控件，再决定适配方式：

### 判断根因：先确认是"不缩放"还是"缩放比例不一致"
- 打印 `form.DeviceDpi`、各关键控件 `CreateGraphics().DpiX`、`AutoScaleMode`。
- **标准控件窗体（AutoScaleMode.Font）**：WinForms 会自动按字体比例放大，通常无需动代码，只需保证 `app.manifest` 有 `PerMonitorV2` + `App.config` 有 `Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2` 运行时开关（**两个缺一不可**）。**注意：这适用于 Designer 生成的窗体；纯代码窗体要额外显式设 `AutoScaleDimensions(6F,12F)` + `SuspendLayout`/`ResumeLayout` 包裹，否则不缩放**（见下文"纯代码窗体 AutoScale 验证专项"）。
- **自绘控件（AutoScaleMode.None，如 WorkstationGridView）**：画布尺寸和坐标是"96DPI 逻辑像素"，**不随 DPI 放大**，但 pt 字体会自动放大 → 文字溢出格子、与周围标准控件比例失调。这是"主界面显示不正常"的典型根因。

### 自绘控件 DPI 适配三步
1. **计算缩放因子**：句柄创建后（`OnHandleCreated`）算 `_dpiScale = 实际DPI / 96`。**踩坑：`Control.DeviceDpi` 在 PerMonitorV2 下句柄刚创建时返回 96（不准确），必须用 `CreateGraphics().DpiX`（实测 144 才是真值）**。
2. **手动乘坐标，别用 ScaleTransform**：自绘里 TextRenderer 走 GDI 不认 `Graphics.ScaleTransform/TranslateTransform`（见坑 1、V1.51 踩坑），必须写 `Scaled(int/Point/Rectangle)` 辅助方法，把所有绘制坐标、画布 Size、命中检测（鼠标坐标是物理像素）、tooltip、局部重绘矩形统一乘 `_dpiScale`。字体保持 pt 单位自动放大，两者同步放大比例才一致。
3. **命中检测别漏**：鼠标 `e.Location` 是物理像素，与缩放后的布局矩形比较；可见列/行范围计算（`clip.Left / 列宽`）也要用缩放后的列宽，否则只重绘左上角一小块。

### 验证超画布自绘控件：用离屏 OnPaint，别用 PrintWindow
- 自绘画布 3060×3038 远超屏幕，**PrintWindow 只截可见区**，超屏部分全是背景残留色（Magenta），像素扫描全假失败。
- 正解：反射调用 `Control.OnPaint` 渲染到完整尺寸 Bitmap（`new PaintEventArgs(g, new Rectangle(0,0,宽,高))`），无视屏幕限制，再逐像素验证。
- 验证点示例（150% 缩放 = 逻辑坐标 ×1.5）：面板左上角 = `Scaled(2)=3`；上电块 = `Offset(Scaled(RcPower),3,3)`；行全选列 x = `Scaled(8*245+2)=2943`。

### 纯代码窗体（无 Designer）AutoScale 验证专项（V1.58.4 沉淀）
用户报"纯代码窗体高分屏不放大/偏小"时，先分清是哪个环节坏了。**这类问题 harness 默认验证不出来，必须按下面的 PerMonitorV2 全套配置跑，否则拿到的是假结果**：

- **harness 只调 `SetProcessDPIAware()` 是 System-aware，测不出 AutoScale 缩放**：System-aware 下 WinForms 会把 `AutoScaleDimensions` 初始化为"当前 DPI 设计值"（144 DPI 下自动变 9×18），导致设计基准==运行基准、缩放因子恒为 1、窗体永不放大。**必须两步配齐才走 PerMonitorV2 路径**：
  1. 编译时带 manifest：`csc ... "/win32manifest:C:\...\pmv2.manifest"`（manifest 内容见下方模板）。
  2. 运行时带 config：在 bin 放 `<exe名>.exe.config`，配 `<AppContextSwitchOverrides value="Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2" />`。
  - 判断依据：同样流程下 **Designer 窗体（如 SettingsForm）能缩放、你的纯代码窗体不缩放**，说明 harness 环境对了，问题在窗体的 AutoScale 写法。
- **纯代码窗体高 DPI 三要素**（缺一不可，缺任意一个就"永不放大"）：
  1. `AutoScaleDimensions = new SizeF(6F, 12F)`——只设 `AutoScaleMode.Font` 会以 96DPI 为基准不缩放；
  2. **`SuspendLayout()` 包裹全部控件创建 + 末尾 `ResumeLayout(false)`**——未挂起时逐次 `Controls.Add` 会触发 PerformAutoScale 并把 AutoScaleDimensions 固化成当前 DPI 值，缩放失效。这是最隐蔽的一环（Designer 窗体天生带，所以从没人发现纯代码窗体缺它）；
  3. 主程序 `app.manifest`(PerMonitorV2) + `App.config`(DpiAwareness=PerMonitorV2) 两处已配。
- **验证"是否缩放"的正确探针**：打印 `form.AutoScaleDimensions` / `form.CurrentAutoScaleDimensions` / `form.ClientSize`，以及关键面板 `Size`。144 DPI 下修复后应为 `ClientSize=640×520 → 960×780`、各面板×1.5（`pnlValues 148→222`、预览区 294→441），且 `AutoScaleDimensions` 构造后仍是 `6×12`（**若读回 9×18 说明被固化了，就是 SuspendLayout 缺失**）。96 DPI 下缩放因子=1、保持原尺寸，由公式保证，无需专门模拟。

pmv2.manifest 模板（放临时目录，harness 编译用）：
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```
exe.config 模板：
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <runtime>
    <AppContextSwitchOverrides value="Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2" />
  </runtime>
</configuration>
```

### 自绘控件性能优化（V1.57.3 血泪教训，AGENTS.md 强制红线）
排查"自绘控件卡顿/掉帧/每秒卡死"时，先背这几条，别走弯路：

- **禁止"离屏 Bitmap 整幅预渲染 + OnPaint DrawImage 拷贝"**：实测离屏大图（2040×2025）上 `TextRenderer.DrawText` 每处约 **2.2ms**（屏幕 DC 上近 0ms），全量渲染 72 面板一次高达 **2247ms**；若再配"每秒全量刷新"，整个软件每 1 秒卡死一次。且 `g.Clear(白色)` 会把面板间隙刷白，导致"面板连成一片"的视觉 bug。
- **正确做法**：OnPaint 只重绘**可见区**面板——用 `e.ClipRectangle` 反推行列范围，循环里跳过 `!rect.IntersectsWith(e.ClipRectangle)` 的面板；数据/选中变化只 `Invalidate()`（让系统按需合并重绘）；滚动卡顿用 **16ms 定时器节流 AutoScrollPosition + 画刷/画笔缓存字段**，不要每次 OnPaint 现造 Brush/Pen。
- **性能判断必须用真实屏幕 DC**：用 `CreateGraphics()` 拿屏幕 DC 测帧速/耗时。**离屏 Graphics 上的 TextRenderer 慢是 GDI+ 固有行为，不代表真实帧速**——在离屏 Bitmap 上测得慢不等于真实渲染慢，反之亦然。
- **改自绘坐标前先查配置模型**：本项目自绘控件（WorkstationGridView 等）的坐标/颜色/字号常量一律**外部化**到 `Models/PanelLayoutConfig.cs`（可被 `PanelLayout.json` 覆盖），**禁止写死像素常量**。改布局先在配置文件里找对应项，改完跑主程序看 `PanelLayout.json` 是否已有现场覆盖值干扰。

### 改界面代码前必读头部 ASCII 图（项目约定，防改错布局）
- 所有 View/Dialog（`Views/*.cs`、`Dialogs/*.cs`）类 XML 注释里都有一张用 `┌─┐│└┘` 画的界面布局图（参考 `RecipeManagerForm.cs` / `WorkstationGridView.cs` 头部），**框内标注控件名与关键交互点**。AI 无法看图，改界面全靠这张图。
- 改布局前先读目标窗体的 ASCII 图，确认控件名/坐标；**改完必须同步更新该图**（坐标、控件名、按钮文字都要和实际一致），否则下次维护的 AI 拿到错图会改错布局。



## 六、验证与收尾（必做）

- 构建通过（MSBuild 输出 exe、无 error）。
- harness 复跑，对比修复前/后同坐标像素颜色，确认目标线/色消失且不引入新问题。
- 冒烟测试主程序：`Start-Process` 启动 exe，等几秒确认进程存活再 `Stop-Process`。
- 更新 `CHANGELOG.md`（写清改动范围/为什么/优化点），必要时 `docs/通讯接入.md`、`README.md`。
- 删除 `bin/Debug` 下生成的 harness exe；源文件在临时目录不提交。
- UTF-8 自检：`[IO.File]::ReadAllText(path, [Text.Encoding]::UTF8).Contains("预期中文")`。

## 七、推广到其他项目

套路本身与项目无关：**任意 WinForms 项目**都可以"构建 → csc 引 bin 里的 exe+DLL 编译独立 harness → 直接 new 目标窗体 → 反射探私有字段 + PrintWindow 截图 + 像素扫描"。换项目只需替换：MSBuild/csc 路径、bin 目录、DLL 引用清单、目标窗体类名与构造函数。
