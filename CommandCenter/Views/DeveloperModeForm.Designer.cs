using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// DeveloperModeForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 布局说明：五个 GroupBox 纵向排布——
    ///   grpAccount（账号管理，V2.15.10 新增，最顶部）/ grpCamera（相机测试区）/
    ///   grpScanner（扫码枪测试区）/ grpPlc（PLC 测试区）/ grpLog（日志区）。
    /// 详细 ASCII 布局图见 DeveloperModeForm.cs 类注释，本文件负责控件外观与坐标。
    /// 控件命名遵循匈牙利前缀：cmb=ComboBox / lbl=Label / btn=Button / txt=TextBox。
    ///
    /// 【垂直居中对齐约定】PLC 区每行由按钮（高34）、文本框（高25）、标签（高19）混排，
    /// 均按"行中心线"对齐：btn 直接定位在行顶，txt 顶=行顶+4，lbl 顶=行顶+7（差值
    /// (34-25)/2≈4、(34-19)/2≈7），保证一行内各控件上下视觉居中。
    /// </summary>
    partial class DeveloperModeForm
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
            this.grpAccount = new System.Windows.Forms.GroupBox();
            this.lblAccAccount = new System.Windows.Forms.Label();
            this.lblAccTip = new System.Windows.Forms.Label();
            this.btnChangePwd = new System.Windows.Forms.Button();
            this.txtNewPwd2 = new System.Windows.Forms.TextBox();
            this.txtNewPwd = new System.Windows.Forms.TextBox();
            this.lblNewPwd2 = new System.Windows.Forms.Label();
            this.lblNewPwd = new System.Windows.Forms.Label();
            this.dgvAccounts = new System.Windows.Forms.DataGridView();
            this.colAccUser = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAccRole = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAccEnabled = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAccPwd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.grpCamera = new System.Windows.Forms.GroupBox();
            this.lblCamResult = new System.Windows.Forms.Label();
            this.btnTriggerRead = new System.Windows.Forms.Button();
            this.btnTrigger = new System.Windows.Forms.Button();
            this.lblCamState = new System.Windows.Forms.Label();
            this.cmbCamera = new System.Windows.Forms.ComboBox();
            this.btnSwProg2 = new System.Windows.Forms.Button();
            this.btnSwProg1 = new System.Windows.Forms.Button();
            this.lblCurrentProgram = new System.Windows.Forms.Label();
            this.btnReadProgramNo = new System.Windows.Forms.Button();
            this.picTestShot = new System.Windows.Forms.PictureBox();
            this.lblTestImagePath = new System.Windows.Forms.Label();
            this.grpScanner = new System.Windows.Forms.GroupBox();
            this.btnScannerTrigger = new System.Windows.Forms.Button();
            this.btnShowScannerFail = new System.Windows.Forms.Button();
            this.lblScannerCode = new System.Windows.Forms.Label();
            this.lblScannerHint = new System.Windows.Forms.Label();
            this.lblScannerState = new System.Windows.Forms.Label();
            this.cmbScanner = new System.Windows.Forms.ComboBox();
            this.grpPlc = new System.Windows.Forms.GroupBox();
            this.lblOffsetTip = new System.Windows.Forms.Label();
            this.txtOffset = new System.Windows.Forms.TextBox();
            this.lblOffset = new System.Windows.Forms.Label();
            this.lblReadValTip = new System.Windows.Forms.Label();
            this.txtReadVal = new System.Windows.Forms.TextBox();
            this.btnReadReg = new System.Windows.Forms.Button();
            this.lblReadAddrTip = new System.Windows.Forms.Label();
            this.txtReadAddr = new System.Windows.Forms.TextBox();
            this.lblWriteValTip = new System.Windows.Forms.Label();
            this.txtWriteVal = new System.Windows.Forms.TextBox();
            this.btnWriteReg = new System.Windows.Forms.Button();
            this.txtWriteAddr = new System.Windows.Forms.TextBox();
            this.btnResCamReset = new System.Windows.Forms.Button();
            this.btnResCamDown = new System.Windows.Forms.Button();
            this.btnResCamUp = new System.Windows.Forms.Button();
            this.btnResScan2 = new System.Windows.Forms.Button();
            this.btnResScan1 = new System.Windows.Forms.Button();
            this.btnResScan0 = new System.Windows.Forms.Button();
            this.txtModel = new System.Windows.Forms.TextBox();
            this.btnWriteModel = new System.Windows.Forms.Button();
            this.lblMoveVal = new System.Windows.Forms.Label();
            this.btnReadCamReq = new System.Windows.Forms.Button();
            this.btnReadScanReq = new System.Windows.Forms.Button();
            this.lblPlcState = new System.Windows.Forms.Label();
            this.grpLog = new System.Windows.Forms.GroupBox();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.grpAccount.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAccounts)).BeginInit();
            this.grpCamera.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picTestShot)).BeginInit();
            this.grpScanner.SuspendLayout();
            this.grpPlc.SuspendLayout();
            this.grpLog.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpAccount
            // 
            this.grpAccount.Controls.Add(this.lblAccAccount);
            this.grpAccount.Controls.Add(this.lblAccTip);
            this.grpAccount.Controls.Add(this.btnChangePwd);
            this.grpAccount.Controls.Add(this.txtNewPwd2);
            this.grpAccount.Controls.Add(this.txtNewPwd);
            this.grpAccount.Controls.Add(this.lblNewPwd2);
            this.grpAccount.Controls.Add(this.lblNewPwd);
            this.grpAccount.Controls.Add(this.dgvAccounts);
            this.grpAccount.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.grpAccount.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpAccount.Location = new System.Drawing.Point(16, 12);
            this.grpAccount.Name = "grpAccount";
            this.grpAccount.Size = new System.Drawing.Size(768, 156);
            this.grpAccount.TabIndex = 10;
            this.grpAccount.TabStop = false;
            this.grpAccount.Text = "账号管理";
            // 
            // lblAccAccount
            // 
            this.lblAccAccount.AutoSize = true;
            this.lblAccAccount.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblAccAccount.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(174)))), ((int)(((byte)(96)))));
            this.lblAccAccount.Location = new System.Drawing.Point(110, 124);
            this.lblAccAccount.Name = "lblAccAccount";
            this.lblAccAccount.Size = new System.Drawing.Size(79, 19);
            this.lblAccAccount.TabIndex = 2;
            this.lblAccAccount.Text = "（未选中）";
            // 
            // lblAccTip
            // 
            this.lblAccTip.AutoSize = true;
            this.lblAccTip.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblAccTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblAccTip.Location = new System.Drawing.Point(24, 124);
            this.lblAccTip.Name = "lblAccTip";
            this.lblAccTip.Size = new System.Drawing.Size(68, 20);
            this.lblAccTip.TabIndex = 1;
            this.lblAccTip.Text = "选中账号:";
            // 
            // btnChangePwd
            // 
            this.btnChangePwd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnChangePwd.FlatAppearance.BorderSize = 0;
            this.btnChangePwd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnChangePwd.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnChangePwd.ForeColor = System.Drawing.Color.White;
            this.btnChangePwd.Location = new System.Drawing.Point(634, 118);
            this.btnChangePwd.Name = "btnChangePwd";
            this.btnChangePwd.Size = new System.Drawing.Size(118, 32);
            this.btnChangePwd.TabIndex = 7;
            this.btnChangePwd.Text = "修改密码";
            this.btnChangePwd.UseVisualStyleBackColor = false;
            this.btnChangePwd.Click += new System.EventHandler(this.BtnChangePwd_Click);
            // 
            // txtNewPwd2
            // 
            this.txtNewPwd2.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtNewPwd2.Location = new System.Drawing.Point(520, 121);
            this.txtNewPwd2.Name = "txtNewPwd2";
            this.txtNewPwd2.PasswordChar = '●';
            this.txtNewPwd2.Size = new System.Drawing.Size(100, 25);
            this.txtNewPwd2.TabIndex = 6;
            // 
            // txtNewPwd
            // 
            this.txtNewPwd.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtNewPwd.Location = new System.Drawing.Point(315, 121);
            this.txtNewPwd.Name = "txtNewPwd";
            this.txtNewPwd.PasswordChar = '●';
            this.txtNewPwd.Size = new System.Drawing.Size(110, 25);
            this.txtNewPwd.TabIndex = 4;
            // 
            // lblNewPwd2
            // 
            this.lblNewPwd2.AutoSize = true;
            this.lblNewPwd2.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblNewPwd2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblNewPwd2.Location = new System.Drawing.Point(440, 124);
            this.lblNewPwd2.Name = "lblNewPwd2";
            this.lblNewPwd2.Size = new System.Drawing.Size(68, 20);
            this.lblNewPwd2.TabIndex = 5;
            this.lblNewPwd2.Text = "确认密码:";
            // 
            // lblNewPwd
            // 
            this.lblNewPwd.AutoSize = true;
            this.lblNewPwd.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblNewPwd.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblNewPwd.Location = new System.Drawing.Point(235, 124);
            this.lblNewPwd.Name = "lblNewPwd";
            this.lblNewPwd.Size = new System.Drawing.Size(54, 20);
            this.lblNewPwd.TabIndex = 3;
            this.lblNewPwd.Text = "新密码:";
            // 
            // dgvAccounts
            // 
            this.dgvAccounts.AllowUserToAddRows = false;
            this.dgvAccounts.AllowUserToDeleteRows = false;
            this.dgvAccounts.AllowUserToResizeRows = false;
            this.dgvAccounts.BackgroundColor = System.Drawing.Color.White;
            this.dgvAccounts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAccounts.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colAccUser,
            this.colAccRole,
            this.colAccEnabled,
            this.colAccPwd});
            this.dgvAccounts.Location = new System.Drawing.Point(16, 34);
            this.dgvAccounts.MultiSelect = false;
            this.dgvAccounts.Name = "dgvAccounts";
            this.dgvAccounts.ReadOnly = true;
            this.dgvAccounts.RowHeadersVisible = false;
            this.dgvAccounts.RowTemplate.Height = 23;
            this.dgvAccounts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvAccounts.Size = new System.Drawing.Size(736, 78);
            this.dgvAccounts.TabIndex = 0;
            // 
            // colAccUser
            // 
            this.colAccUser.HeaderText = "账号";
            this.colAccUser.Name = "colAccUser";
            this.colAccUser.ReadOnly = true;
            this.colAccUser.Width = 180;
            // 
            // colAccRole
            // 
            this.colAccRole.HeaderText = "角色";
            this.colAccRole.Name = "colAccRole";
            this.colAccRole.ReadOnly = true;
            this.colAccRole.Width = 160;
            // 
            // colAccEnabled
            // 
            this.colAccEnabled.HeaderText = "启用";
            this.colAccEnabled.Name = "colAccEnabled";
            this.colAccEnabled.ReadOnly = true;
            // 
            // colAccPwd
            // 
            this.colAccPwd.HeaderText = "密码";
            this.colAccPwd.Name = "colAccPwd";
            this.colAccPwd.ReadOnly = true;
            this.colAccPwd.Width = 280;
            // 
            // grpCamera
            // 
            this.grpCamera.Controls.Add(this.lblCamResult);
            this.grpCamera.Controls.Add(this.btnTriggerRead);
            this.grpCamera.Controls.Add(this.btnTrigger);
            this.grpCamera.Controls.Add(this.lblCamState);
            this.grpCamera.Controls.Add(this.cmbCamera);
            this.grpCamera.Controls.Add(this.btnSwProg2);
            this.grpCamera.Controls.Add(this.btnSwProg1);
            this.grpCamera.Controls.Add(this.lblCurrentProgram);
            this.grpCamera.Controls.Add(this.btnReadProgramNo);
            this.grpCamera.Controls.Add(this.picTestShot);
            this.grpCamera.Controls.Add(this.lblTestImagePath);
            this.grpCamera.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.grpCamera.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpCamera.Location = new System.Drawing.Point(16, 170);
            this.grpCamera.Name = "grpCamera";
            this.grpCamera.Size = new System.Drawing.Size(768, 212);
            this.grpCamera.TabIndex = 0;
            this.grpCamera.TabStop = false;
            this.grpCamera.Text = "相机测试";
            // 
            // lblCamResult
            // 
            this.lblCamResult.AutoSize = true;
            this.lblCamResult.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblCamResult.ForeColor = System.Drawing.Color.Gray;
            this.lblCamResult.Location = new System.Drawing.Point(24, 153);
            this.lblCamResult.Name = "lblCamResult";
            this.lblCamResult.Size = new System.Drawing.Size(121, 20);
            this.lblCamResult.TabIndex = 4;
            this.lblCamResult.Text = "（尚未操作相机）";
            // 
            // btnTriggerRead
            // 
            this.btnTriggerRead.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnTriggerRead.FlatAppearance.BorderSize = 0;
            this.btnTriggerRead.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTriggerRead.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnTriggerRead.ForeColor = System.Drawing.Color.White;
            this.btnTriggerRead.Location = new System.Drawing.Point(186, 68);
            this.btnTriggerRead.Name = "btnTriggerRead";
            this.btnTriggerRead.Size = new System.Drawing.Size(260, 36);
            this.btnTriggerRead.TabIndex = 3;
            this.btnTriggerRead.Text = "触发+判定T2（取图存图）";
            this.btnTriggerRead.UseVisualStyleBackColor = false;
            this.btnTriggerRead.Click += new System.EventHandler(this.BtnTriggerRead_Click);
            // 
            // btnTrigger
            // 
            this.btnTrigger.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnTrigger.FlatAppearance.BorderSize = 0;
            this.btnTrigger.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTrigger.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnTrigger.ForeColor = System.Drawing.Color.White;
            this.btnTrigger.Location = new System.Drawing.Point(24, 68);
            this.btnTrigger.Name = "btnTrigger";
            this.btnTrigger.Size = new System.Drawing.Size(150, 36);
            this.btnTrigger.TabIndex = 2;
            this.btnTrigger.Text = "仅触发拍照 T1";
            this.btnTrigger.UseVisualStyleBackColor = false;
            this.btnTrigger.Click += new System.EventHandler(this.BtnTrigger_Click);
            // 
            // lblCamState
            // 
            this.lblCamState.AutoSize = true;
            this.lblCamState.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblCamState.ForeColor = System.Drawing.Color.Red;
            this.lblCamState.Location = new System.Drawing.Point(270, 33);
            this.lblCamState.Name = "lblCamState";
            this.lblCamState.Size = new System.Drawing.Size(49, 20);
            this.lblCamState.TabIndex = 1;
            this.lblCamState.Text = "○ 断连";
            // 
            // cmbCamera
            // 
            this.cmbCamera.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCamera.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.cmbCamera.FormattingEnabled = true;
            this.cmbCamera.Location = new System.Drawing.Point(24, 30);
            this.cmbCamera.Name = "cmbCamera";
            this.cmbCamera.Size = new System.Drawing.Size(220, 27);
            this.cmbCamera.TabIndex = 0;
            // 
            // btnSwProg2
            // 
            this.btnSwProg2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnSwProg2.FlatAppearance.BorderSize = 0;
            this.btnSwProg2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSwProg2.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnSwProg2.ForeColor = System.Drawing.Color.White;
            this.btnSwProg2.Location = new System.Drawing.Point(186, 112);
            this.btnSwProg2.Name = "btnSwProg2";
            this.btnSwProg2.Size = new System.Drawing.Size(150, 34);
            this.btnSwProg2.TabIndex = 8;
            this.btnSwProg2.Text = "切换程序 → P002";
            this.btnSwProg2.UseVisualStyleBackColor = false;
            this.btnSwProg2.Click += new System.EventHandler(this.BtnSwProg2_Click);
            // 
            // btnSwProg1
            // 
            this.btnSwProg1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnSwProg1.FlatAppearance.BorderSize = 0;
            this.btnSwProg1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSwProg1.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnSwProg1.ForeColor = System.Drawing.Color.White;
            this.btnSwProg1.Location = new System.Drawing.Point(24, 112);
            this.btnSwProg1.Name = "btnSwProg1";
            this.btnSwProg1.Size = new System.Drawing.Size(150, 34);
            this.btnSwProg1.TabIndex = 7;
            this.btnSwProg1.Text = "切换程序 → P001";
            this.btnSwProg1.UseVisualStyleBackColor = false;
            this.btnSwProg1.Click += new System.EventHandler(this.BtnSwProg1_Click);
            // 
            // lblCurrentProgram
            // 
            this.lblCurrentProgram.AutoSize = true;
            this.lblCurrentProgram.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblCurrentProgram.ForeColor = System.Drawing.Color.Gray;
            this.lblCurrentProgram.Location = new System.Drawing.Point(37, 180);
            this.lblCurrentProgram.Name = "lblCurrentProgram";
            this.lblCurrentProgram.Size = new System.Drawing.Size(86, 19);
            this.lblCurrentProgram.TabIndex = 6;
            this.lblCurrentProgram.Text = "当前程序：?";
            // 
            // btnReadProgramNo
            // 
            this.btnReadProgramNo.FlatAppearance.BorderSize = 0;
            this.btnReadProgramNo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReadProgramNo.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnReadProgramNo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnReadProgramNo.Location = new System.Drawing.Point(350, 112);
            this.btnReadProgramNo.Name = "btnReadProgramNo";
            this.btnReadProgramNo.Size = new System.Drawing.Size(130, 34);
            this.btnReadProgramNo.TabIndex = 5;
            this.btnReadProgramNo.Text = "读当前程序号";
            this.btnReadProgramNo.UseVisualStyleBackColor = true;
            this.btnReadProgramNo.Click += new System.EventHandler(this.BtnReadProgramNo_Click);
            // 
            // picTestShot
            // 
            this.picTestShot.BackColor = System.Drawing.Color.Black;
            this.picTestShot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picTestShot.Location = new System.Drawing.Point(592, 36);
            this.picTestShot.Name = "picTestShot";
            this.picTestShot.Size = new System.Drawing.Size(160, 140);
            this.picTestShot.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picTestShot.TabIndex = 9;
            this.picTestShot.TabStop = false;
            // 
            // lblTestImagePath
            // 
            this.lblTestImagePath.AutoEllipsis = true;
            this.lblTestImagePath.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblTestImagePath.ForeColor = System.Drawing.Color.Gray;
            this.lblTestImagePath.Location = new System.Drawing.Point(592, 180);
            this.lblTestImagePath.Name = "lblTestImagePath";
            this.lblTestImagePath.Size = new System.Drawing.Size(160, 19);
            this.lblTestImagePath.TabIndex = 10;
            this.lblTestImagePath.Text = "（最近未测试存图）";
            // 
            // grpScanner
            // 
            this.grpScanner.Controls.Add(this.btnScannerTrigger);
            this.grpScanner.Controls.Add(this.btnShowScannerFail);
            this.grpScanner.Controls.Add(this.lblScannerCode);
            this.grpScanner.Controls.Add(this.lblScannerHint);
            this.grpScanner.Controls.Add(this.lblScannerState);
            this.grpScanner.Controls.Add(this.cmbScanner);
            this.grpScanner.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.grpScanner.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpScanner.Location = new System.Drawing.Point(16, 383);
            this.grpScanner.Name = "grpScanner";
            this.grpScanner.Size = new System.Drawing.Size(768, 128);
            this.grpScanner.TabIndex = 2;
            this.grpScanner.TabStop = false;
            this.grpScanner.Text = "扫码枪测试";
            // 
            // btnScannerTrigger
            // 
            this.btnScannerTrigger.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnScannerTrigger.Location = new System.Drawing.Point(350, 92);
            this.btnScannerTrigger.Name = "btnScannerTrigger";
            this.btnScannerTrigger.Size = new System.Drawing.Size(180, 27);
            this.btnScannerTrigger.TabIndex = 4;
            this.btnScannerTrigger.Text = "发送触发指令";
            this.btnScannerTrigger.UseVisualStyleBackColor = true;
            // 
            // btnShowScannerFail
            // 
            this.btnShowScannerFail.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnShowScannerFail.Location = new System.Drawing.Point(540, 92);
            this.btnShowScannerFail.Name = "btnShowScannerFail";
            this.btnShowScannerFail.Size = new System.Drawing.Size(208, 27);
            this.btnShowScannerFail.TabIndex = 5;
            this.btnShowScannerFail.Text = "显示扫码枪异常弹窗";
            this.btnShowScannerFail.UseVisualStyleBackColor = true;
            // 
            // lblScannerCode
            // 
            this.lblScannerCode.AutoSize = true;
            this.lblScannerCode.Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Bold);
            this.lblScannerCode.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(174)))), ((int)(((byte)(96)))));
            this.lblScannerCode.Location = new System.Drawing.Point(356, 44);
            this.lblScannerCode.MaximumSize = new System.Drawing.Size(720, 24);
            this.lblScannerCode.Name = "lblScannerCode";
            this.lblScannerCode.Size = new System.Drawing.Size(164, 24);
            this.lblScannerCode.TabIndex = 2;
            this.lblScannerCode.Text = "（尚未读到条码）";
            // 
            // lblScannerHint
            // 
            this.lblScannerHint.AutoSize = true;
            this.lblScannerHint.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblScannerHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblScannerHint.Location = new System.Drawing.Point(6, 21);
            this.lblScannerHint.Name = "lblScannerHint";
            this.lblScannerHint.Size = new System.Drawing.Size(296, 17);
            this.lblScannerHint.TabIndex = 3;
            this.lblScannerHint.Text = "把条码放到扫码枪下读，读到会实时显示（共用连接）";
            // 
            // lblScannerState
            // 
            this.lblScannerState.AutoSize = true;
            this.lblScannerState.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblScannerState.ForeColor = System.Drawing.Color.Red;
            this.lblScannerState.Location = new System.Drawing.Point(238, 47);
            this.lblScannerState.Name = "lblScannerState";
            this.lblScannerState.Size = new System.Drawing.Size(49, 20);
            this.lblScannerState.TabIndex = 1;
            this.lblScannerState.Text = "○ 断连";
            // 
            // cmbScanner
            // 
            this.cmbScanner.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbScanner.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.cmbScanner.FormattingEnabled = true;
            this.cmbScanner.Location = new System.Drawing.Point(9, 44);
            this.cmbScanner.Name = "cmbScanner";
            this.cmbScanner.Size = new System.Drawing.Size(220, 27);
            this.cmbScanner.TabIndex = 0;
            // 
            // grpPlc
            // 
            this.grpPlc.Controls.Add(this.lblOffsetTip);
            this.grpPlc.Controls.Add(this.txtOffset);
            this.grpPlc.Controls.Add(this.lblOffset);
            this.grpPlc.Controls.Add(this.lblReadValTip);
            this.grpPlc.Controls.Add(this.txtReadVal);
            this.grpPlc.Controls.Add(this.btnReadReg);
            this.grpPlc.Controls.Add(this.lblReadAddrTip);
            this.grpPlc.Controls.Add(this.txtReadAddr);
            this.grpPlc.Controls.Add(this.lblWriteValTip);
            this.grpPlc.Controls.Add(this.txtWriteVal);
            this.grpPlc.Controls.Add(this.btnWriteReg);
            this.grpPlc.Controls.Add(this.txtWriteAddr);
            this.grpPlc.Controls.Add(this.btnResCamReset);
            this.grpPlc.Controls.Add(this.btnResCamDown);
            this.grpPlc.Controls.Add(this.btnResCamUp);
            this.grpPlc.Controls.Add(this.btnResScan2);
            this.grpPlc.Controls.Add(this.btnResScan1);
            this.grpPlc.Controls.Add(this.btnResScan0);
            this.grpPlc.Controls.Add(this.txtModel);
            this.grpPlc.Controls.Add(this.btnWriteModel);
            this.grpPlc.Controls.Add(this.lblMoveVal);
            this.grpPlc.Controls.Add(this.btnReadCamReq);
            this.grpPlc.Controls.Add(this.btnReadScanReq);
            this.grpPlc.Controls.Add(this.lblPlcState);
            this.grpPlc.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.grpPlc.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpPlc.Location = new System.Drawing.Point(16, 512);
            this.grpPlc.Name = "grpPlc";
            this.grpPlc.Size = new System.Drawing.Size(768, 320);
            this.grpPlc.TabIndex = 1;
            this.grpPlc.TabStop = false;
            this.grpPlc.Text = "PLC 测试";
            // 
            // lblOffsetTip
            // 
            this.lblOffsetTip.AutoSize = true;
            this.lblOffsetTip.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblOffsetTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblOffsetTip.Location = new System.Drawing.Point(238, 82);
            this.lblOffsetTip.Name = "lblOffsetTip";
            this.lblOffsetTip.Size = new System.Drawing.Size(307, 17);
            this.lblOffsetTip.TabIndex = 3;
            this.lblOffsetTip.Text = "实际D地址 = 输入地址 + 偏移量（默认0按D地址读写）";
            // 
            // txtOffset
            // 
            this.txtOffset.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtOffset.Location = new System.Drawing.Point(156, 79);
            this.txtOffset.Name = "txtOffset";
            this.txtOffset.Size = new System.Drawing.Size(70, 25);
            this.txtOffset.TabIndex = 2;
            this.txtOffset.Text = "0";
            // 
            // lblOffset
            // 
            this.lblOffset.AutoSize = true;
            this.lblOffset.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblOffset.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblOffset.Location = new System.Drawing.Point(24, 81);
            this.lblOffset.Name = "lblOffset";
            this.lblOffset.Size = new System.Drawing.Size(82, 20);
            this.lblOffset.TabIndex = 1;
            this.lblOffset.Text = "协议偏移量:";
            // 
            // lblReadValTip
            // 
            this.lblReadValTip.AutoSize = true;
            this.lblReadValTip.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblReadValTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblReadValTip.Location = new System.Drawing.Point(314, 119);
            this.lblReadValTip.Name = "lblReadValTip";
            this.lblReadValTip.Size = new System.Drawing.Size(86, 20);
            this.lblReadValTip.TabIndex = 7;
            this.lblReadValTip.Text = "→ 读到的值:";
            // 
            // txtReadVal
            // 
            this.txtReadVal.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtReadVal.Location = new System.Drawing.Point(408, 116);
            this.txtReadVal.Name = "txtReadVal";
            this.txtReadVal.ReadOnly = true;
            this.txtReadVal.Size = new System.Drawing.Size(80, 25);
            this.txtReadVal.TabIndex = 8;
            this.txtReadVal.Text = "？";
            // 
            // btnReadReg
            // 
            this.btnReadReg.FlatAppearance.BorderSize = 0;
            this.btnReadReg.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReadReg.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnReadReg.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnReadReg.Location = new System.Drawing.Point(238, 112);
            this.btnReadReg.Name = "btnReadReg";
            this.btnReadReg.Size = new System.Drawing.Size(60, 34);
            this.btnReadReg.TabIndex = 6;
            this.btnReadReg.Text = "读 取";
            this.btnReadReg.UseVisualStyleBackColor = true;
            this.btnReadReg.Click += new System.EventHandler(this.BtnReadReg_Click);
            // 
            // lblReadAddrTip
            // 
            this.lblReadAddrTip.AutoSize = true;
            this.lblReadAddrTip.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblReadAddrTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblReadAddrTip.Location = new System.Drawing.Point(24, 119);
            this.lblReadAddrTip.Name = "lblReadAddrTip";
            this.lblReadAddrTip.Size = new System.Drawing.Size(82, 20);
            this.lblReadAddrTip.TabIndex = 4;
            this.lblReadAddrTip.Text = "读地址测试:";
            // 
            // txtReadAddr
            // 
            this.txtReadAddr.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtReadAddr.Location = new System.Drawing.Point(156, 117);
            this.txtReadAddr.Name = "txtReadAddr";
            this.txtReadAddr.Size = new System.Drawing.Size(70, 25);
            this.txtReadAddr.TabIndex = 5;
            this.txtReadAddr.Text = "2";
            // 
            // lblWriteValTip
            // 
            this.lblWriteValTip.AutoSize = true;
            this.lblWriteValTip.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblWriteValTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblWriteValTip.Location = new System.Drawing.Point(24, 159);
            this.lblWriteValTip.Name = "lblWriteValTip";
            this.lblWriteValTip.Size = new System.Drawing.Size(82, 20);
            this.lblWriteValTip.TabIndex = 7;
            this.lblWriteValTip.Text = "写地址测试:";
            // 
            // txtWriteVal
            // 
            this.txtWriteVal.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtWriteVal.Location = new System.Drawing.Point(238, 156);
            this.txtWriteVal.Name = "txtWriteVal";
            this.txtWriteVal.Size = new System.Drawing.Size(70, 25);
            this.txtWriteVal.TabIndex = 10;
            this.txtWriteVal.Text = "8";
            // 
            // btnWriteReg
            // 
            this.btnWriteReg.FlatAppearance.BorderSize = 0;
            this.btnWriteReg.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWriteReg.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnWriteReg.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnWriteReg.Location = new System.Drawing.Point(320, 152);
            this.btnWriteReg.Name = "btnWriteReg";
            this.btnWriteReg.Size = new System.Drawing.Size(60, 34);
            this.btnWriteReg.TabIndex = 11;
            this.btnWriteReg.Text = "写 入";
            this.btnWriteReg.UseVisualStyleBackColor = true;
            this.btnWriteReg.Click += new System.EventHandler(this.BtnWriteReg_Click);
            // 
            // txtWriteAddr
            // 
            this.txtWriteAddr.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtWriteAddr.Location = new System.Drawing.Point(156, 156);
            this.txtWriteAddr.Name = "txtWriteAddr";
            this.txtWriteAddr.Size = new System.Drawing.Size(70, 25);
            this.txtWriteAddr.TabIndex = 9;
            this.txtWriteAddr.Text = "5";
            // 
            // btnResCamReset
            // 
            this.btnResCamReset.FlatAppearance.BorderSize = 0;
            this.btnResCamReset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResCamReset.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnResCamReset.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnResCamReset.Location = new System.Drawing.Point(393, 274);
            this.btnResCamReset.Name = "btnResCamReset";
            this.btnResCamReset.Size = new System.Drawing.Size(115, 34);
            this.btnResCamReset.TabIndex = 22;
            this.btnResCamReset.Text = "相机复位 = 0";
            this.btnResCamReset.UseVisualStyleBackColor = true;
            this.btnResCamReset.Click += new System.EventHandler(this.BtnResCamReset_Click);
            // 
            // btnResCamDown
            // 
            this.btnResCamDown.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnResCamDown.FlatAppearance.BorderSize = 0;
            this.btnResCamDown.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResCamDown.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnResCamDown.ForeColor = System.Drawing.Color.White;
            this.btnResCamDown.Location = new System.Drawing.Point(279, 274);
            this.btnResCamDown.Name = "btnResCamDown";
            this.btnResCamDown.Size = new System.Drawing.Size(115, 34);
            this.btnResCamDown.TabIndex = 21;
            this.btnResCamDown.Text = "相机NG = 2";
            this.btnResCamDown.UseVisualStyleBackColor = false;
            this.btnResCamDown.Click += new System.EventHandler(this.BtnResCamDown_Click);
            // 
            // btnResCamUp
            // 
            this.btnResCamUp.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnResCamUp.FlatAppearance.BorderSize = 0;
            this.btnResCamUp.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResCamUp.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnResCamUp.ForeColor = System.Drawing.Color.White;
            this.btnResCamUp.Location = new System.Drawing.Point(156, 274);
            this.btnResCamUp.Name = "btnResCamUp";
            this.btnResCamUp.Size = new System.Drawing.Size(115, 34);
            this.btnResCamUp.TabIndex = 20;
            this.btnResCamUp.Text = "相机OK = 1";
            this.btnResCamUp.UseVisualStyleBackColor = false;
            this.btnResCamUp.Click += new System.EventHandler(this.BtnResCamUp_Click);
            // 
            // btnResScan2
            // 
            this.btnResScan2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(57)))), ((int)(((byte)(43)))));
            this.btnResScan2.FlatAppearance.BorderSize = 0;
            this.btnResScan2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResScan2.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnResScan2.ForeColor = System.Drawing.Color.White;
            this.btnResScan2.Location = new System.Drawing.Point(279, 234);
            this.btnResScan2.Name = "btnResScan2";
            this.btnResScan2.Size = new System.Drawing.Size(110, 34);
            this.btnResScan2.TabIndex = 19;
            this.btnResScan2.Text = "扫码NG = 2";
            this.btnResScan2.UseVisualStyleBackColor = false;
            this.btnResScan2.Click += new System.EventHandler(this.BtnResScan2_Click);
            // 
            // btnResScan1
            // 
            this.btnResScan1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnResScan1.FlatAppearance.BorderSize = 0;
            this.btnResScan1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResScan1.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnResScan1.ForeColor = System.Drawing.Color.White;
            this.btnResScan1.Location = new System.Drawing.Point(156, 234);
            this.btnResScan1.Name = "btnResScan1";
            this.btnResScan1.Size = new System.Drawing.Size(110, 34);
            this.btnResScan1.TabIndex = 18;
            this.btnResScan1.Text = "扫码OK = 1";
            this.btnResScan1.UseVisualStyleBackColor = false;
            this.btnResScan1.Click += new System.EventHandler(this.BtnResScan1_Click);
            // 
            // btnResScan0
            // 
            this.btnResScan0.FlatAppearance.BorderSize = 0;
            this.btnResScan0.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResScan0.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnResScan0.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnResScan0.Location = new System.Drawing.Point(398, 234);
            this.btnResScan0.Name = "btnResScan0";
            this.btnResScan0.Size = new System.Drawing.Size(110, 34);
            this.btnResScan0.TabIndex = 17;
            this.btnResScan0.Text = "扫码结果 = 0";
            this.btnResScan0.UseVisualStyleBackColor = true;
            this.btnResScan0.Click += new System.EventHandler(this.BtnResScan0_Click);
            // 
            // txtModel
            // 
            this.txtModel.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.txtModel.Location = new System.Drawing.Point(156, 199);
            this.txtModel.MaxLength = 10;
            this.txtModel.Name = "txtModel";
            this.txtModel.Size = new System.Drawing.Size(170, 25);
            this.txtModel.TabIndex = 16;
            this.txtModel.Text = "Z1212";
            // 
            // btnWriteModel
            // 
            this.btnWriteModel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnWriteModel.FlatAppearance.BorderSize = 0;
            this.btnWriteModel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWriteModel.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnWriteModel.ForeColor = System.Drawing.Color.White;
            this.btnWriteModel.Location = new System.Drawing.Point(24, 194);
            this.btnWriteModel.Name = "btnWriteModel";
            this.btnWriteModel.Size = new System.Drawing.Size(120, 34);
            this.btnWriteModel.TabIndex = 15;
            this.btnWriteModel.Text = "写产品型号";
            this.btnWriteModel.UseVisualStyleBackColor = false;
            this.btnWriteModel.Click += new System.EventHandler(this.BtnWriteModel_Click);
            // 
            // lblMoveVal
            // 
            this.lblMoveVal.AutoSize = true;
            this.lblMoveVal.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblMoveVal.ForeColor = System.Drawing.Color.Gray;
            this.lblMoveVal.Location = new System.Drawing.Point(516, 282);
            this.lblMoveVal.Name = "lblMoveVal";
            this.lblMoveVal.Size = new System.Drawing.Size(51, 20);
            this.lblMoveVal.TabIndex = 14;
            this.lblMoveVal.Text = "？未读";
            // 
            // btnReadCamReq
            // 
            this.btnReadCamReq.FlatAppearance.BorderSize = 0;
            this.btnReadCamReq.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReadCamReq.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnReadCamReq.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnReadCamReq.Location = new System.Drawing.Point(24, 274);
            this.btnReadCamReq.Name = "btnReadCamReq";
            this.btnReadCamReq.Size = new System.Drawing.Size(120, 34);
            this.btnReadCamReq.TabIndex = 13;
            this.btnReadCamReq.Text = "读相机请求";
            this.btnReadCamReq.UseVisualStyleBackColor = true;
            this.btnReadCamReq.Click += new System.EventHandler(this.BtnReadCamReq_Click);
            // 
            // btnReadScanReq
            // 
            this.btnReadScanReq.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnReadScanReq.FlatAppearance.BorderSize = 0;
            this.btnReadScanReq.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReadScanReq.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.btnReadScanReq.ForeColor = System.Drawing.Color.White;
            this.btnReadScanReq.Location = new System.Drawing.Point(24, 234);
            this.btnReadScanReq.Name = "btnReadScanReq";
            this.btnReadScanReq.Size = new System.Drawing.Size(120, 34);
            this.btnReadScanReq.TabIndex = 12;
            this.btnReadScanReq.Text = "读扫码请求";
            this.btnReadScanReq.UseVisualStyleBackColor = false;
            this.btnReadScanReq.Click += new System.EventHandler(this.BtnReadScanReq_Click);
            // 
            // lblPlcState
            // 
            this.lblPlcState.AutoSize = true;
            this.lblPlcState.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblPlcState.ForeColor = System.Drawing.Color.Red;
            this.lblPlcState.Location = new System.Drawing.Point(24, 41);
            this.lblPlcState.Name = "lblPlcState";
            this.lblPlcState.Size = new System.Drawing.Size(49, 20);
            this.lblPlcState.TabIndex = 0;
            this.lblPlcState.Text = "○ 断连";
            // 
            // grpLog
            // 
            this.grpLog.Controls.Add(this.txtLog);
            this.grpLog.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.grpLog.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpLog.Location = new System.Drawing.Point(16, 832);
            this.grpLog.Name = "grpLog";
            this.grpLog.Size = new System.Drawing.Size(768, 160);
            this.grpLog.TabIndex = 2;
            this.grpLog.TabStop = false;
            this.grpLog.Text = "操作日志";
            // 
            // txtLog
            // 
            this.txtLog.BackColor = System.Drawing.Color.White;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 9.5F);
            this.txtLog.Location = new System.Drawing.Point(16, 34);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(736, 116);
            this.txtLog.TabIndex = 0;
            // 
            // DeveloperModeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 990);
            this.Controls.Add(this.grpAccount);
            this.Controls.Add(this.grpLog);
            this.Controls.Add(this.grpPlc);
            this.Controls.Add(this.grpScanner);
            this.Controls.Add(this.grpCamera);
            this.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DeveloperModeForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "开发者模式";
            this.grpAccount.ResumeLayout(false);
            this.grpAccount.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAccounts)).EndInit();
            this.grpCamera.ResumeLayout(false);
            this.grpCamera.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picTestShot)).EndInit();
            this.grpScanner.ResumeLayout(false);
            this.grpScanner.PerformLayout();
            this.grpPlc.ResumeLayout(false);
            this.grpPlc.PerformLayout();
            this.grpLog.ResumeLayout(false);
            this.grpLog.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        // 设计器声明的字段（命名遵循匈牙利前缀规范：cmb/lbl/btn/txt）
        private GroupBox grpAccount;
        private DataGridView dgvAccounts;
        private DataGridViewTextBoxColumn colAccUser;
        private DataGridViewTextBoxColumn colAccRole;
        private DataGridViewTextBoxColumn colAccEnabled;
        private DataGridViewTextBoxColumn colAccPwd;
        private Label lblAccTip;
        private Label lblAccAccount;
        private Label lblNewPwd;
        private TextBox txtNewPwd;
        private Label lblNewPwd2;
        private TextBox txtNewPwd2;
        private Button btnChangePwd;
        private GroupBox grpCamera;
        private ComboBox cmbCamera;
        private Label lblCamState;
        private Button btnTrigger;
        private Button btnTriggerRead;
        private Label lblCamResult;
        private Button btnReadProgramNo;
        private Label lblCurrentProgram;
        private Button btnSwProg1;
        private Button btnSwProg2;
        private GroupBox grpScanner;
        private ComboBox cmbScanner;
        private Label lblScannerState;
        private Label lblScannerCode;
        private Button btnScannerTrigger;
        private Button btnShowScannerFail;
        private Label lblScannerHint;
        private GroupBox grpPlc;
        private Label lblPlcState;
        private Label lblOffset;
        private TextBox txtOffset;
        private Label lblOffsetTip;
        private Label lblReadAddrTip;
        private TextBox txtReadAddr;
        private Button btnReadReg;
        private Label lblReadValTip;
        private TextBox txtReadVal;
        private Label lblWriteValTip;
        private TextBox txtWriteAddr;
        private TextBox txtWriteVal;
        private Button btnWriteReg;
        private Button btnReadScanReq;
        private Button btnReadCamReq;
        private Label lblMoveVal;
        private Button btnWriteModel;
        private TextBox txtModel;
        private Button btnResScan0;
        private Button btnResScan1;
        private Button btnResScan2;
        private Button btnResCamUp;
        private Button btnResCamDown;
        private Button btnResCamReset;
        private GroupBox grpLog;
        private TextBox txtLog;
        private PictureBox picTestShot;
        private Label lblTestImagePath;
    }
}