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
    ///   目录结构默认（V2.12.1 起带 {相机} 层，因上下相机点位号各自从 1 起、必须靠相机隔开）：
    ///     保存根目录 / {年月日} / {SN} / {OKNG} / {相机}
    ///   文件名默认：{点位}.jpeg（+ 时间戳后缀）+ 同名的 .iv4p
    ///   占位符：{年月日} {年} {月} {日} {SN} {OKNG} {点位} {相机} {时间}，其余文字原样保留。
    ///   目录层级由 ImageConfig.SubDirs 列表逐级驱动（每级一个名字/生成规则），逐级渲染后建目录。
    ///   【为什么存图点位=相机点位号 + 目录 {相机} 层】：点位由"相机点位表"唯一决定，
    ///   上下相机点位号会重复（上1、下1…），文件名 {点位} 自然也会重复；{相机} 目录层保证
    ///   两家各存各的目录、互不覆盖，文件名看起来/归档上都符合"该相机点位N"的现场语义。
    ///
    /// 【线程安全】FileSystemWatcher 回调运行在监听线程，事件一定要跨线程同步到 UI（Invoke）。
    ///
    /// 【存图定期清理（V2.14.12）】
    ///   存图目录只保留最近 KeepDays 天的内容（默认 30 天，0 = 不自动清理）。
    ///   上位机启动后由 MainForm 调 StartPeriodicCleanup() 启动后台定时器（启动后 30 秒跑第一次，
    ///   之后每 24 小时一次），在【线程池线程】上扫描 SaveRootDir 的【顶层子目录】：
    ///     - 快速路径：目录名是标准日期（{年月日} 渲染的 "2026年08月11日" 或 "20260811"），
    ///       日期早于保留阈值 → 整棵目录连同子目录一起删除（默认结构第一层就是日期目录）；
    ///     - 通用路径：目录名不是日期（现场自定义了层级结构）→ 递归扫描整棵子树，
    ///       只要子树里【所有文件】的修改时间都早于阈值才删除，否则保留（防误删仍有新图）。
    ///   删除全程后台执行、不占 UI 线程；且只动 SaveRootDir 下顶层目录，绝不动相机 FTP 取图目录。
    /// </summary>
    public class ImageStore : IDisposable
    {
        private readonly ImageConfig _cfg;
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly List<string> _watchedDirs = new List<string>();

        /// <summary>存图定期清理后台定时器（V2.14.12，每天一次；null = 未启动）。</summary>
        private System.Threading.Timer _cleanupTimer;

        /// <summary>清理任务是否正在执行（防重入：删除大目录较耗时，上次没跑完不能再起下一次）。</summary>
        private volatile bool _cleanupRunning;

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
        /// <param name="stationNo">相机点位号（本相机点位表 StationNo，进文件名 {点位}；上下相机
        ///     同号重复，靠目录里的 {相机} 层隔离，见类注释）</param>
        /// <param name="isOk">本次结果（OK/NG 进目录 {OKNG}）</param>
        /// <param name="serial">产品序列号（进 {SN} 目录；可能来自扫码枪/手动输入）</param>
        /// <param name="cameraName">相机名（进目录/文件名 {相机}；保留 null/空时渲染为"未知相机"）</param>
        public string SaveImage(Image image, int stationNo, bool isOk, string serial, string cameraName)
        {
            try
            {
                DateTime now = DateTime.Now;
                string renderedFile = RenderTemplate(_cfg.FileNameTemplate, now, serial, isOk, stationNo, cameraName);

                // 目录：按 SubDirs 逐级渲染（每级名字清洗掉非法字符防路径被搞坏），逐级拼到根目录下
                var levels = _cfg.SubDirs ?? new List<string>();
                if (levels.Count == 0) levels.Add("{年月日}");   // 兜底：目录层级别是空的
                var segs = new List<string>();
                foreach (var lvl in levels)
                {
                    string rendered = RenderTemplate(lvl, now, serial, isOk, stationNo, cameraName);
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
        /// 目录按 ImageConfig.SubDirs 逐级渲染建目录。
        /// 归档文件名（V2.14.11 定稿）= **相机源文件原名 + "_" + 时间戳(yyyyMMdd_HHmmss_fff)**
        ///   ——即取 FTP 目录里源文件去掉扩展名的主名（如 0084 → 0084），
        ///   直接拼时间戳后缀，**不再用 FileNameTemplate 模板渲染**（现场要求"原文件名_时间戳"，
        ///   一眼能对应回相机里的原图；时间戳防同点位重复拍照重名覆盖）。
        ///   例：源 0084.jpeg/0084.iv4p → 归档 0084_20260814_102030_123.jpeg / 同名 .iv4p。
        /// 注意：本方法只做"复制"，【不删除】FTP 取图目录源文件——删除动作由协调器在
        /// 复制成功且确认归档完成后执行（见 ProductionCoordinator.FinishAll），避免复制失败丢图。
        /// </summary>
        /// <param name="jpegPath">FTP 取图目录里的 jpeg 源文件完整路径</param>
        /// <param name="iv4pPath">FTP 取图目录里的 iv4p 源文件完整路径（可为空/不存在则跳过）</param>
        /// <param name="stationNo">相机点位号（本相机点位表 StationNo，进目录 {OKNG} 与文件名模板占位；
        ///     上下相机同号重复，靠目录里的 {相机} 层隔离）</param>
        /// <param name="isOk">本次结果（OK/NG 进目录 {OKNG}）</param>
        /// <param name="serial">产品序列号（进 {SN} 目录）</param>
        /// <param name="cameraName">相机名（进目录/文件名 {相机}；保留 null/空时渲染为"未知相机"）</param>
        /// <returns>归档后的 jpeg 完整路径（供显示/上报）；失败返回 null</returns>
        public string SaveImageFilePair(string jpegPath, string iv4pPath, int stationNo, bool isOk, string serial, string cameraName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jpegPath) || !File.Exists(jpegPath))
                {
                    LogHelper.Error($"双格式归档失败：jpeg 源文件不存在 → {jpegPath}");
                    return null;
                }
                DateTime now = DateTime.Now;
                // 文件名主体（V2.14.11）= 相机源文件去掉扩展名的主名（如 0084），不再用模板渲染。
                // 现场要求"原文件名_时间戳"：一眼能对应回相机原图；时间戳防同点位重复拍照覆盖。
                string srcStem = Path.GetFileNameWithoutExtension(jpegPath);
                if (string.IsNullOrWhiteSpace(srcStem))
                    srcStem = "IMG_" + now.ToString("yyyyMMdd_HHmmss_fff");   // 兜底：异常文件名也用时间戳
                // 时间戳后缀（默认追加，防同点位重复拍照覆盖旧图）
                if (_cfg.FileTimestampSuffix)
                    srcStem = srcStem + "_" + now.ToString("yyyyMMdd_HHmmss_fff");

                // 目录：按 SubDirs 逐级渲染（与 SaveImage 完全同一套规则，保证两种入口归档位置一致）
                var levels = _cfg.SubDirs ?? new List<string>();
                if (levels.Count == 0) levels.Add("{年月日}");
                var segs = new List<string>();
                foreach (var lvl in levels)
                {
                    string rendered = RenderTemplate(lvl, now, serial, isOk, stationNo, cameraName);
                    if (!string.IsNullOrWhiteSpace(rendered))
                        segs.Add(SanitizeForPath(rendered));
                }
                string dir = Path.Combine(_cfg.SaveRootDir, Path.Combine(segs.ToArray()));
                Directory.CreateDirectory(dir);

                // 复制 jpeg（保持原格式，不再重编码——现场要求显示/归档都走相机原图）
                string jpegName = SanitizeForPath(srcStem) + ".jpeg";
                string jpegTarget = Path.Combine(dir, jpegName);
                CopyWithRetry(jpegPath, jpegTarget, "jpeg");

                // 复制 iv4p（原样，同名同序，供基恩士复盘问题）
                string iv4pResult = null;
                if (!string.IsNullOrWhiteSpace(iv4pPath) && File.Exists(iv4pPath))
                {
                    string iv4pName = SanitizeForPath(srcStem) + ".iv4p";
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

        /// <summary>
        /// 启动"存图目录定期清理"后台任务（V2.14.12）。MainForm 在 BuildServices 创建本服务后调用；
        /// 热更/关窗时本服务 Dispose 会自动停掉定时器（见 StopCleanup），不会残留后台任务。
        /// 定时器回调跑在【线程池线程】，删除大目录不阻塞任何 UI/协调器线程。
        /// 清理规则见类注释"存图定期清理（V2.14.12）"。
        /// </summary>
        public void StartPeriodicCleanup()
        {
            StopCleanup();
            _cleanupTimer = new System.Threading.Timer(
                _ => RunCleanupOnce(),
                null,
                TimeSpan.FromSeconds(30),   // 启动后 30 秒先跑第一次（给取图/归档留余量，不影响开机流程）
                TimeSpan.FromHours(24));    // 之后每天执行一次
            LogHelper.Info($"存图定期清理已启动：根目录 {_cfg.SaveRootDir}，保留 {_cfg.KeepDays} 天"
                + (_cfg.KeepDays <= 0 ? "（0 = 不自动清理）" : "，每天后台执行一次"));
        }

        /// <summary>
        /// 执行一次清理：扫描 SaveRootDir 顶层子目录，把"整棵子树都已过期"的目录连子目录一起删除。
        /// 全程 try-catch：任何单个目录失败只记日志跳过，绝不让清理异常影响程序其它部分。
        /// </summary>
        private void RunCleanupOnce()
        {
            // 防重入：上一次清理还没跑完（删除几十 GB 目录可能耗时数分钟），直接跳过本次
            if (_cleanupRunning) return;
            _cleanupRunning = true;
            try
            {
                int keepDays = _cfg.KeepDays;
                if (keepDays <= 0) return;   // 0 = 不自动清理
                string root = _cfg.SaveRootDir;
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;

                // 安全护栏：存图根目录绝不能是盘符根目录（如 "D:\"），否则枚举出的顶层目录就是
                // 整个盘的直接子目录，误删会毁掉整盘内容。发现是盘根直接放弃本轮清理并告警。
                if (string.Equals(Path.GetPathRoot(root).TrimEnd('\\', '/'),
                                  root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    LogHelper.Warn("存图定期清理：根目录是盘符根目录，已跳过清理（防止误删整盘）→ " + root);
                    return;
                }

                // 保留阈值：只保留最近 keepDays 天的目录（本地时间，与 File.GetLastWriteTime 同基准）
                DateTime cutoff = DateTime.Now.AddDays(-keepDays);
                int deleted = 0, skipped = 0;
                foreach (string dir in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        if (IsDirExpired(dir, cutoff))
                        {
                            Directory.Delete(dir, true);   // 递归删除整棵（目录 + 所有子目录 + 文件）
                            deleted++;
                            LogHelper.Info($"存图定期清理：已删除过期目录（早于 {cutoff:yyyy-MM-dd}，保留 {keepDays} 天）{dir}");
                        }
                        else skipped++;
                    }
                    catch (Exception ex)
                    {
                        // 单个目录删除失败（被占用/权限/正在写入）只记日志跳过，不影响其它目录
                        LogHelper.Warn($"存图定期清理：跳过目录 {dir} → {ex.Message}");
                    }
                }
                LogHelper.Info($"存图定期清理完成：共扫描 {deleted + skipped} 个顶层目录，删除 {deleted} 个，保留 {skipped} 个");
            }
            catch (Exception ex)
            {
                LogHelper.Warn("存图定期清理异常：" + ex.Message);
            }
            finally
            {
                _cleanupRunning = false;
            }
        }

        /// <summary>
        /// 判断一个目录是否"整棵子树都已过期"（即可以整体删除）。
        /// 【快速路径】目录名是标准日期（默认结构第一层就是 {年月日} 渲染的 "2026年08月11日"
        ///   或紧凑 "20260811"）：日期目录里的图都是当天拍的，目录名即最后写入日期，
        ///   日期早于保留阈值直接判定过期——O(1) 判定，不用遍历几千个文件。
        /// 【通用路径】目录名不是日期（现场自定义了层级，如顶层是 {SN} 或 {相机}）：
        ///   递归扫描整棵子树找【所有文件】的最新修改时间，最新文件都早于阈值才算过期。
        ///   只要子树里还有一个"保留期内"的文件就不删——保证只删真正过期的整棵目录。
        /// </summary>
        /// <param name="dir">要判断的目录完整路径（SaveRootDir 的直接子目录）</param>
        /// <param name="cutoff">保留阈值时间（本地时间）；早于它的文件视为过期</param>
        /// <returns>true = 整棵目录都可删除</returns>
        private static bool IsDirExpired(string dir, DateTime cutoff)
        {
            string name = Path.GetFileName(dir);
            if (TryParseDirDate(name, out DateTime dirDate) && dirDate < cutoff)
                return true;   // 日期目录：目录名即最后写入日期，直接过期
            return IsDirTreeOlderThan(dir, cutoff);   // 非日期目录：递归查最新文件时间
        }

        /// <summary>
        /// 解析标准日期目录名："{年月日}" 渲染的 "2026年08月11日"，或紧凑 "20260811"。
        /// 解析失败返回 false（目录名不是日期，交由通用路径判定）。
        /// </summary>
        private static bool TryParseDirDate(string name, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(name)) return false;
            // 格式1：2026年08月11日（ImageStore.RenderTemplate 的 {年月日} 默认渲染）
            var m = System.Text.RegularExpressions.Regex.Match(
                name, @"^(\d{4})年(\d{2})月(\d{2})日$");
            if (m.Success)
                return DateTime.TryParse(string.Format("{0}-{1}-{2}",
                    m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value), out date);
            // 格式2：20260811（紧凑日期）
            if (name.Length == 8 && int.TryParse(name, out int v) && v > 19000000)
                return DateTime.TryParseExact(name, "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out date);
            return false;
        }

        /// <summary>
        /// 递归判断目录子树内【所有文件】的修改时间是否都早于保留阈值（都老=可删）。
        /// 本目录直接含的文件逐个比较；任一文件比阈值新 → 子树未过期，立即返回 false 剪枝。
        /// 子目录逐级递归：任一子目录未过期 → 整棵未过期。
        /// 遍历/读时间失败（被占用/权限）一律视为"未过期"返回 false——保守不误删。
        /// </summary>
        private static bool IsDirTreeOlderThan(string dir, DateTime cutoff)
        {
            try
            {
                foreach (string f in Directory.EnumerateFiles(dir))
                {
                    try
                    {
                        // 本地时间比较（与 cutoff 同基准）；读不到时间的文件视为"新"保守保留
                        if (File.GetLastWriteTime(f) >= cutoff) return false;
                    }
                    catch { return false; }
                }
                foreach (string d in Directory.EnumerateDirectories(dir))
                {
                    if (!IsDirTreeOlderThan(d, cutoff)) return false;
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>停掉清理定时器（Dispose/热更重建时调用，避免后台任务残留）。</summary>
        private void StopCleanup()
        {
            try { _cleanupTimer?.Dispose(); }
            catch { }
            _cleanupTimer = null;
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
        /// <param name="stationNo">相机点位号（同 SaveImage 的 stationNo，进文件名 {点位}）</param>
        /// <param name="isOk">本次结果（OK/NG 进目录 {OKNG}）</param>
        /// <param name="serial">产品序列号（进 {SN} 目录）</param>
        /// <param name="cameraName">相机名（进目录/文件名 {相机}；保留 null/空时渲染为"未知相机"）</param>
        public string SaveImageBytes(byte[] imageData, int stationNo, bool isOk, string serial, string cameraName)
        {
            try
            {
                using (var ms = new MemoryStream(imageData))
                using (var img = Image.FromStream(ms))
                using (var copy = new Bitmap(img))
                {
                    return SaveImage(copy, stationNo, isOk, serial, cameraName);
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
        /// {相机} 渲染成相机名（cameraName）；空/未传 → "未知相机"（防目录层为空、也防两级合并）。
        /// 设为 internal：目录结构配置对话框（DirTreeEditForm）也要用同样的渲染规则做实时预览。
        /// </summary>
        internal static string RenderTemplate(string template, DateTime now,
                                             string serial, bool isOk, int stationNo, string cameraName)
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
                .Replace("{相机}", string.IsNullOrWhiteSpace(cameraName) ? "未知相机" : cameraName)
                .Replace("{时间}", now.ToString("yyyyMMdd_HHmmss_fff"));
        }

        public void Dispose()
        {
            StopCleanup();   // V2.14.12：先停后台清理定时器，再关 FTP 监听
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