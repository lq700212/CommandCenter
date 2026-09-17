namespace IrisVision.Views
{
    partial class ActivationForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// 界面布局（ClientSize 400x348，外观对齐 LoginForm：顶部蓝色横幅 + 白色面板 + 蓝主按钮）：
        /// ┌────────────────────────────────────┐
        /// │ pnlHeader 蓝横幅48 "软件授权"       │ lblBanner 白色粗体居中
        /// ├────────────────────────────────────┤
        /// │ lblStatus "激活状态: …"            │ Row0 状态行
        /// │ 设备ID:[txtDeviceId 只读]          │ Row1 设备ID（本机 CPU 号）
        /// │ 设备码:[txtDeviceCode 只读]        │ Row2 设备码（报给厂商算激活码）
        /// │ 激活码:[txtActivationCode 可输]    │ Row3 激活码（厂商给的码，输完点激活）
        /// │ lblHint 填法小字说明               │ Row4 灰色小字
        /// │        [btnActivate激活][btnClose] │ Row5 蓝主按钮 + 白次按钮
        /// └────────────────────────────────────┘
        /// </summary>
        private void InitializeComponent()
        {
            this.pnlHeader = new System.Windows.Forms.Panel();
            this.lblBanner = new System.Windows.Forms.Label();
            this.pnlBody = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblDeviceId = new System.Windows.Forms.Label();
            this.txtDeviceId = new System.Windows.Forms.TextBox();
            this.lblDeviceCode = new System.Windows.Forms.Label();
            this.txtDeviceCode = new System.Windows.Forms.TextBox();
            this.lblActCode = new System.Windows.Forms.Label();
            this.txtActivationCode = new System.Windows.Forms.TextBox();
            this.lblHint = new System.Windows.Forms.Label();
            this.btnActivate = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.pnlHeader.SuspendLayout();
            this.pnlBody.SuspendLayout();
            this.SuspendLayout();
            //
            // pnlHeader
            // 顶部蓝色横幅：固定高度 48，标题白色粗体居中（与 LoginForm 同色同高）。
            //
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.pnlHeader.Controls.Add(this.lblBanner);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(400, 48);
            this.pnlHeader.TabIndex = 0;
            //
            // lblBanner
            // 横幅标题文字（白色粗体），Dock=Fill 铺满横幅、文字居中
            //
            this.lblBanner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblBanner.Font = new System.Drawing.Font("Microsoft YaHei", 14F, System.Drawing.FontStyle.Bold);
            this.lblBanner.ForeColor = System.Drawing.Color.White;
            this.lblBanner.Location = new System.Drawing.Point(0, 0);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.Size = new System.Drawing.Size(400, 48);
            this.lblBanner.TabIndex = 0;
            this.lblBanner.Text = "软件授权";
            this.lblBanner.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // pnlBody
            // 白色内容面板：Dock=Fill 铺满横幅下方剩余区域，三框 + 状态行 + 说明 + 双按钮
            //
            this.pnlBody.BackColor = System.Drawing.Color.White;
            this.pnlBody.Controls.Add(this.btnClose);
            this.pnlBody.Controls.Add(this.btnActivate);
            this.pnlBody.Controls.Add(this.lblHint);
            this.pnlBody.Controls.Add(this.txtActivationCode);
            this.pnlBody.Controls.Add(this.lblActCode);
            this.pnlBody.Controls.Add(this.txtDeviceCode);
            this.pnlBody.Controls.Add(this.lblDeviceCode);
            this.pnlBody.Controls.Add(this.txtDeviceId);
            this.pnlBody.Controls.Add(this.lblDeviceId);
            this.pnlBody.Controls.Add(this.lblStatus);
            this.pnlBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlBody.Location = new System.Drawing.Point(0, 48);
            this.pnlBody.Name = "pnlBody";
            this.pnlBody.Size = new System.Drawing.Size(400, 300);
            this.pnlBody.TabIndex = 1;
            //
            // lblStatus
            // 激活状态行：打开/激活后由 RefreshStatus/BtnActivate_Click 回填
            // （"激活状态: 永久使用" / "剩余使用天数 / N" / "未绑定设备" / "软件已过期"）
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblStatus.Location = new System.Drawing.Point(24, 14);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(100, 19);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "激活状态: …";
            //
            // lblDeviceId
            // 设备ID 标签：与输入框左边缘垂直对齐
            //
            this.lblDeviceId.AutoSize = true;
            this.lblDeviceId.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblDeviceId.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblDeviceId.Location = new System.Drawing.Point(24, 50);
            this.lblDeviceId.Name = "lblDeviceId";
            this.lblDeviceId.Size = new System.Drawing.Size(61, 19);
            this.lblDeviceId.TabIndex = 1;
            this.lblDeviceId.Text = "设备ID:";
            //
            // txtDeviceId
            // 设备ID 只读框：本机 CPU 序列号，打开自动回填，Tab 跳过（TabStop=false）
            //
            this.txtDeviceId.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtDeviceId.Location = new System.Drawing.Point(112, 46);
            this.txtDeviceId.Name = "txtDeviceId";
            this.txtDeviceId.ReadOnly = true;
            this.txtDeviceId.Size = new System.Drawing.Size(260, 25);
            this.txtDeviceId.TabIndex = 2;
            this.txtDeviceId.TabStop = false;
            //
            // lblDeviceCode
            //
            this.lblDeviceCode.AutoSize = true;
            this.lblDeviceCode.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblDeviceCode.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblDeviceCode.Location = new System.Drawing.Point(24, 86);
            this.lblDeviceCode.Name = "lblDeviceCode";
            this.lblDeviceCode.Size = new System.Drawing.Size(61, 19);
            this.lblDeviceCode.TabIndex = 3;
            this.lblDeviceCode.Text = "设备码:";
            //
            // txtDeviceCode
            // 设备码只读框：把这个码报给厂商，厂商用《获取激活码》工具算出激活码
            //
            this.txtDeviceCode.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtDeviceCode.Location = new System.Drawing.Point(112, 82);
            this.txtDeviceCode.Name = "txtDeviceCode";
            this.txtDeviceCode.ReadOnly = true;
            this.txtDeviceCode.Size = new System.Drawing.Size(260, 25);
            this.txtDeviceCode.TabIndex = 4;
            this.txtDeviceCode.TabStop = false;
            //
            // lblActCode
            //
            this.lblActCode.AutoSize = true;
            this.lblActCode.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblActCode.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblActCode.Location = new System.Drawing.Point(24, 122);
            this.lblActCode.Name = "lblActCode";
            this.lblActCode.Size = new System.Drawing.Size(61, 19);
            this.lblActCode.TabIndex = 5;
            this.lblActCode.Text = "激活码:";
            //
            // txtActivationCode
            // 激活码输入框：粘贴厂商给的 30 天码/永久码，唯一可输框，Tab 顺序第一位
            //
            this.txtActivationCode.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtActivationCode.Location = new System.Drawing.Point(112, 118);
            this.txtActivationCode.Name = "txtActivationCode";
            this.txtActivationCode.Size = new System.Drawing.Size(260, 25);
            this.txtActivationCode.TabIndex = 0;
            //
            // lblHint
            // 灰色小字说明：两行，讲清"设备码报厂商→拿激活码→粘这里点激活"
            //
            this.lblHint.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.lblHint.ForeColor = System.Drawing.Color.Gray;
            this.lblHint.Location = new System.Drawing.Point(24, 152);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(348, 56);
            this.lblHint.TabIndex = 6;
            this.lblHint.Text = "把设备码报给厂商换激活码，粘到上面点激活。\r\n也可双击 tools/auto_activate.bat 一键激活。";
            //
            // btnActivate
            // 蓝色主按钮"激活"：回车触发（Form.AcceptButton），逻辑在 BtnActivate_Click
            //
            this.btnActivate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnActivate.FlatAppearance.BorderSize = 0;
            this.btnActivate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnActivate.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.btnActivate.ForeColor = System.Drawing.Color.White;
            this.btnActivate.Location = new System.Drawing.Point(112, 216);
            this.btnActivate.Name = "btnActivate";
            this.btnActivate.Size = new System.Drawing.Size(120, 36);
            this.btnActivate.TabIndex = 7;
            this.btnActivate.Text = "激活";
            this.btnActivate.UseVisualStyleBackColor = false;
            this.btnActivate.Click += new System.EventHandler(this.BtnActivate_Click);
            //
            // btnClose
            // 白色次按钮"关闭"：Esc 触发（Form.CancelButton），直接关窗
            //
            this.btnClose.BackColor = System.Drawing.Color.White;
            this.btnClose.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(189)))), ((int)(((byte)(195)))), ((int)(((byte)(199)))));
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnClose.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.btnClose.Location = new System.Drawing.Point(252, 216);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(120, 36);
            this.btnClose.TabIndex = 8;
            this.btnClose.Text = "关闭";
            this.btnClose.UseVisualStyleBackColor = false;
            //
            // ActivationForm
            //
            this.AcceptButton = this.btnActivate;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(400, 348);
            this.Controls.Add(this.pnlBody);
            this.Controls.Add(this.pnlHeader);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ActivationForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "软件授权";
            this.pnlHeader.ResumeLayout(false);
            this.pnlBody.ResumeLayout(false);
            this.pnlBody.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        // 设计器声明的字段（命名遵循匈牙利前缀规范：pnl=Panel / lbl=Label / txt=TextBox / btn=Button）
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblBanner;
        private System.Windows.Forms.Panel pnlBody;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblDeviceId;
        private System.Windows.Forms.TextBox txtDeviceId;
        private System.Windows.Forms.Label lblDeviceCode;
        private System.Windows.Forms.TextBox txtDeviceCode;
        private System.Windows.Forms.Label lblActCode;
        private System.Windows.Forms.TextBox txtActivationCode;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Button btnActivate;
        private System.Windows.Forms.Button btnClose;
    }
}
