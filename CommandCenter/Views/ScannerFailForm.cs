using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CommandCenter.Services;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 扫码枪异常提醒对话框（V2.14.32）。
    ///
    /// 【出现的时机】
    ///   扫码枪读码失败时（基恩士无协议模式会把错误字符串 ERROR / ER,READ,00 / NG 当条码推上来，
    ///   收码层已按 IgnoreScanTexts 名单过滤不当 SN、触发 IScanner.ScanFailed 事件），
    ///   MainForm 弹本窗提醒操作员：扫码枪异常，请先检查扫码枪（连接/激光/读码状态），
    ///   或点【人工补录】直接手动输入本条序列号接手处理。
    ///   （配合 V2.14.30/33：同一条失败信号协调器已把结果写 2 通知 PLC，PLC 死等人工补录
    ///    直到上位机把 40004 覆盖成 1 才继续——本窗只是"人看的提醒"，不参与业务判定。）
    ///
    /// 【界面布局】（风格对齐 LoginForm：顶部蓝色横幅 + 白色内容面板 + 蓝色主按钮）
    /// ┌────────────────────────────────────────┐
    /// │▓ 扫码枪异常（pnlHeader 蓝色横幅，白字）▓ │
    /// ├────────────────────────────────────────┤
    /// │  扫码枪读码失败，请先检查扫码枪：       │
    /// │   · 连接线缆 / TCP 是否在线             │
    /// │   · 激光是否正常、条码是否清晰可读      │
    /// │  (lblFailText 读到失败文本，小字灰显)   │
    /// │  ☐ 今日不再提醒 (chkMuteToday)         │
    /// │                                        │
    /// │ [btnLater 稍后处理]  [btnManual 人工补录]│
    /// └────────────────────────────────────────┘
    /// 外观（布局/颜色/字体）全部在 Designer 分部文件 ScannerFailForm.Designer.cs 中声明，
    /// 可用 Visual Studio 设计器直接拖拽微调，本类只负责业务交互。
    ///
    /// 【交互】
    ///   - 【人工补录】（btnManual，蓝色主按钮）→ DialogResult.OK 关闭，
    ///     调用方（MainForm）收到 OK 后打开 SerialInputForm 手动录入序列号接手处理；
    ///   - 【稍后处理】（btnLater，白底次按钮）→ DialogResult.Cancel 关闭，
    ///     暂不补录。⚠️ 注意：本条结果已写 2、**PLC 会一直死等补录**（不复位请求、不判 NG），
    ///     流程会停在扫码这一步——操作员稍后须通过主界面【人工补录】按钮（btnManualSerial）补录，
    ///     协调器才会把 40004 从 2 覆盖成 1、PLC 才继续（V2.14.33 协议）。
    ///   - **【今日不再提醒】（chkMuteToday）**：勾选后点任一按钮关闭，MainForm 记录
    ///     "今日已屏蔽"，当天后续扫码枪失败都不再弹本窗（跨多个弹窗实例全局生效、次日恢复）。
    ///     适合"枪坏了但已安排维修、不想被持续弹窗打扰"的现场场景；屏蔽期间业务照跑——
    ///     扫码 NG 判定照旧、日志照记，只是不弹窗。
    ///   - **【读到真码自动关闭（V2.14.48）】**：本窗打开期间若扫码枪恢复、读到一条真码
    ///     （触发 IScanner.SerialNumberScanned），说明人工补录已不需要（扫码路径已自动把
    ///     40004 从 2 覆盖成 1），本窗自动以"稍后处理"语义关闭，不再打扰操作员。
    ///   - 回车/Esc 兜底：AcceptButton=btnManual、CancelButton=btnLater。
    /// 本窗体不做任何通讯 IO，只提醒 + 返回用户选择，永远在 UI 主线程使用。
    /// </summary>
    public partial class ScannerFailForm : Form
    {
        /// <summary>用户是否勾选了"今日不再提醒"（【人工补录】/【稍后处理】关闭后由调用方读取，当日全局屏蔽）。</summary>
        public bool MuteToday => chkMuteToday.Checked;

        /// <summary>订阅的扫码枪服务（用于窗口打开期间监听"读到真码"自动关闭）。</summary>
        private readonly IEnumerable<IScanner> _scanners;

        /// <summary>已因"读到真码"自动关闭的标志：防扫码枪连续推码时重复 Close（只在 UI 线程访问）。</summary>
        private bool _autoClosed;

        /// <summary>
        /// 创建扫码枪异常提醒对话框。
        /// </summary>
        /// <param name="failText">扫码枪推上来的原始错误文本（已确认命中 IgnoreScanTexts 名单，
        /// 仅作补充展示，可为空——空时不显示失败文本行）。</param>
        /// <param name="scanners">扫码枪服务列表（V2.14.48）：本窗打开期间订阅每台枪的
        /// SerialNumberScanned，读到真码说明人工补录已不需要，自动关闭；null=不监听（旧行为）。</param>
        public ScannerFailForm(string failText, IEnumerable<IScanner> scanners = null)
        {
            // Designer 已建好全部控件与外观（见 ScannerFailForm.Designer.cs），这里只补业务。
            InitializeComponent();
            _scanners = scanners ?? new List<IScanner>();

            // 失败文本有值才显示（线程安全：构造在 UI 线程，只在 ShowDialog 前设置一次）
            lblFailText.Visible = !string.IsNullOrEmpty(failText);
            if (!string.IsNullOrEmpty(failText))
                lblFailText.Text = I18n.T("读码失败文本：" + failText, "Failed text: " + failText);

            // 回车=人工补录 / Esc=稍后处理（窗体的 AcceptButton/CancelButton）
            AcceptButton = btnManual;
            CancelButton = btnLater;

            // 【人工补录】→ 返回 OK，调用方弹 SerialInputForm 手动录 SN 接手处理
            btnManual.Click += (s, e) => { DialogResult = DialogResult.OK; };

            // 【稍后处理】→ 关闭，仅提醒不补录（DialogResult 由 CancelButton 兜底）
            btnLater.Click += (s, e) => { DialogResult = DialogResult.Cancel; };

            // 【V2.14.48 读到真码自动关闭】窗口打开期间订阅每台扫码枪的 SerialNumberScanned：
            // 一旦读到真码，说明人工补录已不需要（扫码路径已把 40004 覆盖成 1），本窗自动以
            // "稍后处理"语义（Cancel）关闭——MainForm 收到非 OK 不会再弹手动录入窗，不打扰操作员。
            // 订阅/退订成对维护（FormClosed 兜底退订，杜绝事件泄漏）。
            foreach (var sc in _scanners)
            {
                if (sc != null) sc.SerialNumberScanned += OnScannerScanned;
            }
            FormClosed += (s, e) => UnsubscribeScanners();
            ApplyLanguage(); // V2.15.0 国际化：按当前语言初始化文本
        }

        /// <summary>
        /// V2.15.0 国际化：按当前语言刷新本窗体全部界面文字。
        /// 在构造函数末尾调用（模态对话框打开瞬间按当前语言初始化；模态期间语言不会变化）。
        /// 注意 lblFailText 由构造参数动态设置（含读码失败原文），这里不覆盖它。
        /// </summary>
        private void ApplyLanguage()
        {
            this.Text = I18n.T("扫码枪异常", "Scanner Error");
            lblBanner.Text = I18n.T("扫码枪异常", "Scanner Error");
            lblMessage.Text = I18n.T(
                "扫码枪读码失败，请检查扫码枪：\n· 电源是否正常 / TCP 是否已连接\n· 触发指令配置与扫码枪设置是否一致",
                "Scanner failed to read. Please check the scanner:\n· Power / TCP connection\n· Trigger command matches the scanner settings");
            chkMuteToday.Text = I18n.T("今日不再提醒", "Don't remind today");
            btnLater.Text = I18n.T("稍后处理", "Later");
            btnManual.Text = I18n.T("人工补录", "Manual Input");
        }

        /// <summary>
        /// 扫码枪读到真码（V2.14.48）：工作线程事件，BeginInvoke 切回 UI 线程自动关闭本窗。
        /// 真码由扫码路径统一处理（MainForm.OnSerialScanned 更新序列号、协调器写结果 1），
        /// 这里只负责"人看的提醒窗"自己退场，不参与任何业务判定。
        /// </summary>
        private void OnScannerScanned(object sender, string code)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || _autoClosed) return;   // 防重复关闭（连续推码/多枪同时读到）
                    _autoClosed = true;
                    LogHelper.Info("扫码枪异常弹窗打开期间读到真码，自动关闭（无需人工补录）：" + code);
                    DialogResult = DialogResult.Cancel;      // 非 OK：不触发 MainForm 弹手动录入窗
                    Close();
                }));
            }
            catch { /* 窗体即将释放时 BeginInvoke 可能失败，直接忽略（本窗马上关闭） */ }
        }

        /// <summary>退订扫码枪读码事件（FormClosed 调用，成对维护防泄漏）。</summary>
        private void UnsubscribeScanners()
        {
            foreach (var sc in _scanners)
            {
                if (sc != null) sc.SerialNumberScanned -= OnScannerScanned;
            }
        }
    }
}