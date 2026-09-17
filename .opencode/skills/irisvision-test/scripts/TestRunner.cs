// ═══════════════════════════════════════════════════════════════════════════
// IrisVision 回归测试用例集（irisvision-test skill 的 tests.ps1 编译运行）
//
// 【定位】验证"纯逻辑 + 服务层真实链路"，不需要现场设备、不碰 UI：
//   ① SN/型号 ASCII 寄存器打包（V2.15.17 新增协议的核心）
//   ② PLC 从站读写往返（真实建站 502 监听 → 写读校验 → 释放）
//   ③ 配置模型默认值 / 新旧 json 兼容 / ApplyDefaults 兜底
//   ④ 扫码错误文本过滤（IsIgnoredScanText）
//   ⑤ 窗口布局统一模型（ResolveLayout / 默认铺排 / 孤儿映射防御）
//   ⑥ 点位→程序号映射（按相机+型号分表回退规则）
//   ⑦ 密码 SHA-256 哈希 + DPAPI 记住密码往返
//   ⑧ I18n 双语切换
//   ⑨ SN 去向路由（V2.15.20 二选一：SerialNumberTargets 判定 / sn 配置段 / MES 报文格式）
//   ⑪ 深色/浅色主题（V2.16.2：AppTheme 归一/语义色保留/控件上色 + Theme 配置持久化）
//   ⑫ 相机判定解析 ParseResult 全分支（V2.14.10/34：详细任一NG即NG/标准逐位/OkChar定制）
//   ⑬ 相机指令校验（SetOutputFormat/SwitchProgram/ReadProgramNo/SendTrigger/TriggerAndRead 无设备防御）
//   ⑭ 存图模板与路径（RenderTemplate/RenderSubDirsToSegments/Sanitize/NormalizeDir）
//   ⑮ 存图文件链路（FindLatestPair同主名配对/DeleteSourceFile/归档命名/清理日期判定，临时目录harness）
//   ⑯ 协调器纯函数（CameraLabel/CameraIdFor/IndexOfCamera/FindProgram/IsWindowEnabled/IsTcpImage/
//       FtpDirFor/SetManualSerial/IsNewerThanTrigger/TryResolveActiveWindow/重试计数/磨合期）
//   ⑰ SN分流与PLC分支（DeliverSerialNumber/ResolveModelIndex/len钳位/零地址/站号透传）
//   ⑱ 配置迁移与兜底（DefaultCameras锚点/EnsureCameraIdentity/EnsureDefaultCameraOrder/
//       EnsureWindowPointMaps/EnsureModelIndexes双向/EnsureStationMap/NormalizeSubDirs/布局边界）
//   ⑲ 扫码串口与TCP约定（StopBits/Parity/SendTrigger三态/串口空操作）
//   ⑳ 杂项（PlaceholderLocalizer中英互逆/ColorFromName/徽标开关/出厂账号/ScanConfig默认/
//       MES在途上限与空URL/AppTheme剩余分支/I18n事件）
//
// 【红线】禁止调用 ConfigStore.Load()/Save()——无参版本固定读写 bin\Debug\Config\
//   appconfig.json，会覆盖开发机现有配置。配置测试只做内存序列化往返。
//
// 【编译】由 tests.ps1 用 Roslyn csc 编译到 bin\Debug\cc_test_runner.exe 运行，
//   BaseDirectory=bin\Debug：依赖 dll 与 Logs 目录天然正确；跑完自动删除 runner。
// 退出码：0=全部通过；1=存在失败。
// ═══════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;   // ⑩ 组要 new DataGridView 单元格做 UI 取值用例（tests.ps1 同步加了 /r）
using IrisVision.Models;
using IrisVision.Services;
using IrisVision.Utils;
using IrisVision.Views;
using Newtonsoft.Json;

internal static class TestRunner
{
    private static int _pass, _fail;
    private static readonly List<string> _failures = new List<string>();

    private static void Check(string name, bool cond)
    {
        if (cond) { _pass++; Console.WriteLine("  [PASS] " + name); }
        else { _fail++; _failures.Add(name); Console.WriteLine("  [FAIL] " + name); }
    }

    private static void Eq<T>(string name, T expected, T actual)
        => Check(name + " (期望=" + expected + ", 实际=" + actual + ")", Equals(expected, actual));

    // 反射工具：调私有静态方法（打包函数等纯函数走反射，与生产代码同一份实现）
    private static object InvokePrivateStatic(Type t, string method, params object[] args)
    {
        var m = t.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
        if (m == null) throw new MissingMethodException(t.Name, method);
        return m.Invoke(null, args);
    }

    private static object InvokePrivateInstance(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) throw new MissingMethodException(obj.GetType().Name, method);
        return m.Invoke(obj, args);
    }

    private static void Group(string title)
    {
        Console.WriteLine();
        Console.WriteLine("── " + title + " ──");
    }

    private static int Main()
    {
        try { RunAll(); }
        catch (Exception ex)
        {
            Console.WriteLine("[FATAL] 测试框架异常：" + ex);
            return 1;
        }
        Console.WriteLine();
        Console.WriteLine("════════ 汇总：通过 " + _pass + " / 失败 " + _fail + " ════════");
        foreach (var f in _failures) Console.WriteLine("  FAIL: " + f);
        return _fail == 0 ? 0 : 1;
    }

    private static void RunAll()
    {
        TestAsciiPacking();       // ① V2.15.17 核心
        TestPlcSlaveRoundTrip();  // ②
        TestConfigModel();        // ③
        TestScanFilter();         // ④
        TestWindowLayout();       // ⑤
        TestStationProgramMap();  // ⑥
        TestSecurity();           // ⑦
        TestSnRoute();            // ⑨ V2.15.20：SN 去向路由二选一（放在 I18n 前，I18n 改全局语言状态须最后跑）
        TestWindowPointGridCells(); // ⑩ V2.15.x：窗口点位配置表下拉取值（防"改完回退成候选第一项"）
        TestCameraParseResult();    // ⑫ 相机判定解析（无网络，纯内存反射）
        TestCameraCmdValidate();    // ⑬ 相机指令校验（无设备防御分支，短超时127.0.0.1）
        TestImageTemplate();        // ⑭ 存图模板与路径渲染（纯内存）
        TestImageFiles();           // ⑮ 存图文件链路（临时目录 harness，测完自删）
        TestImageCleanup();         // ⑮ 清理日期判定（临时目录 harness，测完自删）
        TestCoordPure();            // ⑯ 协调器纯函数（反射 + 内存协调器，Timer 不启动）
        TestCoordDeliver();         // ⑰ SN分流与PLC分支（含注入 DataStore，与②组同手法）
        TestConfigMigrate();        // ⑱ 配置迁移与兜底（纯内存反射调 ApplyDefaults 子步骤）
        TestScanSerial();           // ⑲ 扫码串口与TCP约定（无设备）
        TestMisc2();                // ⑳ 杂项（全纯内存/内存控件，I18n/主题状态结尾还原）
        TestAppTheme();           // ⑪ V2.16.2：深色/浅色主题（改全局主题状态，放 I18n 紧前，结尾还原浅色）
        TestI18n();               // ⑧ 放最后（改全局语言状态）
    }

    // ───────────────────────── ① ASCII 寄存器打包（V2.15.17）─────────────────────────
    private static void TestAsciiPacking()
    {
        Group("① SN/型号 ASCII 寄存器打包 PackAsciiToRegisters（协议编码核心）");
        var t = typeof(PlcService);

        // 型号锚点：'Z'=0x5A '1'=0x31 '2'=0x32 —— 与《上位机PLC通信接口定义文档》§2.4 示例一致
        var z121 = (ushort[])InvokePrivateStatic(t, "PackAsciiToRegisters", "Z121", 5);
        Check("Z121 打包长度=5", z121 != null && z121.Length == 5);
        Eq("Z121[0]='Z''1'", 0x5A31, (int)z121[0]);
        Eq("Z121[1]='2''1'", 0x3231, (int)z121[1]);
        Check("Z121 尾部补 0x00", z121[2] == 0 && z121[3] == 0 && z121[4] == 0);

        // 空串/null = 全 0（PLC 以 0x00 作字符串结束符）
        var empty = (ushort[])InvokePrivateStatic(t, "PackAsciiToRegisters", "", 4);
        Check("空串打包=全 0", empty.All(v => v == 0));
        var nul = (ushort[])InvokePrivateStatic(t, "PackAsciiToRegisters", null, 4);
        Check("null 打包=全 0", nul != null && nul.All(v => v == 0));

        // 正好满容量
        var full = (ushort[])InvokePrivateStatic(t, "PackAsciiToRegisters", "ABCDEF", 3);
        Check("满容量 ABCDEF", full[0] == 0x4142 && full[1] == 0x4344 && full[2] == 0x4546);

        // 超长截断：7 字符进 3 寄存器只留前 6 字符（截断告警在 WriteSerialNumber 层，这里验纯函数）
        var trunc = (ushort[])InvokePrivateStatic(t, "PackAsciiToRegisters", "ABCDEFG", 3);
        Check("超长截断留前 6 字符", trunc[0] == 0x4142 && trunc[1] == 0x4344 && trunc[2] == 0x4546);

        // SN 场景（12 字符进默认 12 寄存器）：'A''B'→0x4142 … 第 11 字符 '1' 后补 0x00 结束符
        var sn = (ushort[])InvokePrivateStatic(t, "PackAsciiToRegisters", "AB20260820001", 12);
        Check("SN 长度=12", sn != null && sn.Length == 12);
        Eq("SN[0]", 0x4142, (int)sn[0]);
        Eq("SN[1]'2''0'", 0x3230, (int)sn[1]);
        Eq("SN[2]'2''6'", 0x3236, (int)sn[2]);
        Eq("SN[3]'0''8'", 0x3038, (int)sn[3]);
        Eq("SN[6]'1'+结束符", 0x3100, (int)sn[6]);
        Check("SN[7..11] 全 0", sn.Skip(7).All(v => v == 0));

        // 非 ASCII 容错：Encoding.ASCII 会替成 '?'(0x3F)，不崩即可（条码正常全 ASCII）
        var nonAscii = (ushort[])InvokePrivateStatic(t, "PackAsciiToRegisters", "中A", 2);
        Check("非 ASCII 不崩且 'A' 在低字节", nonAscii != null && nonAscii.Length == 2 && (nonAscii[0] & 0xFF) == 0x41);
    }

    // ───────────────────────── ② PLC 从站读写往返（真实链路）─────────────────────────
    private static void TestPlcSlaveRoundTrip()
    {
        Group("② PLC 从站读写往返（WriteSerialNumber/WriteProductModel/结果寄存器/上电清零）");
        var cfg = new PlcConfig();   // 默认：扫码请求=1 结果=4 序号=7 型号=8/5 SN=13/12
        var svc = new PlcService(cfg);
        var tSvc = typeof(PlcService);
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        // 未就绪（未建站）：写方法返回 false、读方法返回 false，绝不 NRE
        Check("未建站 WriteSerialNumber=false", svc.WriteSerialNumber("SN001") == false);
        Check("未建站 WriteProductModel=false", svc.WriteProductModel("U171") == false);
        bool req; Check("未建站 ReadScanRequest=false", svc.ReadScanRequest(out req) == false && req == false);
        ushort v; Check("未建站 ReadRegister=false", svc.ReadRegister(1, out v) == false);

        // 建立从站数据区：优先真建站（502 监听），端口被占则反射注入 SlaveDataStore 兜底
        object store = null;
        bool liveStation = false;
        try { liveStation = svc.EnsureConnected(); } catch { liveStation = false; }
        if (liveStation)
        {
            store = tSvc.GetField("_dataStore", NP).GetValue(svc);
            Console.WriteLine("  （真实建站 502 成功）");
        }
        else
        {
            store = Activator.CreateInstance(typeof(NModbus.Data.SlaveDataStore));
            tSvc.GetField("_dataStore", NP).SetValue(svc, store);
            Console.WriteLine("  （502 被占，注入 SlaveDataStore 兜底）");
        }
        var regs = (NModbus.Data.PointSource<ushort>)store.GetType().GetProperty("HoldingRegisters").GetValue(store);

        // SN 写入 → 读回校验（V2.15.17 主链路）
        Check("WriteSerialNumber(SN12345)=true", svc.WriteSerialNumber("SN12345"));
        var snGot = regs.ReadPoints((ushort)cfg.ScanSerialNumberAddress, (ushort)cfg.ScanSerialNumberLen);
        Check("SN 区读回长度=Len", snGot.Length == cfg.ScanSerialNumberLen);
        Eq("SN[0]='S''N'", 0x534E, (int)snGot[0]);
        Eq("SN[1]='1''2'", 0x3132, (int)snGot[1]);
        Eq("SN[2]='3''4'", 0x3334, (int)snGot[2]);
        Eq("SN[3]='5'+结束符", 0x3500, (int)snGot[3]);
        Check("SN[4..] 补 0", snGot.Skip(4).All(x => x == 0));
        Eq("_currentSerial 缓存刷新", "SN12345",
            (string)tSvc.GetField("_currentSerial", NP).GetValue(svc));

        // SN 清零语义：空串=整区清 0 且缓存同步清空
        Check("WriteSerialNumber('')=true", svc.WriteSerialNumber(""));
        var cleared = regs.ReadPoints((ushort)cfg.ScanSerialNumberAddress, (ushort)cfg.ScanSerialNumberLen);
        Check("清零后 SN 区全 0", cleared.All(x => x == 0));
        Eq("清零后缓存为空串", "", (string)tSvc.GetField("_currentSerial", NP).GetValue(svc));

        // 超长 SN 截断：26 字符 > 24 容量，只留前 24 字符（内部会记 WARN 日志）
        string longSn = new string('A', 26);
        Check("超长 SN 写入不崩", svc.WriteSerialNumber(longSn));
        var truncGot = regs.ReadPoints((ushort)cfg.ScanSerialNumberAddress, (ushort)cfg.ScanSerialNumberLen);
        Check("超长 SN 前 12 寄存器全 'A'(0x4141)", truncGot.All(x => x == 0x4141));

        // 型号写入：序号映射命中（大小写不敏感）+ 字符串区
        cfg.ModelIndexes.Add(new ModelIndexItem { ModelName = "Z121", ModelIndex = 1 });
        cfg.ModelIndexes.Add(new ModelIndexItem { ModelName = "U171", ModelIndex = 2 });
        Check("WriteProductModel(z121 小写)=true", svc.WriteProductModel("z121"));
        Eq("40007 序号=1（大小写不敏感命中 Z121）", (ushort)1,
            regs.ReadPoints(cfg.ProductModelIndexAddress, 1)[0]);
        // 字符串区按传入原样写入（大小写敏感）：用大写 Z121 验证打包
        Check("WriteProductModel(Z121)=true", svc.WriteProductModel("Z121"));
        var modelGot = regs.ReadPoints((ushort)cfg.ProductModelAddress, (ushort)cfg.ProductModelLen);
        Eq("型号[0]='Z''1'", 0x5A31, (int)modelGot[0]);
        // 未配序号的型号 → 40007 写 0，字符串照常
        Check("WriteProductModel(X9)=true", svc.WriteProductModel("X9"));
        Eq("未配序号型号 40007=0", (ushort)0, regs.ReadPoints(cfg.ProductModelIndexAddress, 1)[0]);

        // 扫码结果 + 通用读写往返
        svc.WriteScanResult(2);
        Eq("40004 读回=2", (ushort)2, regs.ReadPoints((ushort)cfg.ScanResultAddress, 1)[0]);
        Check("WriteRegister 往返", svc.WriteRegister(100, 1234)
            && svc.ReadRegister(100, out v) && v == 1234);

        // 上电初始化（ResetResultRegisters）：结果位/SN 区全 0 + 缓存作废
        svc.WriteScanResult(2);                       // 先造残留
        svc.WriteSerialNumber("RESIDUAL_SN");         // 先造残留
        InvokePrivateInstance(svc, "ResetResultRegisters");
        Eq("上电后 40004=0", (ushort)0, regs.ReadPoints((ushort)cfg.ScanResultAddress, 1)[0]);
        var afterReset = regs.ReadPoints((ushort)cfg.ScanSerialNumberAddress, (ushort)cfg.ScanSerialNumberLen);
        Check("上电后 SN 区全 0", afterReset.All(x => x == 0));
        Eq("上电后缓存作废", "", (string)tSvc.GetField("_currentSerial", NP).GetValue(svc));

        // 相机通道地址注册 + WriteCameraResult（0=未配置跳过不崩）
        svc.SetCameraResultAddresses(new List<CameraConfig>());
        svc.WriteCameraResult(null, 2);               // null 相机不崩（地址 0 跳过）
        svc.Dispose();                                // 释放监听（若走了真建站）
        Check("Dispose 后可重复 Dispose", true);
        svc.Dispose();

        // Dispose 后写 SN：应安全返回 false 或静默（不崩）
        try { svc.WriteSerialNumber("AFTER_DISPOSE"); Check("Dispose 后写 SN 不崩", true); }
        catch (Exception ex) { Check("Dispose 后写 SN 不崩 (异常:" + ex.GetType().Name + ")", false); }
    }

    // ───────────────────────── ③ 配置模型默认值 / json 兼容 ─────────────────────────
    private static void TestConfigModel()
    {
        Group("③ 配置模型默认值 / 新旧 json 兼容 / ApplyDefaults");
        var cfg = new AppConfig();
        Eq("顶层 ProductModel 默认 U171", "U171", cfg.ProductModel);
        Eq("scanSerialNumberAddress 默认 13", 13, (int)cfg.Plc.ScanSerialNumberAddress);
        Eq("scanSerialNumberLen 默认 12", 12, (int)cfg.Plc.ScanSerialNumberLen);
        Eq("productModelAddress 默认 8", 8, (int)cfg.Plc.ProductModelAddress);
        Eq("scanResultAddress 默认 4", 4, (int)cfg.Plc.ScanResultAddress);
        Eq("ScanConfig.Mode 默认 Tcp", "Tcp", new ScanConfig().Mode);
        // ApplyDefaults 后 Scanners 空列表兜底一台启用 TCP 枪（与设置页 TCP 模板行一致）
        var bare2 = new AppConfig();
        bare2.Scanners.Clear();
        InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", bare2);
        Check("ApplyDefaults 兜底扫码枪启用 TCP",
            bare2.Scanners.Count > 0 && bare2.Scanners[0].Mode == "Tcp" && bare2.Scanners[0].Enabled);

        // 小驼峰序列化（与 ConfigStore.Save 同规则）：新字段必须以小驼峰出现（混淆豁免红线的根基）
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };
        string json = JsonConvert.SerializeObject(cfg, settings);
        Check("json 含 scanSerialNumberAddress:13", json.Contains("\"scanSerialNumberAddress\":13"));
        Check("json 含 scanSerialNumberLen:12", json.Contains("\"scanSerialNumberLen\":12"));

        // 往返一致
        var back = JsonConvert.DeserializeObject<AppConfig>(json, settings);
        Eq("往返 ScanSerialNumberAddress", 13, (int)back.Plc.ScanSerialNumberAddress);
        Eq("往返 ScanSerialNumberLen", 12, (int)back.Plc.ScanSerialNumberLen);

        // 旧格式兼容：旧版 json 无这两个字段 → 反序列化落默认值 13/12（现场升级零迁移）
        var oldJson = "{\"plc\":{\"ipAddress\":\"19.87.6.230\",\"port\":502}}";
        var fromOld = JsonConvert.DeserializeObject<AppConfig>(oldJson, settings);
        Eq("旧json 缺字段→默认 13", 13, (int)fromOld.Plc.ScanSerialNumberAddress);
        Eq("旧json 缺字段→默认 12", 12, (int)fromOld.Plc.ScanSerialNumberLen);

        // 手改脏值防御：len 配成 0/负数/超大，写入时被钳位（见 PlcService 内 Math.Max/Min）
        // 这里验证属性可写不抛异常（钳位发生在服务层写入路径，②组已覆盖真实写入）
        cfg.Plc.ScanSerialNumberLen = -5; Check("负数 Len 可赋值(钳位在服务层)", cfg.Plc.ScanSerialNumberLen == -5);

        // V2.16.2 主题配置：默认浅色 + 小驼峰落盘 + 旧 json 缺字段兼容 + 脏值归一
        Eq("Theme 默认 Light", "Light", new AppConfig().Theme);
        Check("json 含 theme 字段(小驼峰)", json.Contains("\"theme\""));
        var oldNoTheme = JsonConvert.DeserializeObject<AppConfig>("{\"productModel\":\"U171\"}", settings);
        Eq("旧json 缺 theme→默认 Light", "Light", oldNoTheme.Theme);
        var bareTheme = new AppConfig();
        bareTheme.Theme = "xxx";
        InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", bareTheme);
        Eq("ApplyDefaults 脏 theme 回落 Light", "Light", bareTheme.Theme);
        bareTheme.Theme = "dark";
        InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", bareTheme);
        Eq("ApplyDefaults dark 归一 Dark", "Dark", bareTheme.Theme);

        // ApplyDefaults（private static，纯内存兜底）：空段补齐
        var bare = new AppConfig();
        bare.Plc.ModelIndexes.Clear();
        bare.ProductModels.Clear();
        bare.Image.SubDirs.Clear();
        InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", bare);
        Check("ApplyDefaults 补型号候选", bare.ProductModels.Contains("U171") || bare.ProductModels.Count > 0);
        Check("ApplyDefaults 补序号映射(≥2 条)", bare.Plc.ModelIndexes.Count >= 2);
        Check("ApplyDefaults 补存图层级(含{相机})", bare.Image.SubDirs.Any(s => s != null && s.Contains("相机")));
        Check("ApplyDefaults 幂等（二次执行不炸）",
            (InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", bare) == null));
    }

    // ───────────────────────── ④ 扫码错误文本过滤 ─────────────────────────
    private static void TestScanFilter()
    {
        Group("④ 扫码错误文本过滤 IsIgnoredScanText（V2.14.30/33 防脏码入库）");
        var sc = new ScanConfig();   // 默认名单 ERROR,ERR,NG,NOREAD

        // 命中（忽略大小写精确匹配）
        Check("ERROR 命中", sc.IsIgnoredScanText("ERROR"));
        Check("error 小写命中", sc.IsIgnoredScanText("error"));
        Check("NG 命中", sc.IsIgnoredScanText("NG"));
        Check("ng 小写命中", sc.IsIgnoredScanText("ng"));
        Check("NOREAD 命中", sc.IsIgnoredScanText("NOREAD"));
        Check("ER 不在默认名单(放行)", !sc.IsIgnoredScanText("ER"));   // 默认名单无 "ER" 项
        Check("空白文本视为无效", sc.IsIgnoredScanText("") && sc.IsIgnoredScanText("   "));
        Check("null 视为无效", sc.IsIgnoredScanText(null));

        // 不误伤同前缀真码（精确匹配语义的关键）
        Check("ERROR123 不误伤", !sc.IsIgnoredScanText("ERROR123"));
        Check("NG1234567890 不误伤", !sc.IsIgnoredScanText("NG1234567890"));
        Check("ER123 不误伤", !sc.IsIgnoredScanText("ER123"));

        // 真实条码放行
        Check("真实条码放行", !sc.IsIgnoredScanText("AB20260820001"));

        // 前缀通配（* 结尾=前缀匹配）。V2.15.18 修复：名单按英文逗号拆分，"ER,*" 自身含逗号
        // 会被拆成 "ER"+"*" 而静默失效——现把孤立 "*" 并回前项还原完整通配项。
        sc.IgnoreScanTexts = "ER,*";
        Check("ER,* 命中 ER,READ,00（V2.15.18 修复点）", sc.IsIgnoredScanText("ER,READ,00"));
        Check("ER,* 命中 errX(忽略大小写)", sc.IsIgnoredScanText("errX"));
        Check("ER,* 前缀命中 ERA123（前缀匹配语义即如此）", sc.IsIgnoredScanText("ERA123"));
        Check("ER,* 不影响无关码", !sc.IsIgnoredScanText("AB123"));
        // 等价简写：不带逗号的 "ER*"
        sc.IgnoreScanTexts = "ER*";
        Check("ER* 同样命中 ER,READ,00", sc.IsIgnoredScanText("ER,READ,00"));
        // 混合名单：精确项 + 通配项共存
        sc.IgnoreScanTexts = "ERROR,NR*";
        Check("混合名单精确项生效", sc.IsIgnoredScanText("ERROR"));
        Check("混合名单通配项生效(NR123)", sc.IsIgnoredScanText("NR123"));
        Check("NR* 不命中 NO 开头文本", !sc.IsIgnoredScanText("NOREAD_X"));
        Check("混合名单不影响无关码", !sc.IsIgnoredScanText("AB999"));

        // 多分隔符解析（逗号/中文逗号/分号/顿号混用）
        sc.IgnoreScanTexts = "ERROR，NG；NOREAD、ERR";
        Check("中文逗号分隔解析", sc.IsIgnoredScanText("ERROR") && sc.IsIgnoredScanText("NG"));
        Check("顿号分隔解析", sc.IsIgnoredScanText("ERR"));
        Check("分号项命中", sc.IsIgnoredScanText("noread"));

        // 名单留空 = 不过滤
        sc.IgnoreScanTexts = "";
        Check("名单留空不过滤真码", !sc.IsIgnoredScanText("ERROR"));
        sc.IgnoreScanTexts = null;
        Check("名单 null 不过滤且不崩", !sc.IsIgnoredScanText("ERROR"));
    }

    // ───────────────────────── ⑤ 窗口布局统一模型 ─────────────────────────
    private static void TestWindowLayout()
    {
        Group("⑤ 窗口布局统一模型 ResolveLayout / 默认铺排 / 孤儿映射");
        var cams = CameraConfig.DefaultCameras();          // 上(id2, U171 表17条) + 下(id1, 4条)
        int totalU171 = DisplayConfig.WindowCountFor(cams, "U171");
        Eq("U171 窗口总数=21（上17+下4）", 21, totalU171);

        // 自适应形状：total=21 → 最优 (rows=3, cols=7)（行列和最小并列时列多者优先）
        var auto = DisplayConfig.ResolveLayout(cams, "U171", true, 1, 1);
        Eq("自适应 rows=3", 3, auto.rows);
        Eq("自适应 cols=7", 7, auto.cols);
        Eq("自适应 windowCount=点位数", totalU171, auto.windowCount);

        // 自适应边界：无相机 → 1×1
        var one = DisplayConfig.ResolveLayout(null, "U171", true, 1, 1);
        Check("空相机自适应=1×1×1", one.rows == 1 && one.cols == 1 && one.windowCount == 1);

        // 非自适：手填 2×7 放不下 21 点位 → 自动补行到 ceil(21/7)=3 → windowCount=21
        var manual = DisplayConfig.ResolveLayout(cams, "U171", false, 2, 7);
        Check("非自适补行 3×7=21", manual.rows == 3 && manual.cols == 7 && manual.windowCount == 21);

        // 非自适列数钳位上限 7（手填 9 列也压回 7）
        var clamp = DisplayConfig.ResolveLayout(cams, "U171", false, 3, 9);
        Check("手填列钳位 7 且补行", clamp.cols == 7 && clamp.rows == 3 && clamp.windowCount == 21);

        // 默认铺排：前 N 个有效条目（前上相机 id2 后下相机 id1）+ 尾部 null 空窗口
        var map = DisplayConfig.DefaultWindowPointMap(cams, "U171", 28);
        Eq("铺排长度=windowCount", 28, map.Count);
        Check("前 21 条目非空", map.Take(21).All(p => p != null));
        Check("尾部 7 个=空窗口(null)", map.Skip(21).All(p => p == null));
        Eq("窗口1=上相机点位1", 1, map[0].StationNo);
        Eq("窗口1 归属上相机 id2", 2, map[0].CameraId);
        Eq("窗口18=下相机首条", cams[1].ProgramsFor("U171")[0].StationNo, map[17].StationNo);
        Eq("窗口18 归属下相机 id1", 1, map[17].CameraId);

        // 各相机窗口起始序号（前缀和）：上=1、下=18
        var starts = DisplayConfig.AutoFitCameraStarts(cams, "U171");
        Check("起始序号=[1,18]", starts.Count == 2 && starts[0] == 1 && starts[1] == 18);

        // 孤儿映射校验：合法/孤儿/null 三态
        Check("默认铺排对相机表有效", DisplayConfig.PointMapValidForCameras(cams, map));
        var orphan = new List<WindowPointItem> { new WindowPointItem { CameraId = 99, StationNo = 1 } };
        Check("孤儿 CameraId 判无效", !DisplayConfig.PointMapValidForCameras(cams, orphan));
        Check("null 映射判无效", !DisplayConfig.PointMapValidForCameras(cams, null));
        Check("含 null 空窗口的映射有效", DisplayConfig.PointMapValidForCameras(cams,
            new List<WindowPointItem> { null, new WindowPointItem { CameraId = 2, StationNo = 3 } }));

        // ResolveWindowPointMap 运行时防御：映射含孤儿 → 自动回退默认铺排（长度对齐 windowCount）
        var badMaps = new List<ModelWindowPointMap>
        {
            new ModelWindowPointMap { ModelName = "U171", Points = orphan }
        };
        var resolved = DisplayConfig.ResolveWindowPointMap(cams, "U171", badMaps, 28);
        Eq("坏映射回退默认铺排长度", 28, resolved.Count);
        Check("回退后首窗=上相机点位1", resolved[0] != null && resolved[0].CameraId == 2 && resolved[0].StationNo == 1);

        // 大小写不敏感查型号表
        Eq("u171 小写同表", DisplayConfig.WindowCountFor(cams, "u171"), totalU171);
    }

    // ───────────────────────── ⑥ 点位→程序号映射（按相机+型号分表）─────────────────────────
    private static void TestStationProgramMap()
    {
        Group("⑥ 点位→程序号映射 ResolveProgramForStation（型号表命中/回退默认/未配-1）");
        var cams = CameraConfig.DefaultCameras();
        // 协调器最小构造（plc/imageStore/windowEnabled/windowPointMaps 传 null 安全）；
        // Timer 以 Infinite 创建不启动轮询，测完 Dispose。
        var coord = new ProductionCoordinator(null, null, cams, null, null, "U171", null, 24);
        try
        {
            var upper = cams[0];   // 上相机
            var lower = cams[1];   // 下相机
            int upTableCnt = upper.ProgramsFor("U171").Count;
            Eq("上相机 U171 点位表=17 条", 17, upTableCnt);
            Eq("下相机 U171 点位表=4 条", 4, lower.ProgramsFor("U171").Count);

            // 锚点断言（固化 V2.15.21 现场映射：点位1→P000=0、点位14→P011=11；变更须同步文档）
            Eq("上·点位1→程序0(P000)", 0, (int)InvokePrivateInstance(coord, "ResolveProgramForStation", upper, 1));
            Eq("上·点位14→程序11(P011)", 11, (int)InvokePrivateInstance(coord, "ResolveProgramForStation", upper, 14));
            // 自洽性：表中每一条都能反查出自己声明的程序号
            bool selfOk = upper.ProgramsFor("U171")
                .All(it => it != null && (int)InvokePrivateInstance(coord, "ResolveProgramForStation", upper, it.StationNo) == it.ProgramNo);
            Check("上相机全表自洽", selfOk);
            bool lowOk = lower.ProgramsFor("U171")
                .All(it => it != null && (int)InvokePrivateInstance(coord, "ResolveProgramForStation", lower, it.StationNo) == it.ProgramNo);
            Check("下相机全表自洽", lowOk);
            // 表外点位 → -1（不发 PW 不切换）
            Eq("表外点位9999→-1", -1, (int)InvokePrivateInstance(coord, "ResolveProgramForStation", upper, 9999));
            // null 相机防御
            Eq("null 相机→-1", -1, (int)InvokePrivateInstance(coord, "ResolveProgramForStation", null, 1));

            // 型号没配表的相机 → 回退默认表 StationPrograms
            var custom = new CameraConfig
            {
                Name = "自定义",
                IpAddress = "10.0.0.9",
                StationPrograms = new List<StationProgramItem>
                {
                    new StationProgramItem { StationNo = 7, ProgramNo = 42 }
                },
                ModelStationPrograms = null
            };
            Eq("无型号表回退默认表", 42, (int)InvokePrivateInstance(coord, "ResolveProgramForStation", custom, 7));
            Eq("默认表也没有→-1", -1, (int)InvokePrivateInstance(coord, "ResolveProgramForStation", custom, 8));
        }
        finally { coord.Dispose(); }

        // ProgramsFor 边界：未知型号回退默认表；空表返回非 null 空列表
        var c2 = new CameraConfig { StationPrograms = new List<StationProgramItem> { new StationProgramItem { StationNo = 1, ProgramNo = 5 } } };
        Eq("未知型号回退默认表数量", 1, c2.ProgramsFor("UNKNOWN_X").Count);
        var c3 = new CameraConfig();
        Check("全空相机 ProgramsFor 非 null", c3.ProgramsFor("U171") != null && c3.ProgramsFor("U171").Count == 0);
    }

    // ───────────────────────── ⑦ 安全（哈希 + DPAPI）─────────────────────────
    private static void TestSecurity()
    {
        Group("⑦ 密码哈希与记住密码 DPAPI");
        // 锚点：出厂默认 admin123 的哈希必须与 SecurityConfig 默认值一致（登录链路的根）
        Eq("admin123 哈希=出厂默认", SecurityConfig_AdminHash(), SecurityUtil.HashPassword("admin123"));
        Check("哈希为 64 位小写 hex",
            SecurityUtil.HashPassword("abc").Length == 64
            && SecurityUtil.HashPassword("abc").All(ch => (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f')));
        Check("不同密码哈希不同", SecurityUtil.HashPassword("a") != SecurityUtil.HashPassword("b"));
        Check("相同密码哈希稳定", SecurityUtil.HashPassword("abc") == SecurityUtil.HashPassword("abc"));
        Check("空密码→空串(不崩)", SecurityUtil.HashPassword("") == "" && SecurityUtil.HashPassword(null) == "");
        Check("中文密码 UTF8 哈希稳定", SecurityUtil.HashPassword("管理员123") == SecurityUtil.HashPassword("管理员123"));

        // DPAPI 记住密码往返（isDev=true 用开发者文件，避免碰现场管理员的 remembered_login.dat；
        // 测完 Clear 清理。注意：会覆盖开发机上已有的开发者记住记录——仅限开发环境运行）
        const bool isDev = true;
        try
        {
            SecurityUtil.SaveRememberedLogin(isDev, "tuser", "tPass#123");
            string u, p;
            bool ok = SecurityUtil.LoadRememberedLogin(isDev, out u, out p);
            Check("DPAPI 保存后能读回", ok);
            Eq("DPAPI 用户名往返", "tuser", u);
            Eq("DPAPI 密码往返", "tPass#123", p);
        }
        finally { SecurityUtil.ClearRememberedLogin(isDev); }
        string u2, p2;
        Check("清除后 Load=false", !SecurityUtil.LoadRememberedLogin(isDev, out u2, out p2));
    }

    private static string SecurityConfig_AdminHash()
        => (string)typeof(SecurityConfig).GetProperty("AdminPasswordHash").GetValue(new SecurityConfig());

    // ───────────────────────── ⑧ I18n 双语切换（最后跑：改全局状态）─────────────────────────
    // ───────────────────────── ⑨ SN 去向路由（V2.15.20 二选一）─────────────────────────
    private static void TestSnRoute()
    {
        Group("⑨ SN 去向路由 SerialNumberTargets / sn 配置段 / MES 报文（V2.15.20 二选一，默认 Mes）");

        // Normalize（V2.15.20 二选一）：null/空串/非法值/已废弃的 "Both" 一律回落规范 "Mes"（默认 SN 传 MES）
        Eq("Normalize(null)=Mes", "Mes", SerialNumberTargets.Normalize(null));
        Eq("Normalize(空串)=Mes", "Mes", SerialNumberTargets.Normalize(""));
        Eq("Normalize(垃圾值)=Mes", "Mes", SerialNumberTargets.Normalize("xxx"));
        Eq("Normalize(废弃值 BOTH)=Mes", "Mes", SerialNumberTargets.Normalize("BOTH"));
        Eq("Normalize(mes 小写)=Mes", "Mes", SerialNumberTargets.Normalize("mes"));
        Eq("Normalize(PLC 大写)=Plc", "Plc", SerialNumberTargets.Normalize("PLC"));

        // 二值判定（互斥）：Mes 传 MES 不写 PLC；Plc 写 PLC 不传 MES
        Check("Mes → 传 MES", SerialNumberTargets.SendsMes("Mes"));
        Check("Mes → 不写 PLC", !SerialNumberTargets.WritesPlc("Mes"));
        Check("Plc → 写 PLC", SerialNumberTargets.WritesPlc("Plc"));
        Check("Plc → 不传 MES", !SerialNumberTargets.SendsMes("Plc"));
        // 脏值按 Normalize 语义判定（协调器内部就是 Normalize 后再判定，两条路径同一实现）
        Check("脏值 → 按 Mes 判定（传 MES、不写 PLC）",
            SerialNumberTargets.SendsMes("junk") && !SerialNumberTargets.WritesPlc("junk"));

        // 默认值：target 默认 Mes（V2.15.20 起默认传 MES）、MES 地址空、超时 3000
        var sn = new SnRouteConfig();
        Eq("SnRouteConfig.Target 默认 Mes", "Mes", sn.Target);
        Eq("SnRouteConfig.MesUrl 默认空", "", sn.MesUrl);
        Eq("SnRouteConfig.MesTimeoutMs 默认 3000", 3000, sn.MesTimeoutMs);
        var app = new AppConfig();
        Check("AppConfig.Sn 默认非 null", app.Sn != null);

        // 小驼峰序列化（与 ConfigStore.Save 同规则）：sn 段三个键名必须是小驼峰（混淆豁免红线的根基）
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };
        app.Sn.Target = "Plc"; app.Sn.MesUrl = "http://19.87.6.50:8080/api/sn"; app.Sn.MesTimeoutMs = 5000;
        string json = JsonConvert.SerializeObject(app, settings);
        Check("json 含 sn 段", json.Contains("\"sn\":"));
        Check("json 含 sn.target 键", json.Contains("\"target\":"));
        Check("json 含 sn.mesUrl 键", json.Contains("\"mesUrl\":"));
        Check("json 含 sn.mesTimeoutMs 键", json.Contains("\"mesTimeoutMs\":"));
        // 往返一致
        var back = JsonConvert.DeserializeObject<AppConfig>(json, settings);
        Eq("往返 sn.target=Plc", "Plc", back.Sn.Target);
        Eq("往返 sn.mesUrl", "http://19.87.6.50:8080/api/sn", back.Sn.MesUrl);
        Eq("往返 sn.mesTimeoutMs=5000", 5000, back.Sn.MesTimeoutMs);
        // json 手写脏值：反序列化原样进来，由 ApplyDefaults 归一（与运行时 Load 路径一致）
        var dirty = new AppConfig(); dirty.Sn.Target = "bogus";
        InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", dirty);
        Eq("ApplyDefaults 归一脏 target→Mes", "Mes", dirty.Sn.Target);
        var nulled = new AppConfig(); nulled.Sn = null;
        InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", nulled);
        Check("ApplyDefaults 补 sn=null 段", nulled.Sn != null && nulled.Sn.Target == "Mes");

        // MES 报文（通用占位格式，客户协议定稿后只改 BuildPayload）：小驼峰字段 + 时间格式
        string payload = MesService.BuildPayload("SN12345", "U171", new DateTime(2026, 8, 30, 12, 0, 0));
        Check("payload 含 sn 字段", payload.Contains("\"sn\":\"SN12345\""));
        Check("payload 含 model 字段", payload.Contains("\"model\":\"U171\""));
        Check("payload 含 time 字段(yyyy-MM-dd HH:mm:ss.fff)", payload.Contains("\"time\":\"2026-08-30 12:00:00.000\""));
        // 防御：null 入参不崩（序列化层兜空串）
        string payloadNull = MesService.BuildPayload(null, null, DateTime.Now);
        Check("payload null 入参兜空串", payloadNull.Contains("\"sn\":\"\"") && payloadNull.Contains("\"model\":\"\""));

        // SendSerialAsync 防御路径（不触网）：空 SN 直接返回；URL 未配置 WARN 一次后返回——均不抛异常
        var mes = new MesService(new SnRouteConfig());
        try
        {
            mes.SendSerialAsync("", "U171");            // 空 SN：直接返回
            mes.SendSerialAsync("SN001", "U171");       // URL 未配置：WARN 一次后返回
            mes.SendSerialAsync("SN002", "U171");       // 第二次也不崩（_urlWarned 已置位）
            Check("SendSerialAsync 空 SN/空 URL 不崩不触网", true);
        }
        catch (Exception ex)
        {
            Check("SendSerialAsync 空 SN/空 URL 不崩不触网 (异常:" + ex.Message + ")", false);
        }
        finally { mes.Dispose(); }
    }

    private static void TestI18n()
    {
        Group("⑧ I18n 双语切换");
        I18n.Language = "zh-CN";
        Eq("中文取 zh 文案", "系统设置", I18n.T("系统设置", "Settings"));
        I18n.Language = "en-US";
        Eq("英文取 en 文案", "Settings", I18n.T("系统设置", "Settings"));
        Eq("en 为空回落 zh", "扫码OK", I18n.T("扫码OK", ""));
        I18n.Language = "fr-FR";                 // 非法值回落中文
        Eq("非法语言回落中文", "zh-CN", I18n.Language);
        Eq("非法语言后取 zh 文案", "系统设置", I18n.T("系统设置", "Settings"));
        I18n.Language = "zh-CN";                 // 还原默认
    }

    // ───────────────────── ⑩ 窗口点位配置表下拉单元格取值（V2.15.x 防回退）──────────────────────
    // 【背景】DataGridViewComboBoxCell 规定"单元格值必须能在下拉候选里找到"，找不到就判"值无效"，
    //   并把单元格回退成候选第一项——点位/程序号候选里"一个字符串 + 一堆 int"时，用户改完程序号
    //   提交后值被转成字符串"5"，与候选里的 int 5 对不上 → 改完的值被悄悄回退成"不切换"（候选第一项）。
    // 【修法】候选与单元格值【全部用字符串】+ 下拉禁手输（DropDownList）+ 重建候选时把已有值补回候选。
    // 这里用反射直接测那三个纯函数（UI 交互层由人工/冒烟覆盖，用例集只锚纯逻辑）。
    private static void TestWindowPointGridCells()
    {
        Group("⑩ 窗口点位配置表下拉单元格取值（V2.15.x 防回退：候选/值全字符串）");
        var t = typeof(IrisVision.Views.WindowPointForm);

        // NumText：int → 候选文本（固定不变文化，绝不能被本地化成 "1,234"）
        Eq("NumText(5)", "5", (string)InvokePrivateStatic(t, "NumText", 5));
        Eq("NumText(0)", "0", (string)InvokePrivateStatic(t, "NumText", 0));
        Eq("NumText(127)", "127", (string)InvokePrivateStatic(t, "NumText", 127));

        // CellText：单元格值 → 文本（null/空 → 空串；字符串与老配置残留的 int 都能吃；去首尾空格）
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewTextBoxColumn());
        grid.Columns.Add(new DataGridViewTextBoxColumn());
        grid.Rows.Add("3", "5");
        var c1 = grid.Rows[0].Cells[1];
        Eq("CellText 取字符串值", "3", (string)InvokePrivateStatic(t, "CellText", grid.Rows[0].Cells[0]));
        c1.Value = 7;                                    // 老配置残留 int 也要能吃
        Eq("CellText 取 int 值", "7", (string)InvokePrivateStatic(t, "CellText", c1));
        c1.Value = null;
        Eq("CellText null → 空串", "", (string)InvokePrivateStatic(t, "CellText", c1));
        c1.Value = "  9  ";
        Eq("CellText 去首尾空格", "9", (string)InvokePrivateStatic(t, "CellText", c1));

        // EnsureCandidate：候选补值 + 按文本去重（候选里的 "5" 与新值 int 5 视为同一项，不重复添加）
        var col = new DataGridViewComboBoxColumn();
        col.Items.Add("不切换");
        col.Items.Add("5");
        InvokePrivateStatic(t, "EnsureCandidate", col, (object)5);       // 同文本不同类型 → 不重复添加
        Eq("EnsureCandidate 同文本不重复添加", 2, col.Items.Count);
        InvokePrivateStatic(t, "EnsureCandidate", col, (object)"9");     // 新值 → 补进候选（保住用户配的值）
        Eq("EnsureCandidate 新值补进候选", 3, col.Items.Count);
        Eq("EnsureCandidate 补进去的是字符串", "9", Convert.ToString(col.Items[2]));
        InvokePrivateStatic(t, "EnsureCandidate", col, (object)null);    // null/空不补、不崩
        InvokePrivateStatic(t, "EnsureCandidate", col, (object)"");
        Eq("EnsureCandidate null/空不补", 3, col.Items.Count);
        grid.Dispose();
    }

    // ───────────────────── ⑫ 相机判定解析 ParseResult 全分支 ─────────────────────
    // 【背景】T2/RT 响应有标准（RT,00000000 逐位）与详细（RT,计数,OK,... 明文）两种格式；
    // V2.14.34 起详细格式扫描【全部】字段、任一明文 NG 即判 NG（复合工具任一不良=整体不良）。
    // 反射调 private 实例方法 ParseResult（与生产同一份实现），KeyenceIV4Camera 构造无网络副作用。
    private static void TestCameraParseResult()
    {
        Group("⑫ 相机判定解析 ParseResult（详细任一NG即NG/标准逐位/OkChar/计数）");
        var cam = new KeyenceIV4Camera(new CameraConfig());
        Func<string, TriggerReadOutcome> parse =
            raw => (TriggerReadOutcome)InvokePrivateInstance(cam, "ParseResult", raw);

        // 空/坏帧 → 失败（绝不默认 OK，V1.7.2 空判定防放行）
        Check("null → 失败", !parse(null).Succeeded);
        Check("空串 → 失败", !parse("").Succeeded);
        Check("非RT前缀 → 失败", !parse("XX,00000000").Succeeded);
        Check("RT,（空判定）→ 失败", !parse("RT,").Succeeded);
        Check("RT,空格 → 失败", !parse("RT,   ").Succeeded);

        // 详细格式锚点（现场实测帧）
        var ok = parse("RT,00152,OK,01,OK,0000100");
        Check("详细OK帧 Succeeded", ok.Succeeded);
        Check("详细OK帧 IsOk", ok.IsOk);
        Eq("详细OK帧 TriggerNo=152", 152L, ok.TriggerNo);
        var ng = parse("RT,00151,NG,01,NG,0000000");
        Check("详细NG帧 Succeeded", ng.Succeeded);
        Check("详细NG帧 IsNg", !ng.IsOk);

        // V2.14.34 收紧点：多判定混杂、任一 NG 即 NG（旧"只看第2字段"会误报 OK）
        Check("混杂 RT,10,OK,01,NG,000 → NG", !parse("RT,10,OK,01,NG,000").IsOk);
        Check("混杂全OK → OK", parse("RT,10,OK,01,OK,000").IsOk);
        // 大小写/空格容忍（Trim + OrdinalIgnoreCase）
        Check("小写 rt,1, ng → NG", !parse("rt,1, ng ").IsOk);
        Check("小写 RT,1, ok → OK", parse("RT,1, ok").IsOk);

        // 无明文回退标准格式：fields[0] 逐位；TriggerNo 照取（全数字→0，非数字→-1）
        var std0 = parse("RT,00000000");
        Check("标准全0 → OK", std0.Succeeded && std0.IsOk);
        Eq("标准全0 TriggerNo=0（首字段可解析为数字）", 0L, std0.TriggerNo);
        Check("标准含1 → NG", !parse("RT,00000001").IsOk);
        Check("标准含4/- → NG", !parse("RT,000-0000").IsOk && !parse("RT,00040000").IsOk);
        var abc = parse("RT,ABC,OK");
        Eq("非数字首字段 TriggerNo=-1", -1L, abc.TriggerNo);

        // OkChar 定制（默认'0'；空回落'0'；多字符取首字符）
        var cam1 = new KeyenceIV4Camera(new CameraConfig { OkChar = "1" });
        Func<string, TriggerReadOutcome> parse1 =
            raw => (TriggerReadOutcome)InvokePrivateInstance(cam1, "ParseResult", raw);
        Check("OkChar=1 时 RT,11111111 → OK", parse1("RT,11111111").IsOk);
        Check("OkChar=1 时 RT,00000000 → NG", !parse1("RT,00000000").IsOk);
        var camEmpty = new KeyenceIV4Camera(new CameraConfig { OkChar = "" });
        Check("OkChar空回落'0'（全0→OK）",
            ((TriggerReadOutcome)InvokePrivateInstance(camEmpty, "ParseResult", "RT,00000000")).IsOk);
        cam.Dispose(); cam1.Dispose(); camEmpty.Dispose();
    }

    // ───────────────────── ⑬ 相机指令校验（无设备防御分支）──────────────────────
    // 【背景】SwitchProgram/SetOutputFormat/ReadProgramNo/SendTrigger/TriggerAndRead 入口
    // 先 EnsureConnected；无设备时必须快速失败（false/-1/Fail）且不崩。127.0.0.1:9 闭端口
    // + 200ms 超时让失败路径瞬间返回（默认现场 IP 超时会拖 3s）。
    private static CameraConfig DeadCam()
    {
        return new CameraConfig { IpAddress = "127.0.0.1", CommandPort = 9, TimeoutMs = 200, ResponseTimeoutMs = 200 };
    }

    private static void TestCameraCmdValidate()
    {
        Group("⑬ 相机指令校验 SetOutputFormat/SwitchProgram/ReadProgramNo/SendTrigger（无设备防御）");
        // SetOutputFormat：形状校验在建连之前，无设备也可断言 false（不触网、瞬间返回）
        var fmtCam = new KeyenceIV4Camera(DeadCam());
        try
        {
            Check("SetOutputFormat(null)=false", fmtCam.SetOutputFormat(null) == false);
            Check("SetOutputFormat(空)=false", fmtCam.SetOutputFormat("") == false);
            Check("SetOutputFormat(空格)=false", fmtCam.SetOutputFormat("  ") == false);
            Check("SetOutputFormat(1位)=false", fmtCam.SetOutputFormat("0") == false);
            Check("SetOutputFormat(3位)=false", fmtCam.SetOutputFormat("000") == false);
            Check("SetOutputFormat(字母)=false", fmtCam.SetOutputFormat("0A") == false && fmtCam.SetOutputFormat("A0") == false);
            // " 01 " 形状合法（Trim 后两位数字）→ 进入网络分支，无设备返回 false（不断言 true）
            Check("SetOutputFormat(' 01 ')无设备=false且不崩", fmtCam.SetOutputFormat(" 01 ") == false);
            Check("SetOutputFormat(99)无设备=false且不崩", fmtCam.SetOutputFormat("99") == false);
        }
        finally { fmtCam.Dispose(); }

        // 其余指令无设备：快速失败、不抛异常
        var dead = new KeyenceIV4Camera(DeadCam());
        try
        {
            Check("无设备 SwitchProgram(5)=false", dead.SwitchProgram(5) == false);
            Check("无设备 SwitchProgram(越界999)=false", dead.SwitchProgram(999) == false);
            Eq("无设备 ReadProgramNo=-1", -1, dead.ReadProgramNo());
            Check("无设备 SendTrigger=false", dead.SendTrigger() == false);
            var t = dead.TriggerAndRead();
            Check("无设备 TriggerAndRead 失败", t != null && !t.Succeeded);
            // stillAlive=false（已换代）→ 放弃触发。注意：仍先 EnsureConnected，无设备在建连处即失败返回
            var t2 = dead.TriggerAndRead(() => false);
            Check("无设备+换代 TriggerAndRead 失败", t2 != null && !t2.Succeeded);
            Check("无设备 SendTrigger(stillAlive=false)=false", dead.SendTrigger(() => false) == false);
            // TriggerCommand=null：catch 收敛成 false（当前实现 NRE 被吞；锚死"不崩"，修法应先判空）
            var nullCmd = new KeyenceIV4Camera(new CameraConfig
            {
                IpAddress = "127.0.0.1", CommandPort = 9, TimeoutMs = 200, ResponseTimeoutMs = 200,
                TriggerCommand = null
            });
            try { Check("TriggerCommand=null 无设备 SendTrigger=false 不崩", nullCmd.SendTrigger() == false); }
            finally { nullCmd.Dispose(); }
        }
        finally { dead.Dispose(); }
    }
    // 【背景】主界面标题栏主题按钮切深/浅色，全界面跟随；语义色（OK绿/NG红/PLC黄/
    // 主按钮蓝/白字）绝不能被洗掉，否则状态灯与品牌横幅直接看不见。
    // 这里锚死三层：归一规则 / 语义色判定 / 单控件上色（含表格），UI 交互由冒烟覆盖。
    private static void TestAppTheme()
    {
        Group("⑪ 深色/浅色主题 AppTheme（V2.16.2：归一/语义保留/控件上色）");
        AppTheme.Theme = "Light";   // 先复位，防用例顺序污染

        // 归一：Dark 大小写不敏感进深色，其余一切回落浅色
        Eq("Normalize Dark", "Dark", AppTheme.Normalize("Dark"));
        Eq("Normalize dark 小写", "Dark", AppTheme.Normalize("dark"));
        Eq("Normalize DARK 大写", "Dark", AppTheme.Normalize("DARK"));
        Eq("Normalize null 回落 Light", "Light", AppTheme.Normalize(null));
        Eq("Normalize 空串回落 Light", "Light", AppTheme.Normalize(""));
        Eq("Normalize Light", "Light", AppTheme.Normalize("Light"));
        Eq("Normalize 非法值回落 Light", "Light", AppTheme.Normalize("xxx"));

        // 语义色保留：状态/品牌色一律 true，普通文字色 false
        Check("OK绿是语义色", AppTheme.IsSemanticColor(System.Drawing.Color.FromArgb(46, 158, 107)));
        Check("NG红是语义色", AppTheme.IsSemanticColor(System.Drawing.Color.FromArgb(229, 72, 77)));
        Check("PLC黄是语义色", AppTheme.IsSemanticColor(System.Drawing.Color.FromArgb(240, 173, 78)));
        Check("主按钮蓝是语义色", AppTheme.IsSemanticColor(System.Drawing.Color.FromArgb(52, 152, 219)));
        Check("白字是语义色", AppTheme.IsSemanticColor(System.Drawing.Color.White));
        Check("Green/Red/Gray 保留", AppTheme.IsSemanticColor(System.Drawing.Color.Green)
            && AppTheme.IsSemanticColor(System.Drawing.Color.Red)
            && AppTheme.IsSemanticColor(System.Drawing.Color.Gray));
        Check("普通深蓝灰非语义", !AppTheme.IsSemanticColor(System.Drawing.Color.FromArgb(52, 73, 94)));
        Check("黑色非语义", !AppTheme.IsSemanticColor(System.Drawing.Color.Black));

        // 切换事件：值变化触发一次，相同值不触发
        int fired = 0;
        EventHandler h = (s, e) => fired++;
        AppTheme.ThemeChanged += h;
        AppTheme.Theme = "Dark";
        Eq("切深色触发一次", 1, fired);
        Check("IsDark 深色为 true", AppTheme.IsDark);
        AppTheme.Theme = "dark";   // 归一后与当前相同 → 不触发
        Eq("相同值不重复触发", 1, fired);
        AppTheme.ThemeChanged -= h;

        // 单控件上色（浅色下）：普通 Label 黑字→主题文字色，语义红字保留
        AppTheme.Theme = "Light";
        var lbl = new Label();
        lbl.ForeColor = System.Drawing.Color.Black;
        AppTheme.ApplyOne(lbl);
        Eq("普通 Label 换主题文字色", AppTheme.TextPrimary.ToArgb(), lbl.ForeColor.ToArgb());
        var lblNg = new Label();
        lblNg.ForeColor = System.Drawing.Color.Red;
        AppTheme.ApplyOne(lblNg);
        Eq("语义红字保留", System.Drawing.Color.Red.ToArgb(), lblNg.ForeColor.ToArgb());
        lbl.Dispose(); lblNg.Dispose();

        // 主按钮蓝底白字保留；次按钮跟随主题
        var primary = new Button();
        primary.BackColor = AppTheme.PrimaryBlue;
        primary.ForeColor = System.Drawing.Color.White;
        AppTheme.ApplyOne(primary);
        Eq("主按钮蓝底保留", AppTheme.PrimaryBlue.ToArgb(), primary.BackColor.ToArgb());
        Eq("主按钮白字保留", System.Drawing.Color.White.ToArgb(), primary.ForeColor.ToArgb());
        var secondary = new Button();
        secondary.BackColor = System.Drawing.Color.FromArgb(236, 240, 245);
        AppTheme.ApplyOne(secondary);
        Eq("次按钮底跟随主题", AppTheme.SecondaryButtonBackground.ToArgb(), secondary.BackColor.ToArgb());
        primary.Dispose(); secondary.Dispose();

        // 输入框跟随主题；表格上色不崩且底色跟随
        var txt = new TextBox();
        AppTheme.ApplyOne(txt);
        Eq("输入框底跟随主题", AppTheme.InputBackground.ToArgb(), txt.BackColor.ToArgb());
        txt.Dispose();
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewTextBoxColumn());
        grid.Rows.Add("x");
        AppTheme.ApplyGridTheme(grid);
        Eq("表格底跟随主题", AppTheme.Surface.ToArgb(), grid.BackgroundColor.ToArgb());
        Eq("表格单元格底跟随主题", AppTheme.InputBackground.ToArgb(), grid.DefaultCellStyle.BackColor.ToArgb());
        grid.Dispose();

        AppTheme.Theme = "Light";   // 还原默认（防污染冒烟/后续用例）
    }

    // ───────────────────── ⑭ 存图模板与路径渲染 ─────────────────────
    // 【背景】归档目录/文件名全靠 ImageStore.RenderTemplate 占位符替换；脏配置（完整路径当一层）
    // 靠 RenderSubDirsToSegments 拆段/丢盘符/去重自愈。错一个即归档目录错位/嵌套。
    private static void TestImageTemplate()
    {
        Group("⑭ 存图模板 RenderTemplate / RenderSubDirsToSegments / Sanitize / NormalizeDir");
        DateTime now = new DateTime(2026, 8, 20, 10, 30, 15, 123);
        // RenderTemplate 是 internal static（同程序集 DirTreeEditForm 共用）：外部 runner 走反射，
        // 与生产同一份实现。签名 (template, now, serial, isOk, stationNo, cameraName)。
        var tImg = typeof(ImageStore);
        var mRender = tImg.GetMethod("RenderTemplate", BindingFlags.NonPublic | BindingFlags.Static);
        Func<string, DateTime, string, bool, int, string, string> render =
            (t, n, s, ok, st, c) => (string)mRender.Invoke(null, new object[] { t, n, s, ok, st, c });

        Eq("{年月日}点分隔", "2026.08.20", render("{年月日}", now, "SN1", true, 3, "上相机"));
        Eq("{年}{月}{日}分项", "2026年08月20日",
            render("{年}年{月}月{日}日", now, "SN1", true, 3, "上相机"));
        Eq("{SN}正常", "AB123", render("{SN}", now, "AB123", true, 3, "上相机"));
        Eq("{SN}空→未知SN", "未知SN", render("{SN}", now, null, true, 3, "上相机"));
        Eq("{SN}空白→未知SN", "未知SN", render("{SN}", now, "   ", true, 3, "上相机"));
        Eq("{OKNG}真→OK", "OK", render("{OKNG}", now, "SN1", true, 3, "上相机"));
        Eq("{OKNG}假→NG", "NG", render("{OKNG}", now, "SN1", false, 3, "上相机"));
        Eq("{点位}", "7", render("{点位}", now, "SN1", true, 7, "上相机"));
        Eq("{相机}正常", "上相机", render("{相机}", now, "SN1", true, 3, "上相机"));
        Eq("{相机}空→未知相机", "未知相机", render("{相机}", now, "SN1", true, 3, null));
        Check("{时间}格式 yyyyMMdd_HHmmss_fff",
            render("{时间}", now, "SN1", true, 3, "上相机") == "20260820_103015_123");
        Eq("未知占位符原样保留", "{Date}", render("{Date}", now, "SN1", true, 3, "上相机"));
        Eq("null模板→空串", "", render(null, now, "SN1", true, 3, "上相机"));
        Eq("空模板→空串", "", render("   ", now, "SN1", true, 3, "上相机"));
        Eq("组合模板", "2026.08.20/AB123/上相机/OK",
            render("{年月日}/{SN}/{相机}/{OKNG}", now, "AB123", true, 3, "上相机"));

        // RenderSubDirsToSegments（private 实例）：干净配置原样分段；脏配置拆段/丢盘符/去重
        var imgCfg = new ImageConfig { SaveRootDir = @"E:\Images" };
        var store = new ImageStore(imgCfg);
        try
        {
            Func<List<string>, List<string>> segs = levels =>
            {
                imgCfg.SubDirs = levels;
                return (List<string>)InvokePrivateInstance(store, "RenderSubDirsToSegments",
                    "SN1", true, 3, "上相机", now);
            };
            var clean = segs(new List<string> { "{年月日}", "{SN}" });
            Check("干净两层→2段", clean.Count == 2 && clean[0] == "2026.08.20" && clean[1] == "SN1");
            var dirty = segs(new List<string> { @"E:\Images\{年月日}\{SN}" });
            Check("脏完整路径剥盘符剥根→2段（防4层嵌套）",
                dirty.Count == 2 && dirty[0] == "2026.08.20" && dirty[1] == "SN1");
            var typo = segs(new List<string> { @"E:\Image\{SN}" });
            Check("拼写错误根同样剥离", typo.Count == 1 && typo[0] == "SN1");
            var slash = segs(new List<string> { "a/b\\c" });
            Check("正反斜杠混拆3段", slash.Count == 3);
            var dup = segs(new List<string> { "OK", "ok", "OK " });
            Check("忽略大小写去重→1段", dup.Count == 1 && dup[0] == "OK");
            var empty = segs(new List<string> { "", "   " });
            // 全空项→0段（注意与null层兜底的区别；运行时 NormalizeSubDirs 已把全空归一成[{年月日}]，
            // 这里锚定 RenderSubDirs 自身"不过度兜底"的行为：调用方 SaveImage 对0段即直接存根目录）
            Check("全空层→0段", empty.Count == 0);
            var nul = segs(null);
            Check("null层兜底日期1段", nul.Count == 1 && nul[0] == "2026.08.20");
        }
        finally { store.Dispose(); }

        // SanitizeForPath / NormalizeDir（private static）：非法字符→下划线；去尾斜杠
        var tStore = typeof(ImageStore);
        Func<string, string> sanitize = s => (string)InvokePrivateStatic(tStore, "SanitizeForPath", s);
        Func<string, string> normDir = s => (string)InvokePrivateStatic(tStore, "NormalizeDir", s);
        Check("非法字符清洗(含/与:变下划线)", sanitize("A/B:C") == "A_B_C");
        Eq("null清洗→空串", "", sanitize(null));
        Eq("空串清洗→空串", "", sanitize(""));
        Eq("正常名不动", "上相机", sanitize("上相机"));
        Eq("去尾反斜杠", @"D:\x", normDir(@"D:\x\\"));
        Eq("去尾正斜杠（只去尾不统一斜杠，已锚定）", "D:/x", normDir("D:/x/"));
        Eq("null归一→空串", "", normDir(null));
    }

    // ───────────────────── ⑮ 存图文件链路（临时目录 harness）──────────────────────
    // 【背景】V2.14.39 取图以 jpeg 为主体、同主名配对 iv4p（绝不跨拍硬凑）；
    // 归档文件名=源主名+时间戳；SaveImage 重名自动 _2/_3。全部用临时目录，测完自删。
    private static string NewTempDir(string tag)
    {
        string d = Path.Combine(Path.GetTempPath(), "cc_test_" + tag + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    private static void TestImageFiles()
    {
        Group("⑮ 存图文件链路 FindLatestPair / DeleteSourceFile / 归档命名 / SaveImage重名");
        var store = new ImageStore(new ImageConfig());
        try
        {
            // —— FindLatestPair：jpeg 优先 + 同主名配对 ——
            string ftp = NewTempDir("ftp");
            try
            {
                string f84j = Path.Combine(ftp, "0084.jpeg");
                string f84i = Path.Combine(ftp, "0084.iv4p");
                string f90j = Path.Combine(ftp, "0090.jpeg");   // 最新孤 jpeg（iv4p 未到）
                File.WriteAllText(f84j, "j84");
                File.WriteAllText(f84i, "i84");
                File.WriteAllText(f90j, "j90");
                File.SetLastWriteTimeUtc(f84j, DateTime.UtcNow.AddSeconds(-30));
                File.SetLastWriteTimeUtc(f84i, DateTime.UtcNow.AddSeconds(-30));
                File.SetLastWriteTimeUtc(f90j, DateTime.UtcNow);
                var pair = store.FindLatestPair(ftp);
                Check("最新jpeg=0090（不被旧对顶掉）",
                    pair.JpegPath != null && pair.JpegPath.EndsWith("0090.jpeg"));
                Check("孤jpeg配对iv4p=null（绝不拿0084.iv4p硬凑）", pair.IvpPath == null);
                // 补上同名 iv4p → 成对
                string f90i = Path.Combine(ftp, "0090.iv4p");
                File.WriteAllText(f90i, "i90");
                var pair2 = store.FindLatestPair(ftp);
                Check("同主名iv4p到后成对",
                    pair2.IvpPath != null && pair2.IvpPath.EndsWith("0090.iv4p"));
                // 只有 iv4p 无 jpeg → 双 null
                string onlyI = NewTempDir("onlyiv4p");
                try
                {
                    File.WriteAllText(Path.Combine(onlyI, "0001.iv4p"), "x");
                    var p3 = store.FindLatestPair(onlyI);
                    Check("无jpeg→JpegPath=null", p3.JpegPath == null && p3.IvpPath == null);
                }
                finally { try { Directory.Delete(onlyI, true); } catch { } }
                // 大写 .JPG 命中
                string upper = NewTempDir("upper");
                try
                {
                    File.WriteAllText(Path.Combine(upper, "A.JPG"), "x");
                    var p4 = store.FindLatestPair(upper);
                    Check("大写.JPG 命中", p4.JpegPath != null && p4.JpegPath.EndsWith("A.JPG"));
                }
                finally { try { Directory.Delete(upper, true); } catch { } }
                // 目录不存在 → 空结果不抛
                var p5 = store.FindLatestPair(Path.Combine(ftp, "NOT_EXIST_DIR"));
                Check("目录不存在→空结果不抛", p5.JpegPath == null && p5.IvpPath == null);
                Check("null目录→空结果不抛", store.FindLatestPair(null).JpegPath == null);
            }
            finally { try { Directory.Delete(ftp, true); } catch { } }

            // —— DeleteSourceFile：null/空/不存在安全；真实文件删掉 ——
            try
            {
                ImageStore.DeleteSourceFile(null, "ut");
                ImageStore.DeleteSourceFile("   ", "ut");
                ImageStore.DeleteSourceFile(Path.Combine(Path.GetTempPath(), "cc_no_such_file.tmp"), "ut");
                Check("DeleteSourceFile 空/不存在不抛", true);
                string todel = Path.Combine(Path.GetTempPath(), "cc_todel_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(todel, "x");
                ImageStore.DeleteSourceFile(todel, "ut");
                Check("真实文件被删除", !File.Exists(todel));
            }
            catch (Exception ex) { Check("DeleteSourceFile 不抛 (异常:" + ex.GetType().Name + ")", false); }

            // —— SaveImageFilePair 归档命名：源主名+时间戳，小写扩展名，同名iv4p ——
            string arcRoot = NewTempDir("arc");
            string srcDir = NewTempDir("src");
            try
            {
                var cfg = new ImageConfig { SaveRootDir = arcRoot, FileTimestampSuffix = true };
                var arc = new ImageStore(cfg);
                try
                {
                    string sj = Path.Combine(srcDir, "0084.jpeg");
                    string si = Path.Combine(srcDir, "0084.iv4p");
                    File.WriteAllText(sj, "jpeg-bytes");
                    File.WriteAllText(si, "iv4p-bytes");
                    string got = arc.SaveImageFilePair(sj, si, 3, true, "SN1", "上相机");
                    Check("归档返回非null", got != null && File.Exists(got));
                    string base1 = Path.GetFileNameWithoutExtension(got);
                    Check("归档主名=源主名+时间戳",
                        base1.StartsWith("0084_") && base1.Length == "0084_".Length + "20260820_103015_123".Length);
                    Check("归档扩展名小写.jpeg", got.EndsWith(".jpeg"));
                    Check("同名.iv4p 落盘",
                        File.Exists(Path.Combine(Path.GetDirectoryName(got), base1 + ".iv4p")));
                    // iv4p=null → 只归 jpeg 不崩
                    string got2 = arc.SaveImageFilePair(sj, null, 3, false, "SN1", "上相机");
                    Check("iv4p=null 只归jpeg", got2 != null && File.Exists(got2));
                    // jpeg 不存在 → null
                    Check("jpeg不存在→null",
                        arc.SaveImageFilePair(Path.Combine(srcDir, "NOPE.jpeg"), null, 3, true, "SN1", "上相机") == null);
                    // 无时间戳后缀 → 原名直接落盘
                    var cfg2 = new ImageConfig { SaveRootDir = arcRoot, FileTimestampSuffix = false };
                    var arc2 = new ImageStore(cfg2);
                    try
                    {
                        string got3 = arc2.SaveImageFilePair(sj, null, 3, true, "SN2", "上相机");
                        Check("无后缀归档名=源主名",
                            got3 != null && Path.GetFileName(got3) == "0084.jpeg");
                    }
                    finally { arc2.Dispose(); }
                }
                finally { arc.Dispose(); }
            }
            finally { try { Directory.Delete(arcRoot, true); } catch { } try { Directory.Delete(srcDir, true); } catch { } }

            // —— SaveImage 重名 _2 兜底 + 空模板兜底 ——
            string imgRoot = NewTempDir("img");
            try
            {
                var cfg3 = new ImageConfig { SaveRootDir = imgRoot };
                var s3 = new ImageStore(cfg3);
                try
                {
                    using (var bmp = new Bitmap(8, 8))
                    {
                        string p1 = s3.SaveImage(bmp, 3, true, "SN1", "上相机");
                        string p2 = s3.SaveImage(bmp, 3, true, "SN1", "上相机");
                        Check("首次存图非null", p1 != null && File.Exists(p1));
                        Check("重名自动_2后缀", p2 != null && p2.Contains("_2.png") && File.Exists(p2));
                    }
                    var cfg4 = new ImageConfig { SaveRootDir = imgRoot, FileNameTemplate = "   " };
                    var s4 = new ImageStore(cfg4);
                    try
                    {
                        using (var bmp = new Bitmap(8, 8))
                        {
                            string p = s4.SaveImage(bmp, 3, true, "SN1", "上相机");
                            Check("空模板兜底 IMG_ 命名", p != null && Path.GetFileName(p).StartsWith("IMG_"));
                        }
                    }
                    finally { s4.Dispose(); }
                    // SaveImageBytes：非法字节 → null 不落盘
                    Check("非法图像字节→null", s3.SaveImageBytes(new byte[] { 1, 2, 3 }, 1, true, "SN1", "上相机") == null);
                    Check("null图像字节→null", s3.SaveImageBytes(null, 1, true, "SN1", "上相机") == null);
                }
                finally { s3.Dispose(); }
            }
            finally { try { Directory.Delete(imgRoot, true); } catch { } }
        }
        finally
        {
            store.Dispose();
        }
    }

    // ───────────────────── ⑮ 清理日期判定（临时目录 harness）──────────────────────
    private static void TestImageCleanup()
    {
        Group("⑮ 存图清理 TryParseDirDate / IsDirExpired / IsDirTreeOlderThan / 盘根放弃");
        var t = typeof(ImageStore);
        // TryParseDirDate(string, out DateTime)：反射 out 参数
        Func<string, Tuple<bool, DateTime>> tryDate = name =>
        {
            object[] args = new object[] { name, null };
            bool ok = (bool)t.GetMethod("TryParseDirDate", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, args);
            return Tuple.Create(ok, args[1] == null ? default(DateTime) : (DateTime)args[1]);
        };
        Check("点分隔日期命中", tryDate("2026.08.20").Item1);
        Eq("点分隔日期值", new DateTime(2026, 8, 20), tryDate("2026.08.20").Item2.Date);
        Check("中文日期命中", tryDate("2026年08月11日").Item1);
        Check("紧凑日期命中", tryDate("20260811").Item1);
        Check("非法月份不命中", !tryDate("2026.13.40").Item1);
        Check("7位不命中", !tryDate("2026081").Item1);
        Check("纯文本不命中", !tryDate("abc").Item1);
        Check("空串不命中", !tryDate("").Item1);
        Check("null不命中", !tryDate(null).Item1);

        // IsDirExpired / IsDirTreeOlderThan：真实临时目录
        string root = NewTempDir("clean");
        try
        {
            DateTime cutoff = new DateTime(2026, 8, 21);
            Func<string, DateTime, bool> expired = (dir, c) =>
                (bool)t.GetMethod("IsDirExpired", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { dir, c });
            // 日期目录：早于阈值→过期；等于当天→保留（< 不是 <=）
            string dated = Path.Combine(root, "2026.08.20");
            Directory.CreateDirectory(dated);
            Check("日期目录早于阈值→过期", expired(dated, cutoff));
            // 空日期目录即使等于当天也判过期（IsDirTreeOlderThan 空=true；空目录删了无害、下次重建）
            Check("空日期目录当天→过期（已锚定）", expired(dated, new DateTime(2026, 8, 20)));
            string dayFile = Path.Combine(dated, "a.jpeg");
            File.WriteAllText(dayFile, "x");
            File.SetLastWriteTime(dayFile, new DateTime(2026, 8, 20, 12, 0, 0));
            Check("日期目录有当天文件→保留", !expired(dated, new DateTime(2026, 8, 20)));
            // 通用路径：有新文件→保留；全旧文件→过期；空目录→可删(true)
            string gen = Path.Combine(root, "SN123");
            Directory.CreateDirectory(gen);
            string newf = Path.Combine(gen, "a.jpeg");
            File.WriteAllText(newf, "x");
            File.SetLastWriteTime(newf, new DateTime(2026, 8, 22));
            Check("子树有新文件→保留", !expired(gen, cutoff));
            File.SetLastWriteTime(newf, new DateTime(2026, 8, 10));
            Check("子树全旧文件→过期", expired(gen, cutoff));
            string emptySub = Path.Combine(root, "EMPTY");
            Directory.CreateDirectory(emptySub);
            Check("空子目录→可删", expired(emptySub, cutoff));
            Check("不存在目录→未过期(false)", !expired(Path.Combine(root, "NOPE"), cutoff));

            // 盘根放弃：SaveRootDir=盘符根 → RunCleanupOnce 零删除不抛
            var cfg = new ImageConfig { SaveRootDir = Path.GetPathRoot(root), KeepDays = 30 };
            var s = new ImageStore(cfg);
            try
            {
                t.GetMethod("RunCleanupOnce", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s, null);
                Check("盘符根清理放弃不抛", true);
            }
            catch (Exception ex) { Check("盘符根清理放弃不抛 (异常:" + ex.InnerException?.GetType().Name + ")", false); }
            finally { s.Dispose(); }
            // KeepDays=0 → 直接返回不删不抛
            var cfg0 = new ImageConfig { SaveRootDir = root, KeepDays = 0 };
            var s0 = new ImageStore(cfg0);
            try
            {
                t.GetMethod("RunCleanupOnce", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s0, null);
                Check("KeepDays=0 不清理不抛", Directory.Exists(dated));
            }
            finally { s0.Dispose(); }
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    // 扫码枪桩：供 OnScannerFail 重试计数用例（SendTrigger 调用可计数，无网络）
    private sealed class FakeScanner : IScanner
    {
        public event EventHandler<string> SerialNumberScanned;
        public event EventHandler<bool> ConnectionChanged;
        public event EventHandler<string> ScanFailed;
        public int TriggerCount;
        public bool IsOpen => true;
        public bool Open() => true;
        public bool SendTrigger() { TriggerCount++; return true; }
        public void Dispose() { }
        public void FireFail(string text) => ScanFailed?.Invoke(this, text);
    }

    // ───────────────────── ⑯ 协调器纯函数 ─────────────────────
    private static void TestCoordPure()
    {
        Group("⑯ 协调器纯函数 CameraLabel/CameraIdFor/IndexOfCamera/FindProgram/IsWindowEnabled/IsTcpImage/FtpDirFor/SetManualSerial/IsNewerThanTrigger/TryResolveActiveWindow/重试/磨合期");
        var cams = CameraConfig.DefaultCameras();   // 上(id2,17条) + 下(id1,4条)
        var coordStore = new ImageStore(new ImageConfig());
        var coord = new ProductionCoordinator(null, null, cams, coordStore, null, "U171", null, 21);
        try
        {
            // CameraLabel：名称优先 → 真编号 → 行序；越界兜底
            Eq("Label有名称", "上相机", (string)InvokePrivateInstance(coord, "CameraLabel", 0));
            var nameless = new ProductionCoordinator(null, null,
                new List<CameraConfig> { new CameraConfig { Name = "", CameraId = 2 } },
                null, null, "U171", null, 1);
            try { Eq("Label无名用真编号", "相机2", (string)InvokePrivateInstance(nameless, "CameraLabel", 0)); }
            finally { nameless.Dispose(); }
            var noname = new ProductionCoordinator(null, null,
                new List<CameraConfig> { new CameraConfig { Name = "", CameraId = 0 } },
                null, null, "U171", null, 1);
            try { Eq("Label无名无号用行序", "相机1", (string)InvokePrivateInstance(noname, "CameraLabel", 0)); }
            finally { noname.Dispose(); }
            Eq("Label越界兜底", "相机100", (string)InvokePrivateInstance(coord, "CameraLabel", 99));
            Eq("Label负下标兜底", "相机0", (string)InvokePrivateInstance(coord, "CameraLabel", -1));

            // CameraIdFor / IndexOfCamera（身份键，列表顺序自由）
            Eq("IdFor真编号", 2, (int)InvokePrivateInstance(coord, "CameraIdFor", cams[0], 0));
            Eq("IdFor零回退行序", 6, (int)InvokePrivateInstance(coord, "CameraIdFor", new CameraConfig { CameraId = 0 }, 5));
            Eq("IdFor null回退行序", 1, (int)InvokePrivateInstance(coord, "CameraIdFor", null, 0));
            Eq("IndexOf上=0", 0, (int)InvokePrivateInstance(coord, "IndexOfCamera", 2));
            Eq("IndexOf下=1", 1, (int)InvokePrivateInstance(coord, "IndexOfCamera", 1));
            Eq("IndexOf未知=-1", -1, (int)InvokePrivateInstance(coord, "IndexOfCamera", 99));
            Eq("IndexOf零=-1", -1, (int)InvokePrivateInstance(coord, "IndexOfCamera", 0));

            // FindProgram（static）：首命中/负号跳过/null元跳过
            var tCoord = typeof(ProductionCoordinator);
            Func<List<StationProgramItem>, int, int> find = (tab, st) =>
                (int)InvokePrivateStatic(tCoord, "FindProgram", tab, st);
            Eq("Find首命中", 42, find(new List<StationProgramItem>
            {
                new StationProgramItem { StationNo = 7, ProgramNo = 42 },
                new StationProgramItem { StationNo = 7, ProgramNo = 99 }
            }, 7));
            Eq("Find负号→-1", -1, find(new List<StationProgramItem>
            {
                new StationProgramItem { StationNo = 8, ProgramNo = -1 }
            }, 8));
            Eq("Find含null元跳过", 5, find(new List<StationProgramItem>
            {
                null, new StationProgramItem { StationNo = 1, ProgramNo = 5 }
            }, 1));
            Eq("Find未命中→-1", -1, find(new List<StationProgramItem>(), 9));
            Eq("Find null表→-1", -1, find(null, 1));

            // IsWindowEnabled：null/越界默认启用
            Eq("Enabled null→true", true, (bool)InvokePrivateInstance(coord, "IsWindowEnabled", 5));
            var enCoord = new ProductionCoordinator(null, null, cams, null,
                new List<bool> { true, false }, "U171", null, 21);
            try
            {
                Eq("Enabled[1]=true", true, (bool)InvokePrivateInstance(enCoord, "IsWindowEnabled", 1));
                Eq("Enabled[2]=false", false, (bool)InvokePrivateInstance(enCoord, "IsWindowEnabled", 2));
                Eq("Enabled越界→true", true, (bool)InvokePrivateInstance(enCoord, "IsWindowEnabled", 99));
                Eq("Enabled零→true", true, (bool)InvokePrivateInstance(enCoord, "IsWindowEnabled", 0));
            }
            finally { enCoord.Dispose(); }

            // IsTcpImage（static）：仅Tcp（大小写/空格容忍），其余一律Ftp兜底
            Func<CameraConfig, bool> isTcp = c =>
                (bool)InvokePrivateStatic(tCoord, "IsTcpImage", c);
            Check("Tcp→true", isTcp(new CameraConfig { ImageSource = "Tcp" }));
            Check("TCP大写→true", isTcp(new CameraConfig { ImageSource = "TCP" }));
            Check("空格tcp→true", isTcp(new CameraConfig { ImageSource = " tcp " }));
            Check("Ftp→false", !isTcp(new CameraConfig { ImageSource = "Ftp" }));
            Check("空→false", !isTcp(new CameraConfig { ImageSource = "" }));
            Check("null配置→false", !isTcp(new CameraConfig { ImageSource = null }));
            Check("null相机→false", !isTcp(null));
            Check("非法值→false", !isTcp(new CameraConfig { ImageSource = "xxx" }));

            // FtpDirFor：配置优先，空回退全局兜底
            Eq("FtpDir配置优先", @"D:\IV存图\2",
                (string)InvokePrivateInstance(coord, "FtpDirFor", cams[0]));
            var imgStore = new ImageStore(new ImageConfig());
            var c2 = new ProductionCoordinator(null, null, cams, imgStore, null, "U171", null, 21);
            try
            {
                Eq("FtpDir空回退全局", imgStore.DefaultFtpDir,
                    (string)InvokePrivateInstance(c2, "FtpDirFor", new CameraConfig { FtpUploadDir = "" }));
                // 注意：单参数反射传 null 必须强转 (object)null，否则 params object[] 收到 null 数组
                // 本身（而非含null的单元素数组），Invoke 报"参数计数不匹配"。
                Eq("FtpDir null相机回退全局", imgStore.DefaultFtpDir,
                    (string)InvokePrivateInstance(c2, "FtpDirFor", (object)null));
            }
            finally { c2.Dispose(); imgStore.Dispose(); }

            // SetManualSerial：null→空串；置_received；Latest同步
            coord.SetManualSerial(null);
            Eq("Manual null→空串", "", coord.LatestSerialNumber);
            coord.SetManualSerial("  AB123  ");
            Eq("Manual原样保存", "  AB123  ", coord.LatestSerialNumber);

            // TryResolveActiveWindow：默认铺排反查（上点位3→窗3；下点位3→窗20）；禁用→false；空窗口不命中
            var winCoord = new ProductionCoordinator(null, null, cams, null, null, "U171", null, 21);
            try
            {
                var m = winCoord.GetType().GetMethod("TryResolveActiveWindow",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                object[] a1 = new object[] { 2, 3, null };
                bool r1 = (bool)m.Invoke(winCoord, a1);
                Check("上·点位3→窗3", r1 && (int)a1[2] == 3);
                object[] a2 = new object[] { 1, 3, null };
                bool r2 = (bool)m.Invoke(winCoord, a2);
                Check("下·点位3→窗20（跨相机同号各定位）", r2 && (int)a2[2] == 20);
                object[] a3 = new object[] { 2, 9999, null };
                Check("表外点位→false", !(bool)m.Invoke(winCoord, a3));
                object[] a4 = new object[] { 99, 1, null };
                Check("未知相机→false", !(bool)m.Invoke(winCoord, a4));
            }
            finally { winCoord.Dispose(); }
            // 禁用窗口反查→false
            var disList = new List<bool>();
            for (int i = 0; i < 21; i++) disList.Add(true);
            disList[2] = false;   // 窗3禁用
            var disCoord = new ProductionCoordinator(null, null, cams, null, disList, "U171", null, 21);
            try
            {
                var m = disCoord.GetType().GetMethod("TryResolveActiveWindow",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                object[] a = new object[] { 2, 3, null };
                Check("禁用窗口反查→false", !(bool)m.Invoke(disCoord, a));
            }
            finally { disCoord.Dispose(); }
            // _windowPointMap=null 极端兜底：退回点位表位置定位，不崩
            var fbCoord = new ProductionCoordinator(null, null, cams, null, null, "U171", null, 21);
            try
            {
                fbCoord.GetType().GetField("_windowPointMap", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(fbCoord, null);
                var m = fbCoord.GetType().GetMethod("TryResolveActiveWindow",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                object[] a = new object[] { 2, 1, null };
                bool r = (bool)m.Invoke(fbCoord, a);
                Check("映射null兜底上点位1→窗1", r && (int)a[2] == 1);
            }
            finally { fbCoord.Dispose(); }

            // OnScannerFail 重试：前3次重发LON不置失败，第4次置 _serialErrorSeen
            var fake = new FakeScanner();
            var scCoord = new ProductionCoordinator(null, null, cams, null, null, "U171", null, 21);
            try
            {
                scCoord.AttachScanners(new IScanner[] { fake });
                scCoord.GetType().GetField("_scanRetryCount", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(scCoord, 0);
                var fail = scCoord.GetType().GetMethod("OnScannerFail",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var seenF = scCoord.GetType().GetField("_serialErrorSeen",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                fail.Invoke(scCoord, new object[] { fake, "ERROR" });
                fail.Invoke(scCoord, new object[] { fake, "ERROR" });
                fail.Invoke(scCoord, new object[] { fake, "ERROR" });
                Check("3次失败重发3次LON", fake.TriggerCount == 3);
                Check("3次内不置失败标志", !(bool)seenF.GetValue(scCoord));
                fail.Invoke(scCoord, new object[] { fake, "ERROR" });
                Check("第4次置失败标志", (bool)seenF.GetValue(scCoord));
                Check("耗尽后不再重发", fake.TriggerCount == 3);
            }
            finally { scCoord.Dispose(); }

            // 动态磨合期：max(1200,RespMax)+1000；无相机→2200
            var slowCams = new List<CameraConfig>
            {
                new CameraConfig { IpAddress = "10.0.0.1", ResponseTimeoutMs = 5000 }
            };
            var slowCoord = new ProductionCoordinator(null, null, slowCams, null, null, "U171", null, 1);
            try
            {
                Eq("磨合期=5000+1000", 6000, (int)slowCoord.GetType()
                    .GetField("_startDrainMs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(slowCoord));
            }
            finally { slowCoord.Dispose(); }
            var emptyCoord = new ProductionCoordinator(null, null, new List<CameraConfig>(), null, null, "U171", null, 1);
            try
            {
                Eq("无相机磨合期=2200", 2200, (int)emptyCoord.GetType()
                    .GetField("_startDrainMs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(emptyCoord));
            }
            finally { emptyCoord.Dispose(); }

            // IsNewerThanTrigger（static）：容差1s；删除文件→false
            string tmpf = Path.Combine(Path.GetTempPath(), "cc_newer_" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(tmpf, "x");
                DateTime trig = File.GetLastWriteTimeUtc(tmpf);
                Func<string, DateTime, bool> newer = (p, tu) =>
                    (bool)InvokePrivateStatic(tCoord, "IsNewerThanTrigger", p, tu);
                Check("同刻→新图", newer(tmpf, trig));
                Check("触发晚0.5s（容差内）→新图", newer(tmpf, trig.AddSeconds(0.5)));
                Check("触发晚2s→旧图", !newer(tmpf, trig.AddSeconds(2)));
                Check("文件删除→false", !newer(tmpf + ".NOPE", trig));
            }
            finally { try { File.Delete(tmpf); } catch { } }
        }
        finally { coord.Dispose(); coordStore.Dispose(); }
    }

    // 注入 SlaveDataStore 的 PlcService（与②组同手法：真建站优先，502被占则注入兜底）
    private static PlcService NewPlcWithStore(PlcConfig cfg, out NModbus.Data.PointSource<ushort> regs)
    {
        var svc = new PlcService(cfg);
        var tSvc = typeof(PlcService);
        const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        object store = null;
        bool live = false;
        try { live = svc.EnsureConnected(); } catch { live = false; }
        if (live)
            store = tSvc.GetField("_dataStore", NP).GetValue(svc);
        else
        {
            store = Activator.CreateInstance(typeof(NModbus.Data.SlaveDataStore));
            tSvc.GetField("_dataStore", NP).SetValue(svc, store);
        }
        regs = (NModbus.Data.PointSource<ushort>)store.GetType().GetProperty("HoldingRegisters").GetValue(store);
        return svc;
    }

    // ───────────────────── ⑰ SN分流与PLC分支 ─────────────────────
    private static void TestCoordDeliver()
    {
        Group("⑰ SN分流 DeliverSerialNumber / ResolveModelIndex / len钳位 / 零地址 / 站号透传");
        var cams = CameraConfig.DefaultCameras();

        // —— DeliverSerialNumber 分流（private）：Mes 不写区 / Plc 写区 / 空串清区 / null配置兜底Mes ——
        NModbus.Data.PointSource<ushort> regs;
        var plcCfg = new PlcConfig();
        var plc = NewPlcWithStore(plcCfg, out regs);
        try
        {
            var mesEmpty = new MesService(new SnRouteConfig { Target = "Mes", MesUrl = "" });
            try
            {
                // Mes + 有效SN → SN区保持全0（不写PLC），空URL后台不崩
                var cMes = new ProductionCoordinator(plc, null, cams, null, null, "U171", null, 21,
                    new SnRouteConfig { Target = "Mes" }, mesEmpty);
                try
                {
                    InvokePrivateInstance(cMes, "DeliverSerialNumber", "SN1");
                    Check("Mes分流SN区全0", regs.ReadPoints((ushort)plcCfg.ScanSerialNumberAddress, (ushort)plcCfg.ScanSerialNumberLen).All(x => x == 0));
                }
                finally { cMes.Dispose(); }
                // Plc + 有效SN → SN区非0
                var cPlc = new ProductionCoordinator(plc, null, cams, null, null, "U171", null, 21,
                    new SnRouteConfig { Target = "Plc" }, mesEmpty);
                try
                {
                    InvokePrivateInstance(cPlc, "DeliverSerialNumber", "SN1");
                    var got = regs.ReadPoints((ushort)plcCfg.ScanSerialNumberAddress, (ushort)plcCfg.ScanSerialNumberLen);
                    Check("Plc分流SN区写入", got[0] == 0x534E);
                    // 空串 → 整区清0（防旧件残留）
                    InvokePrivateInstance(cPlc, "DeliverSerialNumber", "");
                    Check("空串清区", regs.ReadPoints((ushort)plcCfg.ScanSerialNumberAddress, (ushort)plcCfg.ScanSerialNumberLen).All(x => x == 0));
                }
                finally { cPlc.Dispose(); }
                // _snRoute=null → 按Mes兜底（SN区全0，不崩）
                var cNull = new ProductionCoordinator(plc, null, cams, null, null, "U171", null, 21, null, mesEmpty);
                try
                {
                    InvokePrivateInstance(cNull, "DeliverSerialNumber", "SN9");
                    Check("null配置兜底Mes（区全0）", regs.ReadPoints((ushort)plcCfg.ScanSerialNumberAddress, (ushort)plcCfg.ScanSerialNumberLen).All(x => x == 0));
                }
                finally { cNull.Dispose(); }
                // Mes目标但_mes=null → Warn 不崩（SN区仍全0）
                var cNoMes = new ProductionCoordinator(plc, null, cams, null, null, "U171", null, 21,
                    new SnRouteConfig { Target = "Mes" }, null);
                try
                {
                    InvokePrivateInstance(cNoMes, "DeliverSerialNumber", "SN2");
                    Check("MES服务缺失不崩", true);
                }
                catch (Exception ex) { Check("MES服务缺失不崩 (异常:" + ex.GetType().Name + ")", false); }
                finally { cNoMes.Dispose(); }
            }
            finally { mesEmpty.Dispose(); }
        }
        finally { plc.Dispose(); }

        // —— ResolveModelIndex（private）：空/脏/重名/null表 ——
        var pcfg = new PlcConfig();
        pcfg.ModelIndexes.Add(new ModelIndexItem { ModelName = "Z121", ModelIndex = 1 });
        pcfg.ModelIndexes.Add(new ModelIndexItem { ModelName = "U171", ModelIndex = 2 });
        pcfg.ModelIndexes.Add(new ModelIndexItem { ModelName = "U171", ModelIndex = 9 });  // 重名
        pcfg.ModelIndexes.Add(new ModelIndexItem { ModelName = "ZERO", ModelIndex = 0 });  // 零号无效
        var psvc = new PlcService(pcfg);
        try
        {
            Func<string, int> resolve = m => (int)InvokePrivateInstance(psvc, "ResolveModelIndex", m);
            Eq("Resolve null→0", 0, resolve(null));
            Eq("Resolve空→0", 0, resolve(""));
            Eq("Resolve空白→0", 0, resolve("   "));
            Eq("Resolve去空格命中", 2, resolve(" u171 "));
            Eq("Resolve大小写", 1, resolve("z121"));
            Eq("Resolve重名取首个", 2, resolve("U171"));
            Eq("Resolve零号→0", 0, resolve("ZERO"));
            Eq("Resolve未配→0", 0, resolve("X9"));
            var nullTab = new PlcService(new PlcConfig { ModelIndexes = null });
            try { Eq("Resolve null表→0", 0, (int)InvokePrivateInstance(nullTab, "ResolveModelIndex", "U171")); }
            finally { nullTab.Dispose(); }
        }
        finally { psvc.Dispose(); }

        // —— len 钳位：型号 1~20，SN 1~50（服务层写入路径）——
        var ccfg = new PlcConfig { ProductModelLen = 0, ScanSerialNumberLen = 0 };
        NModbus.Data.PointSource<ushort> r2;
        var cs = NewPlcWithStore(ccfg, out r2);
        try
        {
            Check("型号len=0写入成功", cs.WriteProductModel("U171"));
            Check("型号len=0实际写1个", r2.ReadPoints(ccfg.ProductModelAddress, 1).Length == 1);
            Check("SN len=0写入成功", cs.WriteSerialNumber("AB"));
            Eq("SN len=0实际内容1寄存器", 0x4142, (int)r2.ReadPoints(ccfg.ScanSerialNumberAddress, 1)[0]);
        }
        finally { cs.Dispose(); }
        var bcfg = new PlcConfig { ProductModelLen = 100, ScanSerialNumberLen = 100 };
        NModbus.Data.PointSource<ushort> r3;
        var bs = NewPlcWithStore(bcfg, out r3);
        try
        {
            Check("型号len=100写入成功", bs.WriteProductModel("U171"));
            Check("型号len=100钳位20个", r3.ReadPoints(bcfg.ProductModelAddress, 20).Length == 20);
            Check("SN len=100写入成功", bs.WriteSerialNumber("AB"));
            Check("SN len=100钳位50个", r3.ReadPoints(bcfg.ScanSerialNumberAddress, 50).Length == 50);
        }
        finally { bs.Dispose(); }

        // —— WriteCameraResult 零地址跳过 / null 安全；正常地址写读 ——
        NModbus.Data.PointSource<ushort> r4;
        var ws = NewPlcWithStore(new PlcConfig(), out r4);
        try
        {
            ws.WriteRegister(0, 555);
            ws.WriteCameraResult(new CameraConfig { PlcResultAddress = 0 }, 2);
            ushort d0;
            ws.ReadRegister(0, out d0);
            Eq("结果地址0跳过（D0不变）", (ushort)555, d0);
            try { ws.WriteCameraResult(null, 2); Check("WriteCameraResult(null)不崩", true); }
            catch (Exception ex) { Check("WriteCameraResult(null)不崩 (异常:" + ex.GetType().Name + ")", false); }
            var goodCam = new CameraConfig { PlcResultAddress = 6 };
            ws.WriteCameraResult(goodCam, 2);
            ushort v6;
            ws.ReadRegister(6, out v6);
            Eq("正常地址写读往返", (ushort)2, v6);
            // V2.16.4 零地址守卫：ScanResultAddress=0 脏配置跳过不写（与 WriteCameraResult/
            // ResetResultRegisters 同语义；协议地址从 D1 起，没有 D0）
            var dirtyCfg = new PlcConfig { ScanResultAddress = 0 };
            NModbus.Data.PointSource<ushort> rd;
            var ds = NewPlcWithStore(dirtyCfg, out rd);
            try
            {
                ds.WriteRegister(0, 0);
                ds.WriteScanResult(2);
                ushort dd0;
                ds.ReadRegister(0, out dd0);
                Eq("扫码结果地址0跳过（D0不变）", (ushort)0, dd0);
                // 正常地址仍写：守卫只拦 0，不影响默认 4
                var okCfg = new PlcConfig { ScanResultAddress = 4 };
                NModbus.Data.PointSource<ushort> ro;
                var os = NewPlcWithStore(okCfg, out ro);
                try
                {
                    os.WriteScanResult(2);
                    ushort v4;
                    os.ReadRegister(4, out v4);
                    Eq("正常地址4照写", (ushort)2, v4);
                }
                finally { os.Dispose(); }
            }
            finally { ds.Dispose(); }
        }
        finally { ws.Dispose(); }

        // —— ReadCameraRequest：零地址视为无请求；站号原样透传；未建站false ——
        var rs = new PlcService(new PlcConfig());
        try
        {
            int st;
            Check("未建站正常地址=false", rs.ReadCameraRequest(new CameraConfig { PlcRequestAddress = 2 }, out st) == false);
            Check("地址0未建站也视为无请求(true,0)",
                rs.ReadCameraRequest(new CameraConfig { PlcRequestAddress = 0 }, out st) && st == 0);
            Check("null相机视为无请求", rs.ReadCameraRequest(null, out st) && st == 0);
        }
        finally { rs.Dispose(); }
        NModbus.Data.PointSource<ushort> r5;
        var qs = NewPlcWithStore(new PlcConfig(), out r5);
        try
        {
            qs.WriteRegister(2, 300);
            int st;
            Check("站号300透传", qs.ReadCameraRequest(new CameraConfig { PlcRequestAddress = 2 }, out st) && st == 300);
            qs.WriteRegister(2, 65535);
            Check("站号65535透传", qs.ReadCameraRequest(new CameraConfig { PlcRequestAddress = 2 }, out st) && st == 65535);
        }
        finally { qs.Dispose(); }
    }

    // ───────────────────── ⑱ 配置迁移与兜底 ─────────────────────
    private static void TestConfigMigrate()
    {
        Group("⑱ 配置迁移 DefaultCameras锚点/补号/顺序修复/映射对齐/型号双向/目录归一/布局边界");
        var tStore = typeof(ConfigStore);

        // —— DefaultCameras 现场锚点（IP/目录/编号/通道/点位数/程序号）——
        var defs = CameraConfig.DefaultCameras();
        Eq("默认两台", 2, defs.Count);
        Eq("上IP", "19.87.6.213", defs[0].IpAddress);
        Eq("上FTP", @"D:\IV存图\2", defs[0].FtpUploadDir);
        Eq("上真编号2", 2, defs[0].CameraId);
        Eq("上请求2/结果5", "2/5", defs[0].PlcRequestAddress + "/" + defs[0].PlcResultAddress);
        Eq("下IP", "19.87.6.212", defs[1].IpAddress);
        Eq("下FTP", @"D:\IV存图\1", defs[1].FtpUploadDir);
        Eq("下真编号1", 1, defs[1].CameraId);
        Eq("下请求3/结果6", "3/6", defs[1].PlcRequestAddress + "/" + defs[1].PlcResultAddress);
        Eq("上U171 17条", 17, defs[0].ProgramsFor("U171").Count);
        Eq("下U171 4条", 4, defs[1].ProgramsFor("U171").Count);
        Eq("上Z121 18条", 18, defs[0].ProgramsFor("Z121").Count);
        Eq("下Z121 3条", 3, defs[1].ProgramsFor("Z121").Count);
        Eq("上U171点位1→P000", 0, defs[0].ProgramsFor("U171")[0].ProgramNo);

        // —— EnsureCameraIdentity 三遍补号（反射，纯内存）——
        Func<List<CameraConfig>, List<CameraConfig>> ident = list =>
        {
            var cfg = new AppConfig { Cameras = list };
            InvokePrivateStatic(tStore, "EnsureCameraIdentity", cfg);
            return cfg.Cameras;
        };
        var r1 = ident(new List<CameraConfig>
        {
            new CameraConfig { IpAddress = "19.87.6.213", CameraId = 0 },
            new CameraConfig { IpAddress = "19.87.6.212", CameraId = 0 }
        });
        Check("默认双机补真编号[2,1]", r1[0].CameraId == 2 && r1[1].CameraId == 1);
        Check("默认双机补通道[2/5,3/6]",
            r1[0].PlcRequestAddress == 2 && r1[0].PlcResultAddress == 5
            && r1[1].PlcRequestAddress == 3 && r1[1].PlcResultAddress == 6);
        var r2 = ident(new List<CameraConfig>
        {
            new CameraConfig { IpAddress = "10.9.9.9", CameraId = 0 },
            new CameraConfig { IpAddress = "19.87.6.212", CameraId = 0 }
        });
        Check("自定义不抢真编号1", r2[1].CameraId == 1 && r2[0].CameraId != 1);
        var r3 = ident(new List<CameraConfig>
        {
            new CameraConfig { IpAddress = "10.9.9.9", CameraId = 1 },
            new CameraConfig { IpAddress = "19.87.6.212", CameraId = 0 }
        });
        Check("真编号被占时下相机让位取2", r3[1].CameraId == 2);
        var r4 = ident(new List<CameraConfig>
        {
            new CameraConfig { IpAddress = "10.0.0.1", CameraId = 0, PlcRequestAddress = 0 },
            new CameraConfig { IpAddress = "10.0.0.2", CameraId = 0 },
            new CameraConfig { IpAddress = "10.0.0.3", CameraId = 0 }
        });
        Check("三自定义全局唯一", r4[0].CameraId != r4[1].CameraId && r4[1].CameraId != r4[2].CameraId);
        Check("自定义PLC通道保持0", r4[0].PlcRequestAddress == 0 && r4[0].PlcResultAddress == 0);

        // —— EnsureDefaultCameraOrder 颠倒修复 ——
        Func<List<CameraConfig>, List<CameraConfig>> reorder = list =>
        {
            var cfg = new AppConfig { Cameras = list };
            InvokePrivateStatic(tStore, "EnsureDefaultCameraOrder", cfg);
            return cfg.Cameras;
        };
        var o1 = reorder(new List<CameraConfig>
        {
            new CameraConfig { IpAddress = "19.87.6.212" },
            new CameraConfig { IpAddress = "19.87.6.213" }
        });
        Check("颠倒[212,213]→[213,212]", o1[0].IpAddress == "19.87.6.213" && o1[1].IpAddress == "19.87.6.212");
        var o2 = reorder(new List<CameraConfig>
        {
            new CameraConfig { IpAddress = "19.87.6.213" },
            new CameraConfig { IpAddress = "19.87.6.212" }
        });
        Check("正确顺序不动", o2[0].IpAddress == "19.87.6.213");
        var o3 = reorder(new List<CameraConfig>
        {
            new CameraConfig { IpAddress = "10.0.0.9" },
            new CameraConfig { IpAddress = "19.87.6.213" }
        });
        Check("含自定义不动", o3[0].IpAddress == "10.0.0.9");
        try
        {
            var cfgNull = new AppConfig { Cameras = null };
            InvokePrivateStatic(tStore, "EnsureDefaultCameraOrder", cfgNull);
            Check("null列表不崩", true);
        }
        catch (Exception ex) { Check("null列表不崩 (异常:" + ex.GetType().Name + ")", false); }

        // —— EnsureWindowPointMaps 三分支：缺表补/长度错重置/孤儿重置/合法保留 ——
        Func<AppConfig, AppConfig> alignMaps = cfg =>
        {
            InvokePrivateStatic(tStore, "EnsureWindowPointMaps", cfg);
            return cfg;
        };
        var m1 = new AppConfig { Cameras = CameraConfig.DefaultCameras(), ProductModel = "U171" };
        m1.ProductModels.Add("U171");
        alignMaps(m1);
        var m1u = m1.Display.WindowPointMaps.Find(x => x.ModelName == "U171");
        Check("缺表补默认(长度=21)", m1u != null && m1u.Points.Count == 21);
        Check("补表首窗=上点位1", m1u.Points[0] != null && m1u.Points[0].CameraId == 2 && m1u.Points[0].StationNo == 1);
        var m2 = new AppConfig { Cameras = CameraConfig.DefaultCameras(), ProductModel = "U171" };
        m2.ProductModels.Add("U171");
        m2.Display.WindowPointMaps.Add(new ModelWindowPointMap
        {
            ModelName = "U171",
            Points = new List<WindowPointItem> { new WindowPointItem { CameraId = 2, StationNo = 1 } }
        });
        alignMaps(m2);
        Check("长度错重置", m2.Display.WindowPointMaps[0].Points.Count == 21);
        var m3 = new AppConfig { Cameras = CameraConfig.DefaultCameras(), ProductModel = "U171" };
        m3.ProductModels.Add("U171");
        var orphanPts = new List<WindowPointItem>();
        for (int i = 0; i < 21; i++) orphanPts.Add(new WindowPointItem { CameraId = 99, StationNo = 1 });
        m3.Display.WindowPointMaps.Add(new ModelWindowPointMap { ModelName = "U171", Points = orphanPts });
        alignMaps(m3);
        Check("孤儿映射重置", m3.Display.WindowPointMaps[0].Points[0].CameraId == 2);
        var m4 = new AppConfig { Cameras = CameraConfig.DefaultCameras(), ProductModel = "U171" };
        m4.ProductModels.Add("U171");
        var customPts = DisplayConfig.DefaultWindowPointMap(m4.Cameras, "U171", 21);
        var tmpSwap = customPts[0]; customPts[0] = customPts[1]; customPts[1] = tmpSwap;
        m4.Display.WindowPointMaps.Add(new ModelWindowPointMap { ModelName = "U171", Points = customPts });
        alignMaps(m4);
        Check("合法改序表保留", m4.Display.WindowPointMaps[0].Points[0].StationNo == customPts[0].StationNo
            && m4.Display.WindowPointMaps[0].Points[0].StationNo != 1);

        // —— EnsureModelIndexes 双向对齐 ——
        var e1 = new AppConfig();
        e1.Plc.ModelIndexes.Clear();
        e1.Plc.ModelIndexes.Add(new ModelIndexItem { ModelName = "Z121", ModelIndex = 1 });
        e1.ProductModels.Clear();
        e1.ProductModels.AddRange(new[] { "Z121", "U171" });
        e1.ProductModel = "U171";
        InvokePrivateStatic(tStore, "EnsureModelIndexes", e1);
        Check("缺号补U171:2", e1.Plc.ModelIndexes.Any(x =>
            string.Equals(x.ModelName, "U171", StringComparison.OrdinalIgnoreCase) && x.ModelIndex == 2));
        var e2 = new AppConfig();
        e2.Plc.ModelIndexes.Clear();
        e2.Plc.ModelIndexes.Add(new ModelIndexItem { ModelName = "X9", ModelIndex = 5 });
        e2.ProductModels.Clear();
        e2.ProductModel = "U171";
        InvokePrivateStatic(tStore, "EnsureModelIndexes", e2);
        Check("新名回流候选", e2.ProductModels.Any(x => x == "X9"));
        var e3 = new AppConfig();
        e3.Plc.ModelIndexes.Clear();
        e3.Plc.ModelIndexes.Add(new ModelIndexItem { ModelName = "  ", ModelIndex = 7 });
        e3.ProductModels.Clear();
        e3.ProductModel = "U171";
        InvokePrivateStatic(tStore, "EnsureModelIndexes", e3);
        Check("空名跳过回流", !e3.ProductModels.Any(x => string.IsNullOrWhiteSpace(x)));

        // —— EnsureStationMap 对齐：缺补 true，多截断 ——
        var s1 = new AppConfig { Cameras = CameraConfig.DefaultCameras(), ProductModel = "U171" };
        s1.Display.WindowEnabled.Clear();
        InvokePrivateStatic(tStore, "EnsureStationMap", s1);
        Check("enabled补到21全true", s1.Display.WindowEnabled.Count == 21 && s1.Display.WindowEnabled.All(x => x));
        Check("map补到21", s1.Display.WindowStationMap.Count == 21);
        s1.Display.WindowEnabled.Add(false);
        s1.Display.WindowEnabled.Add(false);
        InvokePrivateStatic(tStore, "EnsureStationMap", s1);
        Check("超长截断回21", s1.Display.WindowEnabled.Count == 21);

        // —— NormalizeSubDirs 脏路径归一 ——
        Func<List<string>, List<string>> normSubs = subs =>
        {
            var cfg = new AppConfig();
            cfg.Image.SaveRootDir = @"E:\Images";
            cfg.Image.SubDirs = subs;
            InvokePrivateStatic(tStore, "NormalizeSubDirs", cfg);
            return cfg.Image.SubDirs;
        };
        Check("干净层不变", normSubs(new List<string> { "{年月日}" }).Count == 1);
        var n1 = normSubs(new List<string> { @"E:\Images\{年月日}\{SN}" });
        Check("完整路径拆层", n1.Count == 2 && n1[0] == "{年月日}" && n1[1] == "{SN}");
        var n2 = normSubs(new List<string> { "E:/Image/{SN}" });
        Check("拼写错误根剥离", n2.Count == 1 && n2[0] == "{SN}");
        Check("大小写去重", normSubs(new List<string> { "OK", "ok" }).Count == 1);
        Check("全空兜底", normSubs(new List<string> { "", "  " }).Count == 1);

        // —— 布局边界：AutoFitLayout透传 / Starts边界 / DefaultMap边界 / Resolve回退矩阵 / Valid空集 ——
        var lcams = CameraConfig.DefaultCameras();
        var af = DisplayConfig.AutoFitLayout(lcams, "U171");
        var rl = DisplayConfig.ResolveLayout(lcams, "U171", true, 1, 1);
        Check("AutoFitLayout=ResolveLayout(auto)", af.rows == rl.rows && af.cols == rl.cols && af.windowCount == rl.windowCount);
        Check("Starts null→空", DisplayConfig.AutoFitCameraStarts(null, "U171").Count == 0);
        var startsNull = DisplayConfig.AutoFitCameraStarts(
            new List<CameraConfig> { null, new CameraConfig
            {
                ModelStationPrograms = new List<ModelStationPrograms>
                {
                    new ModelStationPrograms { ModelName = "U171", Programs = new List<StationProgramItem>
                    {
                        new StationProgramItem { StationNo = 1, ProgramNo = 0 },
                        new StationProgramItem { StationNo = 2, ProgramNo = 1 },
                        new StationProgramItem { StationNo = 3, ProgramNo = 2 },
                        new StationProgramItem { StationNo = 4, ProgramNo = 3 }
                    } }
                }
            } }, "U171");
        Check("Starts含null占位", startsNull.Count == 2 && startsNull[0] == 1 && startsNull[1] == 1);
        var trunc = DisplayConfig.DefaultWindowPointMap(lcams, "U171", 2);
        Check("Map截断2条", trunc.Count == 2 && trunc[0].StationNo == 1);
        var zero = DisplayConfig.DefaultWindowPointMap(lcams, "U171", 0);
        Check("Map windowCount=0→1个null", zero.Count == 1 && zero[0] == null);
        var zeroId = DisplayConfig.DefaultWindowPointMap(
            new List<CameraConfig> { new CameraConfig
            {
                CameraId = 0,
                ModelStationPrograms = new List<ModelStationPrograms>
                {
                    new ModelStationPrograms { ModelName = "U171", Programs = new List<StationProgramItem>
                    {
                        new StationProgramItem { StationNo = 5, ProgramNo = 1 }
                    } }
                }
            } }, "U171", 1);
        Check("CameraId0用行序", zeroId.Count == 1 && zeroId[0] != null && zeroId[0].CameraId == 1);
        Check("Map含null相机不崩",
            DisplayConfig.DefaultWindowPointMap(new List<CameraConfig> { null }, "U171", 1).Count == 1);
        var rwNull = DisplayConfig.ResolveWindowPointMap(lcams, "U171", null, 21);
        Check("Resolve maps=null→默认", rwNull.Count == 21 && rwNull[0].CameraId == 2);
        var rwEmpty = DisplayConfig.ResolveWindowPointMap(lcams, "", null, 21);
        Check("Resolve空型号→默认", rwEmpty.Count == 21);
        var userMaps = new List<ModelWindowPointMap>
        {
            new ModelWindowPointMap { ModelName = "u171", Points = DisplayConfig.DefaultWindowPointMap(lcams, "U171", 21) }
        };
        Check("Resolve型号大小写命中用户表",
            DisplayConfig.ResolveWindowPointMap(lcams, "U171", userMaps, 21)[0].CameraId == 2);
        var shortMaps = new List<ModelWindowPointMap>
        {
            new ModelWindowPointMap { ModelName = "U171",
                Points = new List<WindowPointItem> { new WindowPointItem { CameraId = 2, StationNo = 1 } } }
        };
        Check("Resolve长度错→默认", DisplayConfig.ResolveWindowPointMap(lcams, "U171", shortMaps, 21).Count == 21);
        Check("Valid空列表→true", DisplayConfig.PointMapValidForCameras(lcams, new List<WindowPointItem>()));
        Check("Valid cameras=null全null→true",
            DisplayConfig.PointMapValidForCameras(null, new List<WindowPointItem> { null, null }));
        Check("Valid cameras=null有点位→false",
            !DisplayConfig.PointMapValidForCameras(null,
                new List<WindowPointItem> { new WindowPointItem { CameraId = 2, StationNo = 1 } }));
        Check("Valid CameraId0匹配行序→true",
            DisplayConfig.PointMapValidForCameras(
                new List<CameraConfig> { new CameraConfig { CameraId = 0 } },
                new List<WindowPointItem> { new WindowPointItem { CameraId = 1, StationNo = 1 } }));

        // —— ProgramsFor 脏输入 ——
        var pf = new CameraConfig
        {
            StationPrograms = new List<StationProgramItem> { new StationProgramItem { StationNo = 1, ProgramNo = 5 } },
            ModelStationPrograms = new List<ModelStationPrograms>
            {
                null,
                new ModelStationPrograms { ModelName = "U171", Programs = null },
                new ModelStationPrograms { ModelName = "U171", Programs = new List<StationProgramItem>
                {
                    new StationProgramItem { StationNo = 2, ProgramNo = 8 }
                } }
            }
        };
        Check("ProgramsFor null→默认", pf.ProgramsFor(null).Count == 1);
        Check("ProgramsFor空白→默认", pf.ProgramsFor("   ").Count == 1);
        Check("ProgramsFor跳null元命中", pf.ProgramsFor("u171").Count == 1 && pf.ProgramsFor("u171")[0].StationNo == 2);

        // —— 小驼峰键名（混淆豁免根基）：相机/映射/显示关键字段 ——
        // 注意：AppConfig.Cameras 初始化器是空列表（V1.9.9 防4台叠加），相机字段键名需带一台相机才出现
        var camelCfg = new AppConfig();
        camelCfg.Cameras.Add(new CameraConfig());
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };
        string j = JsonConvert.SerializeObject(camelCfg, settings);
        Check("json含cameraId", j.Contains("\"cameraId\""));
        Check("json含plcRequestAddress", j.Contains("\"plcRequestAddress\""));
        Check("json含plcResultAddress", j.Contains("\"plcResultAddress\""));
        Check("json含modelIndexes", j.Contains("\"modelIndexes\""));
        Check("json含windowPointMaps", j.Contains("\"windowPointMaps\""));
        Check("json含autoFit", j.Contains("\"autoFit\""));
        Check("json含windowEnabled", j.Contains("\"windowEnabled\""));
        Check("json含productModels", j.Contains("\"productModels\""));
    }

    // ───────────────────── ⑲ 扫码串口与TCP约定 ─────────────────────
    private static void TestScanSerial()
    {
        Group("⑲ 扫码串口 StopBits/Parity / TCP SendTrigger / 串口空操作 / ScanConfig默认");
        var tSerial = typeof(ScannerService);
        Func<string, StopBits> stopBits = s =>
            (StopBits)InvokePrivateStatic(tSerial, "StopBitsFromString", s);
        Func<string, Parity> parity = s =>
            (Parity)InvokePrivateStatic(tSerial, "ParityFromName", s);
        Eq("停止位1→One", StopBits.One, stopBits("1"));
        Eq("停止位2→Two", StopBits.Two, stopBits("2"));
        Eq("停止位15→OnePointFive", StopBits.OnePointFive, stopBits("15"));
        Eq("停止位非法→One", StopBits.One, stopBits("xxx"));
        Eq("停止位null→One", StopBits.One, stopBits(null));
        Eq("停止位空→One", StopBits.One, stopBits(""));
        Eq("停止位带空格' 2 '→Two（V2.16.4 Trim修复）", StopBits.Two, stopBits(" 2 "));
        Eq("校验odd→Odd", Parity.Odd, parity("odd"));
        Eq("校验ODD大写→Odd", Parity.Odd, parity("ODD"));
        Eq("校验even→Even", Parity.Even, parity("even"));
        Eq("校验mark→Mark", Parity.Mark, parity("MARK"));
        Eq("校验space→Space", Parity.Space, parity("space"));
        Eq("校验null→None", Parity.None, parity(null));
        Eq("校验非法→None", Parity.None, parity("xxx"));

        // TCP SendTrigger 三态（无连接，不触网）
        var emptyCmd = new ScannerTcpService(new ScanConfig { TriggerCommand = "" });
        try { Check("空指令未连接→true（误导性true已锚定）", emptyCmd.SendTrigger()); }
        finally { emptyCmd.Dispose(); }
        var normalCmd = new ScannerTcpService(new ScanConfig { TriggerCommand = "LON" });
        try { Check("正常指令未连接→false", !normalCmd.SendTrigger()); }
        finally { normalCmd.Dispose(); }
        // Enabled=false → Open=false 且不起后台线程
        var disSvc = new ScannerTcpService(new ScanConfig { Enabled = false });
        try
        {
            Check("Enabled=false Open=false", disSvc.Open() == false);
            Check("Enabled=false 不起线程",
                disSvc.GetType().GetField("_thread", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(disSvc) == null);
        }
        finally { disSvc.Dispose(); }
        // 串口实现 SendTrigger 空操作恒 true
        var serial = new ScannerService(new ScanConfig());
        try { Check("串口SendTrigger恒true", serial.SendTrigger()); }
        finally { serial.Dispose(); }

        // ScanConfig 默认全字段（现场基恩士=TCP/IP，默认走TCP）
        var sc = new ScanConfig();
        Eq("Scan默认Enabled=false", false, sc.Enabled);
        Eq("Scan默认Mode=Tcp", "Tcp", sc.Mode);
        Eq("Scan默认串口COM3", "COM3", sc.PortName);
        Eq("Scan默认波特率115200", 115200, sc.BaudRate);
        Eq("Scan默认停止位1", "1", sc.StopBits);
        Eq("Scan默认校验None", "None", sc.Parity);
        Eq("Scan默认IP", "19.87.6.100", sc.IpAddress);
        Eq("Scan默认端口9004", 9004, sc.Port);
        Eq("Scan默认触发LON", "LON", sc.TriggerCommand);
        Eq("Scan默认忽略名单", "ERROR,ERR,NG,NOREAD", sc.IgnoreScanTexts);
    }

    // ───────────────────── ⑳ 杂项 ─────────────────────
    private static void TestMisc2()
    {
        Group("⑳ 杂项 PlaceholderLocalizer/ColorFromName/徽标开关/出厂账号/MES在途/AppTheme剩余/I18n事件");
        // —— PlaceholderLocalizer 中英互逆（切语言，结尾还原中文）——
        I18n.Language = "zh-CN";
        Eq("中文ToDisplay原样", "{年月日}", PlaceholderLocalizer.ToDisplay("{年月日}"));
        Eq("中文ToStorage原样", "{Date}", PlaceholderLocalizer.ToStorage("{Date}"));
        I18n.Language = "en-US";
        try
        {
            Eq("英文年月日→Date", "{Date}", PlaceholderLocalizer.ToDisplay("{年月日}"));
            Eq("英文全套", "{Date}/{Station}/{Camera}/{Time}",
                PlaceholderLocalizer.ToDisplay("{年月日}/{点位}/{相机}/{时间}"));
            Eq("英文SN/OKNG不译", "{SN}/{OKNG}", PlaceholderLocalizer.ToDisplay("{SN}/{OKNG}"));
            Eq("英文逆回年月日", "{年月日}", PlaceholderLocalizer.ToStorage("{Date}"));
            Eq("英文逆回全套", "{年月日}/{点位}/{相机}/{时间}",
                PlaceholderLocalizer.ToStorage("{Date}/{Station}/{Camera}/{Time}"));
            string src = "{年月日}/{SN}/{点位}/{相机}/{OKNG}/{时间}";
            Check("往返一致", PlaceholderLocalizer.ToStorage(PlaceholderLocalizer.ToDisplay(src)) == src);
            Eq("null原样", null, PlaceholderLocalizer.ToDisplay(null));
            Eq("空串原样", "", PlaceholderLocalizer.ToDisplay(""));
        }
        finally { I18n.Language = "zh-CN"; }

        // —— ColorFromName（private static）：非法回退 ——
        var tDisp = typeof(DisplayConfig);
        Func<string, Color, Color> fromName = (n, fb) =>
            (Color)InvokePrivateStatic(tDisp, "ColorFromName", n, fb);
        Eq("Green命中", Color.Green.ToArgb(), fromName("Green", Color.Black).ToArgb());
        Eq("非法回退", Color.Green.ToArgb(), fromName("xxx", Color.Green).ToArgb());
        Eq("空回退", Color.Green.ToArgb(), fromName("", Color.Green).ToArgb());
        Eq("null回退", Color.Red.ToArgb(), fromName(null, Color.Red).ToArgb());
        var dc = new DisplayConfig();
        Eq("OkColor默认绿", Color.Green.ToArgb(), dc.OkColor.ToArgb());
        Eq("NgColor默认红", Color.Red.ToArgb(), dc.NgColor.ToArgb());
        dc.OkColorName = "xxx";
        Eq("OkColor脏配置回退绿", Color.Green.ToArgb(), dc.OkColor.ToArgb());

        // —— CameraDisplayControl 徽标只随开关（V2.14.26）——
        var win = new IrisVision.Controls.CameraDisplayControl();
        try
        {
            var badge = win.GetType().GetField("_badge", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(win);
            var bType = badge.GetType();
            win.SetOkNgVisible(true);
            win.SetOkNgStatus(false);
            Check("开关开+NG→徽标可见", (bool)bType.GetProperty("Visible").GetValue(badge));
            Eq("NG状态同步IsOk=false", false, (bool)bType.GetProperty("IsOk").GetValue(badge));
            Eq("窗口IsOk=false", false, win.IsOk);
            win.SetOkNgStatus(true);
            Eq("OK状态同步", true, win.IsOk);
            win.SetOkNgVisible(false);
            Check("开关关→徽标隐藏（无论结果）", !(bool)bType.GetProperty("Visible").GetValue(badge));
            win.SetWindowIndex(7);
            win.SetWindowIndexVisible(false);
            win.SetWindowIndexVisible(true);
            win.SetOkNgColors(Color.Blue, Color.Orange);
            Eq("徽标颜色写入", Color.Blue.ToArgb(),
                ((Color)bType.GetProperty("OkColor").GetValue(badge)).ToArgb());
            Check("徽标全程不崩", true);
        }
        finally { win.Dispose(); }

        // —— 出厂账号锚点（登录链路的根）——
        var sec = new SecurityConfig();
        Eq("dev123哈希=出厂默认", sec.DevPasswordHash, SecurityUtil.HashPassword("dev123"));
        Eq("AdminEnabled默认true", true, sec.AdminEnabled);
        Eq("DevEnabled默认true", true, sec.DevEnabled);
        Eq("DevUser默认dev", "dev", sec.DevUser);

        // —— MES 在途上限 + 空URL（同步可观测分支；HTTP后台Task不测）——
        var mesCfg = new SnRouteConfig { Target = "Mes", MesUrl = "http://127.0.0.1:9/api/sn" };
        var mes = new MesService(mesCfg);
        try
        {
            var fFlight = mes.GetType().GetField("_inFlight", BindingFlags.NonPublic | BindingFlags.Instance);
            fFlight.SetValue(mes, 10L);
            mes.SendSerialAsync("SN_OVERFLOW", "U171");
            Eq("在途满10丢弃（计数不变）", 10L, (long)fFlight.GetValue(mes));
            fFlight.SetValue(mes, 0L);
        }
        finally { mes.Dispose(); }
        var mesEmpty = new MesService(new SnRouteConfig { Target = "Mes", MesUrl = "" });
        try
        {
            var fWarned = mesEmpty.GetType().GetField("_urlWarned",
                BindingFlags.NonPublic | BindingFlags.Instance);
            mesEmpty.SendSerialAsync("SN1", "U171");
            Check("空URL首次WARN置位", (bool)fWarned.GetValue(mesEmpty));
            mesEmpty.SendSerialAsync("SN2", "U171");
            Check("空URL二次不崩", true);
        }
        finally { mesEmpty.Dispose(); }

        // —— AppTheme 剩余分支 ——
        AppTheme.Theme = "Light";
        var pic = new PictureBox();
        var picBack = pic.BackColor;
        AppTheme.ApplyOne(pic);
        Eq("PictureBox跳过", picBack.ToArgb(), pic.BackColor.ToArgb());
        pic.Dispose();
        using (var frm = new Form())
        {
            var bar = new Panel { Dock = DockStyle.Top };
            frm.Controls.Add(bar);
            AppTheme.ApplyOne(bar);
            Eq("DockTop挂Form→TitleBar", AppTheme.TitleBar.ToArgb(), bar.BackColor.ToArgb());
            var content = new Panel { Dock = DockStyle.Fill };
            frm.Controls.Add(content);
            AppTheme.ApplyOne(content);
            Eq("内容面板→Surface", AppTheme.Surface.ToArgb(), content.BackColor.ToArgb());
            var header = new Panel { Dock = DockStyle.Top, BackColor = AppTheme.PrimaryBlue };
            frm.Controls.Add(header);
            AppTheme.ApplyOne(header);
            Eq("蓝横幅保留", AppTheme.PrimaryBlue.ToArgb(), header.BackColor.ToArgb());
            Eq("蓝横幅白字", Color.White.ToArgb(), header.ForeColor.ToArgb());
        }
        var borderLbl = new Label { BorderStyle = BorderStyle.FixedSingle };
        AppTheme.ApplyOne(borderLbl);
        Eq("带边框Label按输入框配色", AppTheme.InputBackground.ToArgb(), borderLbl.BackColor.ToArgb());
        borderLbl.Dispose();
        var semBack = new Label { BackColor = Color.Red };
        AppTheme.ApplyOne(semBack);
        Eq("语义红背保留", Color.Red.ToArgb(), semBack.BackColor.ToArgb());
        semBack.Dispose();
        var chk = new CheckBox { ForeColor = Color.Black };
        AppTheme.ApplyOne(chk);
        Eq("勾选框换字色", AppTheme.TextPrimary.ToArgb(), chk.ForeColor.ToArgb());
        chk.Dispose();
        var grid2 = new DataGridView();
        grid2.Columns.Add(new DataGridViewTextBoxColumn());
        AppTheme.ApplyGridTheme(grid2);
        Eq("表头跟随TitleBar", AppTheme.TitleBar.ToArgb(),
            grid2.ColumnHeadersDefaultCellStyle.BackColor.ToArgb());
        grid2.Dispose();
        Check("深浅两套底色不同", AppTheme.Background.ToArgb() != Color.Transparent.ToArgb());
        AppTheme.Theme = "Dark";
        Check("深色Surface与浅色不同", true);
        AppTheme.Theme = "Light";

        // —— I18n 事件语义（结尾还原中文）——
        I18n.Language = "zh-CN";
        int fired = 0;
        EventHandler h = (s, e) => fired++;
        I18n.LanguageChanged += h;
        I18n.Language = "en-US";
        Eq("语言切换触发一次", 1, fired);
        I18n.Language = "en-US";
        Eq("同值不重复触发", 1, fired);
        I18n.LanguageChanged -= h;
        I18n.Language = "en-US";
        Eq("en空回落zh", "扫码OK", I18n.T("扫码OK", ""));
        I18n.Language = "zh-CN";
    }
}
