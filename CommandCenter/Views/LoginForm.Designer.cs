using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// LoginForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 界面由"顶部蓝色横幅 + 两个可切换面板"组成：
    ///   - 横幅 pnlHeader/lblBanner：白色大标题，登录/改密码两种状态下文字不同；
    ///   - 登录面板 pnlLogin：用户名/密码 + 记住密码勾选框 + 链接式"修改密码" + 蓝色主按钮"登录"；
    ///   - 改密码面板 pnlChangePwd：原密码/新密码/确认密码 + 链接式"返回登录" + 蓝色主按钮"保存修改"。
    /// 两个面板同尺寸叠放，靠 Visible + BringToFront 切换（见 LoginForm.cs）。
    /// 整体布局见 LoginForm.cs 类注释的 ASCII 图。
    /// </summary>
    partial class LoginForm
    {
        private System.ComponentModel.IContainer components = null;

        /// <summary>清理正在使用的资源。</summary>
        /// <param name="disposing">为 true 时释放托管资源；为 false 时只释放非托管资源。</param>
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
            this.pnlLogin = new System.Windows.Forms.Panel();
            this.chkRemember = new System.Windows.Forms.CheckBox();
            this.btnChangePwd = new System.Windows.Forms.Button();
            this.btnLogin = new System.Windows.Forms.Button();
            this.txtPwd = new System.Windows.Forms.TextBox();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.lblUser = new System.Windows.Forms.Label();
            this.lblPwd = new System.Windows.Forms.Label();
            this.pnlChangePwd = new System.Windows.Forms.Panel();
            this.lblPwdHint = new System.Windows.Forms.Label();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnSavePwd = new System.Windows.Forms.Button();
            this.txtNewPwd2 = new System.Windows.Forms.TextBox();
            this.txtNewPwd = new System.Windows.Forms.TextBox();
            this.txtOldPwd = new System.Windows.Forms.TextBox();
            this.lblNewPwd2 = new System.Windows.Forms.Label();
            this.lblNewPwd = new System.Windows.Forms.Label();
            this.lblOldPwd = new System.Windows.Forms.Label();
            this.pnlHeader.SuspendLayout();
            this.pnlLogin.SuspendLayout();
            this.pnlChangePwd.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 顶部蓝色横幅：固定高度 48，标题白色粗体居中。登录/改密码共用，
            // 状态切换时只改 lblBanner.Text（见 LoginForm.ShowLoginPanel/ShowChangePwdPanel）。
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.pnlHeader.Controls.Add(this.lblBanner);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(380, 48);
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
            this.lblBanner.Size = new System.Drawing.Size(380, 48);
            this.lblBanner.TabIndex = 0;
            this.lblBanner.Text = "管理员登录";
            this.lblBanner.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pnlLogin
            // 登录面板：用户名/密码 + 链接"修改密码" + 蓝色"登录"主按钮。
            // Dock=Fill 铺满横幅下方剩余区域；与 pnlChangePwd 叠放，靠 Visible 切换。
            // 
            this.pnlLogin.BackColor = System.Drawing.Color.White;
            this.pnlLogin.Controls.Add(this.chkRemember);
            this.pnlLogin.Controls.Add(this.btnChangePwd);
            this.pnlLogin.Controls.Add(this.btnLogin);
            this.pnlLogin.Controls.Add(this.txtPwd);
            this.pnlLogin.Controls.Add(this.txtUser);
            this.pnlLogin.Controls.Add(this.lblUser);
            this.pnlLogin.Controls.Add(this.lblPwd);
            this.pnlLogin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLogin.Location = new System.Drawing.Point(0, 48);
            this.pnlLogin.Name = "pnlLogin";
            this.pnlLogin.Size = new System.Drawing.Size(380, 232);
            this.pnlLogin.TabIndex = 1;
            // 
            // lblUser
            // 用户名标签：与输入框左边缘垂直对齐（框 y=26 高25，标签 y=30 高19 → 视觉居中）
            // 
            this.lblUser.AutoSize = true;
            this.lblUser.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblUser.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblUser.Location = new System.Drawing.Point(24, 30);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(61, 19);
            this.lblUser.TabIndex = 0;
            this.lblUser.Text = "用户名:";
            // 
            // txtUser
            // 管理员用户名输入框：宽 240 与密码框对齐，Tab 顺序第一位
            // 
            this.txtUser.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtUser.Location = new System.Drawing.Point(112, 26);
            this.txtUser.Name = "txtUser";
            this.txtUser.Size = new System.Drawing.Size(240, 25);
            this.txtUser.TabIndex = 1;
            // 
            // lblPwd
            // 
            this.lblPwd.AutoSize = true;
            this.lblPwd.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblPwd.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblPwd.Location = new System.Drawing.Point(24, 70);
            this.lblPwd.Name = "lblPwd";
            this.lblPwd.Size = new System.Drawing.Size(61, 19);
            this.lblPwd.TabIndex = 2;
            this.lblPwd.Text = "密　码:";
            // 
            // txtPwd
            // 密码输入框：圆点显示（UseSystemPasswordChar），回车即登录
            // 
            this.txtPwd.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtPwd.Location = new System.Drawing.Point(112, 66);
            this.txtPwd.Name = "txtPwd";
            this.txtPwd.Size = new System.Drawing.Size(240, 25);
            this.txtPwd.TabIndex = 3;
            this.txtPwd.UseSystemPasswordChar = true;
            // 
            // chkRemember
            // "记住密码"复选框：勾选后下次打开登录框自动回填用户名+密码（DPAPI 加密存本地，
            // 见 SecurityUtil.SaveRememberedLogin）；取消勾选登录成功时删除旧记录（ClearRememberedLogin）
            // 
            this.chkRemember.AutoSize = true;
            this.chkRemember.Font = new System.Drawing.Font("Microsoft YaHei", 9.5F);
            this.chkRemember.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.chkRemember.Location = new System.Drawing.Point(112, 96);
            this.chkRemember.Name = "chkRemember";
            this.chkRemember.Size = new System.Drawing.Size(100, 23);
            this.chkRemember.TabIndex = 7;
            this.chkRemember.Text = "记住密码";
            // 
            // btnChangePwd
            // 链接式"修改密码"：无边框、蓝字，点击切到改密码面板（ShowChangePwdPanel）
            // 
            this.btnChangePwd.FlatAppearance.BorderSize = 0;
            this.btnChangePwd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnChangePwd.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnChangePwd.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnChangePwd.Location = new System.Drawing.Point(24, 140);
            this.btnChangePwd.Name = "btnChangePwd";
            this.btnChangePwd.Size = new System.Drawing.Size(100, 34);
            this.btnChangePwd.TabIndex = 5;
            this.btnChangePwd.Text = "修改密码";
            this.btnChangePwd.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnChangePwd.UseVisualStyleBackColor = true;
            this.btnChangePwd.Click += new System.EventHandler(this.ShowChangePwdPanel);
            // 
            // btnLogin
            // 蓝色主按钮"登录"：回车触发（Form.AcceptButton），校验在 LoginForm.BtnLogin_Click
            // 
            this.btnLogin.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnLogin.FlatAppearance.BorderSize = 0;
            this.btnLogin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLogin.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.btnLogin.ForeColor = System.Drawing.Color.White;
            this.btnLogin.Location = new System.Drawing.Point(248, 136);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(104, 38);
            this.btnLogin.TabIndex = 6;
            this.btnLogin.Text = "登 录";
            this.btnLogin.UseVisualStyleBackColor = false;
            this.btnLogin.Click += new System.EventHandler(this.BtnLogin_Click);
            // 
            // pnlChangePwd
            // 修改密码面板：原密码/新密码/确认密码 + 链接"返回登录" + 蓝色"保存修改"主按钮。
            // 与 pnlLogin 叠放同尺寸，靠 Visible 切换；原密码需验证正确才能保存（见 BtnSavePwd_Click）
            // 
            this.pnlChangePwd.BackColor = System.Drawing.Color.White;
            this.pnlChangePwd.Controls.Add(this.lblPwdHint);
            this.pnlChangePwd.Controls.Add(this.btnBack);
            this.pnlChangePwd.Controls.Add(this.btnSavePwd);
            this.pnlChangePwd.Controls.Add(this.txtNewPwd2);
            this.pnlChangePwd.Controls.Add(this.txtNewPwd);
            this.pnlChangePwd.Controls.Add(this.txtOldPwd);
            this.pnlChangePwd.Controls.Add(this.lblNewPwd2);
            this.pnlChangePwd.Controls.Add(this.lblNewPwd);
            this.pnlChangePwd.Controls.Add(this.lblOldPwd);
            this.pnlChangePwd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlChangePwd.Location = new System.Drawing.Point(0, 48);
            this.pnlChangePwd.Name = "pnlChangePwd";
            this.pnlChangePwd.Size = new System.Drawing.Size(380, 232);
            this.pnlChangePwd.TabIndex = 2;
            // 
            // lblOldPwd
            // 
            this.lblOldPwd.AutoSize = true;
            this.lblOldPwd.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblOldPwd.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblOldPwd.Location = new System.Drawing.Point(24, 28);
            this.lblOldPwd.Name = "lblOldPwd";
            this.lblOldPwd.Size = new System.Drawing.Size(61, 19);
            this.lblOldPwd.TabIndex = 0;
            this.lblOldPwd.Text = "原密码:";
            // 
            // txtOldPwd
            // 原密码（=当前密码），验证正确才允许改；圆点显示
            // 
            this.txtOldPwd.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtOldPwd.Location = new System.Drawing.Point(112, 24);
            this.txtOldPwd.Name = "txtOldPwd";
            this.txtOldPwd.Size = new System.Drawing.Size(240, 25);
            this.txtOldPwd.TabIndex = 1;
            this.txtOldPwd.UseSystemPasswordChar = true;
            // 
            // lblNewPwd
            // 
            this.lblNewPwd.AutoSize = true;
            this.lblNewPwd.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblNewPwd.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblNewPwd.Location = new System.Drawing.Point(24, 68);
            this.lblNewPwd.Name = "lblNewPwd";
            this.lblNewPwd.Size = new System.Drawing.Size(61, 19);
            this.lblNewPwd.TabIndex = 2;
            this.lblNewPwd.Text = "新密码:";
            // 
            // txtNewPwd
            // 新密码：至少 6 位（界面校验），圆点显示
            // 
            this.txtNewPwd.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtNewPwd.Location = new System.Drawing.Point(112, 64);
            this.txtNewPwd.Name = "txtNewPwd";
            this.txtNewPwd.Size = new System.Drawing.Size(240, 25);
            this.txtNewPwd.TabIndex = 3;
            this.txtNewPwd.UseSystemPasswordChar = true;
            // 
            // lblNewPwd2
            // 
            this.lblNewPwd2.AutoSize = true;
            this.lblNewPwd2.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblNewPwd2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblNewPwd2.Location = new System.Drawing.Point(24, 108);
            this.lblNewPwd2.Name = "lblNewPwd2";
            this.lblNewPwd2.Size = new System.Drawing.Size(61, 19);
            this.lblNewPwd2.TabIndex = 4;
            this.lblNewPwd2.Text = "确认密码:";
            // 
            // txtNewPwd2
            // 确认新密码：必须与"新密码"完全一致才保存，防止输错
            // 
            this.txtNewPwd2.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtNewPwd2.Location = new System.Drawing.Point(112, 104);
            this.txtNewPwd2.Name = "txtNewPwd2";
            this.txtNewPwd2.Size = new System.Drawing.Size(240, 25);
            this.txtNewPwd2.TabIndex = 5;
            this.txtNewPwd2.UseSystemPasswordChar = true;
            // 
            // btnSavePwd
            // 蓝色主按钮"保存修改"：回车触发（Form.AcceptButton），逻辑在 BtnSavePwd_Click
            // 
            this.btnSavePwd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnSavePwd.FlatAppearance.BorderSize = 0;
            this.btnSavePwd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSavePwd.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.btnSavePwd.ForeColor = System.Drawing.Color.White;
            this.btnSavePwd.Location = new System.Drawing.Point(248, 158);
            this.btnSavePwd.Name = "btnSavePwd";
            this.btnSavePwd.Size = new System.Drawing.Size(104, 38);
            this.btnSavePwd.TabIndex = 7;
            this.btnSavePwd.Text = "保存修改";
            this.btnSavePwd.UseVisualStyleBackColor = false;
            this.btnSavePwd.Click += new System.EventHandler(this.BtnSavePwd_Click);
            // 
            // btnBack
            // 链接式"返回登录"：无边框蓝字，点击切回登录面板（ShowLoginPanel）
            // 
            this.btnBack.FlatAppearance.BorderSize = 0;
            this.btnBack.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBack.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnBack.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnBack.Location = new System.Drawing.Point(24, 162);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(100, 34);
            this.btnBack.TabIndex = 6;
            this.btnBack.Text = "返回登录";
            this.btnBack.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnBack.UseVisualStyleBackColor = true;
            this.btnBack.Click += new System.EventHandler(this.ShowLoginPanel);
            // 
            // lblPwdHint
            // 改密码提示（灰色小字）：提醒长度与"改完需用新密码登录"
            // 
            this.lblPwdHint.AutoSize = true;
            this.lblPwdHint.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.lblPwdHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblPwdHint.Location = new System.Drawing.Point(114, 140);
            this.lblPwdHint.Name = "lblPwdHint";
            this.lblPwdHint.Size = new System.Drawing.Size(220, 17);
            this.lblPwdHint.TabIndex = 8;
            this.lblPwdHint.Text = "新密码至少 6 位，改后需用新密码登录";
            // 
            // LoginForm
            // 【V1.9.4】窗体高度 280→256：下方空白过多，两个面板内容（按钮行最高点 ~196）
            //   在 208 高的面板里仍有余量，不会裁切；横幅 48 + 面板 208 = 256。
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(380, 256);
            this.Controls.Add(this.pnlLogin);
            this.Controls.Add(this.pnlChangePwd);
            this.Controls.Add(this.pnlHeader);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "管理员登录";
            this.pnlHeader.ResumeLayout(false);
            this.pnlLogin.ResumeLayout(false);
            this.pnlLogin.PerformLayout();
            this.pnlChangePwd.ResumeLayout(false);
            this.pnlChangePwd.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        // 设计器声明的字段（命名遵循匈牙利前缀规范：lbl=Label / txt=TextBox / btn=Button / pnl=Panel）
        private Panel pnlHeader;
        private Label lblBanner;
        private Panel pnlLogin;
        private Label lblUser;
        private TextBox txtUser;
        private Label lblPwd;
        private TextBox txtPwd;
        private CheckBox chkRemember;
        private Button btnChangePwd;
        private Button btnLogin;
        private Panel pnlChangePwd;
        private Label lblOldPwd;
        private TextBox txtOldPwd;
        private Label lblNewPwd;
        private TextBox txtNewPwd;
        private Label lblNewPwd2;
        private TextBox txtNewPwd2;
        private Label lblPwdHint;
        private Button btnBack;
        private Button btnSavePwd;
    }
}
