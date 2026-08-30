// ═══════════════════════════════════════════════════════════════════════════
// CommandCenter 回归测试用例集（commandcenter-test skill 的 tests.ps1 编译运行）
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
//   ⑨ SN 去向路由（V2.15.19：SerialNumberTargets 三值判定 / sn 配置段 / MES 报文格式）
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
using System.Linq;
using System.Reflection;
using System.Text;
using CommandCenter.Models;
using CommandCenter.Services;
using CommandCenter.Utils;
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
        TestSnRoute();            // ⑨ V2.15.19：SN 去向路由（放在 I18n 前，I18n 改全局语言状态须最后跑）
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
        var cams = CameraConfig.DefaultCameras();          // 上(id2, U171 表20条) + 下(id1, 4条)
        int totalU171 = DisplayConfig.WindowCountFor(cams, "U171");
        Eq("U171 窗口总数=24（上20+下4）", 24, totalU171);

        // 自适应形状：total=24 → 最优 (rows=4, cols=6)（行列和最小并列时列多者优先）
        var auto = DisplayConfig.ResolveLayout(cams, "U171", true, 1, 1);
        Eq("自适应 rows=4", 4, auto.rows);
        Eq("自适应 cols=6", 6, auto.cols);
        Eq("自适应 windowCount=点位数", totalU171, auto.windowCount);

        // 自适应边界：无相机 → 1×1
        var one = DisplayConfig.ResolveLayout(null, "U171", true, 1, 1);
        Check("空相机自适应=1×1×1", one.rows == 1 && one.cols == 1 && one.windowCount == 1);

        // 非自适：手填 2×7 放不下 24 点位 → 自动补行到 ceil(24/7)=4 → windowCount=28
        var manual = DisplayConfig.ResolveLayout(cams, "U171", false, 2, 7);
        Check("非自适补行 4×7=28", manual.rows == 4 && manual.cols == 7 && manual.windowCount == 28);

        // 非自适列数钳位上限 7（手填 9 列也压回 7）
        var clamp = DisplayConfig.ResolveLayout(cams, "U171", false, 3, 9);
        Check("手填列钳位 7 且补行", clamp.cols == 7 && clamp.rows == 4 && clamp.windowCount == 28);

        // 默认铺排：前 N 个有效条目（前上相机 id2 后下相机 id1）+ 尾部 null 空窗口
        var map = DisplayConfig.DefaultWindowPointMap(cams, "U171", 28);
        Eq("铺排长度=windowCount", 28, map.Count);
        Check("前 24 条目非空", map.Take(24).All(p => p != null));
        Check("尾部 4 个=空窗口(null)", map.Skip(24).All(p => p == null));
        Eq("窗口1=上相机点位1", 1, map[0].StationNo);
        Eq("窗口1 归属上相机 id2", 2, map[0].CameraId);
        Eq("窗口21=下相机首条", cams[1].ProgramsFor("U171")[0].StationNo, map[20].StationNo);
        Eq("窗口21 归属下相机 id1", 1, map[20].CameraId);

        // 各相机窗口起始序号（前缀和）：上=1、下=21
        var starts = DisplayConfig.AutoFitCameraStarts(cams, "U171");
        Check("起始序号=[1,21]", starts.Count == 2 && starts[0] == 1 && starts[1] == 21);

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
            Eq("上相机 U171 点位表=20 条", 20, upTableCnt);
            Eq("下相机 U171 点位表=4 条", 4, lower.ProgramsFor("U171").Count);

            // 锚点断言（固化 V2.14.16 现场映射：点位1→P000=0、点位14→P010=10；变更须同步文档）
            Eq("上·点位1→程序0(P000)", 0, (int)InvokePrivateInstance(coord, "ResolveProgramForStation", upper, 1));
            Eq("上·点位14→程序10(P010)", 10, (int)InvokePrivateInstance(coord, "ResolveProgramForStation", upper, 14));
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
    // ───────────────────────── ⑨ SN 去向路由（V2.15.19）─────────────────────────
    private static void TestSnRoute()
    {
        Group("⑨ SN 去向路由 SerialNumberTargets / sn 配置段 / MES 报文（V2.15.19）");

        // Normalize：空/null/非法值一律回落规范 "Plc"（宁可少传 MES 也不让扫码主流程走偏）
        Eq("Normalize(null)=Plc", "Plc", SerialNumberTargets.Normalize(null));
        Eq("Normalize(空串)=Plc", "Plc", SerialNumberTargets.Normalize(""));
        Eq("Normalize(垃圾值)=Plc", "Plc", SerialNumberTargets.Normalize("xxx"));
        Eq("Normalize(plc 小写)=Plc", "Plc", SerialNumberTargets.Normalize("plc"));
        Eq("Normalize(mes 小写)=Mes", "Mes", SerialNumberTargets.Normalize("mes"));
        Eq("Normalize(BOTH 混大小写)=Both", "Both", SerialNumberTargets.Normalize("BOTH"));

        // 三值判定：Plc/Both 写 PLC；Mes/Both 传 MES（写 PLC 与传 MES 互不排斥）
        Check("Plc → 写 PLC", SerialNumberTargets.WritesPlc("Plc"));
        Check("Plc → 不传 MES", !SerialNumberTargets.SendsMes("Plc"));
        Check("Mes → 不写 PLC", !SerialNumberTargets.WritesPlc("Mes"));
        Check("Mes → 传 MES", SerialNumberTargets.SendsMes("Mes"));
        Check("Both → 写 PLC 且传 MES", SerialNumberTargets.WritesPlc("Both") && SerialNumberTargets.SendsMes("Both"));
        // 脏值按 Normalize 语义判定（协调器内部就是 Normalize 后再判定，两条路径同一实现）
        Check("脏值 → 按 Plc 判定（写 PLC、不传 MES）",
            SerialNumberTargets.WritesPlc("junk") && !SerialNumberTargets.SendsMes("junk"));

        // 默认值：target 默认 Plc（既有现场流程）、MES 地址空、超时 3000
        var sn = new SnRouteConfig();
        Eq("SnRouteConfig.Target 默认 Plc", "Plc", sn.Target);
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
        app.Sn.Target = "Both"; app.Sn.MesUrl = "http://19.87.6.50:8080/api/sn"; app.Sn.MesTimeoutMs = 5000;
        string json = JsonConvert.SerializeObject(app, settings);
        Check("json 含 sn 段", json.Contains("\"sn\":"));
        Check("json 含 sn.mesUrl 键", json.Contains("\"mesUrl\":"));
        Check("json 含 sn.mesTimeoutMs 键", json.Contains("\"mesTimeoutMs\":"));
        // 往返一致
        var back = JsonConvert.DeserializeObject<AppConfig>(json, settings);
        Eq("往返 sn.target=Both", "Both", back.Sn.Target);
        Eq("往返 sn.mesUrl", "http://19.87.6.50:8080/api/sn", back.Sn.MesUrl);
        Eq("往返 sn.mesTimeoutMs=5000", 5000, back.Sn.MesTimeoutMs);
        // json 手写脏值：反序列化原样进来，由 ApplyDefaults 归一（与运行时 Load 路径一致）
        var dirty = new AppConfig(); dirty.Sn.Target = "bogus";
        InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", dirty);
        Eq("ApplyDefaults 归一脏 target→Plc", "Plc", dirty.Sn.Target);
        var nulled = new AppConfig(); nulled.Sn = null;
        InvokePrivateStatic(typeof(ConfigStore), "ApplyDefaults", nulled);
        Check("ApplyDefaults 补 sn=null 段", nulled.Sn != null && nulled.Sn.Target == "Plc");

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
}
