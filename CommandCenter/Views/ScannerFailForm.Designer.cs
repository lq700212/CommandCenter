using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// ScannerFailForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护/设计器微调）。
    /// 把窗体外观（横幅/标签/按钮的布局、颜色、字体）全部放进设计器，
    /// 业务逻辑在 ScannerFailForm.cs 中。
    /// 【界面布局】（风格对齐 LoginForm：顶部蓝色横幅 + 白色面板 + 蓝色主按钮）
    ///   ┌────────────────────────────────────────┐
    ///   │▓ 扫码枪异常（pnlHeader 蓝色横幅，白字居中）▓│
    ///   ├────────────────────────────────────────┤
    ///   │  扫码枪读码失败，请先检查扫码枪：       │
    ///   │   · 连接线缆 / TCP 是否在线             │
    ///   │   · 激光是否正常、条码是否清晰可读      │
    ///   │  (lblFailText 读到失败文本，小字灰显)   │
    ///   │                                        │
    ///   │ [btnLater 稍后处理]  [btnManual 人工补录]│
    ///   └────────────────────────────────────────┘
    ///   用 Visual Studio 打开 ScannerFailForm.Designer.cs（视图→设计器）即可拖拽微调控件。
    /// </summary>
    partial class ScannerFailForm
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
            this.lblMessage = new System.Windows.Forms.Label();
            this.lblFailText = new System.Windows.Forms.Label();
            this.btnLater = new System.Windows.Forms.Button();
            this.btnManual = new System.Windows.Forms.Button();
            this.pnlHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.pnlHeader.Controls.Add(this.lblBanner);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(420, 52);
            this.pnlHeader.TabIndex = 0;
            // 
            // lblBanner
            // 
            this.lblBanner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblBanner.Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Bold);
            this.lblBanner.ForeColor = System.Drawing.Color.White;
            this.lblBanner.Location = new System.Drawing.Point(0, 0);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.Size = new System.Drawing.Size(420, 52);
            this.lblBanner.TabIndex = 0;
            this.lblBanner.Text = "扫码枪异常";
            this.lblBanner.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblMessage
            // 
            this.lblMessage.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblMessage.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblMessage.Location = new System.Drawing.Point(28, 66);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Size = new System.Drawing.Size(364, 70);
            this.lblMessage.TabIndex = 1;
            this.lblMessage.Text = "扫码枪读码失败，请检查扫码枪：\n· 连接线缆 / TCP 是否在线\n· 激光是否正常、条码是否清晰可读";
            // 
            // lblFailText
            // 
            this.lblFailText.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblFailText.ForeColor = System.Drawing.Color.Gray;
            this.lblFailText.Location = new System.Drawing.Point(28, 124);
            this.lblFailText.Name = "lblFailText";
            this.lblFailText.Size = new System.Drawing.Size(364, 30);
            this.lblFailText.TabIndex = 2;
            // 
            // btnLater
            // 
            this.btnLater.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(240)))), ((int)(((byte)(245)))));
            this.btnLater.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnLater.FlatAppearance.BorderSize = 0;
            this.btnLater.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLater.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnLater.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.btnLater.Location = new System.Drawing.Point(28, 172);
            this.btnLater.Name = "btnLater";
            this.btnLater.Size = new System.Drawing.Size(110, 38);
            this.btnLater.TabIndex = 3;
            this.btnLater.Text = "稍后处理";
            this.btnLater.UseVisualStyleBackColor = false;
            // 
            // btnManual
            // 
            this.btnManual.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnManual.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnManual.FlatAppearance.BorderSize = 0;
            this.btnManual.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnManual.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.btnManual.ForeColor = System.Drawing.Color.White;
            this.btnManual.Location = new System.Drawing.Point(282, 172);
            this.btnManual.Name = "btnManual";
            this.btnManual.Size = new System.Drawing.Size(110, 38);
            this.btnManual.TabIndex = 4;
            this.btnManual.Text = "人工补录";
            this.btnManual.UseVisualStyleBackColor = false;
            // 
            // ScannerFailForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(420, 222);
            this.Controls.Add(this.btnLater);
            this.Controls.Add(this.btnManual);
            this.Controls.Add(this.lblFailText);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.pnlHeader);
            this.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ScannerFailForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "扫码枪异常";
            this.pnlHeader.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private Panel pnlHeader;
        private Label lblBanner;
        private Label lblMessage;
        private Label lblFailText;
        private Button btnLater;
        private Button btnManual;
    }
}