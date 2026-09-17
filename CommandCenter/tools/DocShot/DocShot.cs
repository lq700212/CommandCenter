using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Services;
using CommandCenter.Views;
using Newtonsoft.Json;

// DocShot：培训文档截图 harness（V2.16.6 新建，一次全跑 11 张真图，存 docs/images/）。
// 手段：独立 exe 直接 new 真实窗体（PrintWindow 整窗，含标题栏），绝不手工开窗。
// 数据：① 主界面完成态走生产路径——演示 jpeg 经 ProductionCoordinator.LoadThumbnailSafe
//   真实解码 → 反射调 MainForm.OnInspectionFinished（该方法注释明示支持 harness 直连），
//   计数/徽标/图片全是真实方法刷的；演示图自带"演示图片"字样，文档注明演示数据。
//   ② 其余窗口均为真实构造 + 真实配置（clone 传参，不写盘）。
// 防卡住：看门狗线程（300ms 扫本进程意外模态框，否/取消优先点掉）+ 关窗只 Dispose。
// 非空校验：METRIC distinct>=12 && dark>=15（MetricProbe 实测校准：空白对照 7/35，
//   最稀疏真图 serialinput 19/23；阈值取两者之间）。FAIL 重拍一次，再 FAIL 存盘转人工。
// 编译（workdir=仓库根，产物跑完即删，不污染 bin）：
//   & csc /nologo /target:exe /platform:AnyCPU /codepage:65001
//     /out:CommandCenter\bin\Debug\cc_docshot.exe
//     /r:CommandCenter\bin\Debug\CommandCenter.exe
//     /r:CommandCenter\bin\Debug\Newtonsoft.Json.dll
//     /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
//     CommandCenter\tools\DocShot\DocShot.cs
// 运行（workdir=CommandCenter\bin\Debug，读真实 Config，仅显示不保存；Logs 备份还原）：
//   .\cc_docshot.exe docsImages绝对路径
public static class DocShot
{
    // ---------- Win32 ----------
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private const uint GW_OWNER = 4;
    private const uint BM_CLICK = 0xF5;
    private const uint WM_CLOSE = 0x10;

    // ---------- 看门狗（抄 skill §九，否/取消优先） ----------
    private static readonly HashSet<IntPtr> knownWindows = new HashSet<IntPtr>();
    private static volatile bool watchdogRun = false;
    private static void StartWatchdog()
    {
        watchdogRun = true;
        var t = new Thread(() =>
        {
            uint me = (uint)Process.GetCurrentProcess().Id;
            while (watchdogRun) { try { ScanPopups(me); } catch { } Thread.Sleep(300); }
        });
        t.IsBackground = true;
        t.Start();
    }
    private static void StopWatchdog() { watchdogRun = false; }
    private static void ScanPopups(uint me)
    {
        EnumWindows((h, l) =>
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid != me || !IsWindowVisible(h)) return true;
                lock (knownWindows) { if (knownWindows.Contains(h)) return true; }
                var cls = new StringBuilder(256);
                GetClassName(h, cls, 256);
                bool isPopup = cls.ToString() == "#32770" || GetWindow(h, GW_OWNER) != IntPtr.Zero;
                if (!isPopup) return true;
                var btns = new List<Tuple<IntPtr, string>>();
                EnumChildWindows(h, (ch, ll) =>
                {
                    var cc = new StringBuilder(256);
                    GetClassName(ch, cc, 256);
                    if (cc.ToString() == "Button")
                    {
                        var tx = new StringBuilder(256);
                        GetWindowText(ch, tx, 256);
                        btns.Add(Tuple.Create(ch, tx.ToString()));
                    }
                    return true;
                }, IntPtr.Zero);
                IntPtr target = IntPtr.Zero;
                string why = "WM_CLOSE";
                foreach (var b in btns)
                    if (b.Item2.Contains("否") || b.Item2.Contains("取消")
                        || b.Item2.Contains("No") || b.Item2.Contains("Cancel"))
                    { target = b.Item1; why = "click[" + b.Item2 + "]"; break; }
                if (target == IntPtr.Zero)
                    foreach (var b in btns)
                        if (b.Item2.Contains("确定") || b.Item2.Contains("OK")
                            || b.Item2 == "是" || b.Item2.Contains("关闭"))
                        { target = b.Item1; why = "click[" + b.Item2 + "]"; break; }
                if (target != IntPtr.Zero) SendMessage(target, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                else PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                var cap = new StringBuilder(256);
                GetWindowText(h, cap, 256);
                Console.WriteLine("WATCHDOG dismiss '" + cap + "' via " + why);
            }
            catch { }
            return true;
        }, IntPtr.Zero);
    }

    // ---------- 工具 ----------
    private static string imgDir;
    private static int failCount = 0;
    private static readonly List<string> failNames = new List<string>();

    private static T Clone<T>(T src)
    {
        return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(src));
    }

    private static bool CheckNonEmpty(Bitmap bmp, string name)
    {
        var colors = new HashSet<int>();
        int dark = 0;
        for (int y = 60; y < bmp.Height; y += 7)
            for (int x = 0; x < bmp.Width; x += 7)
            {
                Color c = bmp.GetPixel(x, y);
                colors.Add((c.R >> 4) << 8 | (c.G >> 4) << 4 | (c.B >> 4));
                if (c.R + c.G + c.B < 240) dark++;
            }
        bool pass = colors.Count >= 12 && dark >= 15;
        Console.WriteLine((pass ? "[PASS] " : "[FAIL] ") + name
            + " distinct=" + colors.Count + " dark=" + dark);
        return pass;
    }

    private static void Shoot(Form f, string name)
    {
        f.StartPosition = FormStartPosition.CenterScreen;
        f.Show();
        lock (knownWindows) { knownWindows.Add(f.Handle); }
        // V2.16.8：激活窗口再拍——非激活态标题栏是灰蓝色，与真实使用态（激活蓝）不一致。
        try { f.Activate(); }
        catch { }
        Application.DoEvents();
        Thread.Sleep(700);
        Application.DoEvents();
        bool ok = false;
        for (int attempt = 0; attempt < 2 && !ok; attempt++)
        {
            if (attempt > 0) { Thread.Sleep(500); Application.DoEvents(); }
            RECT r;
            GetWindowRect(f.Handle, out r);
            using (var bmp = new Bitmap(Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top)))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    try { PrintWindow(f.Handle, hdc, 0); }
                    finally { g.ReleaseHdc(hdc); }
                }
                ok = CheckNonEmpty(bmp, name);
                if (ok) bmp.Save(Path.Combine(imgDir, name + ".png"));
                else if (attempt == 1) bmp.Save(Path.Combine(imgDir, name + ".FAIL.png"));
            }
        }
        if (!ok) { failCount++; failNames.Add(name); }
        try { f.Dispose(); }
        catch { }
    }

    // 演示件图：自带"演示图片"字样，经生产 LoadThumbnailSafe 解码进内存。
    private static string MakeDemoImage(string dir, string file, bool isOk, string sn)
    {
        string path = Path.Combine(dir, file);
        using (var bmp = new Bitmap(800, 600))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(240, 244, 248));
            using (var pen = new Pen(Color.FromArgb(52, 73, 94), 6))
                g.DrawRectangle(pen, 20, 20, 760, 560);
            // 中心十字（模拟检测件定位）
            using (var pen = new Pen(Color.FromArgb(52, 73, 94), 4))
            {
                g.DrawLine(pen, 400, 150, 400, 450);
                g.DrawLine(pen, 250, 300, 550, 300);
                g.DrawEllipse(pen, 330, 230, 140, 140);
            }
            using (var font = new Font("微软雅黑", 40, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(52, 73, 94)))
            {
                g.DrawString("演示图片 " + sn, font, brush, 150, 60);
                g.DrawString(isOk ? "OK" : "NG", new Font("微软雅黑", 72, FontStyle.Bold),
                    new SolidBrush(isOk ? Color.Green : Color.Red), 330, 470);
            }
            bmp.Save(path);
        }
        return path;
    }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length < 1) { Console.WriteLine("用法：cc_docshot.exe <docs/images绝对路径>"); return 2; }
        // V2.16.8 血泪：必须与 Program.Main 一致先开视觉样式，否则全部标准控件
        // （ComboBox 边框/滚动条/表格头/按钮）按 Windows Classic 渲染，与真实软件不一致。
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        imgDir = args[0];
        Directory.CreateDirectory(imgDir);
        string workDir = Environment.CurrentDirectory;
        string logDir = Path.Combine(workDir, "Logs");
        // Logs 备份（拍完还原，不污染开发机当天日志）
        string logBak = Path.Combine(Path.GetTempPath(), "docshot_logs_bak");
        try
        {
            if (Directory.Exists(logBak)) Directory.Delete(logBak, true);
            if (Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logBak);
                foreach (var f in Directory.GetFiles(logDir))
                    File.Copy(f, Path.Combine(logBak, Path.GetFileName(f)), true);
            }
        }
        catch (Exception ex) { Console.WriteLine("WARN 日志备份失败（继续）：" + ex.Message); }

        StartWatchdog();
        try
        {
            var cfg = CommandCenter.Utils.ConfigStore.Load();
            var clone = Clone(cfg);
            const string demoSn = "DEMO-SN-0001";

            // ---- 轻窗体（各拍一张） ----
            Shoot(new SerialInputForm(demoSn, null), "serialinput");
            Shoot(new ScannerFailForm("ERROR", null), "scannerfail");
            Shoot(new LoginForm(clone), "login");
            Shoot(new ModelIndexEditForm(clone.Plc.ModelIndexes), "modelindex");
            Shoot(new DirTreeEditForm(clone.Image), "dirtree");
            var models = new List<string>(AppConfig.DefaultProductModels());
            foreach (var m in clone.ProductModels)
                if (!models.Contains(m)) models.Add(m);
            Shoot(new WindowPointForm(
                new List<int>(clone.Display.WindowStationMap ?? new List<int>()),
                clone.Display.Rows, clone.Display.Columns, clone.Cameras,
                new List<bool>(clone.Display.WindowEnabled ?? new List<bool>()),
                models, clone.Display.AutoFit, clone.ProductModel ?? "U171",
                clone.Display.WindowPointMaps, m => { }), "windowpoint");
            Shoot(new SettingsForm(clone, clone.ProductModel ?? "U171"), "settings");
            Shoot(new DeveloperModeForm(null, new List<KeyenceIV4Camera>(),
                new List<IScanner>(), new List<ScanConfig>(), null,
                new List<CameraConfig>(), demoSn, clone), "devmode");

            // ---- 主界面（三态：空闲 / OK / NG，同一窗体连续演出） ----
            var main = new MainForm();
            main.StartPosition = FormStartPosition.CenterScreen;
            main.Show();
            lock (knownWindows) { knownWindows.Add(main.Handle); }
            Application.DoEvents();
            Thread.Sleep(2500);   // 等建站/连接超时结束、状态灯稳定
            Application.DoEvents();
            // V2.16.8：铺满工作区（与真实 OnShown 一致），不再固定 1400×820——
            // 窄尺寸会把标题栏按钮挤掉（"系统设置"截断、语言/主题按钮消失），与真机不符。
            main.Bounds = Screen.PrimaryScreen.WorkingArea;
            try { main.Activate(); }
            catch { }
            Application.DoEvents();
            Thread.Sleep(700);
            Shoot_NoShow(main, "main_idle");

            // 完成态演出：演示图经生产解码 → 真实 OnInspectionFinished（UI 线程同步路径）
            string demoOk = MakeDemoImage(Path.GetTempPath(), "docshot_ok.png", true, demoSn);
            string demoNg = MakeDemoImage(Path.GetTempPath(), "docshot_ng.png", false, demoSn);
            var wc = (System.Collections.IDictionary)typeof(MainForm)
                .GetField("_windowControls",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(main);
            var keys = new List<int>();
            foreach (int k in wc.Keys) keys.Add(k);
            keys.Sort();
            var mi = typeof(MainForm).GetMethod("OnInspectionFinished",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int seq = 1;
            // 3 个 OK（窗口1~3）
            for (int i = 0; i < 3 && i < keys.Count; i++)
            {
                using (var src = Image.FromFile(demoOk))
                {
                    var data = new WindowData
                    {
                        SeqNo = seq++,
                        IsOk = true,
                        ImagePath = "",
                        PreviewImage = ProductionCoordinator.LoadThumbnailSafe(demoOk),
                        SerialNumber = demoSn,
                        ResultText = "00000000",
                        StationNo = i + 1
                    };
                    mi.Invoke(main, new object[] { data, keys[i] });
                }
            }
            Application.DoEvents();
            Thread.Sleep(600);
            Shoot_NoShow(main, "main_ok");
            // 2 个 NG（窗口4~5）
            for (int i = 3; i < 5 && i < keys.Count; i++)
            {
                var data = new WindowData
                {
                    SeqNo = seq++,
                    IsOk = false,
                    ImagePath = "",
                    PreviewImage = ProductionCoordinator.LoadThumbnailSafe(demoNg),
                    SerialNumber = demoSn,
                    ResultText = "00000001",
                    StationNo = i + 1
                };
                mi.Invoke(main, new object[] { data, keys[i] });
            }
            Application.DoEvents();
            Thread.Sleep(600);
            Shoot_NoShow(main, "main_ng");
            try { main.Dispose(); }
            catch { }
            try { File.Delete(demoOk); File.Delete(demoNg); }
            catch { }

            Console.WriteLine(failCount == 0 ? "[DOCSHOT-ALL-PASS]" : "[DOCSHOT-FAIL] " + string.Join(",", failNames));
            return failCount == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DOCSHOT-FAIL] 异常：" + ex.GetType().Name + "：" + ex.Message);
            return 1;
        }
        finally
        {
            StopWatchdog();
            // Logs 还原
            try
            {
                if (Directory.Exists(logBak))
                {
                    if (Directory.Exists(logDir)) Directory.Delete(logDir, true);
                    Directory.CreateDirectory(logDir);
                    foreach (var f in Directory.GetFiles(logBak))
                        File.Copy(f, Path.Combine(logDir, Path.GetFileName(f)), true);
                    Directory.Delete(logBak, true);
                    Console.WriteLine("Logs 已还原");
                }
            }
            catch (Exception ex) { Console.WriteLine("WARN 日志还原失败：" + ex.Message); }
        }
    }

    // 已 Show 窗体的补拍（不重复 Show，只 PrintWindow + 校验 + 存盘）
    private static void Shoot_NoShow(Form f, string name)
    {
        try { f.Activate(); }
        catch { }
        Application.DoEvents();
        Thread.Sleep(700);
        Application.DoEvents();
        bool ok = false;
        for (int attempt = 0; attempt < 2 && !ok; attempt++)
        {
            if (attempt > 0) { Thread.Sleep(500); Application.DoEvents(); }
            RECT r;
            GetWindowRect(f.Handle, out r);
            using (var bmp = new Bitmap(Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top)))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    try { PrintWindow(f.Handle, hdc, 0); }
                    finally { g.ReleaseHdc(hdc); }
                }
                ok = CheckNonEmpty(bmp, name);
                if (ok) bmp.Save(Path.Combine(imgDir, name + ".png"));
                else if (attempt == 1) bmp.Save(Path.Combine(imgDir, name + ".FAIL.png"));
            }
        }
        if (!ok) { failCount++; failNames.Add(name); }
    }
}
