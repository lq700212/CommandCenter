using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// RealShot：真实进程全部门窗实拍驱动（与 DocShot 配对做 A/B），V2 重写版。
//
// 【V1 血泪根因（2026-09-17 现场事故）】V1 用 SendMessage(BM_CLICK) 点按钮——
// SendMessage 是同步的，按钮一旦弹出模态框，发送线程就卡死在里面直到框被关掉，
// 结果每个弹窗都得用户手工关，自动化变"人工化"。V2 铁律：开窗类点击一律
// PostMessage(BM_CLICK)（异步，点了就回）+ WaitTitle 等窗出现，发送线程永不阻塞。
// 配套三条：① 登录框 user/pass 按屏幕坐标（上→下）排序定位，一次填对，杜绝
// "顺序反了→登录报错弹窗"；② 看门狗自动关未知小弹窗（保护 9 个目标窗，只关别的）；
// ③ 任何 FAIL 当场截全屏留证；④ 启动前检查单实例（已有进程则直接退出，不弹
// "程序已在运行"添乱）；⑤ 日志全英文（中文控制台解码乱码，事后读不懂等于没记）。
//
// 只开窗 + 截图 + 取消，绝不点"保存/删除/启动/触发/写入"类真动作按钮。
// 编译（仓库根）：csc /nologo /target:exe /codepage:65001
//   /out:CommandCenter\bin\Debug\cc_realshot.exe
//   /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
//   CommandCenter\tools\DocShot\RealShot.cs
// 运行（workdir=CommandCenter\bin\Debug）：
//   .\cc_realshot.exe <输出目录绝对路径>
// 退出码恒 0（缺拍名单打终端转人工，不抛异常吓人）。
public static class RealShot
{
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWndProc cb, IntPtr l);
    private delegate bool EnumWndProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr p, EnumWndProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, string l);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    private const uint BM_CLICK = 0xF5;
    private const uint WM_CLOSE = 0x10;
    private const uint WM_SETTEXT = 0x000C;
    private const uint WM_KEYDOWN = 0x0100;
    private const int VK_ESCAPE = 0x1B;

    // 9 个目标窗标题关键字（看门狗保护名单，不在名单的本进程小弹窗一律关掉）
    private static readonly string[] ProtectedTitles = new string[]
    {
        "光阑视界", "账号登录", "手动输入序列号", "系统设置", "窗口/点位",
        "图片存储目录结构配置", "产品型号配置", "扫码枪异常", "开发者模式"
    };

    private static uint targetPid;
    private static string outDir;
    private static readonly List<string> missing = new List<string>();
    private static volatile bool watchdogRun = false;

    private static void Log(string s) { Console.WriteLine(s); }

    private static List<IntPtr> TopWindows()
    {
        var list = new List<IntPtr>();
        try
        {
            EnumWindows((h, l) =>
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == targetPid && IsWindowVisible(h)) list.Add(h);
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        return list;
    }

    private static string TitleOf(IntPtr h)
    {
        var sb = new StringBuilder(512);
        GetWindowText(h, sb, 512);
        return sb.ToString();
    }

    private static bool IsProtected(string title)
    {
        foreach (var p in ProtectedTitles)
            if (title.Contains(p)) return true;
        return false;
    }

    // 看门狗：只关"非目标"小弹窗（#32770 对话框类）。目标窗再怪也不碰。
    private static void StartWatchdog()
    {
        watchdogRun = true;
        var t = new Thread(() =>
        {
            while (watchdogRun)
            {
                try
                {
                    foreach (var h in TopWindows())
                    {
                        var cls = new StringBuilder(256);
                        GetClassName(h, cls, 256);
                        if (cls.ToString() != "#32770") continue;
                        string title = TitleOf(h);
                        if (IsProtected(title)) continue;
                        PostMessage(h, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
                        PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        Log("[REALSHOT-WD] dismissed popup [" + title + "]");
                    }
                }
                catch { }
                Thread.Sleep(300);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    private static IntPtr WaitTitle(string part, int seconds, string tag)
    {
        for (int i = 0; i < seconds * 2; i++)
        {
            foreach (var h in TopWindows())
                if (TitleOf(h).Contains(part)) return h;
            Thread.Sleep(500);
        }
        Log("[REALSHOT-FAIL] wait-timeout [" + tag + "] want-title-contains=" + part);
        FailShot("wait-" + tag);
        missing.Add("wait:" + tag);
        return IntPtr.Zero;
    }

    private static void FailShot(string tag)
    {
        // FAIL 当场截全屏留证（事后复盘用，不进文档）
        try
        {
            var b = Screen.PrimaryScreen.Bounds;
            using (var bmp = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(b.X, b.Y, 0, 0, b.Size);
                bmp.Save(Path.Combine(outDir, "FAIL-" + tag + ".png"));
            }
        }
        catch { }
    }

    private static List<IntPtr> Children(IntPtr parent, string clsPart)
    {
        var list = new List<IntPtr>();
        EnumChildWindows(parent, (h, l) =>
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            if (sb.ToString().Contains(clsPart)) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    // 异步点击（V2 核心修复）：PostMessage 发完即回，绝不等；成败由后续 WaitTitle 判定。
    private static bool PostClick(IntPtr parent, string textPart, string tag)
    {
        foreach (var b in Children(parent, "BUTTON"))
        {
            if (TitleOf(b).Contains(textPart))
            {
                PostMessage(b, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
        }
        Log("[REALSHOT-FAIL] button-not-found [" + tag + "] want-button-contains=" + textPart);
        FailShot("button-" + tag);
        missing.Add("button:" + tag);
        return false;
    }

    private static void Shoot(IntPtr h, string name)
    {
        try { SetForegroundWindow(h); }
        catch { }
        Thread.Sleep(700);
        RECT r;
        GetWindowRect(h, out r);
        using (var bmp = new Bitmap(Math.Max(1, r.R - r.L), Math.Max(1, r.B - r.T)))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try { PrintWindow(h, hdc, 0); }
                finally { g.ReleaseHdc(hdc); }
            }
            bmp.Save(Path.Combine(outDir, name + "-real.png"));
        }
        Log("[REALSHOT-OK] " + name);
    }

    private struct EditPos { public IntPtr H; public int Top; public int Left; }

    private static List<EditPos> SortedEdits(IntPtr parent)
    {
        var pos = new List<EditPos>();
        foreach (var e in Children(parent, "EDIT"))
        {
            RECT r;
            GetWindowRect(e, out r);
            pos.Add(new EditPos { H = e, Top = r.T, Left = r.L });
        }
        pos.Sort((a, b) => a.Top != b.Top ? a.Top - b.Top : a.Left - b.Left);
        return pos;
    }

    // 登录：Edit 按屏幕坐标上→下排序（用户名行在上、密码行在下），一次填对。
    private static bool LoginAs(IntPtr loginWnd, string user, string pwd)
    {
        var pos = SortedEdits(loginWnd);
        if (pos.Count < 2)
        {
            Log("[REALSHOT-FAIL] login-edits<2");
            FailShot("login-edits");
            missing.Add("login-edits");
            return false;
        }
        SendMessage(pos[0].H, WM_SETTEXT, IntPtr.Zero, user);
        SendMessage(pos[1].H, WM_SETTEXT, IntPtr.Zero, pwd);
        Thread.Sleep(400);
        if (!PostClick(loginWnd, "登 录", "do-login")) return false;
        Thread.Sleep(2500);
        foreach (var h in TopWindows())
            if (TitleOf(h) == "账号登录")
            {
                Log("[REALSHOT-FAIL] login-window-still-there (bad-credential-or-blocked)");
                FailShot("login-stuck");
                missing.Add("login:" + user);
                Esc(h);
                Thread.Sleep(800);
                return false;
            }
        return true;
    }

    private static void Esc(IntPtr h)
    {
        PostMessage(h, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
        PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length < 1) { Log("usage: cc_realshot.exe <outdir>"); return 2; }
        outDir = args[0];
        Directory.CreateDirectory(outDir);

        // 单实例预检：已有进程则退出，不弹"程序已在运行"添乱
        foreach (var ex in Process.GetProcessesByName("CommandCenter"))
        {
            Log("[REALSHOT-ABORT] another CommandCenter process alive, pid=" + ex.Id + " (close it first)");
            return 3;
        }

        var exe = Path.Combine(Environment.CurrentDirectory, "CommandCenter.exe");
        // Logs 备份（跑完还原：真进程启动即写当天日志，不备份会把测试登录留在开发机日志里）
        string workDir = Environment.CurrentDirectory;
        string logDir = Path.Combine(workDir, "Logs");
        string logBak = Path.Combine(Path.GetTempPath(), "realshot_logs_bak");
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
        catch (Exception ex) { Log("WARN log-backup-failed (continue): " + ex.Message); }
        var p = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Environment.CurrentDirectory });
        targetPid = (uint)p.Id;
        Log("target pid=" + targetPid);
        Thread.Sleep(14000);
        StartWatchdog();

        try
        {
            IntPtr main = WaitTitle("光阑视界", 10, "main");
            if (main == IntPtr.Zero) return 1;

            // (1) manual-serial (no login needed).
            // Demo content: type DEMO SN so docs show where to type (Esc discards).
            if (PostClick(main, "人工补录", "open-serial"))
            {
                IntPtr serial = WaitTitle("手动输入序列号", 8, "serial");
                if (serial != IntPtr.Zero)
                {
                    var se = SortedEdits(serial);
                    if (se.Count > 0) SendMessage(se[0].H, WM_SETTEXT, IntPtr.Zero, "DEMO-SN-0001");
                    Thread.Sleep(300);
                    Shoot(serial, "serialinput");
                    PostClick(serial, "取 消", "close-serial");
                    Thread.Sleep(800);
                }
            }

            // (2) login dialog itself. Demo content: username=admin (the operator-facing account).
            if (PostClick(main, "系统设置", "open-login"))
            {
                IntPtr login = WaitTitle("账号登录", 8, "login");
                if (login != IntPtr.Zero)
                {
                    var le = SortedEdits(login);
                    if (le.Count > 0) SendMessage(le[0].H, WM_SETTEXT, IntPtr.Zero, "admin");
                    Thread.Sleep(300);
                    Shoot(login, "login");
                }

                // (3) admin -> settings + 3 sub dialogs (open+shoot+cancel, never save)
                if (login != IntPtr.Zero && LoginAs(login, "admin", "admin123"))
                {
                    IntPtr settings = WaitTitle("系统设置", 8, "settings");
                    if (settings != IntPtr.Zero)
                    {
                        Shoot(settings, "settings");
                        if (PostClick(settings, "窗口/点位配置", "open-windowpoint"))
                        {
                            IntPtr wp = WaitTitle("窗口/点位", 8, "windowpoint");
                            if (wp != IntPtr.Zero)
                            {
                                Shoot(wp, "windowpoint");
                                PostClick(wp, "取消", "close-windowpoint");
                                Thread.Sleep(800);
                            }
                        }
                        if (PostClick(settings, "配置目录结构", "open-dirtree"))
                        {
                            IntPtr dt = WaitTitle("图片存储目录结构配置", 8, "dirtree");
                            if (dt != IntPtr.Zero)
                            {
                                Shoot(dt, "dirtree");
                                PostClick(dt, "取消", "close-dirtree");
                                Thread.Sleep(800);
                            }
                        }
                        if (PostClick(settings, "产品型号配置", "open-modelindex"))
                        {
                            IntPtr mi = WaitTitle("产品型号配置", 8, "modelindex");
                            if (mi != IntPtr.Zero)
                            {
                                Shoot(mi, "modelindex");
                                PostClick(mi, "取 消", "close-modelindex");
                                Thread.Sleep(800);
                            }
                        }
                        PostClick(settings, "取消", "close-settings-without-save");
                        Thread.Sleep(1000);
                    }
                }
            }

            // (4) dev -> devmode + scannerfail (invoked by button, real window)
            if (PostClick(main, "系统设置", "open-login2"))
            {
                IntPtr login2 = WaitTitle("账号登录", 8, "login2");
                if (login2 != IntPtr.Zero && LoginAs(login2, "dev", "dev123"))
                {
                    IntPtr dev = WaitTitle("开发者模式", 8, "devmode");
                    if (dev != IntPtr.Zero)
                    {
                        Shoot(dev, "devmode");
                        if (PostClick(dev, "显示扫码枪异常弹窗", "open-scanfail"))
                        {
                            IntPtr sf = WaitTitle("扫码枪异常", 8, "scanfail");
                            if (sf != IntPtr.Zero)
                            {
                                Shoot(sf, "scannerfail");
                                PostClick(sf, "稍后处理", "close-scanfail");
                                Thread.Sleep(800);
                            }
                        }
                        PostMessage(dev, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        Thread.Sleep(1000);
                    }
                }
            }
        }
        finally
        {
            watchdogRun = false;
            try
            {
                if (!p.HasExited)
                {
                    PostMessage(p.MainWindowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    if (!p.WaitForExit(5000)) p.Kill();
                }
                Log(p.HasExited ? "target exited clean" : "target killed");
            }
            catch (Exception ex) { Log("shutdown note: " + ex.GetType().Name); }
            try
            {
                if (Directory.Exists(logBak))
                {
                    if (Directory.Exists(logDir)) Directory.Delete(logDir, true);
                    Directory.CreateDirectory(logDir);
                    foreach (var f in Directory.GetFiles(logBak))
                        File.Copy(f, Path.Combine(logDir, Path.GetFileName(f)), true);
                    Directory.Delete(logBak, true);
                    Log("logs restored");
                }
            }
            catch (Exception ex) { Log("WARN log-restore-failed: " + ex.Message); }
        }

        Log(missing.Count == 0 ? "[REALSHOT-ALL-OK]" : "[REALSHOT-PARTIAL] missing=" + string.Join(",", missing));
        return 0;
    }
}
