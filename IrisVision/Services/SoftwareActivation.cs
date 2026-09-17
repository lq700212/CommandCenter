using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace IrisVision.Services
{
    /// <summary>
    /// 软件激活（V2.17.0，与 AgingTestSystem / HJVision 同源同口径，
    /// 同一套《获取激活码》工具通用，同一套一键激活脚本 tools/auto_activate.* 通用）。
    /// 【出处】AgingTestSystem.Services.SoftwareActivation（V1.87，与 HJVision 的
    /// GYZVision/MainForm.cs"软件激活"区 +《获取激活码》Form1.cs 的 Encrypt 逐字节同源）。
    /// 公式照抄，改一字工具就对不上，详见 AGENTS.md"软件授权铁律"。
    /// 【公式】
    /// - 设备ID = WMI Win32_Processor.ProcessorId 第一块（读不到=空串，不抛）。
    /// - Encrypt(s) = MD5(Encoding.Default) 取前 15 字节 hex（30 字符）。
    ///   CPU 号与后缀全是 ASCII，Encoding.Default 在中文/英文系统下结果一致。
    /// - 设备ID码 = Encrypt(设备ID + "A")：出厂时手写进客户机 MainSetting.ini
    ///   [RunHash] RunHash1（设备绑定；代码只读不写，与 HJVision 一致；
    ///   一键激活脚本会把两键一次写齐，新机不用再找厂商手写第一键）。
    /// - 设备码 = Encrypt(设备ID + "1")：HJVision 源码写的是
    ///   Encrypt(ID + currentTime.Day)，但 currentTime 从初始提交起就没赋值过
    ///   （恒 0001-01-01），Day 恒为 "1"，现场设备码恒定不变；这里直接写 "1"，
    ///   行为与 HJVision 现场一字不差。**不许"顺手修成当天"**，否则设备码分叉。
    /// - 30天码 = Encrypt(设备码 + "30")；永久码 = Encrypt(设备码 + "ALL")。
    /// - RunHash2 = Encrypt(设备ID + i)，i = 0..839；0..767 有效
    ///   （768 小时约 32 天即"30天"），768..839 或找不到 = 过期；
    ///   Encrypt(设备ID + "ALL") = 永久（跳过计时）。
    /// - 计时：主窗授权 Timer 每小时推一格（i → i+1），软件开着才走，关机不耗。
    /// 【存储】程序目录 MainSetting.ini [RunHash] RunHash1 / RunHash2，
    /// 与 AgingTestSystem/HJVision 同名同结构（kernel32 INI API 读写）；
    /// 该文件是运行时数据，gitignore 绝不入库。
    /// 【语义】无密钥、无试用、无启动闸——新机无 ini 即"新设备"，每小时提醒一次；
    /// 不阻断启动、不拦生产（失败只置灰【系统设置】按钮，见 MainForm.LicenseTimer_Tick）。
    /// 不要加试用/宽限/点数/到期——加了《获取激活码》工具发不出，破坏"同一套工具"统一。
    /// 【混淆】本类有两个 kernel32 P/Invoke（GetPrivateProfileString /
    /// WritePrivateProfileString），已按本项目混淆红线显式写 EntryPoint="..."，
    /// 混淆改方法名后仍按导出函数名查找，不会 DllNotFound；行为与原版一字不差。
    /// 【可测性】判定全是纯函数（只碰传入的字符串）：Encrypt 方程 / FindSlot /
    /// ClassifySlot / VerifyActivationCode / ComputeStatus；ini 读写走显式 path
    /// 参数重载（生产走程序目录，回归走隔离临时目录，不碰真实文件）。
    /// </summary>
    public static class SoftwareActivation
    {
        /// <summary>激活文件名（住程序目录，跟机器；与 AgingTestSystem/HJVision 同名）。</summary>
        public const string IniFileName = "MainSetting.ini";

        /// <summary>ini 段名（与 AgingTestSystem/HJVision 一致）。</summary>
        public const string Section = "RunHash";

        /// <summary>设备绑定键（= Encrypt(设备ID + "A")，出厂手写或一键脚本写入）。</summary>
        public const string KeyDevice = "RunHash1";

        /// <summary>运行计数键（= Encrypt(设备ID + i) 或 Encrypt(设备ID + "ALL")）。</summary>
        public const string KeyRuntime = "RunHash2";

        /// <summary>计数格总数（i = 0..839，与 HJVision 的 for (i &lt; 840) 一致）。</summary>
        public const int TotalSlots = 840;

        /// <summary>有效格数（i &lt; 768 有效，与 HJVision 的 if (i &lt; 768) 一致）。</summary>
        public const int ValidSlots = 768;

        /// <summary>激活码比对结果（与 HJVision 激活_Click 的两个 if 分支对齐）。</summary>
        public enum ActivationKind
        {
            /// <summary>对不上（HJVision：静默无操作）。</summary>
            None = 0,
            /// <summary>30天码（写 Encrypt(设备ID + "0") 重置计时）。</summary>
            ThirtyDays = 1,
            /// <summary>永久码（写 Encrypt(设备ID + "ALL")）。</summary>
            Permanent = 2,
        }

        /// <summary>
        /// 激活状态（主窗 Timer 与激活窗共用同一判定，显示文案见 StatusText）。
        /// </summary>
        public enum ActivationStatus
        {
            /// <summary>RunHash1 对不上本机：新设备，找厂商开户。</summary>
            NewDevice,
            /// <summary>永久（RunHash2 = Encrypt(设备ID + "ALL")）。</summary>
            Permanent,
            /// <summary>30天内（Slot 计数有效，DaysLeft = 剩余天数）。</summary>
            InTrial,
            /// <summary>过期（计数超限或找不到）。</summary>
            Expired,
        }

        // P/Invoke 必须显式 EntryPoint（本项目混淆红线：混淆改方法名后默认按方法名
        // 找导出函数会 DllNotFound；显式指定后永远按 kernel32 导出名查找，行为不变）。
        [DllImport("kernel32", EntryPoint = "GetPrivateProfileString",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetPrivateProfileString(string section, string key, string def,
            StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32", EntryPoint = "WritePrivateProfileString",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool WritePrivateProfileString(string section, string key, string val,
            string filePath);

        /// <summary>
        /// 加密（与 HJVision MainForm.Encrypt /《获取激活码》Form1.Encrypt /
        /// AgingTestSystem SoftwareActivation.Encrypt 逐字节一致）。
        /// MD5 取前 15 字节（md5data.Length - 1 = 15），每字节 "x" + PadLeft(2,'0')
        /// 即两位小写 hex，共 30 字符。
        /// </summary>
        public static string Encrypt(string strPwd)
        {
            MD5 md5 = MD5.Create();
            byte[] data = Encoding.Default.GetBytes(strPwd);//将字符编码为一个字节序列
            byte[] md5data = md5.ComputeHash(data);         //计算data字节数组的哈希值
            md5.Clear();       //清空MD5对象
            string str = "";   //定义一个变量，用来记录加密后的密码
            for (int i = 0; i < md5data.Length - 1; i++)
            {
                str += md5data[i].ToString("x").PadLeft(2, '0');
            }
            return str;
        }

        /// <summary>
        /// 取 CPU 序列号（与 HJVision GetCpuSerialNumber 一致：Win32_Processor
        /// 第一块的 ProcessorId；读不到返回空串，不抛异常）。
        /// </summary>
        public static string GetCpuSerialNumber()
        {
            string serialNumber = "";
            try
            {
                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    serialNumber = obj["ProcessorId"].ToString();
                    break; // Assuming there is only one CPU in the system
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while retrieving CPU serial number: " + ex.Message);
            }
            return serialNumber;
        }

        /// <summary>设备ID码 = Encrypt(设备ID + "A")（出厂写 RunHash1 的值，工具"设备ID"框同款）。</summary>
        public static string DeviceIdCode(string cpuId) { return Encrypt((cpuId ?? "") + "A"); }

        /// <summary>设备码 = Encrypt(设备ID + "1")（见类注释的 Day 恒 "1" 说明）。</summary>
        public static string DeviceCode(string cpuId) { return Encrypt((cpuId ?? "") + "1"); }

        /// <summary>永久标记 = Encrypt(设备ID + "ALL")（RunHash2 等于它即永久）。</summary>
        public static string PermanentMark(string cpuId) { return Encrypt((cpuId ?? "") + "ALL"); }

        /// <summary>30天起点 = Encrypt(设备ID + "0")（激活 30 天时写入 RunHash2）。</summary>
        public static string TrialStartMark(string cpuId) { return Encrypt((cpuId ?? "") + "0"); }

        /// <summary>30天码 = Encrypt(设备码 + "30")（工具"30天激活码"同款）。</summary>
        public static string Code30(string deviceCode) { return Encrypt((deviceCode ?? "") + "30"); }

        /// <summary>永久码 = Encrypt(设备码 + "ALL")（工具"永久激活码"同款）。</summary>
        public static string CodePermanent(string deviceCode) { return Encrypt((deviceCode ?? "") + "ALL"); }

        /// <summary>
        /// 激活码比对（与 HJVision 激活_Click 的两个 if 对齐：先永久后 30 天）。
        /// </summary>
        /// <param name="input">用户在激活窗输入的激活码</param>
        /// <param name="deviceCode">本窗显示的设备码</param>
        public static ActivationKind VerifyActivationCode(string input, string deviceCode)
        {
            try
            {
                string code = (input ?? "").Trim();
                string dev = (deviceCode ?? "").Trim();
                if (code.Length == 0 || dev.Length == 0) return ActivationKind.None;
                if (code == Encrypt(dev + "ALL")) return ActivationKind.Permanent;
                if (code == Encrypt(dev + "30")) return ActivationKind.ThirtyDays;
                return ActivationKind.None;
            }
            catch { return ActivationKind.None; }
        }

        /// <summary>设备绑定检查：RunHash1 == Encrypt(设备ID + "A")（对不上=新设备）。</summary>
        public static bool IsDeviceBound(string storedRunHash1, string cpuId)
        {
            try
            {
                return !string.IsNullOrEmpty(storedRunHash1)
                    && storedRunHash1 == Encrypt((cpuId ?? "") + "A");
            }
            catch { return false; }
        }

        /// <summary>
        /// 找计数格（与 HJVision HashTimer_Tick 的 for (i &lt; 840)
        /// 一致：CPU 号只查一次、命中即 break，语义是"第几格"）。
        /// 找不到返回 -1（调用方按过期处理，与 HJVision 的 mFindHash=false 同路）。
        /// </summary>
        public static int FindSlot(string storedRunHash2, string cpuId)
        {
            try
            {
                if (string.IsNullOrEmpty(storedRunHash2)) return -1;
                string cpu = cpuId ?? "";
                for (int i = 0; i < TotalSlots; i++)
                {
                    if (storedRunHash2 == Encrypt(cpu + i.ToString())) return i;
                }
                return -1;
            }
            catch { return -1; }
        }

        /// <summary>格子是否有效（i &lt; 768，与 HJVision 的 if (i &lt; 768) 一致）。</summary>
        public static bool IsSlotValid(int slot) { return slot >= 0 && slot < ValidSlots; }

        /// <summary>剩余天数（与 HJVision 状态显示的 30 - (i / 24) 一致，整数除法）。</summary>
        public static int SlotDaysLeft(int slot) { return 30 - (slot / 24); }

        /// <summary>
        /// 综合判定（主窗 Timer 与激活窗显示共用）：先设备、再永久、再计数。
        /// slot 传 FindSlot 的结果（-1 = 找不到）；daysLeft 传 SlotDaysLeft。
        /// </summary>
        public static ActivationStatus ComputeStatus(
            string storedRunHash1, string storedRunHash2, string cpuId,
            out int slot, out int daysLeft)
        {
            slot = -1;
            daysLeft = 0;
            try
            {
                string cpu = cpuId ?? "";
                if (!IsDeviceBound(storedRunHash1, cpu)) return ActivationStatus.NewDevice;
                if ((storedRunHash2 ?? "") == Encrypt(cpu + "ALL")) return ActivationStatus.Permanent;
                slot = FindSlot(storedRunHash2, cpu);
                if (!IsSlotValid(slot)) return ActivationStatus.Expired;
                daysLeft = SlotDaysLeft(slot);
                return ActivationStatus.InTrial;
            }
            catch { return ActivationStatus.Expired; }
        }

        /// <summary>
        /// 状态显示文案（与 HJVision 激活窗的"激活状态:"三档文案一致）。
        /// </summary>
        public static string StatusText(ActivationStatus status, int slot, int daysLeft)
        {
            switch (status)
            {
                case ActivationStatus.Permanent: return "激活状态: 永久使用";
                case ActivationStatus.InTrial:
                    if (slot > ValidSlots) return "激活状态: 软件已过期";
                    return "激活状态: 剩余使用天数 / " + daysLeft.ToString();
                case ActivationStatus.NewDevice: return "激活状态: 未绑定设备";
                default: return "激活状态: 软件已过期";
            }
        }

        /// <summary>
        /// 付费后是否应恢复【系统设置】入口（纯函数，可单测）。
        /// <para>做什么：把"当前激活状态"翻译成"要不要把主窗系统设置按钮解灰"。</para>
        /// <para>为什么这么写：计时器置灰后本轮不再自动恢复（与 HJVision 一致），
        /// 付费成功必须有人显式解灰；规则收敛到这一处，激活窗与主窗共用，
        /// 以后改"什么状态算付过费"只改这里，不用两边各写一份 if。</para>
        /// <para>怎么改：只有永久 / 试用中算付过费（新设备、过期一律不恢复，
        /// 即使激活窗刚写过 RunHash2 也要以重算出的状态为准，防止新设备靠写 RunHash2 蒙混）。</para>
        /// </summary>
        public static bool ShouldRestoreUserPermission(ActivationStatus status)
        {
            return status == ActivationStatus.Permanent
                || status == ActivationStatus.InTrial;
        }

        /// <summary>生产路径的 ini 全路径（程序目录 + MainSetting.ini，绝对路径）。</summary>
        public static string IniFilePath()
        {
            try { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IniFileName); }
            catch { return IniFileName; }
        }

        /// <summary>
        /// 缺文件建空模板（首次运行/误删后自动补，厂商只填值）。
        /// 只建不覆盖：文件已存在直接返回，绝不碰已有的激活（覆盖=把有效授权洗掉）。
        /// 模板里两键留空 + 中文注释写清填法；空值读出来是空串，照样走"新设备"提醒
        /// （与文件缺席同语义），不改变任何判定。
        /// </summary>
        public static void EnsureIniTemplate()
        {
            EnsureIniTemplateTo(IniFilePath());
        }

        /// <summary>建空模板（显式 path：回归走隔离临时目录，不碰真实 MainSetting.ini）。</summary>
        public static void EnsureIniTemplateTo(string iniPath)
        {
            try
            {
                if (string.IsNullOrEmpty(iniPath) || File.Exists(iniPath)) return;
                string dir = Path.GetDirectoryName(iniPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine("; 光阑视界 IrisVision 激活文件（与 AgingTestSystem/HJVision 同源，V2.17.0）");
                sb.AppendLine("; 出厂/换机时由厂商填写两键：主界面标题栏【软件授权】按钮里看设备ID/设备码；");
                sb.AppendLine("; 设备ID码=《获取激活码》工具\"设备ID\"框算出；");
                sb.AppendLine("; RunHash2 起点：30天=Encrypt(设备ID+\"0\")，永久=Encrypt(设备ID+\"ALL\")（工具\"设备码\"框算出）。");
                sb.AppendLine("; 也可双击 tools/auto_activate.bat 一键激活（两键一次写齐）。");
                sb.AppendLine("[" + Section + "]");
                sb.AppendLine(KeyDevice + "=");
                sb.AppendLine(KeyRuntime + "=");
                File.WriteAllText(iniPath, sb.ToString(), Encoding.Unicode);
            }
            catch { }
        }

        /// <summary>读 RunHash 双键（生产路径；文件缺席/读失败返回空串，调用方按新设备/过期走）。</summary>
        public static void ReadRunHash(out string runHash1, out string runHash2)
        {
            ReadRunHashFrom(IniFilePath(), out runHash1, out runHash2);
        }

        /// <summary>读双键（显式 path：回归走隔离临时目录，不碰真实 MainSetting.ini）。</summary>
        public static void ReadRunHashFrom(string iniPath, out string runHash1, out string runHash2)
        {
            runHash1 = ReadValueFrom(iniPath, KeyDevice);
            runHash2 = ReadValueFrom(iniPath, KeyRuntime);
        }

        /// <summary>写 RunHash2（生产路径；激活成功 / 每小时推进共用）。</summary>
        public static void WriteRunHash2(string value)
        {
            WriteValueTo(IniFilePath(), KeyRuntime, value ?? "");
        }

        /// <summary>读单键（生产路径）。</summary>
        public static string ReadValue(string key)
        {
            return ReadValueFrom(IniFilePath(), key);
        }

        /// <summary>读单键（显式 path；文件不存在返回空串，不抛）。</summary>
        public static string ReadValueFrom(string iniPath, string key)
        {
            try
            {
                if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath)) return "";
                StringBuilder sb = new StringBuilder(1024);
                GetPrivateProfileString(Section, key ?? "", "", sb, sb.Capacity, iniPath);
                return sb.ToString();
            }
            catch { return ""; }
        }

        /// <summary>写单键（显式 path；目录不存在先建，失败吞掉不抛——写不上由调用方下次重写）。</summary>
        public static void WriteValueTo(string iniPath, string key, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(iniPath) || string.IsNullOrEmpty(key)) return;
                string dir = Path.GetDirectoryName(iniPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                WritePrivateProfileString(Section, key, value ?? "", iniPath);
            }
            catch { }
        }
    }
}
