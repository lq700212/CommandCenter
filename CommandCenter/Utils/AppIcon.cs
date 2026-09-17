using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Utils
{
    /// <summary>
    /// 软件主图标统一入口（V2.16.5 光阑视界 IrisVision）。
    ///
    /// 为什么要有这个类：
    ///   软件图标有 4 个露脸的地方——① exe 文件本身（资源管理器里看到的）、
    ///   ② 桌面快捷方式、③ 任务栏（运行中 + 固定到任务栏）、④ 每个窗体的标题栏左上角。
    ///   其中 ①②③ 靠 csproj 的 &lt;ApplicationIcon&gt;Resources\app.ico&lt;/ApplicationIcon&gt;
    ///   自动搞定（图标编译进 exe 本体，快捷方式/任务栏默认就取 exe 的图标）；
    ///   ④ 标题栏图标需要每个窗体逐个指定，本类就是给它们用的"同一个水龙头"。
    ///
    /// 为什么从 exe 里取、而不直接读 Resources\app.ico 文件：
    ///   现场部署时只拷 bin\Obfuscated（或 Debug）目录里的 exe+dll+config，
    ///   Resources 源文件不会跟着去——运行时按文件路径读 ico 必扑空。
    ///   而 exe 本体自带图标（ApplicationIcon 打进去的），用
    ///   Icon.ExtractAssociatedIcon 取"自己这个 exe 的图标"永远有货，
    ///   且与 ①②③ 天然是同一个图标，不会出现"快捷方式一个样、标题栏另一个样"。
    ///
    /// 用法（每个窗体构造里 InitializeComponent() 之后加两行，见 MainForm 等 9 个窗体）：
    ///   var appIcon = Utils.AppIcon.Get();
    ///   if (appIcon != null) this.Icon = appIcon;
    ///
    /// 注意事项：
    ///   - 返回的是进程级缓存单例，绝不 Dispose——窗体的 Icon 属性只是引用它，
    ///     Dispose 后所有标题栏图标会变空白。
    ///   - VS 设计器里打开窗体时返回 null（设计时取到的是 devenv.exe 的图标，
    ///     会污染设计器显示，故直接跳过，运行时才真正赋值）。
    ///   - 取图标失败（极端情况，如 exe 路径不可读）返回 null，调用方保持原 Icon，
    ///     程序照常跑，只是不换图标——图标绝不能成为启动崩溃的原因。
    /// </summary>
    public static class AppIcon
    {
        // 进程级缓存：取图标要读一次 exe 文件，缓存后 9 个窗体复用同一个实例，
        // 既省 IO 又保证所有标题栏是同一个图标对象。lock 防多线程重复提取。
        private static readonly object _lock = new object();
        private static Icon _cached;
        private static bool _tried;   // 是否已经尝试过（失败也记，避免每次都重试读文件）

        /// <summary>
        /// 取软件主图标（exe 内嵌的 ApplicationIcon）。设计时/失败时返回 null。
        /// </summary>
        public static Icon Get()
        {
            // VS 设计器里不取：此时 ExecutablePath 是 devenv.exe，拿它的图标没意义，
            // 还会让设计器里的窗体显示错图标。
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return null;

            lock (_lock)
            {
                if (_cached != null)
                    return _cached;
                if (_tried)
                    return null;   // 上次已失败，不再重复读文件
                _tried = true;

                try
                {
                    string exePath = Application.ExecutablePath;
                    if (string.IsNullOrWhiteSpace(exePath))
                        return null;
                    // 取 exe 的第一个图标 = csproj ApplicationIcon 指定的 Resources\app.ico。
                    _cached = Icon.ExtractAssociatedIcon(exePath);
                    return _cached;
                }
                catch
                {
                    // 图标是"面子"，绝不能因为它让程序起不来：吞掉异常返回 null。
                    return null;
                }
            }
        }
    }
}
