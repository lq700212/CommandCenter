using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    ///   4. 兼容旧版单相机配置：旧 json 的 "camera" 是对象、新版是 "cameras" 列表，
    ///      加载时若列表为空则用旧字段迁移到第 0 项，现场升级不用重配。
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

                    // 兼容迁移：旧配置只有 "camera" 单对象，把它补进 Cameras 列表第 0 项
                    if (cfg.Cameras == null || cfg.Cameras.Count == 0)
                    {
                        var legacy = JObject.Parse(json)["camera"];
                        cfg.Cameras = new List<Models.CameraConfig>();
                        if (legacy != null)
                        {
                            var cam = legacy.ToObject<Models.CameraConfig>();
                            if (cam != null) cfg.Cameras.Add(cam);
                        }
                        if (cfg.Cameras.Count == 0)
                            cfg.Cameras.Add(new Models.CameraConfig()); // 全缺：补一台默认
                    }

                    // 兼容迁移：旧配置只有字符串模板 subDirTemplate（如 {年}/{月}/{日}/{SN}/{OKNG}），
                    // 新版用 SubDirs 层级列表；SubDirs 为空时把旧模板拆成层级列表，现场升级不用重配。
                    if (cfg.Image != null && (cfg.Image.SubDirs == null || cfg.Image.SubDirs.Count == 0)
                        && !string.IsNullOrWhiteSpace(cfg.Image.SubDirTemplate))
                    {
                        cfg.Image.SubDirs = cfg.Image.SubDirTemplate
                            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                            .ToList();
                    }
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
                string json = JsonConvert.SerializeObject(config, new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver() // 小驼峰命名，贴近习惯
                });

                string tmp = ConfigFile + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false)); // 无 BOM UTF-8，避免 x64 下 BOM 干扰
                if (File.Exists(ConfigFile))
                    File.Delete(ConfigFile);
                File.Move(tmp, ConfigFile);
                LogHelper.Info("配置已保存：" + ConfigFile);
            }
            catch (Exception ex)
            {
                LogHelper.Error("保存配置失败：" + ex.Message);
                throw;
            }
        }
    }
}