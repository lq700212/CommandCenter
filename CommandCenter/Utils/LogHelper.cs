using System;
using System.IO;

namespace CommandCenter.Utils
{
    /// <summary>
    /// 极简日志：写程序目录 Logs\运行日志_yyyyMMdd.txt，按天一个文件。
    /// 抛异常时把异常对象传入，自动附带动堆栈便于排查。
    /// 采用静态方法 + lock 保证多线程（PLC 轮询/图像监听线程）写文件不打架。
    /// </summary>
    public static class LogHelper
    {
        private static readonly object _lock = new object();
        private static string Dir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        /// <summary>写入一条信息日志</summary>
        public static void Info(string message) => Write("INFO", message);

        /// <summary>写入一条警告日志</summary>
        public static void Warn(string message) => Write("WARN", message);

        /// <summary>写入一条错误日志</summary>
        public static void Error(string message) => Write("ERROR", message);

        /// <summary>写入一条错误日志并附带异常堆栈</summary>
        public static void Error(string message, Exception ex) =>
            Write("ERROR", message + "\r\n" + (ex?.ToString() ?? ""));

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Dir);
                    string file = Path.Combine(Dir, $"运行日志_{DateTime.Now:yyyyMMdd}.log");
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                    File.AppendAllText(file, line + "\r\n");
                }
            }
            catch
            {
                // 日志本身失败不允许影响业务，静默丢弃
            }
        }
    }
}