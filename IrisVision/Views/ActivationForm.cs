using System;
using System.Windows.Forms;
using IrisVision.Services;
using IrisVision.Utils;

namespace IrisVision.Views
{
    /// <summary>
    /// 软件授权窗（V2.17.0，与 AgingTestSystem SoftActivation 同流程同口径，
    /// 同一套《获取激活码》工具通用；本窗是原生 WinForms，外观对齐 LoginForm）。
    /// 【流程】打开显示设备ID/设备码/激活状态 → 用户找厂商拿激活码
    /// （厂商用《获取激活码》工具：设备码→30天码/永久码）→ 输入点激活：
    /// 对上永久码写 RunHash2=Encrypt(设备ID+"ALL")；对上30天码写
    /// RunHash2=Encrypt(设备ID+"0")；对不上静默无操作（与 HJVision 一致）。
    /// 设备绑定（RunHash1）出厂手写或一键脚本写入，本窗不写（与 HJVision 一致）。
    /// 界面布局见 ActivationForm.Designer.cs 头部 ASCII 图。
    /// 【harness】无参构造、不依赖主窗体；RefreshStatus 公开，自动化可显式触发；
    /// 只读值不断言弹窗（构造/刷新全程吞异常保界面必开）。
    /// </summary>
    public partial class ActivationForm : Form
    {
        /// <summary>
        /// 本次打开是否激活成功过（付费即恢复用）。
        /// <para>做什么：记住"用户这次有没有输对过一次码"。</para>
        /// <para>为什么这么写：主窗要在弹窗关闭后决定"要不要重查状态解灰"，
        /// 不能只看弹窗关没关——用户打开看看就关、输错码关，都不该触发重查；
        /// 只有真写过一次 RunHash2 才值得重读一次 ini。</para>
        /// <para>怎么改：只在 <see cref="BtnActivate_Click"/> 写文件成功后置 true，
        /// 不提供外部 setter，不随 <see cref="RefreshStatus"/> 复位（一次成功整轮有效）。</para>
        /// </summary>
        public bool ActivatedSuccessfully { get; private set; }

        public ActivationForm()
        {
            InitializeComponent();

            // 标题栏图标：与其它 9 个窗体同一水龙头（取 exe 内嵌主图标）。
            var appIcon = Utils.AppIcon.Get();
            if (appIcon != null) this.Icon = appIcon;

            // 关闭按钮只关窗（DialogResult 不设 OK，主窗靠 ActivatedSuccessfully 判断）。
            btnClose.Click += (s, e) => Close();

            // 双语 + 主题（短命模态窗：构造时按当前语言/主题设一次即可）。
            ApplyLanguage();
            ApplyTheme();
        }

        /// <summary>打开时回填三件套（设备ID/设备码/激活状态，与一键脚本算的值一致）。</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshStatus();
        }

        /// <summary>
        /// 双语文本（新增界面文本一律 I18n.T 双语；打开时语言已定，构造设一次）。
        /// </summary>
        private void ApplyLanguage()
        {
            if (IsDisposed) return;
            this.Text = I18n.T("软件授权", "Software Activation");
            lblBanner.Text = I18n.T("软件授权", "Software Activation");
            lblDeviceId.Text = I18n.T("设备ID:", "Device ID:");
            lblDeviceCode.Text = I18n.T("设备码:", "Device Code:");
            lblActCode.Text = I18n.T("激活码:", "Activation Code:");
            lblHint.Text = I18n.T(
                "把设备码报给厂商换激活码，粘到上面点激活。\r\n也可双击 tools/auto_activate.bat 一键激活。",
                "Send the device code to the vendor for an activation code, paste it above and click Activate.\r\nOr double-click tools/auto_activate.bat for one-click activation.");
            btnActivate.Text = I18n.T("激活", "Activate");
            btnClose.Text = I18n.T("关闭", "Close");
        }

        /// <summary>主题跟随（模态打开瞬间上色，与其它弹窗同策略）。</summary>
        private void ApplyTheme()
        {
            if (IsDisposed) return;
            try { AppTheme.ApplyTo(this); } catch { }
        }

        /// <summary>
        /// 回填并显示状态（OnShown 与自动化探针共用；全程吞异常保界面必开）。
        /// </summary>
        public void RefreshStatus()
        {
            try
            {
                string cpuId = SoftwareActivation.GetCpuSerialNumber();
                txtDeviceId.Text = cpuId;
                txtDeviceCode.Text = SoftwareActivation.DeviceCode(cpuId);
                string runHash1;
                string runHash2;
                SoftwareActivation.ReadRunHash(out runHash1, out runHash2);
                int slot;
                int daysLeft;
                SoftwareActivation.ActivationStatus status = SoftwareActivation.ComputeStatus(
                    runHash1, runHash2, cpuId, out slot, out daysLeft);
                lblStatus.Text = SoftwareActivation.StatusText(status, slot, daysLeft);
            }
            catch { }
        }

        /// <summary>
        /// 激活（与 HJVision 激活_Click 对齐：先永久后30天，对不上静默无操作）。
        /// </summary>
        private void BtnActivate_Click(object sender, EventArgs e)
        {
            try
            {
                SoftwareActivation.ActivationKind kind = SoftwareActivation.VerifyActivationCode(
                    txtActivationCode.Text, txtDeviceCode.Text);
                if (kind == SoftwareActivation.ActivationKind.None) return;
                string cpuId = SoftwareActivation.GetCpuSerialNumber();
                if (kind == SoftwareActivation.ActivationKind.Permanent)
                {
                    SoftwareActivation.WriteRunHash2(SoftwareActivation.PermanentMark(cpuId));
                    lblStatus.Text = "激活状态: 永久使用";
                    ActivatedSuccessfully = true;
                }
                else
                {
                    SoftwareActivation.WriteRunHash2(SoftwareActivation.TrialStartMark(cpuId));
                    lblStatus.Text = "激活状态: 剩余使用天数 / "
                        + SoftwareActivation.SlotDaysLeft(0).ToString();
                    ActivatedSuccessfully = true;
                }
            }
            catch { }
        }
    }
}
