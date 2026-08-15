using System;
using System.Drawing;
using System.Windows.Forms;

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
    ///   - 回车/Esc 兜底：AcceptButton=btnManual、CancelButton=btnLater。
    /// 本窗体不做任何通讯 IO，只提醒 + 返回用户选择，永远在 UI 主线程使用。
    /// </summary>
    public partial class ScannerFailForm : Form
    {
        /// <summary>用户是否勾选了"今日不再提醒"（【人工补录】/【稍后处理】关闭后由调用方读取，当日全局屏蔽）。</summary>
        public bool MuteToday => chkMuteToday.Checked;
        /// <summary>
        /// 创建扫码枪异常提醒对话框。
        /// </summary>
        /// <param name="failText">扫码枪推上来的原始错误文本（已确认命中 IgnoreScanTexts 名单，
        /// 仅作补充展示，可为空——空时不显示失败文本行）。</param>
        public ScannerFailForm(string failText)
        {
            // Designer 已建好全部控件与外观（见 ScannerFailForm.Designer.cs），这里只补业务。
            InitializeComponent();

            // 失败文本有值才显示（线程安全：构造在 UI 线程，只在 ShowDialog 前设置一次）
            lblFailText.Visible = !string.IsNullOrEmpty(failText);
            if (!string.IsNullOrEmpty(failText))
                lblFailText.Text = "读码失败文本：" + failText;

            // 回车=人工补录 / Esc=稍后处理（窗体的 AcceptButton/CancelButton）
            AcceptButton = btnManual;
            CancelButton = btnLater;

            // 【人工补录】→ 返回 OK，调用方弹 SerialInputForm 手动录 SN 接手处理
            btnManual.Click += (s, e) => { DialogResult = DialogResult.OK; };

            // 【稍后处理】→ 关闭，仅提醒不补录（DialogResult 由 CancelButton 兜底）
            btnLater.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
        }
    }
}