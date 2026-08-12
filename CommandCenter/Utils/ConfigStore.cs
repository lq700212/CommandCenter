using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CommandCenter.Utils
{
    /// <summary>
    /// 配置存取：把 AppConfig 读写到程序目录 Config/appconfig.json。
    ///
    /// 【设计说明】
    ///   1. 首次运行无配置文件时，用模型自带默认值重建并把默认配置落盘，方便现场看着改；
    ///   2. 序列化时忽略值为 null 的字段、使用缩进格式，人工可读；
    ///   3. 保存先写临时文件再改名替换，避免"写到一半断电把配置写坏"导致程序起不来；
    ///   4. 不做任何旧配置兼容/迁移：项目未上线，配置全部以当前模型为准，字段缺了就用模型默认值。
    /// </summary>
    public static class ConfigStore
    {
        /// <summary>配置文件绝对路径 = 程序目录\Config\appconfig.json</summary>
        public static string ConfigDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        public static string ConfigFile => Path.Combine(ConfigDir, "appconfig.json");

        /// <summary>
        /// 加载配置。文件不存在则返回模型默认值（不自动写盘，由首次点保存时写）。
        /// </summary>
        public static Models.AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string json = File.ReadAllText(ConfigFile, Encoding.UTF8);
                    var cfg = JsonConvert.DeserializeObject<Models.AppConfig>(json) ?? new Models.AppConfig();

                    // 空段兜底（json 缺字段/显式 null 时用模型默认），保证后续代码不 NRE
                    // 相机列表缺省时用现场默认两台相机（V1.9.8：IP 写死，见 CameraConfig.DefaultCameras）
                    // V1.9.9：兜底条件从"仅 null"放宽到"null 或空列表"——因 AppConfig.Cameras
                    // 初始化器已改为空列表（避免 Newtonsoft 反序列化时向已存在实例 Add 而叠成 4 台），
                    // json 没写相机时必须在此补上默认两台现场相机。
                    if (cfg.Cameras == null || cfg.Cameras.Count == 0) cfg.Cameras = Models.CameraConfig.DefaultCameras();
                    if (cfg.Scanners == null) cfg.Scanners = new List<Models.ScanConfig>();
                    if (cfg.Display == null) cfg.Display = new Models.DisplayConfig();
                    if (cfg.Image == null) cfg.Image = new Models.ImageConfig();
                    if (cfg.Security == null) cfg.Security = new Models.SecurityConfig();

                    // 保证窗口→存图点位映射长度与窗口总数一致（缺的补默认、多的截断）
                    EnsureStationMap(cfg);
                    return cfg;
                }
            }
            catch (Exception ex)
            {
                // 配置损坏时不让程序崩，退回默认值并提示（调用方决定是否弹窗）
                LogHelper.Error("读取配置文件失败，已使用默认配置。原因：" + ex.Message);
            }
            return new Models.AppConfig();
        }

        /// <summary>
        /// 保存配置到 json 文件；目录不存在会自动创建。
        /// </summary>
        public static void Save(Models.AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                EnsureStationMap(config);   // 保存前把点位映射对齐到窗口总数，避免写盘出越界/缺项
                string json = JsonConvert.SerializeObject(config, new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver() // 小驼峰命名，贴近习惯
                });

                string tmp = ConfigFile + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false)); // 无 BOM UTF-8，避免 x64 下 BOM 干扰
                // 【V1.8.3 修复】优先用 File.Replace 做原子替换（目标存在时），避免"先删旧再移新"
                // 的窗口期：万一 Move 失败（杀毒软件/权限/磁盘瞬时问题）旧文件已被删，配置丢失回退默认。
                // File.Replace 在某些环境（如目标为只读、不同文件系统）会抛异常，这里 fallback 到
                // 原来的"删旧移新"（比彻底失败强），保证极端情况下也能落盘。
                if (File.Exists(ConfigFile))
                {
                    try
                    {
                        File.Replace(tmp, ConfigFile, null);
                    }
                    catch
                    {
                        try { File.Delete(ConfigFile); } catch { }
                        File.Move(tmp, ConfigFile);
                    }
                }
                else
                {
                    File.Move(tmp, ConfigFile);
                }
                LogHelper.Info("配置已保存：" + ConfigFile);
            }
            catch (Exception ex)
            {
                LogHelper.Error("保存配置失败：" + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 保证 WindowStationMap（窗口→存图点位）与显示窗口总数（Rows×Columns）对齐：
        ///   - 长度不足 → 缺的按"点位=窗口编号"补上（默认规则）；
        ///   - 长度超出 → 多余截断（窗口数改小后，超出部分丢弃）。
        /// 在加载与保存各调一次，保证运行时取 map[i] 永不越界。
        /// </summary>
        private static void EnsureStationMap(Models.AppConfig cfg)
        {
            int rows = Math.Max(1, cfg.Display.Rows);
            int cols = Math.Max(1, cfg.Display.Columns);
            int windowCount = rows * cols;

            var map = cfg.Display.WindowStationMap ?? new List<int>();
            while (map.Count < windowCount) map.Add(map.Count + 1);
            if (map.Count > windowCount) map.RemoveRange(windowCount, map.Count - windowCount);

            cfg.Display.WindowStationMap = map;
        }
    }
}
