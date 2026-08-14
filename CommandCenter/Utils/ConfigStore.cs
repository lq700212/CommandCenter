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
    ///   4. 不做旧配置兼容/迁移：项目未上线，配置全部以当前模型为准，字段缺了就用模型默认值。
    ///   唯一例外：V2.12.1 起存图点位统一为【相机点位号】（上下相机各自从 1 起、会重复），
    ///   归档目录必须含 {相机} 层隔开两相机，否则同点位文件互相覆盖（数据丢失）。因此加载/保存
    ///   时自动补上 {相机} 目录层（原有目录层级保持不变，仅末尾追加一级），已含则不重复加。
    /// </summary>
    public static class ConfigStore
    {
        /// <summary>配置文件绝对路径 = 程序目录\Config\appconfig.json</summary>
        public static string ConfigDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        public static string ConfigFile => Path.Combine(ConfigDir, "appconfig.json");

        /// <summary>
        /// 加载配置。文件不存在则返回模型默认值（补齐默认相机/型号列表等，不自动写盘，
        /// 由首次点保存时写）。文件存在则反序列化后同样走一遍"空段兜底"。
        /// 【V2.12.3 修复窗口塌缩】首次运行无配置文件时此前直接 new AppConfig() 返回，
        /// Cameras 是空列表 → 主界面窗口总数 = 相机点位和 = 0 → 塌成 1 个窗口。
        /// 现在统一走 ApplyDefaults：补默认两台相机 + 默认型号 U171（非空型号才能按点位
        /// 表算出对应窗口数，U171 → 上 18 点 + 下 4 点 = 22 个窗口）。
        /// </summary>
        public static Models.AppConfig Load()
        {
            Models.AppConfig cfg;
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string json = File.ReadAllText(ConfigFile, Encoding.UTF8);
                    cfg = JsonConvert.DeserializeObject<Models.AppConfig>(json) ?? new Models.AppConfig();
                }
                else
                {
                    cfg = new Models.AppConfig();
                }
            }
            catch (Exception ex)
            {
                // 配置损坏时不让程序崩，退回默认值并提示（调用方决定是否弹窗）
                LogHelper.Error("读取配置文件失败，已使用默认配置。原因：" + ex.Message);
                cfg = new Models.AppConfig();
            }
            ApplyDefaults(cfg);
            return cfg;
        }

        /// <summary>
        /// 空段兜底 + 数组对齐（加载与保存共用）：
        ///   - Cameras/ProductModels/Scanners 缺省或为空 → 现场默认值（两台相机、三型号）；
        ///   - Display/Image/Security 为 null → 模型默认实例；
        ///   - WindowStationMap/WindowEnabled 按窗口总数对齐；归档目录补 {相机} 层。
        /// 反序列化时 Newtonsoft 对"属性已有实例的集合"是复用并 Add 而非整值替换，
        /// 所以模型初始化器里的集合必须给空列表，默认值统一在这里补（V1.9.9）。
        /// </summary>
        private static void ApplyDefaults(Models.AppConfig cfg)
        {
            // 相机列表缺省时用现场默认两台相机（V1.9.8：IP 写死，见 CameraConfig.DefaultCameras）
            // V1.9.9：兜底条件从"仅 null"放宽到"null 或空列表"——因 AppConfig.Cameras
            // 初始化器已改为空列表（避免 Newtonsoft 反序列化时向已存在实例 Add 而叠成 4 台），
            // json 没写相机时必须在此补上默认两台现场相机。
            if (cfg.Cameras == null || cfg.Cameras.Count == 0) cfg.Cameras = Models.CameraConfig.DefaultCameras();
            if (cfg.Scanners == null) cfg.Scanners = new List<Models.ScanConfig>();
            // 产品型号候选列表（V2.8）：null/空时用现场默认三型号（U171/U172/Z121），
            // 保证设置窗体"产品型号"下拉与"窗口/点位配置"的型号下拉有候选可点。
            if (cfg.ProductModels == null || cfg.ProductModels.Count == 0)
                cfg.ProductModels = Models.AppConfig.DefaultProductModels();
            if (cfg.Display == null) cfg.Display = new Models.DisplayConfig();
            if (cfg.Image == null) cfg.Image = new Models.ImageConfig();
            if (cfg.Security == null) cfg.Security = new Models.SecurityConfig();

            // V2.13.4：相机配置升级——补 CameraId（旧配置无此字段=0 → 按行序）与 PLC 通道地址
            // （旧配置 plcRequestAddress=0 曾是"按相机序号自动"，V2.13.4 起改为显式配置，这里把
            // 前两台按现场默认补齐 2/3、5/6，保证旧配置文件升级后相机仍参与轮询，行为不变）
            EnsureCameraIdentity(cfg);

            // 保证窗口→存图点位映射长度与窗口总数一致（缺的补默认、多的截断）
            EnsureStationMap(cfg);
            // V2.13：保证窗口↔点位独立映射（WindowPointMaps）各型号表长度与窗口总数一致
            EnsureWindowPointMaps(cfg);
            // V2.12.1：归档子目录必须含 {相机} 层（上下相机同号点位靠它隔开），缺则自动补
            EnsureCameraSubDir(cfg);
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
                EnsureWindowPointMaps(config); // V2.13：窗口↔点位独立映射对齐（缺型号表补默认）
                EnsureCameraSubDir(config); // 保存前保证归档目录含 {相机} 层（见类注释第 4 点）
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
        /// 相机配置升级（V2.13.4）：旧配置文件缺 CameraId / PLC 通道地址（都是 0）时自动补齐，
        /// 保证升级后相机仍参与轮询、点位/通道仍按相机ID定位，行为与旧版一致。
        /// 【为什么需要】V2.13.4 起：
        ///   - 相机身份键 = CameraId（真编号，上=2/下=1），旧 json 没存该字段=0；
        ///   - PLC 通道地址 = 每台相机显式配置（PlcRequestAddress/PlcResultAddress），旧 json 存的
        ///     0 曾是"按相机序号自动"（第1台=2/5、第2台=3/6），若保持 0 会被当成"未配置通道"而不参与轮询。
        /// 【补法】
        ///   - CameraId<=0 → 先按 IP 匹配现场默认两台相机（DefaultCameras）取真编号（213→2、212→1），
        ///     匹配不上（新增自定义相机）才按行序兜底；
        ///   - PlcRequestAddress<=0 且该相机是列表第 1/2 台 → 按现场默认补 2/3（协议 40002/40003）；
        ///     PlcResultAddress<=0 且第 1/2 台 → 补 5/6（协议 40005/40006）。第 3 台起保留 0
        ///     （未配置通道，需现场/PLC 协商地址后在设置页填写）。
        /// </summary>
        private static void EnsureCameraIdentity(Models.AppConfig cfg)
        {
            if (cfg.Cameras == null) return;
            var defaults = Models.CameraConfig.DefaultCameras();
            for (int i = 0; i < cfg.Cameras.Count; i++)
            {
                var cam = cfg.Cameras[i];
                if (cam == null) continue;

                // ① 补 CameraId：按 IP 匹配默认相机取真编号，匹配不上按行序
                if (cam.CameraId <= 0)
                {
                    int byIp = 0;
                    string ip = cam.IpAddress?.Trim() ?? "";
                    foreach (var d in defaults)
                    {
                        if (d != null && (d.IpAddress ?? "").Trim().Equals(ip, StringComparison.OrdinalIgnoreCase))
                        { byIp = d.CameraId; break; }
                    }
                    cam.CameraId = byIp > 0 ? byIp : i + 1;
                }

                // ② 补 PLC 通道地址：仅前两台且为 0 时补现场默认（第3台起保持 0=未配置）
                if (i < defaults.Count && defaults[i] != null)
                {
                    if (cam.PlcRequestAddress <= 0 && defaults[i].PlcRequestAddress > 0)
                        cam.PlcRequestAddress = defaults[i].PlcRequestAddress;
                    if (cam.PlcResultAddress <= 0 && defaults[i].PlcResultAddress > 0)
                        cam.PlcResultAddress = defaults[i].PlcResultAddress;
                }
            }
        }

        /// <summary>
        /// 保证 WindowStationMap（历史兼容字段）与 WindowEnabled（窗口启用列表）都和
        /// 显示窗口总数对齐（V2.12.1 统一）：窗口总数 = 各相机按当前型号点位表条目数之和
        /// （DisplayConfig.WindowCountFor，自适应/非自适应一致——点位由相机点位表唯一决定）。
        /// Rows/Columns 仅决定排列宽度、不决定窗口数；WindowStationMap 已退役，
        /// 只按"点位=窗口编号"补齐对齐留档，不参与任何运行逻辑。
        /// 对齐规则不变：长度不足 → 点位按"点位=窗口编号"补上、启用按 true 补上（默认规则）；
        /// 长度超出 → 多余截断（窗口数改小后，超出部分丢弃）。
        /// 在加载与保存各调一次，保证运行时取 map[i]/enabled[i] 永不越界。
        /// </summary>
        private static void EnsureStationMap(Models.AppConfig cfg)
        {
            int windowCount = Models.DisplayConfig.WindowCountFor(cfg.Cameras, cfg.ProductModel);

            var map = cfg.Display.WindowStationMap ?? new List<int>();
            while (map.Count < windowCount) map.Add(map.Count + 1);
            if (map.Count > windowCount) map.RemoveRange(windowCount, map.Count - windowCount);
            cfg.Display.WindowStationMap = map;

            // 窗口启用列表同步对齐（V1.12.28）：缺的按默认"启用"，多的截断
            var enabled = cfg.Display.WindowEnabled ?? new List<bool>();
            while (enabled.Count < windowCount) enabled.Add(true);
            if (enabled.Count > windowCount) enabled.RemoveRange(windowCount, enabled.Count - windowCount);
            cfg.Display.WindowEnabled = enabled;
        }

        /// <summary>
        /// 保证"窗口↔点位独立映射"（DisplayConfig.WindowPointMaps，V2.13）与窗口总数对齐。
        /// 【为什么需要】V2.13 起允许手动编辑窗口↔(相机,点位) 的对应（WindowPointForm 的
        ///   编辑点位/交换位置/恢复默认）。映射按产品型号分表（ModelWindowPointMap），
        ///   每张表长度必须 = 该型号窗口总数（各相机点位表条目和，见 WindowCountFor），
        ///   否则运行时 ResolveWindowPointMap 因长度不匹配回退默认铺排、用户编辑白改。
        /// 【做法】为每个候选型号（ProductModels ∪ 当前 ProductModel）补一张表：
        ///   - 型号没配表 → 新建默认铺排表（DefaultWindowPointMap，前上相机后下相机）；
        ///   - 已有表长度 ≠ 窗口总数（相机点位表增删点位后没跟着改）→ 整表重置为默认铺排
        ///     （点位由相机点位表唯一决定，数量变了只能回默认，避免"窗口↔点位"错位越界）。
        /// 注意：不能覆盖"长度恰好匹配"的用户自定义表——那是现场手动编辑的结果，保留。
        /// </summary>
        private static void EnsureWindowPointMaps(Models.AppConfig cfg)
        {
            if (cfg.Display == null) cfg.Display = new Models.DisplayConfig();
            var maps = cfg.Display.WindowPointMaps;
            if (maps == null) cfg.Display.WindowPointMaps = maps = new List<Models.ModelWindowPointMap>();

            // 候选型号集合：全局型号候选 ∪ 当前运营型号（保证切到任何型号都有映射表可用）。
            // 本文件无 System.Linq，用 IndexOf 大小写不敏感去重。
            var models = new List<string>();
            if (!string.IsNullOrWhiteSpace(cfg.ProductModel)) models.Add(cfg.ProductModel);
            foreach (var m in cfg.ProductModels ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(m)) continue;
                if (models.FindIndex(x => string.Equals(x, m, StringComparison.OrdinalIgnoreCase)) < 0)
                    models.Add(m);
            }

            foreach (var model in models)
            {
                // 每个型号一张默认铺排表（长度=该型号窗口总数）
                var def = Models.DisplayConfig.DefaultWindowPointMap(cfg.Cameras, model);
                Models.ModelWindowPointMap found = null;          // 手工查找同名型号表（无 Linq）
                for (int i = 0; i < maps.Count; i++)
                {
                    if (maps[i] != null && string.Equals(maps[i].ModelName, model, StringComparison.OrdinalIgnoreCase))
                    { found = maps[i]; break; }
                }
                if (found == null)
                {
                    // 型号没配表：新建默认铺排表（用户在该型号下未编辑过 → 默认=出厂铺排）
                    maps.Add(new Models.ModelWindowPointMap
                    {
                        ModelName = model,
                        Points = def
                    });
                }
                else if (found.Points == null || found.Points.Count != def.Count
                    || ContainsLegacyCameraIndex(found.Points))
                {
                    // 表存在但长度与窗口总数不一致（点位表增删点位后没跟上），或含旧格式条目
                    // （V2.13.4 前存 cameraIndex=列表下标，属性改名 CameraId 后反序列化为 0）：
                    // 点位由相机点位表唯一决定，数量变了只能重置默认，防越界/错位；
                    // 旧格式条目 CameraId=0 无法可靠迁移（原下标值已被丢弃），同样重置默认铺排。
                    found.Points = def;
                }
                // 长度恰好匹配且全为新格式（CameraId>0）→ 保留用户手动编辑过的映射，不动
            }
        }

        /// <summary>
        /// 判断窗口↔点位映射表是否含"旧格式条目"（V2.13.4 迁移检测）：
        /// V2.13.4 前 WindowPointItem 存 cameraIndex（相机列表下标），改名 CameraId 后旧 json 的
        /// cameraIndex 属性对不上新属性名，反序列化时被丢弃 → CameraId=0。新格式条目的 CameraId
        /// 必然 >0（配置升级时 EnsureCameraIdentity 已保证相机有 ID），故"存在 CameraId<=0 的条目"
        /// 即说明是旧格式，需重置默认铺排（原下标值已丢失，无法可靠迁移回相机ID）。
        /// </summary>
        private static bool ContainsLegacyCameraIndex(List<Models.WindowPointItem> points)
        {
            if (points == null) return true;
            foreach (var p in points)
                if (p == null || p.CameraId <= 0) return true;
            return false;
        }

        /// <summary>
        /// 保证归档目录层级里含 "{相机}" 这一层（V2.12.1）。
        /// 【为什么必须】V2.12.1 起存图点位统一为相机点位号（本相机点位表 StationNo），
        /// 上下相机点位号各自从 1 起、会重复（如上相机 1~18、下相机 1~4）；归档若不分相机目录，
        /// 上相机点位 3 与下相机点位 3 会落进同一目录同名覆盖（数据丢失）。FTP 中转目录天然按相机
        /// 分开（一台相机一个目录），但【归档目录】依赖这一层 {相机}。
        /// 【做法（V2.12.3 对齐现场最终目录顺序）】旧配置（SubDirs 未含 {相机}）在加载/保存时
        /// 自动补一层 {相机}：插到 {OKNG} 之前（目标顺序 = "…/{SN}/{相机}/{OKNG}"；无 {OKNG} 则
        /// 追加末尾），原有层级顺序其余保持不变；已含（忽略大小写）则不重复加。单相机现场可手动删掉这层。
        /// </summary>
        private static void EnsureCameraSubDir(Models.AppConfig cfg)
        {
            if (cfg.Image == null) cfg.Image = new Models.ImageConfig();
            var subs = cfg.Image.SubDirs;
            if (subs == null) cfg.Image.SubDirs = subs = new List<string>();
            foreach (var s in subs)
            {
                if (!string.IsNullOrWhiteSpace(s)
                    && s.IndexOf("{相机}", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;   // 已含 {相机} 层，不重复加
            }
            // 插到 {OKNG} 之前（现场要求 "…/SN/{相机}/OK|NG" 顺序）；目录里没有 {OKNG} 就追加到末尾
            int okIdx = subs.FindIndex(s => !string.IsNullOrWhiteSpace(s)
                && s.IndexOf("{OKNG}", StringComparison.OrdinalIgnoreCase) >= 0);
            if (okIdx >= 0) subs.Insert(okIdx, "{相机}");
            else subs.Add("{相机}");
            LogHelper.Info("配置升级：归档目录已自动补上 {相机} 层（上下相机同号点位隔离），已插到 {OKNG} 之前");
        }
    }
}
