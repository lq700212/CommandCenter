using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// DevTestForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 布局说明：四个 GroupBox 纵向排布——
    ///   grpCamera（相机测试区）/ grpScanner（扫码枪测试区）/ grpPlc（PLC 测试区）/ grpLog（日志区）。
    /// 详细 ASCII 布局图见 DevTestForm.cs 类注释，本文件负责控件外观与坐标。
    /// 控件命名遵循匈牙利前缀：cmb=ComboBox / lbl=Label / btn=Button / txt=TextBox。
    ///
    /// 【垂直居中对齐约定】PLC 区每行由按钮（高34）、文本框（高25）、标签（高19）混排，
    /// 均按"行中心线"对齐：btn 直接定位在行顶，txt 顶=行顶+4，lbl 顶=行顶+7（差值
    /// (34-25)/2≈4、(34-19)/2≈7），保证一行内各控件上下视觉居中。
    /// </summary>
    partial class DevTestForm
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
            this.grpCamera = new System.Windows.Forms.GroupBox();
            this.lblCamResult = new System.Windows.Forms.Label();
            this.btnTriggerRead = new System.Windows.Forms.Button();
            this.btnTrigger = new System.Windows.Forms.Button();
            this.lblCamState = new System.Windows.Forms.Label();
            this.cmbCamera = new System.Windows.Forms.ComboBox();
            this.btnReadProgramNo = new System.Windows.Forms.Button();
            this.lblCurrentProgram = new System.Windows.Forms.Label();
            this.btnSwProg1 = new System.Windows.Forms.Button();
            this.btnSwProg2 = new System.Windows.Forms.Button();
            this.grpScanner = new System.Windows.Forms.GroupBox();
            this.btnScannerTrigger = new System.Windows.Forms.Button();
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
            this.btnWriteRecipe = new System.Windows.Forms.Button();
            this.txtRecipe = new System.Windows.Forms.TextBox();
            this.btnDone2 = new System.Windows.Forms.Button();
            this.btnDone1 = new System.Windows.Forms.Button();
            this.btnDone0 = new System.Windows.Forms.Button();
            this.btnStartOff = new System.Windows.Forms.Button();
            this.btnStartOn = new System.Windows.Forms.Button();
            this.lblMoveVal = new System.Windows.Forms.Label();
            this.btnClearMoveDone = new System.Windows.Forms.Button();
            this.btnReadMoveDone = new System.Windows.Forms.Button();
            this.lblPlcState = new System.Windows.Forms.Label();
            this.grpLog = new System.Windows.Forms.GroupBox();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.picTestShot = new System.Windows.Forms.PictureBox();
            this.lblTestImagePath = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.picTestShot)).BeginInit();
            this.grpCamera.SuspendLayout();
            this.grpScanner.SuspendLayout();
            this.grpPlc.SuspendLayout();
            this.grpLog.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpCamera
            // 相机测试区：相机选择下拉框 + 连接状态 + 触发按钮 + 判定结果
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
            this.grpCamera.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.grpCamera.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpCamera.Location = new System.Drawing.Point(16, 12);
            this.grpCamera.Name = "grpCamera";
            this.grpCamera.Size = new System.Drawing.Size(768, 212);
            this.grpCamera.TabIndex = 0;
            this.grpCamera.TabStop = false;
            this.grpCamera.Text = "相机测试";
            // 
            // cmbCamera
            // 相机选择下拉框：列出主窗体传入的所有相机（"相机N IP:端口"），默认选第 0 台
            // 
            this.cmbCamera.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCamera.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.cmbCamera.FormattingEnabled = true;
            this.cmbCamera.Location = new System.Drawing.Point(90, 36);
            this.cmbCamera.Name = "cmbCamera";
            this.cmbCamera.Size = new System.Drawing.Size(220, 27);
            this.cmbCamera.TabIndex = 0;
            // 
            // lblCamState
            // 当前所选相机连接状态：绿=已连接，红=断连（跟随主窗体 ConnectionMonitor）
            // 与下拉框同行（行中心 y=45）：cmb 顶36（高27≈34 阈值），lbl 顶+7=43
            // 
            this.lblCamState.AutoSize = true;
            this.lblCamState.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblCamState.ForeColor = System.Drawing.Color.Red;
            this.lblCamState.Location = new System.Drawing.Point(340, 43);
            this.lblCamState.Name = "lblCamState";
            this.lblCamState.Size = new System.Drawing.Size(64, 19);
            this.lblCamState.TabIndex = 1;
            this.lblCamState.Text = "○ 断连";
            // 
            // btnTrigger
            // 仅触发拍照（T1）：相机拍一张但不读判定，返回是否收到回显
            // 
            this.btnTrigger.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnTrigger.FlatAppearance.BorderSize = 0;
            this.btnTrigger.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTrigger.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnTrigger.ForeColor = System.Drawing.Color.White;
            this.btnTrigger.Location = new System.Drawing.Point(24, 84);
            this.btnTrigger.Name = "btnTrigger";
            this.btnTrigger.Size = new System.Drawing.Size(150, 36);
            this.btnTrigger.TabIndex = 2;
            this.btnTrigger.Text = "仅触发拍照 T1";
            this.btnTrigger.UseVisualStyleBackColor = false;
            this.btnTrigger.Click += new System.EventHandler(this.BtnTrigger_Click);
            // 
            // btnTriggerRead
            // 触发+读判定（T2）：一次完成拍照+取判定，结果直接显示 OK/NG
            // 
            this.btnTriggerRead.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnTriggerRead.FlatAppearance.BorderSize = 0;
            this.btnTriggerRead.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTriggerRead.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnTriggerRead.ForeColor = System.Drawing.Color.White;
            this.btnTriggerRead.Location = new System.Drawing.Point(186, 84);
            this.btnTriggerRead.Name = "btnTriggerRead";
            this.btnTriggerRead.Size = new System.Drawing.Size(176, 36);
            this.btnTriggerRead.TabIndex = 3;
            this.btnTriggerRead.Text = "触发+判定T2（取图存图）";
            this.btnTriggerRead.UseVisualStyleBackColor = false;
            this.btnTriggerRead.Click += new System.EventHandler(this.BtnTriggerRead_Click);
            // 
            // lblCamResult
            // 相机最近一次操作结果：OK=绿 / NG=红 / 失败=灰
            // 
            this.lblCamResult.AutoSize = true;
            this.lblCamResult.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblCamResult.ForeColor = System.Drawing.Color.Gray;
            this.lblCamResult.Location = new System.Drawing.Point(24, 136);
            this.lblCamResult.Name = "lblCamResult";
            this.lblCamResult.Size = new System.Drawing.Size(112, 19);
            this.lblCamResult.TabIndex = 4;
            this.lblCamResult.Text = "（尚未操作相机）";
            // 
            // btnReadProgramNo
            // 读当前程序号（PR 指令，V1.12.19）：联调时先读回相机当前程序号，
            // 确认 PW 切换是否真正生效（对应 KeyenceIV4Camera.ReadProgramNo）。
            // 
            this.btnReadProgramNo.FlatAppearance.BorderSize = 0;
            this.btnReadProgramNo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReadProgramNo.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnReadProgramNo.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnReadProgramNo.Location = new System.Drawing.Point(24, 178);
            this.btnReadProgramNo.Name = "btnReadProgramNo";
            this.btnReadProgramNo.Size = new System.Drawing.Size(130, 34);
            this.btnReadProgramNo.TabIndex = 5;
            this.btnReadProgramNo.Text = "读当前程序号";
            this.btnReadProgramNo.UseVisualStyleBackColor = true;
            this.btnReadProgramNo.Click += new System.EventHandler(this.BtnReadProgramNo_Click);
            // 
            // lblCurrentProgram
            // 当前程序号显示（V1.12.19）：ReadProgramNo 读回后显示 P000/P001/P002…
            // 
            this.lblCurrentProgram.AutoSize = true;
            this.lblCurrentProgram.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblCurrentProgram.ForeColor = System.Drawing.Color.Gray;
            this.lblCurrentProgram.Location = new System.Drawing.Point(170, 185);
            this.lblCurrentProgram.Name = "lblCurrentProgram";
            this.lblCurrentProgram.Size = new System.Drawing.Size(112, 19);
            this.lblCurrentProgram.TabIndex = 6;
            this.lblCurrentProgram.Text = "当前程序：?";
            // 
            // btnSwProg1
            // 切换到 P001（V1.12.19）：发 PW,001 切到相机程序 1（基恩士还在调试，仅供前期验证）
            // 
            this.btnSwProg1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnSwProg1.FlatAppearance.BorderSize = 0;
            this.btnSwProg1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSwProg1.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnSwProg1.ForeColor = System.Drawing.Color.White;
            this.btnSwProg1.Location = new System.Drawing.Point(284, 178);
            this.btnSwProg1.Name = "btnSwProg1";
            this.btnSwProg1.Size = new System.Drawing.Size(150, 34);
            this.btnSwProg1.TabIndex = 7;
            this.btnSwProg1.Text = "切换程序 → P001";
            this.btnSwProg1.UseVisualStyleBackColor = false;
            this.btnSwProg1.Click += new System.EventHandler(this.BtnSwProg1_Click);
            // 
            // btnSwProg2
            // 切换到 P002（V1.12.19）：发 PW,002 切到相机程序 2
            // 
            this.btnSwProg2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnSwProg2.FlatAppearance.BorderSize = 0;
            this.btnSwProg2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSwProg2.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnSwProg2.ForeColor = System.Drawing.Color.White;
            this.btnSwProg2.Location = new System.Drawing.Point(438, 178);
            this.btnSwProg2.Name = "btnSwProg2";
            this.btnSwProg2.Size = new System.Drawing.Size(150, 34);
            this.btnSwProg2.TabIndex = 8;
            this.btnSwProg2.Text = "切换程序 → P002";
            this.btnSwProg2.UseVisualStyleBackColor = false;
            this.btnSwProg2.Click += new System.EventHandler(this.BtnSwProg2_Click);
            // 
            // picTestShot
            // 最近一次 T2 拍照取回的图片预览（V1.12.24）：触发拍照后从 FTP 取图目录拿最新 jpeg 闪图。
            // 黑底 + Zoom 居中缩放，避免未加载状态显示空白刺眼。
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
            // 最近一次取图存档路径（V1.12.24）：T2 触发拍照成功后显示"已存图：完整路径"；
            // 取图/存图失败则红字提示。路径可能很长，超出宽度自动加省略号（完整路径看操作日志）。
            // 
            this.lblTestImagePath.AutoEllipsis = true;
            this.lblTestImagePath.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.lblTestImagePath.ForeColor = System.Drawing.Color.Gray;
            this.lblTestImagePath.Location = new System.Drawing.Point(592, 180);
            this.lblTestImagePath.Name = "lblTestImagePath";
            this.lblTestImagePath.Size = new System.Drawing.Size(160, 19);
            this.lblTestImagePath.TabIndex = 10;
            this.lblTestImagePath.Text = "（最近未测试存图）";
            // 
            // grpScanner
            // 扫码枪测试区：多台扫码枪选择 + 连接状态 + 最近读到条码大字展示
            // 扫码枪为"设备主动推码"模式：主窗体已 Open 并持续监听，这里订阅事件实时展示
            // 
            this.grpScanner.Controls.Add(this.btnScannerTrigger);
            this.grpScanner.Controls.Add(this.lblScannerCode);
            this.grpScanner.Controls.Add(this.lblScannerHint);
            this.grpScanner.Controls.Add(this.lblScannerState);
            this.grpScanner.Controls.Add(this.cmbScanner);
            this.grpScanner.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.grpScanner.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpScanner.Location = new System.Drawing.Point(16, 232);
            this.grpScanner.Name = "grpScanner";
            this.grpScanner.Size = new System.Drawing.Size(768, 128);
            this.grpScanner.TabIndex = 2;
            this.grpScanner.TabStop = false;
            this.grpScanner.Text = "扫码枪测试";
            // 
            // cmbScanner
            // 扫码枪选择下拉框：TCP 显示 IP:端口，串口显示 COM口号+波特率
            // 
            this.cmbScanner.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbScanner.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.cmbScanner.FormattingEnabled = true;
            this.cmbScanner.Location = new System.Drawing.Point(102, 34);
            this.cmbScanner.Name = "cmbScanner";
            this.cmbScanner.Size = new System.Drawing.Size(220, 27);
            this.cmbScanner.TabIndex = 0;
            // 
            // lblScannerState
            // 当前所选扫码枪连接状态：绿=已连接/已打开，红=断连
            // 
            this.lblScannerState.AutoSize = true;
            this.lblScannerState.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblScannerState.ForeColor = System.Drawing.Color.Red;
            this.lblScannerState.Location = new System.Drawing.Point(352, 41);
            this.lblScannerState.Name = "lblScannerState";
            this.lblScannerState.Size = new System.Drawing.Size(64, 19);
            this.lblScannerState.TabIndex = 1;
            this.lblScannerState.Text = "○ 断连";
            // 
            // lblScannerCode
            // 最近读到条码大字展示：扫码枪读到的码实时显示在这里（绿字醒目）
            // 
            this.lblScannerCode.AutoSize = true;
            this.lblScannerCode.Font = new System.Drawing.Font("Microsoft YaHei", 14F, System.Drawing.FontStyle.Bold);
            this.lblScannerCode.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(174)))), ((int)(((byte)(96)))));
            this.lblScannerCode.Location = new System.Drawing.Point(24, 74);
            this.lblScannerCode.MaximumSize = new System.Drawing.Size(720, 24);
            this.lblScannerCode.Name = "lblScannerCode";
            this.lblScannerCode.Size = new System.Drawing.Size(300, 26);
            this.lblScannerCode.TabIndex = 2;
            this.lblScannerCode.Text = "（尚未读到条码）";
            // 
            // lblScannerHint
            // 操作提示：扫码枪是主动推码，测试时直接扫条码即可，读到会自动显示
            // 
            this.lblScannerHint.AutoSize = true;
            this.lblScannerHint.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.lblScannerHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblScannerHint.Location = new System.Drawing.Point(432, 43);
            this.lblScannerHint.Name = "lblScannerHint";
            this.lblScannerHint.Size = new System.Drawing.Size(300, 17);
            this.lblScannerHint.TabIndex = 3;
            this.lblScannerHint.Text = "把条码放到扫码枪下读，读到会实时显示（共用连接）";
            // 
            // btnScannerTrigger
            // 发送触发指令按钮：基恩士 SR 无协议模式下，连接后需发 LON 才读码；
            // 若扫码枪突然不读，点此按钮可手动再发一次（内部 SendTrigger，后台线程）
            // 
            this.btnScannerTrigger.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.btnScannerTrigger.Location = new System.Drawing.Point(350, 92);
            this.btnScannerTrigger.Name = "btnScannerTrigger";
            this.btnScannerTrigger.Size = new System.Drawing.Size(180, 27);
            this.btnScannerTrigger.TabIndex = 4;
            this.btnScannerTrigger.Text = "发送触发指令";
            this.btnScannerTrigger.UseVisualStyleBackColor = true;
            // 
            // grpPlc
            // PLC 测试区（V1.12.0 增强）：协议偏移量配置 + 读地址/写地址测试 + 业务信号 + 配方。
            // 布局规则：每行由按钮(高34)/文本框(高25)/标签(高19)混排，控件按行中心线对齐
            // （txt 顶=行顶+4、lbl 顶=行顶+7），保证一行内上下视觉居中，见文件头说明。
            // 行划分（组内 y 坐标）：
            //   ☆常规行高 40px☆
            //   状态行  y=34
            //   偏移行  y=74（协议偏移量，读写地址自动加上该值）
            //   读测试行 y=112（读地址→读值）
            //   写测试行 y=152（写地址+写值）
            //   到位行  y=194 / 触发行 y=240 / 完成行 y=286 / 配分行 y=332
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
            this.grpPlc.Controls.Add(this.btnWriteRecipe);
            this.grpPlc.Controls.Add(this.txtRecipe);
            this.grpPlc.Controls.Add(this.btnDone2);
            this.grpPlc.Controls.Add(this.btnDone1);
            this.grpPlc.Controls.Add(this.btnDone0);
            this.grpPlc.Controls.Add(this.btnStartOff);
            this.grpPlc.Controls.Add(this.btnStartOn);
            this.grpPlc.Controls.Add(this.lblMoveVal);
            this.grpPlc.Controls.Add(this.btnClearMoveDone);
            this.grpPlc.Controls.Add(this.btnReadMoveDone);
            this.grpPlc.Controls.Add(this.lblPlcState);
            this.grpPlc.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.grpPlc.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpPlc.Location = new System.Drawing.Point(16, 368);
            this.grpPlc.Name = "grpPlc";
            this.grpPlc.Size = new System.Drawing.Size(768, 380);
            this.grpPlc.TabIndex = 1;
            this.grpPlc.TabStop = false;
            this.grpPlc.Text = "PLC 测试";
            // 
            // lblPlcState
            // PLC 连接状态：绿=已连接，红=断连（跟随主窗体 ConnectionMonitor）。状态行
            // 
            this.lblPlcState.AutoSize = true;
            this.lblPlcState.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblPlcState.ForeColor = System.Drawing.Color.Red;
            this.lblPlcState.Location = new System.Drawing.Point(24, 41);
            this.lblPlcState.Name = "lblPlcState";
            this.lblPlcState.Size = new System.Drawing.Size(64, 19);
            this.lblPlcState.TabIndex = 0;
            this.lblPlcState.Text = "○ 断连";
            // 
            // lblOffset
            // 协议偏移量标签：偏移行。实际读写地址 = 输入地址 + 偏移量（见 BtnReadReg_Click）
            // 
            this.lblOffset.AutoSize = true;
            this.lblOffset.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblOffset.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblOffset.Location = new System.Drawing.Point(24, 81);
            this.lblOffset.Name = "lblOffset";
            this.lblOffset.Size = new System.Drawing.Size(91, 19);
            this.lblOffset.TabIndex = 1;
            this.lblOffset.Text = "协议偏移量:";
            // 
            // txtOffset
            // 协议偏移量输入框（0~65535）：输入地址加此偏移得到实际 D 地址。
            // 默认 0 = 按 D 地址直接读写（项目约定，无需换算）。
            // 文本框高25：行中心=74+17=91 → 顶=91-12=79
            // 
            this.txtOffset.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtOffset.Location = new System.Drawing.Point(128, 79);
            this.txtOffset.Name = "txtOffset";
            this.txtOffset.Size = new System.Drawing.Size(70, 25);
            this.txtOffset.TabIndex = 2;
            this.txtOffset.Text = "0";
            // 
            // lblOffsetTip
            // 偏移量说明（灰色小字）：解释偏移量含义，对齐行内
            // 
            this.lblOffsetTip.AutoSize = true;
            this.lblOffsetTip.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.lblOffsetTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblOffsetTip.Location = new System.Drawing.Point(210, 82);
            this.lblOffsetTip.Name = "lblOffsetTip";
            this.lblOffsetTip.Size = new System.Drawing.Size(330, 17);
            this.lblOffsetTip.TabIndex = 3;
            this.lblOffsetTip.Text = "实际D地址 = 输入地址 + 偏移量（默认0按D地址读写）";
            // 
            // lblReadAddrTip
            // 读地址行左侧标签
            // 
            this.lblReadAddrTip.AutoSize = true;
            this.lblReadAddrTip.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblReadAddrTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblReadAddrTip.Location = new System.Drawing.Point(24, 119);
            this.lblReadAddrTip.Name = "lblReadAddrTip";
            this.lblReadAddrTip.Size = new System.Drawing.Size(91, 19);
            this.lblReadAddrTip.TabIndex = 4;
            this.lblReadAddrTip.Text = "读地址测试:";
            // 
            // txtReadAddr
            // 读测试的目标 D 地址（实际 = 输入 + 偏移量）。行中心=112+17=129 → 顶125
            // 
            this.txtReadAddr.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtReadAddr.Location = new System.Drawing.Point(128, 117);
            this.txtReadAddr.Name = "txtReadAddr";
            this.txtReadAddr.Size = new System.Drawing.Size(70, 25);
            this.txtReadAddr.TabIndex = 5;
            this.txtReadAddr.Text = "100";
            // 
            // btnReadReg
            // 读测试按钮：从读地址读取一个保持寄存器值（结果写入 txtReadVal）
            // 
            this.btnReadReg.FlatAppearance.BorderSize = 0;
            this.btnReadReg.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReadReg.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnReadReg.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnReadReg.Location = new System.Drawing.Point(210, 112);
            this.btnReadReg.Name = "btnReadReg";
            this.btnReadReg.Size = new System.Drawing.Size(60, 34);
            this.btnReadReg.TabIndex = 6;
            this.btnReadReg.Text = "读 取";
            this.btnReadReg.UseVisualStyleBackColor = true;
            this.btnReadReg.Click += new System.EventHandler(this.BtnReadReg_Click);
            // 
            // lblReadValTip
            // 读测试行内"读到的值"引导标签：紧跟读取按钮右侧，指向只读结果框
            // 
            this.lblReadValTip.AutoSize = true;
            this.lblReadValTip.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblReadValTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblReadValTip.Location = new System.Drawing.Point(286, 119);
            this.lblReadValTip.Name = "lblReadValTip";
            this.lblReadValTip.Size = new System.Drawing.Size(91, 19);
            this.lblReadValTip.TabIndex = 7;
            this.lblReadValTip.Text = "→ 读到的值:";
            // 
            // txtReadVal
            // 读结果展示（只读）：显示从读地址读出的寄存器值。读测试行右侧
            // 
            this.txtReadVal.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtReadVal.Location = new System.Drawing.Point(380, 116);
            this.txtReadVal.Name = "txtReadVal";
            this.txtReadVal.ReadOnly = true;
            this.txtReadVal.Size = new System.Drawing.Size(80, 25);
            this.txtReadVal.TabIndex = 8;
            this.txtReadVal.Text = "？";
            // 
            // lblWriteValTip
            // 写测试行左侧标签
            // 
            this.lblWriteValTip.AutoSize = true;
            this.lblWriteValTip.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblWriteValTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblWriteValTip.Location = new System.Drawing.Point(24, 159);
            this.lblWriteValTip.Name = "lblWriteValTip";
            this.lblWriteValTip.Size = new System.Drawing.Size(91, 19);
            this.lblWriteValTip.TabIndex = 7;
            this.lblWriteValTip.Text = "写地址测试:";
            // 
            // txtWriteAddr
            // 写测试的目标 D 地址（实际 = 输入 + 偏移量）。写测试行
            // 
            this.txtWriteAddr.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtWriteAddr.Location = new System.Drawing.Point(128, 156);
            this.txtWriteAddr.Name = "txtWriteAddr";
            this.txtWriteAddr.Size = new System.Drawing.Size(70, 25);
            this.txtWriteAddr.TabIndex = 9;
            this.txtWriteAddr.Text = "101";
            // 
            // txtWriteVal
            // 写测试的值：写入目标地址的数值
            // 
            this.txtWriteVal.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtWriteVal.Location = new System.Drawing.Point(210, 156);
            this.txtWriteVal.Name = "txtWriteVal";
            this.txtWriteVal.Size = new System.Drawing.Size(70, 25);
            this.txtWriteVal.TabIndex = 10;
            this.txtWriteVal.Text = "1";
            // 
            // btnWriteReg
            // 写测试按钮：把写值写入写地址
            // 
            this.btnWriteReg.FlatAppearance.BorderSize = 0;
            this.btnWriteReg.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWriteReg.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnWriteReg.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnWriteReg.Location = new System.Drawing.Point(292, 152);
            this.btnWriteReg.Name = "btnWriteReg";
            this.btnWriteReg.Size = new System.Drawing.Size(60, 34);
            this.btnWriteReg.TabIndex = 11;
            this.btnWriteReg.Text = "写 入";
            this.btnWriteReg.UseVisualStyleBackColor = true;
            this.btnWriteReg.Click += new System.EventHandler(this.BtnWriteReg_Click);
            // 
            // btnReadMoveDone
            // 读到位信号：返回 PLC 的到位寄存器是否为 1（到位行）
            // 
            this.btnReadMoveDone.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnReadMoveDone.FlatAppearance.BorderSize = 0;
            this.btnReadMoveDone.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnReadMoveDone.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnReadMoveDone.ForeColor = System.Drawing.Color.White;
            this.btnReadMoveDone.Location = new System.Drawing.Point(24, 194);
            this.btnReadMoveDone.Name = "btnReadMoveDone";
            this.btnReadMoveDone.Size = new System.Drawing.Size(120, 34);
            this.btnReadMoveDone.TabIndex = 12;
            this.btnReadMoveDone.Text = "读到位信号";
            this.btnReadMoveDone.UseVisualStyleBackColor = false;
            this.btnReadMoveDone.Click += new System.EventHandler(this.BtnReadMoveDone_Click);
            // 
            // btnClearMoveDone
            // 清到位信号：到位寄存器写 0 复位，防止重复处理
            // 
            this.btnClearMoveDone.FlatAppearance.BorderSize = 0;
            this.btnClearMoveDone.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClearMoveDone.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnClearMoveDone.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnClearMoveDone.Location = new System.Drawing.Point(154, 194);
            this.btnClearMoveDone.Name = "btnClearMoveDone";
            this.btnClearMoveDone.Size = new System.Drawing.Size(120, 34);
            this.btnClearMoveDone.TabIndex = 13;
            this.btnClearMoveDone.Text = "清到位信号";
            this.btnClearMoveDone.UseVisualStyleBackColor = true;
            this.btnClearMoveDone.Click += new System.EventHandler(this.BtnClearMoveDone_Click);
            // 
            // lblMoveVal
            // 到位信号当前值显示：1（已到位）=绿 / 0（未到位）=灰
            // 
            this.lblMoveVal.AutoSize = true;
            this.lblMoveVal.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.lblMoveVal.ForeColor = System.Drawing.Color.Gray;
            this.lblMoveVal.Location = new System.Drawing.Point(300, 201);
            this.lblMoveVal.Name = "lblMoveVal";
            this.lblMoveVal.Size = new System.Drawing.Size(56, 19);
            this.lblMoveVal.TabIndex = 14;
            this.lblMoveVal.Text = "？未读";
            // 
            // btnStartOn
            // 触发信号置 1（通知 PLC 开始工作）（触发行）
            // 
            this.btnStartOn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnStartOn.FlatAppearance.BorderSize = 0;
            this.btnStartOn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStartOn.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnStartOn.ForeColor = System.Drawing.Color.White;
            this.btnStartOn.Location = new System.Drawing.Point(24, 232);
            this.btnStartOn.Name = "btnStartOn";
            this.btnStartOn.Size = new System.Drawing.Size(120, 34);
            this.btnStartOn.TabIndex = 15;
            this.btnStartOn.Text = "触发信号 = 1";
            this.btnStartOn.UseVisualStyleBackColor = false;
            this.btnStartOn.Click += new System.EventHandler(this.BtnStartOn_Click);
            // 
            // btnStartOff
            // 触发信号置 0
            // 
            this.btnStartOff.FlatAppearance.BorderSize = 0;
            this.btnStartOff.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStartOff.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnStartOff.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnStartOff.Location = new System.Drawing.Point(154, 232);
            this.btnStartOff.Name = "btnStartOff";
            this.btnStartOff.Size = new System.Drawing.Size(120, 34);
            this.btnStartOff.TabIndex = 16;
            this.btnStartOff.Text = "触发信号 = 0";
            this.btnStartOff.UseVisualStyleBackColor = true;
            this.btnStartOff.Click += new System.EventHandler(this.BtnStartOff_Click);
            // 
            // btnDone0
            // 完成信号写 0（复位）（完成行）
            // 
            this.btnDone0.FlatAppearance.BorderSize = 0;
            this.btnDone0.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDone0.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnDone0.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnDone0.Location = new System.Drawing.Point(24, 278);
            this.btnDone0.Name = "btnDone0";
            this.btnDone0.Size = new System.Drawing.Size(120, 34);
            this.btnDone0.TabIndex = 17;
            this.btnDone0.Text = "完成信号 = 0 复位";
            this.btnDone0.UseVisualStyleBackColor = true;
            this.btnDone0.Click += new System.EventHandler(this.BtnDone0_Click);
            // 
            // btnDone1
            // 完成信号写 1（拍照成功）
            // 
            this.btnDone1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnDone1.FlatAppearance.BorderSize = 0;
            this.btnDone1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDone1.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnDone1.ForeColor = System.Drawing.Color.White;
            this.btnDone1.Location = new System.Drawing.Point(154, 278);
            this.btnDone1.Name = "btnDone1";
            this.btnDone1.Size = new System.Drawing.Size(120, 34);
            this.btnDone1.TabIndex = 18;
            this.btnDone1.Text = "完成信号 = 1 成功";
            this.btnDone1.UseVisualStyleBackColor = false;
            this.btnDone1.Click += new System.EventHandler(this.BtnDone1_Click);
            // 
            // btnDone2
            // 完成信号写 2（取像失败）
            // 
            this.btnDone2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(57)))), ((int)(((byte)(43)))));
            this.btnDone2.FlatAppearance.BorderSize = 0;
            this.btnDone2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDone2.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnDone2.ForeColor = System.Drawing.Color.White;
            this.btnDone2.Location = new System.Drawing.Point(284, 278);
            this.btnDone2.Name = "btnDone2";
            this.btnDone2.Size = new System.Drawing.Size(130, 34);
            this.btnDone2.TabIndex = 19;
            this.btnDone2.Text = "完成信号 = 2 失败";
            this.btnDone2.UseVisualStyleBackColor = false;
            this.btnDone2.Click += new System.EventHandler(this.BtnDone2_Click);
            // 
            // txtRecipe
            // 配方号输入框：写 ASCII 数字串到 PLC 配方寄存器（配分行）
            // 
            this.txtRecipe.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.txtRecipe.Location = new System.Drawing.Point(128, 337);
            this.txtRecipe.Name = "txtRecipe";
            this.txtRecipe.Size = new System.Drawing.Size(70, 25);
            this.txtRecipe.TabIndex = 20;
            this.txtRecipe.Text = "1";
            // 
            // btnWriteRecipe
            // 下发配方按钮
            // 
            this.btnWriteRecipe.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnWriteRecipe.FlatAppearance.BorderSize = 0;
            this.btnWriteRecipe.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWriteRecipe.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnWriteRecipe.ForeColor = System.Drawing.Color.White;
            this.btnWriteRecipe.Location = new System.Drawing.Point(24, 332);
            this.btnWriteRecipe.Name = "btnWriteRecipe";
            this.btnWriteRecipe.Size = new System.Drawing.Size(90, 34);
            this.btnWriteRecipe.TabIndex = 21;
            this.btnWriteRecipe.Text = "下发配方";
            this.btnWriteRecipe.UseVisualStyleBackColor = false;
            this.btnWriteRecipe.Click += new System.EventHandler(this.BtnWriteRecipe_Click);
            // 
            // grpLog
            // 日志区：所有操作结果按时间顺序滚动记录
            // 
            this.grpLog.Controls.Add(this.txtLog);
            this.grpLog.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.grpLog.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.grpLog.Location = new System.Drawing.Point(16, 756);
            this.grpLog.Name = "grpLog";
            this.grpLog.Size = new System.Drawing.Size(768, 160);
            this.grpLog.TabIndex = 2;
            this.grpLog.TabStop = false;
            this.grpLog.Text = "操作日志";
            // 
            // txtLog
            // 多行只读日志框：内容由 AppendLog 追加，始终滚动到最新一行
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
            // DevTestForm
            // 功能测试窗体（仅开发者账号可进）：PLC/相机/扫码枪通讯手动验证工具
            // 窗体尺寸：高约 900，标题标明开发者专用
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 932);
            this.Controls.Add(this.grpLog);
            this.Controls.Add(this.grpPlc);
            this.Controls.Add(this.grpScanner);
            this.Controls.Add(this.grpCamera);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DevTestForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "功能测试（开发者）";
            this.grpCamera.ResumeLayout(false);
            this.grpCamera.PerformLayout();
            this.grpScanner.ResumeLayout(false);
            this.grpScanner.PerformLayout();
            this.grpPlc.ResumeLayout(false);
            this.grpPlc.PerformLayout();
            this.grpLog.ResumeLayout(false);
            this.grpLog.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picTestShot)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        // 设计器声明的字段（命名遵循匈牙利前缀规范：cmb/lbl/btn/txt）
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
        private Button btnReadMoveDone;
        private Button btnClearMoveDone;
        private Label lblMoveVal;
        private Button btnStartOn;
        private Button btnStartOff;
        private Button btnDone0;
        private Button btnDone1;
        private Button btnDone2;
        private TextBox txtRecipe;
        private Button btnWriteRecipe;
        private GroupBox grpLog;
        private TextBox txtLog;
        private PictureBox picTestShot;
        private Label lblTestImagePath;
    }
}