using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Services
{
    /// <summary>
    /// 图像存储服务：负责把取到的照片落盘，并监听各相机 FTP 上传目录的新图。
    ///
    /// 【多相机】
    ///   现场不止一台相机时，每台相机会把照片推到【自己的】FTP 目录（CameraConfig.FtpUploadDir），
    ///   因此 ImageStore 为每台相机各建一个 FileSystemWatcher，新图事件带相机索引，
    ///   主流程据此区分"这张图来自哪台相机、对应哪个点位"。
    ///
    /// 【存图规则（可配置，见 ImageConfig）】
    ///   目录结构默认：保存根目录 / {年月日} / {SN} / {OKNG}
    ///   文件名默认：{点位}.png
    ///   占位符：{年月日} {年} {月} {日} {SN} {OKNG} {点位} {时间}，其余文字原样保留。
    ///   目录层级由 ImageConfig.SubDirs 列表逐级驱动（每级一个名字/生成规则），逐级渲染后建目录。
    ///
    /// 【线程安全】FileSystemWatcher 回调运行在监听线程，事件一定要跨线程同步到 UI（Invoke）。
    /// </summary>
    public class ImageStore : IDisposable
    {
        private readonly ImageConfig _cfg;
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly List<string> _watchedDirs = new List<string>();

        /// <summary>
        /// 相机 FTP 上传新图事件。参数：相机索引（对应配置 Cameras 下标）、文件完整路径。
        /// 注意：可能在非 UI 线程触发，UI 订阅方需自己 Invoke。
        /// </summary>
        public event Action<int, string> FtpFileArrived;

        public ImageStore(ImageConfig cfg) => _cfg = cfg;

        /// <summary>全局 FTP 兜底目录（相机未单独配 FtpUploadDir 时用它来监听）</summary>
        public string DefaultFtpDir => _cfg.FtpRootDir;

        /// <summary>
        /// 注册并启动一路相机 FTP 上传目录的监听（不存在的目录自动创建）。
        /// 同一目录重复注册会被忽略；多台相机必须各配各的目录，否则新图归属分不清。
        /// </summary>
        /// <param name="dir">该相机 FTP 上传目录</param>
        /// <param name="cameraIndex">相机索引（0 起，对应 AppConfig.Cameras 下标）</param>
        public void AddMonitor(string dir, int cameraIndex)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            lock (_watchedDirs)
            {
                // 幂等：同目录只监一次。比较时把尾斜杠去掉并忽略大小写（Windows 路径不区分大小写，
                // 否则 "D:\x" 与 "D:\x\" / "d:\X" 会被当成两个目录重复监听，造成重复取图）。
                if (_watchedDirs.Any(x => string.Equals(
                        NormalizeDir(x), NormalizeDir(dir), StringComparison.OrdinalIgnoreCase)))
                    return;
                try
                {
                    Directory.CreateDirectory(dir);
                    var watcher = new FileSystemWatcher(dir)
                    {
                        Filter = "*.*",
                        IncludeSubdirectories = false,
                        EnableRaisingEvents = true
                    };
                    // FTP 上传常是"先临时名后改名"，Created 与 Renamed 都监听
                    watcher.Created += (s, e) => FtpFileArrived?.Invoke(cameraIndex, e.FullPath);
                    watcher.Renamed += (s, e) => FtpFileArrived?.Invoke(cameraIndex, e.FullPath);
                    _watchedDirs.Add(dir);
                    _watchers.Add(watcher);
                    LogHelper.Info($"相机[{cameraIndex}]开始监听 FTP 目录：{dir}");
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"启动相机[{cameraIndex}] FTP 目录监听失败{dir}：{ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 把一张 Bitmap 保存到本地。返回保存后的完整路径；失败返回 null。
        /// 目录按 ImageConfig.SubDirs 逐级渲染建目录（详见类注释），文件名按 FileNameTemplate 模板；
        /// 目录列表为空时兜底建 "{年月日}" 一层，文件名模板为空时兜底用时间戳命名。
        /// </summary>
        /// <param name="image">要保存的图片</param>
        /// <param name="stationNo">拍照点位号（窗口存图点位 DisplayConfig.WindowStationMap，进文件名 {点位}）</param>
        /// <param name="isOk">本次结果（OK/NG 进目录 {OKNG}）</param>
        /// <param name="serial">产品序列号（进 {SN} 目录；可能来自扫码枪/手动输入）</param>
        public string SaveImage(Image image, int stationNo, bool isOk, string serial)
        {
            try
            {
                DateTime now = DateTime.Now;
                string renderedFile = RenderTemplate(_cfg.FileNameTemplate, now, serial, isOk, stationNo);

                // 目录：按 SubDirs 逐级渲染（每级名字清洗掉非法字符防路径被搞坏），逐级拼到根目录下
                var levels = _cfg.SubDirs ?? new List<string>();
                if (levels.Count == 0) levels.Add("{年月日}");   // 兜底：目录层级别是空的
                var segs = new List<string>();
                foreach (var lvl in levels)
                {
                    string rendered = RenderTemplate(lvl, now, serial, isOk, stationNo);
                    if (!string.IsNullOrWhiteSpace(rendered))
                        segs.Add(SanitizeForPath(rendered));
                }
                string dir = Path.Combine(_cfg.SaveRootDir, Path.Combine(segs.ToArray()));
                Directory.CreateDirectory(dir);

                string name = string.IsNullOrWhiteSpace(renderedFile)
                    ? $"IMG_{now:yyyyMMdd_HHmmss_fff}_{(isOk ? "OK" : "NG")}.png"   // 模板留空时的兜底命名
                    : SanitizeForPath(renderedFile) + ".png";

                // 【防重名覆盖】默认文件名模板 "{点位}" 下，同一 SN/判定目录里同点位二次拍照
                // 必然重名，直接覆盖会丢历史图。这里检测重名自动追加 "_2/_3…" 序号兜底
                // （模板带 {时间} 时基本不重名，此逻辑只是保险，不改变任何存图规则）。
                string path = Path.Combine(dir, name);
                int dup = 2;
                while (File.Exists(path))
                {
                    string stem = Path.GetFileNameWithoutExtension(name);
                    path = Path.Combine(dir, $"{stem}_{dup}.png");
                    dup++;
                }
                image.Save(path, ImageFormat.Png);
                LogHelper.Info($"照片已保存：{path}");
                return path;
            }
            catch (Exception ex)
            {
                LogHelper.Error("照片保存失败", ex);
                return null;
            }
        }

        /// <summary>
        /// 把"相机 FTP 取图目录（中转暂存区）里的一对源文件"原样复制归档（V1.12.18）。
        /// 现场约定（与基恩士工程师确认）：相机拍照后往自己的 FTP 取图目录推两个文件——
        ///   `0000.jpeg`：上位机显示/归档用（显示取 jpeg 格式即可）；
        ///   `0000.iv4p`：基恩士复盘问题用的私有格式，上位机不解析、原样复制保存；
        /// 目录按 ImageConfig.SubDirs 逐级渲染建目录，文件名 = FileNameTemplate 渲染结果
        /// + 时间戳后缀（FileTimestampSuffix=true 时，防同点位重复拍照重名覆盖）。
        /// 注意：本方法只做"复制"，【不删除】FTP 取图目录源文件——删除动作由协调器在
        /// 复制成功且确认归档完成后执行（见 ProductionCoordinator.FinishAll），避免复制失败丢图。
        /// </summary>
        /// <param name="jpegPath">FTP 取图目录里的 jpeg 源文件完整路径</param>
        /// <param name="iv4pPath">FTP 取图目录里的 iv4p 源文件完整路径（可为空/不存在则跳过）</param>
        /// <param name="stationNo">拍照点位号（进文件名 {点位}）</param>
        /// <param name="isOk">本次结果（OK/NG 进目录 {OKNG}）</param>
        /// <param name="serial">产品序列号（进 {SN} 目录）</param>
        /// <returns>归档后的 jpeg 完整路径（供显示/上报）；失败返回 null</returns>
        public string SaveImageFilePair(string jpegPath, string iv4pPath, int stationNo, bool isOk, string serial)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jpegPath) || !File.Exists(jpegPath))
                {
                    LogHelper.Error($"双格式归档失败：jpeg 源文件不存在 → {jpegPath}");
                    return null;
                }
                DateTime now = DateTime.Now;
                // 文件名主体 = 模板渲染结果；模板为空时兜底用 "IMG_{时间戳}"
                string stem = RenderTemplate(_cfg.FileNameTemplate, now, serial, isOk, stationNo);
                if (string.IsNullOrWhiteSpace(stem))
                    stem = "IMG_" + now.ToString("yyyyMMdd_HHmmss_fff");
                // 时间戳后缀：默认追加（现场约定，防同点位重复拍照覆盖旧图）
                if (_cfg.FileTimestampSuffix)
                    stem = stem + "_" + now.ToString("yyyyMMdd_HHmmss_fff");

                // 目录：按 SubDirs 逐级渲染（与 SaveImage 完全同一套规则，保证两种入口归档位置一致）
                var levels = _cfg.SubDirs ?? new List<string>();
                if (levels.Count == 0) levels.Add("{年月日}");
                var segs = new List<string>();
                foreach (var lvl in levels)
                {
                    string rendered = RenderTemplate(lvl, now, serial, isOk, stationNo);
                    if (!string.IsNullOrWhiteSpace(rendered))
                        segs.Add(SanitizeForPath(rendered));
                }
                string dir = Path.Combine(_cfg.SaveRootDir, Path.Combine(segs.ToArray()));
                Directory.CreateDirectory(dir);

                // 复制 jpeg（保持原格式，不再重编码——现场要求显示/归档都走相机原图）
                string jpegName = SanitizeForPath(stem) + ".jpeg";
                string jpegTarget = Path.Combine(dir, jpegName);
                CopyWithRetry(jpegPath, jpegTarget, "jpeg");

                // 复制 iv4p（原样，同名同序，供基恩士复盘问题）
                string iv4pResult = null;
                if (!string.IsNullOrWhiteSpace(iv4pPath) && File.Exists(iv4pPath))
                {
                    string iv4pName = SanitizeForPath(stem) + ".iv4p";
                    string iv4pTarget = Path.Combine(dir, iv4pName);
                    CopyWithRetry(iv4pPath, iv4pTarget, "iv4p");
                    iv4pResult = iv4pTarget;
                }
                LogHelper.Info($"图片双格式归档完成：{jpegTarget}" + (iv4pResult != null ? " | " + iv4pResult : "（无 iv4p）"));
                return jpegTarget;
            }
            catch (Exception ex)
            {
                LogHelper.Error("双格式归档异常", ex);
                return null;
            }
        }

        /// <summary>
        /// 从相机 FTP 取图目录里找"修改时间最新"的一对文件（V1.12.24 放错机制）。
        ///
        /// 【背景】基恩士相机推图文件名不保证恒为 0000.jpeg / 0000.iv4p，
        ///   现场实测可能是 0084.jpeg、0084.iv4p 等任意编号。旧实现依赖 FileSystemWatcher
        ///   事件记路径（事件本身兼容任意文件名），但若事件漏报/错过、或归档用的路径写死
        ///   文件名，就会取不到图。本方法【不写死任何文件名】，按扩展名分组后分别取
        ///   LastWriteTimeUtc 最新的一张——不管相机命名成什么样，都能拿到"最近这一张"。
        ///   调用时机在协调器收尾归档前（ProductionCoordinator.FinishAll）与功能测试窗体
        ///   取图（DevTestForm），事件路径仅作为目录扫描失败时的兜底。
        /// </summary>
        /// <param name="dir">该相机的 FTP 取图目录（相机配置 FtpUploadDir，空缺用全局 FtpRootDir）</param>
        /// <returns>最新一对结果：JpegPath / IvpPath（找不到对应文件则为 null；目录不存在返回空结果）</returns>
        public LatestPairResult FindLatestPair(string dir)
        {
            var result = new LatestPairResult();
            // 目录不存在（相机还没建/网盘未挂载）：直接返回空结果，由调用方走事件路径兜底或报错
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return result;
            try
            {
                // 遍历目录顶层文件（不递归），按扩展名分组，各组取修改时间最新的那一个。
                // jpeg 组收 .jpeg/.jpg（都算显示主体）；iv4p 组收 .iv4p（基恩士复盘私有格式）。
                string jpeg = null, iv4p = null;
                DateTime jpegTime = DateTime.MinValue, iv4pTime = DateTime.MinValue;
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    string ext = Path.GetExtension(f);
                    DateTime lastWrite;
                    try { lastWrite = File.GetLastWriteTimeUtc(f); }
                    catch { continue; } // 个别文件读时间失败（被占用/瞬间删除）跳过，不影响整体
                    if (ext.Equals(".iv4p", StringComparison.OrdinalIgnoreCase))
                    {
                        if (lastWrite > iv4pTime) { iv4pTime = lastWrite; iv4p = f; }
                    }
                    else if (ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                          || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        if (lastWrite > jpegTime) { jpegTime = lastWrite; jpeg = f; }
                    }
                }
                result.JpegPath = jpeg;
                result.IvpPath = iv4p;
                if (jpeg != null)
                    LogHelper.Info($"从 FTP 取图目录取到最近图片：{jpeg}" + (iv4p != null ? " | " + iv4p : "（无 iv4p）"));
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"扫描 FTP 取图目录取最新文件失败：{dir}", ex);
                return result;
            }
        }

        /// <summary>
        /// 删除 FTP 取图目录里的单个源文件（V1.12.24 起供功能测试窗体复用，与协调器"处理即删"一致）。
        /// 文件不存在/删除失败一律静默记日志、不抛异常：
        ///   - 不存在：本来就已删（重复删除场景），正常；
        ///   - 被占用删除失败：多留一个文件无害（取图按"修改时间最新"仍能拿到下一张），但记日志供现场排查。
        /// 【调用时机】必须在归档复制成功之后调用（调用方保证），否则复制失败会把图弄丢。
        /// </summary>
        /// <param name="path">要删除的源文件完整路径（可为 null/空/不存在，均安全）</param>
        /// <param name="tag">日志归属标签（如"点位1"/"功能测试 相机1"），仅用于日志定位</param>
        public static void DeleteSourceFile(string path, string tag)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    LogHelper.Info($"{tag} 已删除 FTP 取图源文件：{path}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warn($"{tag} 删除 FTP 源文件失败（不影响结果）：{path} → {ex.Message}");
            }
        }

        /// <summary>复制文件并带重试（FTP 源文件可能正在被相机写/事件早于写完到达）。
        /// 复用 V1.8.3 的 FileShare.ReadWrite 思路：源文件正在写也能复制；失败短延迟重试最多 3 次。</summary>
        private static void CopyWithRetry(string src, string dst, string tag)
        {
            Exception last = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    File.Copy(src, dst, true);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    System.Threading.Thread.Sleep(400);
                }
            }
            throw last ?? new InvalidOperationException($"复制 {tag} 失败：{src}");
        }

        /// <summary>
        /// 把"TCP/BR 指令读回的图像字节"解码成 Bitmap 后按模板归档（V1.7.0，SaveImage 的字节入口）。
        /// 期望字节是完整 24bit BMP 文件（以 'BM' 开头，Image.FromStream 可直接解码）；
        /// 解码失败返回 null（不落盘坏文件），由调用方记日志——若现场实测确认是"裸像素"
        /// （无 BMP 文件头），需在 KeyenceIV4Camera.ReadImage 侧按实测补文件头后再调用本方法。
        /// </summary>
        /// <param name="imageData">BR 指令读回的图像字节</param>
        /// <param name="stationNo">拍照点位号（同 SaveImage 的 stationNo，进文件名 {点位}）</param>
        /// <param name="isOk">本次结果（OK/NG 进目录 {OKNG}）</param>
        /// <param name="serial">产品序列号（进 {SN} 目录）</param>
        public string SaveImageBytes(byte[] imageData, int stationNo, bool isOk, string serial)
        {
            try
            {
                using (var ms = new MemoryStream(imageData))
                using (var img = Image.FromStream(ms))
                using (var copy = new Bitmap(img))
                {
                    return SaveImage(copy, stationNo, isOk, serial);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("图像字节归档失败（若相机非标准 BMP 返回，需按实测格式补 BMP 文件头）", ex);
                return null;
            }
        }

        /// <summary>把非法文件名字符替换成下划线，避免序列号等动态内容把路径搞坏。</summary>
        private static string SanitizeForPath(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            char[] bad = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(bad.Contains(c) ? '_' : c);
            return sb.ToString();
        }

        /// <summary>去掉目录末尾的斜杠（正反斜杠都处理），供幂等判重使用。</summary>
        private static string NormalizeDir(string dir)
        {
            return (dir ?? "").TrimEnd('\\', '/');
        }

        /// <summary>
        /// 渲染模板：替换全部占位符。未识别的 {xxx} 原样保留（由现场自己控制，写错也只是变成路径字符）。
        /// {年月日} 是一个整体目录名（如"2026年08月11日"），不是年/月/日三级目录。
        /// 设为 internal：目录结构配置对话框（DirTreeEditForm）也要用同样的渲染规则做实时预览。
        /// </summary>
        internal static string RenderTemplate(string template, DateTime now,
                                             string serial, bool isOk, int stationNo)
        {
            if (string.IsNullOrWhiteSpace(template)) return "";
            return template
                .Replace("{年月日}", now.ToString("yyyy年MM月dd日"))
                .Replace("{年}", now.ToString("yyyy"))
                .Replace("{月}", now.ToString("MM"))
                .Replace("{日}", now.ToString("dd"))
                .Replace("{SN}", string.IsNullOrWhiteSpace(serial) ? "未知SN" : serial)
                .Replace("{OKNG}", isOk ? "OK" : "NG")
                .Replace("{点位}", stationNo.ToString())
                .Replace("{时间}", now.ToString("yyyyMMdd_HHmmss_fff"));
        }

        public void Dispose()
        {
            foreach (var w in _watchers)
            {
                try { w.EnableRaisingEvents = false; } catch { }
                try { w.Dispose(); } catch { }
            }
            _watchers.Clear();
        }

        /// <summary>
        /// FTP 取图目录里"修改时间最新"的一对文件（V1.12.24，FindLatestPair 的返回值）。
        /// JpegPath 为最新 .jpeg/.jpg（显示/归档主体）；IvpPath 为最新 .iv4p
        /// （基恩士复盘私有格式，可能为 null=目录里没有 iv4p）。文件名不固定（非 0000）。
        /// </summary>
        public class LatestPairResult
        {
            /// <summary>最新 jpeg 源文件完整路径（可能为 null=目录里没有 jpeg）</summary>
            public string JpegPath;

            /// <summary>最新 iv4p 源文件完整路径（可能为 null=目录里没有 iv4p）</summary>
            public string IvpPath;
        }
    }
}