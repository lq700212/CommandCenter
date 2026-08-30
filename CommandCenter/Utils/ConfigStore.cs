using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            // V2.13.8：扫码枪空列表也兜底一台现场默认 TCP 扫码枪（此前只兜底 null、空列表时
            // 主界面/测试窗体【完全没有扫码枪】——表现为"扫码枪状态未连接/测试页找不到选项"，
            // 即使扫码枪网络可达（Test-NetConnection 通）也不会接入）。与 Cameras 空列表兜底
            // 默认相机同一逻辑；默认值对齐现场基恩士 SR 以太网无协议枪（19.87.6.100:9004/LON，
            // 与 SettingsForm TCP 模板行/OnSave 兜底一致）。
            if (cfg.Scanners == null || cfg.Scanners.Count == 0)
                cfg.Scanners = new List<Models.ScanConfig>
                {
                    new Models.ScanConfig
                    {
                        Enabled = true,
                        Mode = "Tcp",
                        IpAddress = "19.87.6.100",
                        Port = 9004,
                        TriggerCommand = "LON"
                    }
                };
            // 产品型号候选列表（V2.8）：null/空时用现场默认型号（U171/Z121），
            // 保证设置窗体"产品型号"下拉与"窗口/点位配置"的型号下拉有候选可点。
            if (cfg.ProductModels == null || cfg.ProductModels.Count == 0)
                cfg.ProductModels = Models.AppConfig.DefaultProductModels();
            // V2.14.13：型号→PLC 序号映射兜底（默认 Z121=1、U171=2），
            // 保证 40007 型号序号寄存器有值可写；同时把候选型号/当前型号里"没配序号"的补一份
            // 默认映射（避免新增型号后 40007 恒写 0 现场排查困难）。补的序号从当前最大序号+1 递增。
            if (cfg.Plc.ModelIndexes == null || cfg.Plc.ModelIndexes.Count == 0)
                cfg.Plc.ModelIndexes = Models.PlcConfig.DefaultModelIndexes();
            EnsureModelIndexes(cfg);
            if (cfg.Display == null) cfg.Display = new Models.DisplayConfig();
            if (cfg.Image == null) cfg.Image = new Models.ImageConfig();
            if (cfg.Security == null) cfg.Security = new Models.SecurityConfig();
            // V2.15.0：界面语言兜底——空串/非法值一律回落中文（json 手改脏也不崩）。
            if (string.IsNullOrWhiteSpace(cfg.Language)) cfg.Language = "zh-CN";
            // V2.15.19：SN 去向配置兜底——sn 段缺失 new 出默认实例（V2.15.20 起 Target 默认 "Mes"），
            // Target 脏值经 Normalize 归一（非法值回落默认 "Mes"，绝不因手改 json 让扫码主流程走偏）。
            if (cfg.Sn == null) cfg.Sn = new Models.SnRouteConfig();
            cfg.Sn.Target = Models.SerialNumberTargets.Normalize(cfg.Sn.Target);

            // V2.13.4：相机配置升级——补 CameraId（旧配置无此字段=0 → 按行序）与 PLC 通道地址
            // （旧配置 plcRequestAddress=0 曾是"按相机序号自动"，V2.13.4 起改为显式配置，这里把
            // 前两台按现场默认补齐 2/3、5/6，保证旧配置文件升级后相机仍参与轮询，行为不变）
            // V2.13.10：补号改为"全局唯一"——避免自定义相机按行序兜底 i+1 与默认相机的真编号
            // 撞出重复 ID（见方法注释，现场曾出现"自定义相机=1、默认下相机也=1"），
            // 且改号/删相机后旧映射里的孤儿 CameraId 会在 EnsureWindowPointMaps 里被重置默认铺排。
            EnsureCameraIdentity(cfg);
            // V2.13.9：恢复现场默认相机顺序（修复 V2.13.8 排序保存的历史配置，见方法注释）
            EnsureDefaultCameraOrder(cfg);

            // 保证窗口→存图点位映射长度与窗口总数一致（缺的补默认、多的截断）
            EnsureStationMap(cfg);
            // V2.13：保证窗口↔点位独立映射（WindowPointMaps）各型号表长度与窗口总数一致
            EnsureWindowPointMaps(cfg);
            // V2.12.1：归档子目录必须含 {相机} 层（上下相机同号点位靠它隔开），缺则自动补
            // V2.14.13：先做"历史脏配置归一化"（把"完整路径当一层"的 SubDirs 拆成单层、去盘符/去根前缀/
            // 去重），再补 {相机}——顺序不能反：若脏项里已含 {相机}（如 E:\Images\{年月日}\{SN}\{相机}
            // \{OKNG}），EnsureCameraSubDir 会误判"已含"而不补，必须归一化在前、补层在后。
            NormalizeSubDirs(cfg);
            EnsureCameraSubDir(cfg);
        }

        /// <summary>
        /// V2.14.13：把"型号→PLC 序号"映射表补齐到覆盖所有候选型号与当前型号。
        /// 背景：PLC 40007 传的是型号序号（不是型号字符串），WriteProductModel 按型号名查
        /// `PlcConfig.ModelIndexes`；若某型号（尤其用户在"产品型号配置…"弹窗新增的型号）没配序号，
        /// 40007 会恒写 0、PLC 分不清型号。故加载/保存时自动把候选型号（ProductModels ∪ 当前
        /// ProductModel）里缺失的补一条默认映射，序号取"当前最大序号 + 1"（保证不冲突）。
        /// 已配置的映射保持用户值，只补缺失项，不覆盖。
        /// V2.14.24：反向回流——`ModelIndexes` 是型号集合的【唯一权威入口】（设置页已删"产品型号"
        /// 下拉，见 SettingsForm），把映射表里用户新增的型号名并回 ProductModels（候选列表），
        /// 保证新增型号在主界面标题栏型号下拉 / 窗口点位配置的型号下拉里可选可用。双向闭环：
        /// ProductModels → 补序号映射，ModelIndexes → 补候选集合。
        /// </summary>
        private static void EnsureModelIndexes(Models.AppConfig cfg)
        {
            var map = cfg.Plc?.ModelIndexes ?? (cfg.Plc.ModelIndexes = new List<Models.ModelIndexItem>());
            // 收集所有候选型号：预置（DefaultProductModels）∪ 配置候选 ∪ 当前型号，去重、忽略空
            var allModels = new List<string>();
            foreach (var m in Models.AppConfig.DefaultProductModels())
                if (!string.IsNullOrWhiteSpace(m)) allModels.Add(m.Trim());
            foreach (var m in cfg.ProductModels ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(m) && !allModels.Contains(m.Trim())) allModels.Add(m.Trim());
            if (!string.IsNullOrWhiteSpace(cfg.ProductModel) && !allModels.Contains(cfg.ProductModel.Trim()))
                allModels.Add(cfg.ProductModel.Trim());

            // 每个缺失型号补一条：序号 = 当前最大序号 + 1（空表从 1 起）
            int maxIndex = map.Count > 0 ? map.Max(x => x?.ModelIndex ?? 0) : 0;
            foreach (var m in allModels)
            {
                if (map.Any(x => x != null && string.Equals(x.ModelName, m, StringComparison.OrdinalIgnoreCase)))
                    continue;
                map.Add(new Models.ModelIndexItem { ModelName = m, ModelIndex = ++maxIndex });
            }

            // V2.14.24 反向回流：映射表里新增的型号名并入 ProductModels（候选列表），
            // 空安全：cfg.ProductModels 被配置手改成 null 时先建空表，防 NRE。
            var models = cfg.ProductModels ?? (cfg.ProductModels = new List<string>());
            foreach (var item in map)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ModelName)) continue;
                string name = item.ModelName.Trim();
                if (!models.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                    models.Add(name);
            }
        }

        /// <summary>
        /// 保存配置到 json 文件；目录不存在会自动创建。
        /// </summary>
        public static void Save(Models.AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                // V2.13.10：保存前先补号对齐（新增相机/手改 json ID=0 时写盘不落 0，且全局唯一，
                // 与加载时 ApplyDefaults→EnsureCameraIdentity 同一套补号规则，保证改号立刻写回、
                // 孤儿映射随即被按下一条 EnsureWindowPointMaps 重置为用新编号的默认铺排）。
                EnsureCameraIdentity(config);
                EnsureStationMap(config);   // 保存前把点位映射对齐到窗口总数，避免写盘出越界/缺项
                EnsureWindowPointMaps(config); // V2.13：窗口↔点位独立映射对齐（缺型号表补默认）
                EnsureCameraSubDir(config); // 保存前保证归档目录含 {相机} 层（见类注释第 4 点）
                EnsureModelIndexes(config); // V2.14.13：型号→PLC 序号映射补齐（新增型号自动配序号）
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
        /// 恢复现场默认相机顺序（V2.13.9，修复 V2.13.8 排序保存的历史配置）：
        /// 上相机（defaults[0]，IP 19.87.6.213）与 下相机（defaults[1]，IP 19.87.6.212）若恰好
        /// 位置颠倒（下相机在下标 0、上相机在下标 1），加载时自动换回 [上, 下]。
        /// 【为什么需要】默认铺排（DisplayConfig.DefaultWindowPointMap / AutoFitCameraStarts 的
        ///   "前上相机后下相机"）依赖相机【列表顺序】；V2.13.8 设置页按 CameraId 升序排序并
        ///   按表格行序保存，会把列表写成 [下(1), 上(2)]。本修复（V2.13.9）让设置页保存时恢复
        ///   原始顺序，但【已受影响的存量 json】（V2.13.8 期间保存过的配置）里 cameras 已经是
        ///   [下,上]——仅靠"保存恢复"救不了它们，必须在加载时迁移一次，否则任何重新生成默认铺排
        ///   的路径（恢复默认/点位表长度变化重置）仍会得到"先下后上"的翻转铺排。
        /// 【安全性】只重排"两台默认相机恰好颠倒且各恰出现一次"的情形；自定义相机/单独一台默认
        ///   相机/已正确顺序的列表都不干预。WindowPointMaps/PLC 通道地址/存图目录全以 CameraId 或
        ///   配置对象为键，重排列表顺序无任何副作用（主界面相机灯/开发者模式窗体按下标与配置对齐，
        ///   按新顺序重建即可）。
        /// </summary>
        private static void EnsureDefaultCameraOrder(Models.AppConfig cfg)
        {
            var cams = cfg.Cameras;
            if (cams == null || cams.Count < 2) return;
            var defaults = Models.CameraConfig.DefaultCameras();
            if (defaults == null || defaults.Count < 2 || defaults[0] == null || defaults[1] == null) return;

            int upIdx = -1, downIdx = -1;   // up=上相机(defaults[0])、down=下相机(defaults[1])
            string upIp = (defaults[0].IpAddress ?? "").Trim();
            string downIp = (defaults[1].IpAddress ?? "").Trim();
            for (int i = 0; i < cams.Count; i++)
            {
                if (cams[i] == null) continue;
                string ip = cams[i].IpAddress?.Trim() ?? "";
                if (ip.Equals(upIp, StringComparison.OrdinalIgnoreCase)) upIdx = i;
                else if (ip.Equals(downIp, StringComparison.OrdinalIgnoreCase)) downIdx = i;
            }
            // 两台默认相机都存在、且顺序颠倒（下相机在 0、上相机在 1）→ 换回现场默认
            if (upIdx >= 0 && downIdx >= 0 && downIdx < upIdx)
            {
                var tmp = cams[downIdx];
                cams[downIdx] = cams[upIdx];
                cams[upIdx] = tmp;
                LogHelper.Info("相机配置升级：检测到默认相机顺序颠倒（下相机在前），已恢复现场顺序 [上相机, 下相机]（V2.13.9）");
            }
        }

        /// <summary>
        /// 相机配置升级（V2.13.4）：旧配置文件缺 CameraId / PLC 通道地址（都是 0）时自动补齐，
        /// 保证升级后相机仍参与轮询、点位/通道仍按相机ID定位，行为与旧版一致。
        /// 【为什么需要】V2.13.4 起：
        ///   - 相机身份键 = CameraId（真编号，上=2/下=1），旧 json 没存该字段=0；
        ///   - PLC 通道地址 = 每台相机显式配置（PlcRequestAddress/PlcResultAddress），旧 json 存的
        ///     0 曾是"按相机序号自动"（第1台=2/5、第2台=3/6），若保持 0 会被当成"未配置通道"而不参与轮询。
        /// 【补法】（一律按 IP 匹配默认相机，不依赖列表下标——V2.13.8 起设置页相机表排序展示、
        ///   保存恢复原始顺序，且 json 可能被手改顺序，用下标匹配 defaults[i] 会张冠李戴）
        ///   - CameraId<=0 → 按 IP 匹配现场默认两台相机（DefaultCameras）取真编号（213→2、212→1），
        ///     匹配不上（新增自定义相机）才按行序兜底；
        ///   - PlcRequestAddress/PlcResultAddress<=0 → 按 IP 匹配默认相机取默认通道地址
        ///     （上相机 2/5 协议 40002/40005、下相机 3/6）；匹配不上（第 3 台起自定义相机）保留 0
        ///     （未配置通道，需现场/PLC 协商地址后在设置页填写）。
        /// 【V2.13.10 全局唯一补号】相机 ID 是"窗口↔点位"反查关联键（WindowPointItem.CameraId），
        ///   必须每台唯一。旧版"匹配不上默认相机就按行序 i+1"会与别的相机撞出重复：
        ///   例如列表 [自定义相机(IP 非默认), 默认下相机 212] 都缺 ID 时，自定义相机按行序得 1、
        ///   下相机按 IP 匹配也得 1 → 两台同为 1，运行反查键 (CameraId,StationNo) 冲突、显示错乱。
        ///   现改为【三遍分配】：
        ///   ① 先占：收集已固定的 ID（>0，用户手动配置/旧版本就有的），兜底补号都要避开；
        ///   ② 再给默认相机定编：按 IP 匹配默认的相机优先取【真编号】（213→2、212→1）——
        ///     真编号是现场固定语义（=存图目录号，见 CameraConfig.CameraId 注释），必须优先保住，
        ///     不能被自定义相机先抢走 1/2（否则编号语义翻转、存图目录错乱）；
        ///   ③ 最后给自定义相机兜底：仍未配 ID 的（IP 匹配不上默认）从 1 起取"第一个未被占用
        ///     的正整数"，保证与②及手动配置的 ID 全局唯一，绝不撞默认相机的 1/2。
        /// </summary>
        private static void EnsureCameraIdentity(Models.AppConfig cfg)
        {
            if (cfg.Cameras == null) return;
            var defaults = Models.CameraConfig.DefaultCameras();

            // ——① 收集"已固定的 CameraId"（>0，用户手动配置/旧版本就有的），后续所有兜底补号
            //   都必须避开这些值，否则自定义相机兜底会撞上已有的真编号。
            var taken = new HashSet<int>();
            foreach (var c in cfg.Cameras)
                if (c != null && c.CameraId > 0) taken.Add(c.CameraId);

            // 按 IP 匹配现场默认相机（V2.13.8 起统一走这里，替代"按下标 defaults[i]"）：
            // 列表顺序可能被设置页排序/手改 json 打乱，只有 IP 是相机的稳定身份。
            // 抽成小函数复用于第②遍；本方法无 System.Linq，手写遍历。
            Func<Models.CameraConfig, Models.CameraConfig> matchByIp = cam =>
            {
                if (cam == null) return null;
                string ip = cam.IpAddress?.Trim() ?? "";
                foreach (var d in defaults)
                {
                    if (d != null && (d.IpAddress ?? "").Trim().Equals(ip, StringComparison.OrdinalIgnoreCase))
                        return d;
                }
                return null;
            };

            // ——② 默认相机优先定编：IP 匹配默认、且真编号尚未被占用的相机，直接取真编号。
            //    （若真编号已被用户手动配给别的相机，则该台让位、走第③遍兜底，防重复）
            for (int i = 0; i < cfg.Cameras.Count; i++)
            {
                var cam = cfg.Cameras[i];
                if (cam == null || cam.CameraId > 0) continue;
                var byIp = matchByIp(cam);
                if (byIp != null && byIp.CameraId > 0 && !taken.Contains(byIp.CameraId))
                {
                    cam.CameraId = byIp.CameraId;
                    taken.Add(cam.CameraId);
                }
            }

            // ——③ 自定义/让位相机兜底：仍未配 ID 的从 1 起取"第一个未被占用的正整数"。
            //    （此前版本是"按行序 i+1"，会与默认相机真编号撞出重复 ID，见类注释）
            for (int i = 0; i < cfg.Cameras.Count; i++)
            {
                var cam = cfg.Cameras[i];
                if (cam == null || cam.CameraId > 0) continue;
                int next = 1;
                while (taken.Contains(next)) next++;
                cam.CameraId = next;
                taken.Add(next);
            }

            // ——④ 补 PLC 通道地址：按 IP 匹配默认相机取默认通道地址（匹配不上=新增相机，保持 0=未配置）
            for (int i = 0; i < cfg.Cameras.Count; i++)
            {
                var cam = cfg.Cameras[i];
                if (cam == null) continue;
                var byIp = matchByIp(cam);
                if (byIp != null)
                {
                    if (cam.PlcRequestAddress <= 0 && byIp.PlcRequestAddress > 0)
                        cam.PlcRequestAddress = byIp.PlcRequestAddress;
                    if (cam.PlcResultAddress <= 0 && byIp.PlcResultAddress > 0)
                        cam.PlcResultAddress = byIp.PlcResultAddress;
                }
            }
        }

        /// <summary>
        /// 保证 WindowStationMap（历史兼容字段）与 WindowEnabled（窗口启用列表）都和
        /// 显示窗口总数对齐（V2.12.1 统一；V2.14.18 窗口总数改按 ResolveLayout.windowCount）：
        /// 窗口总数 = 主界面要创建的窗口控件数（自适应=各相机按当前型号点位表条目数之和；
        /// 非自适=行列乘积、含空窗口）。WindowEnabled 覆盖全部窗口（含空窗口，默认启用），
        /// 禁用任意窗口（含空窗口）主界面就少一格。
        /// Rows/Columns 仅决定排列形状、不决定点位数；WindowStationMap 已退役，
        /// 只按"点位=窗口编号"补齐对齐留档，不参与任何运行逻辑。
        /// 对齐规则不变：长度不足 → 点位按"点位=窗口编号"补上、启用按 true 补上（默认规则）；
        /// 长度超出 → 多余截断（窗口数改小后，超出部分丢弃）。
        /// 在加载与保存各调一次，保证运行时取 map[i]/enabled[i] 永不越界。
        /// </summary>
        private static void EnsureStationMap(Models.AppConfig cfg)
        {
            // V2.14.18：窗口总数 = 布局窗口数（自适应=点位数；非自适=行列乘积、含空窗口），
            // 与主窗体 BuildWindowGrid / WindowPointForm / 协调器走同一套 ResolveLayout。
            var layout = Models.DisplayConfig.ResolveLayout(
                cfg.Cameras, cfg.ProductModel,
                cfg.Display.AutoFit, cfg.Display.Rows, cfg.Display.Columns);
            int windowCount = layout.windowCount;

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
        ///   每张表长度必须 = 该型号布局窗口总数 windowCount（ResolveLayout：自适应=点位数；
        ///   非自适=行列乘积、含空窗口），否则运行时 ResolveWindowPointMap 因长度不匹配回退
        ///   默认铺排、用户编辑白改。
        /// 【做法】为每个候选型号（ProductModels ∪ 当前 ProductModel）补一张表：
        ///   - 型号没配表 → 新建默认铺排表（DefaultWindowPointMap，前上相机后下相机 + 尾部空窗口）；
        ///   - 已有表长度 ≠ 窗口总数（相机点位表增删点位/行列改动后没跟着改）→ 整表重置为默认铺排
        ///     （点位由相机点位表唯一决定，数量变了只能回默认，避免"窗口↔点位"错位越界）；
        ///   - 已有表含"孤儿 CameraId"（V2.13.10）：某条目的 CameraId 不再是任何相机的真编号
        ///     （相机被改号）/旧格式遗留（CameraId<=0，V2.13.4 前 windowPointItem 存
        ///     cameraIndex=列表下标，改名 CameraId 后反序列化丢字段）→ 同样重置默认铺排。
        ///     【为什么改号后必须重置】窗口↔点位条目以 CameraId 为关联键（WindowPointItem），
        ///     相机改号（设置页"相机ID"列 / json cameraId）后旧条目的 ID 已成孤儿，若不重置，
        ///     运行时 ResolveWindowPointMap 拿到孤儿表，TryResolveActiveWindow 按 (孤儿ID,点位)
        ///     反查永远找不到窗口 → 该相机全部点位被判跳 3（整台相机罢工）。
        ///     重置以"当前相机列表"重新生成，天然用上新编号，改号即刻生效。
        ///     校验统一走 DisplayConfig.PointMapValidForCameras（与 ResolveWindowPointMap 同一套，
        ///     两端规则绝不漂移；空窗口 null 条目合法跳过）。注意：不能覆盖"长度恰好匹配且 ID
        ///     全有效"的用户自定义表——那是现场手动编辑的结果，保留。
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
                // 每个型号一张默认铺排表。窗口总数 = 该型号 ResolveLayout.windowCount
                // （自适应=点位和；非自适=行列乘积、含空窗口），见 EnsureStationMap 注释。
                var layout = Models.DisplayConfig.ResolveLayout(
                    cfg.Cameras, model,
                    cfg.Display.AutoFit, cfg.Display.Rows, cfg.Display.Columns);
                var def = Models.DisplayConfig.DefaultWindowPointMap(cfg.Cameras, model, layout.windowCount);
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
                    || !Models.DisplayConfig.PointMapValidForCameras(cfg.Cameras, found.Points))
                {
                    // 表存在但长度与窗口总数不一致（点位表增删点位/行列改动后没跟上）、含孤儿 CameraId
                    // （相机改号后旧 ID 无对应相机）或旧格式条目（V2.13.4 前存 cameraIndex，
                    // 反序列化为 0，PointMapValidForCameras 对 CameraId<=0 一律判无效）：
                    // 点位由相机点位表唯一决定，数量变了/关联键失效只能重置默认，防越界/错位/
                    // 反查全落空；重置默认以当前相机列表生成，也用上改号后的新编号。
                    found.Points = def;
                }
                // 长度恰好匹配且全为有效 ID（当前相机均存在）→ 保留用户手动编辑过的映射，不动
            }
        }

        /// <summary>
        /// 【V2.14.13 加固】历史脏配置归一化：把 SubDirs 里"完整路径当一层"的脏项自动修复。
        ///
        /// 背景（血泪教训）：DirTreeEditForm 曾允许把含反斜杠的完整路径模板（如
        /// "E:\Images\{年月日}\{SN}\{相机}\{OKNG}"）作为单独一层粘贴进配置，现场实测归档路径
        /// 变成"一层套一层"的超长嵌套目录（如 2026年08月14日\SN\相机\NG × 4）。本方法在
        /// 加载/保存时对 SubDirs 做一次性清洗：
        ///   1) 每项若含 `\` 或 `/`，按分隔符拆成独立层级（还原"完整路径"为逐层模板）；
        ///   2) 丢弃纯盘符段（"E:"）与等于保存根目录末段的前缀段（如根 E:\Images 的 "Images"，
        ///      避免把根目录名再重复一层）;
        ///   3) 忽略大小写去重，保持原有先后顺序。
        /// 拆完若还有 {相机} 层，EnsureCameraSubDir 会识别到而不再重复补；单层干净配置不受影响。
        /// </summary>
        private static void NormalizeSubDirs(Models.AppConfig cfg)
        {
            if (cfg.Image == null) cfg.Image = new Models.ImageConfig();
            var subs = cfg.Image.SubDirs;
            if (subs == null) cfg.Image.SubDirs = subs = new List<string>();

            // 保存根目录的末段（如 E:\Images → "Images"）：用于丢弃"完整路径里的根目录前缀段"。
            string root = (cfg.Image.SaveRootDir ?? "").Trim();
            string rootLast = string.IsNullOrWhiteSpace(root) ? "" : Path.GetFileName(root.TrimEnd('\\', '/'));

            var cleaned = new List<string>();
            foreach (var raw in subs)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                // 不含路径分隔符的项是正常的单层模板（如 "{年月日}"、"OK"），原样保留
                if (raw.IndexOf('\\') < 0 && raw.IndexOf('/') < 0)
                {
                    string t = raw.Trim();
                    if (t.Length > 0 && !cleaned.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                        cleaned.Add(t);
                    continue;
                }
                // 含分隔符 → 按正/反斜杠拆成独立层（脏配置的"完整路径"还原为逐层模板）
                var parts = raw.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                // 绝对路径模板（如 "E:\Images\{...}"）整体剥掉前缀：盘符段（E:）+ 根目录段（Images）。
                // 判据 = 首段是盘符（如 "E:"）——说明这一项把整条绝对路径粘进来了，
                // 盘符和紧随其后的根目录名都是路径前缀，不应成为归档子层（不要求根名与 SaveRootDir
                // 拼写完全一致：现场曾出现根目录 E:\Images 但粘贴成 E:\Image 的拼写错误）。
                int startIdx = 0;
                if (parts.Count >= 2
                    && parts[0].Length == 2 && char.IsLetter(parts[0][0]) && parts[0][1] == ':')
                {
                    startIdx = 2;   // 跳过盘符段 + 根目录段（前缀整体丢弃）
                }
                for (int i = startIdx; i < parts.Count; i++)
                {
                    string seg = parts[i];
                    // 纯盘符段兜底（如只剩 "E:" 这种，防御）；根目录末段同名段也丢（防根名再重复一层）
                    if (seg.Length == 2 && char.IsLetter(seg[0]) && seg[1] == ':') continue;
                    if (rootLast.Length > 0 && string.Equals(seg, rootLast, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!cleaned.Any(x => string.Equals(x, seg, StringComparison.OrdinalIgnoreCase)))
                        cleaned.Add(seg);
                }
            }
            // 全部为空/被清掉时兜底一层（与模型默认一致）
            if (cleaned.Count == 0) cleaned.Add("{年月日}");
            cfg.Image.SubDirs = cleaned;
        }

        /// <summary>
        /// 保证归档目录层级里含 "{相机}" 这一层（V2.12.1）。
        /// 【为什么必须】V2.12.1 起存图点位统一为相机点位号（本相机点位表 StationNo），
        /// 上下相机点位号各自从 1 起、会重复（如上相机 1~20、下相机 1~4）；归档若不分相机目录，
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
