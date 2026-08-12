using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CommandCenter.Utils
{
    /// <summary>
    /// 安全工具（V1.9.0 管理员登录用）。
    ///
    /// 【为什么用 SHA-256 而不是明文】
    ///   管理员密码若明文存在 appconfig.json，任何能打开配置文件的人（包括误操作）都能看到密码；
    ///   改成"只存 SHA-256 哈希"后，登录时把用户输入做同样哈希再比对，
    ///   配置里即使被看到也反推不出明文，且登录比对逻辑简单可靠。
    ///
    /// 【为什么不用加盐 / BCrypt】
    ///   本程序是本地单机上位机，风险面是"配置文件被看一眼"而非"数据库被拖库"，
    ///   加盐/慢哈希带来的额外复杂度对现场没有实际收益，SHA-256 已足够且实现最直白。
    ///
    /// 【默认密码约定】出厂默认账号 admin / 密码 admin123，
    ///   其哈希在 AppConfig.SecurityConfig.AdminPasswordHash 的模型默认值里写死。
    /// </summary>
    public static class SecurityUtil
    {
        /// <summary>
        /// 计算密码的 SHA-256 哈希，返回 64 位 hex 小写字符串。
        /// 空/空字符串输入返回空串（不做哈希），避免把"未设密码"误判成"哈希过某个值"。
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2")); // 小写 hex，固定 64 位
                return sb.ToString();
            }
        }

        // ────────────────── 记住密码（V1.9.0，DPAPI 加密）──────────────────
        //
        // 【为什么不能直接存明文】
        //   密码必须能回填到登录框（用户勾"记住密码"后免输入），而 SHA-256 哈希不可逆，
        //   所以只能存"可解密的密文"。此时若用固定密钥加密，等于把钥匙和锁放一起；
        //   Windows 自带 DPAPI（ProtectedData）用当前登录 Windows 用户的凭据加密，
        //   - 只有同一台机器 + 同一个 Windows 用户才能解密，拷走文件到别处也解不开；
        //   - 不需要自己管理密钥，系统托管，实现零复杂度。
        //   这是"自动回填"需求下最稳妥的落盘方式，仍不违反"配置里不存明文密码"的红线。
        //
        // 【存储位置】%LOCALAPPDATA%\CommandCenter\remembered_login.dat
        //   放系统用户目录而非程序目录：程序重装/换目录不丢记忆，且天然不进 git 仓库。

        /// <summary>记住密码文件的完整路径（%LOCALAPPDATA%\CommandCenter\remembered_login.dat）。</summary>
        private static string RememberedFilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CommandCenter");
                return Path.Combine(dir, "remembered_login.dat");
            }
        }

        /// <summary>
        /// 保存"记住的用户名+密码"到本地（DPAPI 加密，绑定当前 Windows 用户）。
        /// 文件内容为 "用户名\n密码" 的 UTF-8 密文，离开本机/本用户不可解密。
        /// 调用方在登录成功后勾选"记住密码"时调用；写失败静默忽略（不阻塞登录）。
        /// </summary>
        public static void SaveRememberedLogin(string userName, string password)
        {
            try
            {
                // 用换行符分隔用户名与密码，解密时按第一个 \n 拆开即可
                string plain = userName + "\n" + password;
                byte[] plainBytes = Encoding.UTF8.GetBytes(plain);
                // DataProtectionScope.CurrentUser：用当前 Windows 用户凭据加密，仅本机本用户可解
                byte[] encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                string path = RememberedFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, encrypted);
            }
            catch
            {
                // 记住密码是"锦上添花"，失败不应影响登录流程，静默即可
            }
        }

        /// <summary>
        /// 读取"记住的用户名+密码"。有记录且能解密返回 true 并填充 out 参数；
        /// 无文件/解密失败（非本用户或文件损坏）返回 false。登录框构造时调用自动回填。
        /// </summary>
        public static bool LoadRememberedLogin(out string userName, out string password)
        {
            userName = null;
            password = null;
            try
            {
                string path = RememberedFilePath;
                if (!File.Exists(path)) return false;
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string plain = Encoding.UTF8.GetString(plainBytes);
                int sep = plain.IndexOf('\n');
                if (sep < 0) return false; // 文件格式非法
                userName = plain.Substring(0, sep);
                password = plain.Substring(sep + 1);
                return true;
            }
            catch
            {
                return false; // 解密失败（换机器/换用户/文件损坏），当没记住处理
            }
        }

        /// <summary>
        /// 删除记住的密码记录。调用方在登录成功后用户"未勾选记住密码"时调用，
        /// 保证取消勾选后旧记录被清掉，不会继续自动回填。
        /// </summary>
        public static void ClearRememberedLogin()
        {
            try
            {
                string path = RememberedFilePath;
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 删除失败不阻塞登录，下次读取时文件损坏也会被当成"没记住"
            }
        }
    }
}
