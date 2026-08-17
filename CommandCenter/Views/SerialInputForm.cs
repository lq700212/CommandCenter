using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CommandCenter.Services;
using CommandCenter.Utils;

namespace CommandCenter.Views
{
    /// <summary>
    /// 手动输入序列号对话框（V2.14.6 恢复，替代 V1.12.19 之后的"框内直录"）。
    ///
    /// 【为什么恢复弹窗】
    ///   框内直录是"txtSerial 本身就是 TextBox、点击即编辑"，与扫码枪自动收码（OnSerialScanned
    ///   直接覆盖 txtSerial.Text）共用同一个输入框，容易互相干扰：扫码枪一推码就把操作员正打的字
    ///   顶掉，且"无确认按钮"让现场对"输入完怎么生效"有疑问。改为"只读展示 + 弹窗补录"：
    ///   扫码枪收码只更新只读框，手动输入走独立的模态对话框 + 明确【确定】/【取消】按钮，
    ///   两条通道彻底隔离，不再冲突。
    ///
    /// 【界面布局】（风格对齐 LoginForm：顶部蓝色横幅 + 白色内容面板 + 蓝色主按钮）
    /// ┌────────────────────────────────────────┐
    /// │▓ 手动输入序列号（蓝色横幅，白字居中）▓    │
    /// ├────────────────────────────────────────┤
    /// │  序列号:  [txtValue 输入框（预填当前）]  │
    /// │                                        │
    /// │ [btnCancel 取 消]    [btnOk 确 定]    │
    /// └────────────────────────────────────────┘
    /// 外观（布局/颜色/字体）全部在 Designer 分部文件 SerialInputForm.Designer.cs 中声明，
    /// 可用 Visual Studio 设计器直接拖拽微调，本类只负责业务交互。
    ///
    /// 【交互】
    ///   - 打开即预填当前已扫/已输的 SN 并全选，方便直接覆盖输入（V1.12.17 同款体验）；
    ///   - 回车 = 【确定】（AcceptButton）、Esc = 【取消】（CancelButton）；
    ///   - 【确定】时输入 trim 后为空 → 弹提示留在窗体，不允许清空提交（防止误清空序列号
    ///     导致本件存图目录归档错乱；想要"不留 SN"直接【取消】）；
    ///   - 【确定】且非空 → DialogResult.OK 关闭，调用方读 SerialNumber 属性写入协调器。
    ///   - **【读到真码自动关闭（V2.14.48）】**：本窗打开期间若扫码枪读到一条真码
    ///     （触发 IScanner.SerialNumberScanned），说明人工补录已不需要（扫码路径已把
    ///     40004 覆盖成 1），本窗自动以"取消"语义关闭——操作员无需再手动输入，省一次操作。
    /// 本窗体不做任何通讯 IO，只收集文本，永远在 UI 主线程使用。
    /// </summary>
    public partial class SerialInputForm : Form
    {
        /// <summary>
        /// 用户最终输入的序列号（【确定】且非空时有效，trim 后）。
        /// <see cref="ShowDialog"/> 返回 DialogResult.OK 后才应读取，其它情况无意义。
        /// </summary>
        public string SerialNumber { get; private set; } = "";

        /// <summary>订阅的扫码枪服务（用于窗口打开期间监听"读到真码"自动关闭）。</summary>
        private readonly IEnumerable<IScanner> _scanners;

        /// <summary>已因"读到真码"自动关闭的标志：防扫码枪连续推码时重复 Close（只在 UI 线程访问）。</summary>
        private bool _autoClosed;

        /// <summary>创建手动输入序列号对话框。</summary>
        /// <param name="current">当前已生效的序列号（扫码收码/上次输入），用于预填方便修改。</param>
        /// <param name="scanners">扫码枪服务列表（V2.14.48）：本窗打开期间订阅每台枪的
        /// SerialNumberScanned，读到真码说明人工补录已不需要，自动关闭；null=不监听（旧行为）。</param>
        public SerialInputForm(string current, IEnumerable<IScanner> scanners = null)
        {
            // Designer 已建好全部控件与外观（见 SerialInputForm.Designer.cs），这里只补业务：
            // 预填 + 全选聚焦 + 回车/Esc + 确定空校验。
            InitializeComponent();
            _scanners = scanners ?? new List<IScanner>();

            // ── 预填当前 SN 并全选：直接打字即覆盖旧值 ────────────────
            txtValue.Text = current ?? "";
            txtValue.SelectAll();
            txtValue.Focus();

            // ── 回车=确定 / Esc=取消（窗体的 AcceptButton/CancelButton）────
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // ── 确定按钮：trim 后非空才真正放行关闭，空输入拦截防误清空 ────
            btnOk.Click += (s, e) =>
            {
                string code = txtValue.Text.Trim();
                if (string.IsNullOrEmpty(code))
                {
                    // 空提交被拦截：留在窗体提示，防误清空导致存图目录错乱
                    MessageBox.Show(this, I18n.T(
                        "序列号不能为空。\n若无需序列号请点【取消】。",
                        "Serial number cannot be empty.\nIf no serial is needed, click Cancel."),
                        I18n.T("手动输入序列号", "Manual Serial Input"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;                 // 撤销默认 OK，保持窗体打开
                    return;
                }
                SerialNumber = code;
                DialogResult = DialogResult.OK;                      // 非空才真正放行关闭
            };

            // 【V2.14.48 读到真码自动关闭】窗口打开期间订阅每台扫码枪的 SerialNumberScanned：
            // 一旦读到真码，说明人工补录已不需要（扫码路径已把 40004 覆盖成 1、协调器已置
            // _serialReceived），本窗自动以"取消"语义（Cancel）关闭——MainForm 收到非 OK
            // 不会调 SetManualSerial，避免与扫码路径重复写入。订阅/退订成对维护防泄漏。
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
        /// </summary>
        private void ApplyLanguage()
        {
            this.Text = I18n.T("手动输入序列号", "Manual Serial Input");
            lblBanner.Text = I18n.T("手动输入序列号", "Manual Serial Input");
            lblSerialTitle.Text = I18n.T("序列号:", "Serial:");
            btnOk.Text = I18n.T("确 定", "OK");
            btnCancel.Text = I18n.T("取 消", "Cancel");
        }

        /// <summary>
        /// 扫码枪读到真码（V2.14.48）：工作线程事件，BeginInvoke 切回 UI 线程自动关闭本窗。
        /// 真码由扫码路径统一处理（MainForm.OnSerialScanned 更新序列号、协调器写结果 1），
        /// 这里只负责弹窗自己退场，不参与业务判定。
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
                    LogHelper.Info("手动输入序列号窗口打开期间扫码枪读到真码，自动关闭（无需人工补录）：" + code);
                    DialogResult = DialogResult.Cancel;      // 非 OK：读到的码由扫码路径处理，不重复提交
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