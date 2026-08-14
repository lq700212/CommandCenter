using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// SerialInputForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护/设计器微调）。
    /// 把窗体外观（横幅/标签/输入框/按钮的布局、颜色、字体）全部放进设计器，
    /// 业务逻辑（预填全选、回车/Esc、空提交拦截）在 SerialInputForm.cs 中。
    /// 【界面布局】（风格对齐 LoginForm：顶部蓝色横幅 + 白色面板 + 蓝色主按钮）
    ///   ┌────────────────────────────────────────┐
    ///   │▓ 手动输入序列号（pnlHeader 蓝色横幅，白字居中）▓│
    ///   ├────────────────────────────────────────┤
    ///   │  序列号:  [txtValue 输入框（预填当前）]   │
    ///   │                                        │
    ///   │ [btnCancel 取 消]      [btnOk 确 定]   │
    ///   └────────────────────────────────────────┘
    ///   用 Visual Studio 打开 SerialInputForm.Designer.cs（视图→设计器）即可拖拽微调控件，
    ///   改完后保存会自动回写本文件，无需改代码逻辑。
    /// </summary>
    partial class SerialInputForm
    {
        private System.ComponentModel.IContainer components = null;

        /// <summary>清理正在使用的资源。</summary>
        /// <param name="disposing">为 true 时释放托管资源（含 COM 等）；为 false 时只释放非托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>设计器支持所需的方法 - 不要修改此方法的内容，使用代码编辑器修改此方法的内容。</summary>
        private void InitializeComponent()
        {
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.lblBanner = new System.Windows.Forms.Label();
            this.lblSerialTitle = new System.Windows.Forms.Label();
            this.txtValue = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pnlHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 顶部蓝色横幅（对齐 LoginForm.pnlHeader 同款主蓝）：固定高 52、深蓝底，
            // 品牌感横幅，一眼看出"这是个录入对话框"。
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.pnlHeader.Controls.Add(this.lblBanner);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(400, 52);
            this.pnlHeader.TabIndex = 0;
            // 
            // lblBanner
            // 横幅内标题：白色粗体、水平垂直居中，文字"手动输入序列号"。
            // 
            this.lblBanner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblBanner.Font = new System.Drawing.Font("Microsoft YaHei", 14F, System.Drawing.FontStyle.Bold);
            this.lblBanner.ForeColor = System.Drawing.Color.White;
            this.lblBanner.Location = new System.Drawing.Point(0, 0);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.Size = new System.Drawing.Size(400, 52);
            this.lblBanner.TabIndex = 0;
            this.lblBanner.Text = "手动输入序列号";
            this.lblBanner.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblSerialTitle
            // "序列号:" 标签：深蓝灰小字，AutoSize 随文本伸缩，位置固定（与输入框垂直对齐）。
            // 
            this.lblSerialTitle.AutoSize = true;
            this.lblSerialTitle.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblSerialTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblSerialTitle.Location = new System.Drawing.Point(28, 84);
            this.lblSerialTitle.Name = "lblSerialTitle";
            this.lblSerialTitle.Size = new System.Drawing.Size(56, 20);
            this.lblSerialTitle.TabIndex = 1;
            this.lblSerialTitle.Text = "序列号:";
            // 
            // txtValue
            // 序列号输入框：FixedSingle 单线边框、微软雅黑粗体、深蓝灰字、白底，
            // 预填当前 SN 并全选（构造函数里做 SelectAll/Focus，这里只做外观）。
            // 
            this.txtValue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtValue.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.txtValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.txtValue.Location = new System.Drawing.Point(98, 80);
            this.txtValue.Name = "txtValue";
            this.txtValue.Size = new System.Drawing.Size(276, 26);
            this.txtValue.TabIndex = 2;
            // 
            // btnOk
            // 确定按钮（蓝色主按钮，对齐 LoginForm.btnLogin）：蓝底白字、Flat 无边框、粗体。
            // 位置在右下：右边缘与 txtValue 右边缘对齐（V2.14.8 调整，**确定在右侧**）。
            // DialogResult 默认 OK 由构造函数 AcceptButton 兜底，真正校验在 Click 事件（空拦截）。
            // 
            this.btnOk.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnOk.FlatAppearance.BorderSize = 0;
            this.btnOk.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOk.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.btnOk.ForeColor = System.Drawing.Color.White;
            this.btnOk.Location = new System.Drawing.Point(270, 128);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(104, 38);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "确  定";
            this.btnOk.UseVisualStyleBackColor = false;
            // 
            // btnCancel
            // 取消按钮（白底描边次按钮）：浅灰蓝底、深蓝灰字、Flat 无边框。
            // 位置在左下：左边缘与 lblSerialTitle 左边缘对齐（V2.14.8 调整，**取消在左侧**）。
            // DialogResult = Cancel，Esc 直接触发（构造函数 CancelButton 兜底）。
            // 
            this.btnCancel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(240)))), ((int)(((byte)(245)))));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.FlatAppearance.BorderSize = 0;
            this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancel.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnCancel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.btnCancel.Location = new System.Drawing.Point(28, 128);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(104, 38);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "取  消";
            this.btnCancel.UseVisualStyleBackColor = false;
            // 
            // SerialInputForm
            // 窗体：FixedDialog 固定边框（禁拉伸）、相对主窗体居中弹出、白底、
            // KeyPreview=true 让窗体先收到按键（回车/Esc 兜底）。
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(400, 176);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.txtValue);
            this.Controls.Add(this.lblSerialTitle);
            this.Controls.Add(this.pnlHeader);
            this.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SerialInputForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "手动输入序列号";
            this.pnlHeader.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private Panel pnlHeader;
        private Label lblBanner;
        private Label lblSerialTitle;
        private TextBox txtValue;
        private Button btnOk;
        private Button btnCancel;
    }
}