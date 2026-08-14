using System;
using System.Drawing;
using System.Windows.Forms;

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
    /// 本窗体不做任何通讯 IO，只收集文本，永远在 UI 主线程使用。
    /// </summary>
    public partial class SerialInputForm : Form
    {
        /// <summary>
        /// 用户最终输入的序列号（【确定】且非空时有效，trim 后）。
        /// <see cref="ShowDialog"/> 返回 DialogResult.OK 后才应读取，其它情况无意义。
        /// </summary>
        public string SerialNumber { get; private set; } = "";

        /// <summary>创建手动输入序列号对话框。</summary>
        /// <param name="current">当前已生效的序列号（扫码收码/上次输入），用于预填方便修改。</param>
        public SerialInputForm(string current)
        {
            // Designer 已建好全部控件与外观（见 SerialInputForm.Designer.cs），这里只补业务：
            // 预填 + 全选聚焦 + 回车/Esc + 确定空校验。
            InitializeComponent();

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
                    MessageBox.Show(this, "序列号不能为空。\n若无需序列号请点【取消】。",
                        "手动输入序列号", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;                 // 撤销默认 OK，保持窗体打开
                    return;
                }
                SerialNumber = code;
                DialogResult = DialogResult.OK;                      // 非空才真正放行关闭
            };
        }
    }
}