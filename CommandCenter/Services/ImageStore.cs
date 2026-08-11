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
                if (_watchedDirs.Contains(dir)) return; // 幂等：同目录只监一次
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

                string path = Path.Combine(dir, name);
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
        /// </summary>
        private static string JoinDirSegments(string rendered)
        {
            var segs = rendered.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return segs.Aggregate("", (acc, s) => Path.Combine(acc, SanitizeForPath(s)));
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
    }
}