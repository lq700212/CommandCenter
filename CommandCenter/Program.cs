using System;
using System.Windows.Forms;
using CommandCenter.Views;

namespace CommandCenter
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// 单实例运行：已有实例时激活旧进程并退出，防止现场误开多个上位机抢 PLC。
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new System.Threading.Mutex(true, "CommandCenter_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("程序已在运行，请勿重复启动。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    Utils.LogHelper.Error("程序异常退出", ex);
                    MessageBox.Show("程序发生未处理异常：" + ex.Message, "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}