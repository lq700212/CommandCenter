using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// SettingsForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 把"静态、数量与位置固定"的控件全部放进设计器，便于可视化拖拽：
    ///   PLC IP/端口/型号序号、显示窗口行列、图片保存相关三个输入框、相机列表 DataGridView、
    ///   添加/删除相机、扫码枪列表 DataGridView（V1.8.1）、添加/删除扫码枪、保存/取消 按钮。
    /// 这些控件都是固定布局（无运行时紧凑重排需求），设计器坐标即最终坐标。
    /// 【重要】整体顺序请参考 SettingsForm.cs 类注释里的 ASCII 布局图。
    ///   ┌──────────────────────────────────────────────────────────┐
    ///   │ PLC IP: [txtPlcIp] 端口:[nudPlcPort] [btnModelConfig 产品型号配置…] │
    ///   │ 显示窗口行:[nudRows] 列:[nudCols]                         │
    ///   │ 图片保存根目录: [txtSaveDir]                               │
    ///   │ 目录结构: [btnEditDirs 配置目录结构…]                     │
    ///   │    （上下各留 12px 空隙，避免与文件名模板行挤在一起）     │
    ///   │ 文件名模板:     [txtFileNameTpl]                          │
    ///   │ 窗口点位: [btnEditPoints 窗口/点位配置…] [√chkWindowIndex 窗口编号] [√chkWindowToolTip 悬停提示] │
    ///   │ OK/NG显示: [√标题栏高亮] [√窗口徽标]                        │
    ///   │ 相机列表:                                                │
    ///   │   ┌──────────────────────────────────────────────────┐   │
    ///   │   │ gridCameras（DataGridView）                      │   │
    ///   │   └──────────────────────────────────────────────────┘   │
    ///   │   [btnAddCam] [btnDelCam]                                │
    ///   │ 扫码枪列表(TCP):                                         │
    ///   │   ┌──────────────────────────────────────────────────┐   │
    ///   │   │ gridScannersTcp（DataGridView）                  │   │
    ///   │   └──────────────────────────────────────────────────┘   │
    ///   │   [btnAddScannerTcp] [btnDelScannerTcp]                  │
    ///   │ 扫码枪列表(串口):                                       │
    ///   │   ┌──────────────────────────────────────────────────┐   │
    ///   │   │ gridScannersSerial（DataGridView）               │   │
    ///   │   └──────────────────────────────────────────────────┘   │
    ///   │   [btnAddScannerSerial] [btnDelScannerSerial]            │
    ///   ├──────────────────────────────────────────────────────────┤
    ///   │                              [btnSave] [btnCancel]       │ ← pnlBottom（固定底部）
    ///   └──────────────────────────────────────────────────────────┘
    ///   ↑ pnlScroll(AutoScroll=true) 包裹上方所有内容，超出高度自动出竖滚动条。
    /// 说明：
    ///   - V1.12.8 起扫码枪列表拆分为 TCP 表 + 串口表两张 DataGridView，
    ///     解决"同一张表行间切换 Tcp/Serial 方式导致列显隐混乱"的 bug（整列显隐无法逐行控制）。
    ///   - V2.14.14 产品型号配置（btnModelConfig）：弹出 ModelIndexEditForm 表格维护"型号↔PLC序号"
    ///     （40007）映射，取代 V2.14.13 的"型号序号"框 nudModelIndex。确定写回 plc.modelIndexes，
    ///     取消关闭不落盘；前几行预载当前已有型号与序号。
    ///   - 控件说明不占界面：原常驻灰字标签已删除，统一改为 ToolTip 气泡。
    ///   - 控件的"显示内容"（IP/端口/行列/目录模板/相机行/扫码枪行）由 SettingsForm.cs 运行时
    ///     从 AppConfig 填充（LoadFromConfig），设计器里的值只是可视化参照。
    ///   - gridCameras / gridScannersTcp / gridScannersSerial 的列由运行时代码添加，
    ///     不在设计器序列化，避免 DataGridView 列序列化代码冗长易错。
    ///   - 保存/取消按钮固定在底部 pnlBottom（不随内容滚动），始终可见可点。
    /// </summary>
    partial class SettingsForm
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            this.lblPlcIp = new System.Windows.Forms.Label();
            this.txtPlcIp = new System.Windows.Forms.TextBox();
            this.lblPlcPort = new System.Windows.Forms.Label();
            this.nudPlcPort = new System.Windows.Forms.NumericUpDown();
            this.btnModelConfig = new System.Windows.Forms.Button();
            this.lblRows = new System.Windows.Forms.Label();
            this.nudRows = new System.Windows.Forms.NumericUpDown();
            this.lblCols = new System.Windows.Forms.Label();
            this.nudCols = new System.Windows.Forms.NumericUpDown();
            this.lblDir = new System.Windows.Forms.Label();
            this.txtSaveDir = new System.Windows.Forms.TextBox();
            this.btnEditDirs = new System.Windows.Forms.Button();
            this.lblFile = new System.Windows.Forms.Label();
            this.txtFileNameTpl = new System.Windows.Forms.TextBox();
            this.lblPoints = new System.Windows.Forms.Label();
            this.btnEditPoints = new System.Windows.Forms.Button();
            this.lblOkNg = new System.Windows.Forms.Label();
            this.chkTitleOkNg = new System.Windows.Forms.CheckBox();
            this.chkWindowOkNg = new System.Windows.Forms.CheckBox();
            this.chkWindowIndex = new System.Windows.Forms.CheckBox();
            this.chkWindowToolTip = new System.Windows.Forms.CheckBox();
            this.chkAutoFit = new System.Windows.Forms.CheckBox();
            this.lblCams = new System.Windows.Forms.Label();
            this.gridCameras = new System.Windows.Forms.DataGridView();
            this.btnAddCam = new System.Windows.Forms.Button();
            this.btnDelCam = new System.Windows.Forms.Button();
            this.lblScannersTcp = new System.Windows.Forms.Label();
            this.gridScannersTcp = new System.Windows.Forms.DataGridView();
            this.btnAddScannerTcp = new System.Windows.Forms.Button();
            this.btnDelScannerTcp = new System.Windows.Forms.Button();
            this.lblScannersSerial = new System.Windows.Forms.Label();
            this.gridScannersSerial = new System.Windows.Forms.DataGridView();
            this.btnAddScannerSerial = new System.Windows.Forms.Button();
            this.btnDelScannerSerial = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pnlScroll = new System.Windows.Forms.Panel();
            this.pnlBottom = new System.Windows.Forms.Panel();
            this.tip = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.nudPlcPort)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudRows)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudCols)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCameras)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridScannersTcp)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridScannersSerial)).BeginInit();
            this.pnlScroll.SuspendLayout();
            this.pnlBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblPlcIp
            // 
            this.lblPlcIp.AutoSize = true;
            this.lblPlcIp.Location = new System.Drawing.Point(20, 21);
            this.lblPlcIp.Name = "lblPlcIp";
            this.lblPlcIp.Size = new System.Drawing.Size(54, 20);
            this.lblPlcIp.TabIndex = 0;
            this.lblPlcIp.Text = "PLC IP:";
            // 
            // txtPlcIp
            // 
            this.txtPlcIp.Location = new System.Drawing.Point(180, 18);
            this.txtPlcIp.Name = "txtPlcIp";
            this.txtPlcIp.Size = new System.Drawing.Size(110, 25);
            this.txtPlcIp.TabIndex = 1;
            this.txtPlcIp.Text = "19.87.6.1";
            this.tip.SetToolTip(this.txtPlcIp, "上位机从站监听绑定 IP（V1.12.11 起 PLC 做主站、上位机做从站）。\r\n填 0.0.0.0 监听所有网卡，或填本机指定 IP（如 19.87.6.23" +
        "0）；\r\n保存后即时生效（自动重启从站监听）。");
            // 
            // lblPlcPort
            // 
            this.lblPlcPort.AutoSize = true;
            this.lblPlcPort.Location = new System.Drawing.Point(306, 21);
            this.lblPlcPort.Name = "lblPlcPort";
            this.lblPlcPort.Size = new System.Drawing.Size(40, 20);
            this.lblPlcPort.TabIndex = 2;
            this.lblPlcPort.Text = "端口:";
            // 
            // nudPlcPort
            // 
            this.nudPlcPort.Location = new System.Drawing.Point(356, 18);
            this.nudPlcPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.nudPlcPort.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudPlcPort.Name = "nudPlcPort";
            this.nudPlcPort.Size = new System.Drawing.Size(70, 25);
            this.nudPlcPort.TabIndex = 3;
            this.tip.SetToolTip(this.nudPlcPort, "上位机从站监听端口（Modbus TCP 标准 502，需与汇川主站通讯指令里的端口一致）。\r\n保存后即时生效（自动重启从站监听）。");
            this.nudPlcPort.Value = new decimal(new int[] {
            502,
            0,
            0,
            0});
            // 
            // btnModelConfig
            // 
            this.btnModelConfig.Location = new System.Drawing.Point(456, 16);
            this.btnModelConfig.Name = "btnModelConfig";
            this.btnModelConfig.Size = new System.Drawing.Size(120, 28);
            this.btnModelConfig.TabIndex = 34;
            this.btnModelConfig.Text = "产品型号配置…";
            this.tip.SetToolTip(this.btnModelConfig, "打开【产品型号配置】对话框（V2.14.14）：用表格维护\"型号名称 ↔ PLC 序号(40007)\"映射。\r\n表格两列：序号、型号名称；前几行默认预载当前已有型" +
        "号与序号，可增删改。\r\n【确定】把当前对应关系保存到配置（重启后自动加载），【取消】关闭不保存。\r\n现场默认 Z121=1、U171=2；每次扫码上位机先写 4" +
        "0007=本序号，再写 40008~40012=型号 ASCII 字符串。");
            this.btnModelConfig.UseVisualStyleBackColor = true;
            // 
            // lblRows
            // 
            this.lblRows.AutoSize = true;
            this.lblRows.Location = new System.Drawing.Point(20, 63);
            this.lblRows.Name = "lblRows";
            this.lblRows.Size = new System.Drawing.Size(82, 20);
            this.lblRows.TabIndex = 4;
            this.lblRows.Text = "显示窗口行:";
            // 
            // nudRows
            // 
            this.nudRows.Location = new System.Drawing.Point(180, 60);
            this.nudRows.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.nudRows.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudRows.Name = "nudRows";
            this.nudRows.Size = new System.Drawing.Size(70, 25);
            this.nudRows.TabIndex = 5;
            this.tip.SetToolTip(this.nudRows, "主界面显示窗口的行数。窗口总数=行×列；保存后即时生效。\r\n新增窗口的存图点位默认=窗口编号，可在下方\"窗口/点位配置...\"里改。\r\n勾选\"自适应\"后本框自动置" +
        "灰（行数由相机点位表自动计算）。");
            this.nudRows.Value = new decimal(new int[] {
            4,
            0,
            0,
            0});
            // 
            // lblCols
            // 
            this.lblCols.AutoSize = true;
            this.lblCols.Location = new System.Drawing.Point(260, 63);
            this.lblCols.Name = "lblCols";
            this.lblCols.Size = new System.Drawing.Size(26, 20);
            this.lblCols.TabIndex = 6;
            this.lblCols.Text = "列:";
            // 
            // nudCols
            // 
            this.nudCols.Location = new System.Drawing.Point(340, 60);
            this.nudCols.Maximum = new decimal(new int[] {
            7,
            0,
            0,
            0});
            this.nudCols.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudCols.Name = "nudCols";
            this.nudCols.Size = new System.Drawing.Size(70, 25);
            this.nudCols.TabIndex = 7;
            this.tip.SetToolTip(this.nudCols, "主界面显示窗口的列数。窗口总数=行×列；保存后即时生效。\r\n新增窗口的存图点位默认=窗口编号，可在下方\"窗口/点位配置...\"里改。\r\n勾选\"自适应\"后本框自动置" +
        "灰（列数由相机点位表自动计算）。");
            this.nudCols.Value = new decimal(new int[] {
            7,
            0,
            0,
            0});
            // 
            // lblDir
            // 
            this.lblDir.AutoSize = true;
            this.lblDir.Location = new System.Drawing.Point(20, 105);
            this.lblDir.Name = "lblDir";
            this.lblDir.Size = new System.Drawing.Size(110, 20);
            this.lblDir.TabIndex = 8;
            this.lblDir.Text = "图片保存根目录:";
            // 
            // txtSaveDir
            // 
            this.txtSaveDir.Location = new System.Drawing.Point(180, 102);
            this.txtSaveDir.Name = "txtSaveDir";
            this.txtSaveDir.Size = new System.Drawing.Size(740, 25);
            this.txtSaveDir.TabIndex = 9;
            this.txtSaveDir.Text = "E:\\Images";
            this.tip.SetToolTip(this.txtSaveDir, "图片保存的根目录（绝对路径）。\r\n实际目录结构按\"配置目录结构...\"里的层级逐级创建。");
            // 
            // btnEditDirs
            // 
            this.btnEditDirs.Location = new System.Drawing.Point(180, 139);
            this.btnEditDirs.Name = "btnEditDirs";
            this.btnEditDirs.Size = new System.Drawing.Size(200, 30);
            this.btnEditDirs.TabIndex = 11;
            this.btnEditDirs.Text = "配置目录结构...";
            this.tip.SetToolTip(this.btnEditDirs, "可视化编辑存图目录结构（目录层级列表 + 文件名规则），并实时预览 OK/NG 两条落盘路径。\r\n当前结构见下方动态提示。");
            this.btnEditDirs.UseVisualStyleBackColor = true;
            // 
            // lblFile
            // 
            this.lblFile.AutoSize = true;
            this.lblFile.Location = new System.Drawing.Point(20, 184);
            this.lblFile.Name = "lblFile";
            this.lblFile.Size = new System.Drawing.Size(82, 20);
            this.lblFile.TabIndex = 12;
            this.lblFile.Text = "文件名模板:";
            // 
            // txtFileNameTpl
            // 
            this.txtFileNameTpl.Location = new System.Drawing.Point(180, 181);
            this.txtFileNameTpl.Name = "txtFileNameTpl";
            this.txtFileNameTpl.Size = new System.Drawing.Size(740, 25);
            this.txtFileNameTpl.TabIndex = 13;
            this.txtFileNameTpl.Text = "{点位}";
            this.tip.SetToolTip(this.txtFileNameTpl, "图片文件名规则，占位符会自动替换：\r\n{点位}→窗口点位号（如 1.png）  {SN}→序列号  {OKNG}→OK 或 NG\r\n{年}/{月}/{日}→日期 " +
        " {时间}→毫秒时间戳；其余文字原样保留。\r\n目录结构里的层级同样支持这些占位符。");
            // 
            // lblPoints
            // 
            this.lblPoints.AutoSize = true;
            this.lblPoints.Location = new System.Drawing.Point(20, 221);
            this.lblPoints.Name = "lblPoints";
            this.lblPoints.Size = new System.Drawing.Size(68, 20);
            this.lblPoints.TabIndex = 15;
            this.lblPoints.Text = "窗口点位:";
            // 
            // btnEditPoints
            // 
            this.btnEditPoints.Location = new System.Drawing.Point(180, 216);
            this.btnEditPoints.Name = "btnEditPoints";
            this.btnEditPoints.Size = new System.Drawing.Size(200, 30);
            this.btnEditPoints.TabIndex = 16;
            this.btnEditPoints.Text = "窗口/点位配置...";
            this.tip.SetToolTip(this.btnEditPoints, resources.GetString("btnEditPoints.ToolTip"));
            this.btnEditPoints.UseVisualStyleBackColor = true;
            // 
            // lblOkNg
            // 
            this.lblOkNg.AutoSize = true;
            this.lblOkNg.Location = new System.Drawing.Point(20, 253);
            this.lblOkNg.Name = "lblOkNg";
            this.lblOkNg.Size = new System.Drawing.Size(87, 20);
            this.lblOkNg.TabIndex = 14;
            this.lblOkNg.Text = "OK/NG显示:";
            // 
            // chkTitleOkNg
            // 
            this.chkTitleOkNg.AutoSize = true;
            this.chkTitleOkNg.Location = new System.Drawing.Point(180, 251);
            this.chkTitleOkNg.Name = "chkTitleOkNg";
            this.chkTitleOkNg.Size = new System.Drawing.Size(98, 24);
            this.chkTitleOkNg.TabIndex = 15;
            this.chkTitleOkNg.Text = "标题栏高亮";
            this.tip.SetToolTip(this.chkTitleOkNg, "标题栏的 OK / NG 计数用\"实心彩色色块 + 白字\"高亮（绿底=OK、红底=NG），\r\n比普通彩色文字醒目得多。取消则回退彩色文字样式。保存后即时生效。");
            this.chkTitleOkNg.UseVisualStyleBackColor = true;
            // 
            // chkWindowOkNg
            // 
            this.chkWindowOkNg.AutoSize = true;
            this.chkWindowOkNg.Location = new System.Drawing.Point(315, 251);
            this.chkWindowOkNg.Name = "chkWindowOkNg";
            this.chkWindowOkNg.Size = new System.Drawing.Size(84, 24);
            this.chkWindowOkNg.TabIndex = 16;
            this.chkWindowOkNg.Text = "窗口徽标";
            this.tip.SetToolTip(this.chkWindowOkNg, "主界面每个显示窗口右下角叠加一个【矩形框 OK/NG 徽标】（样子同标题栏色块，\r\n颜色随 \"OK颜色/NG颜色\" 配置）。默认开启（V2.14.24）；且只有窗" +
        "口对应的点位\r\n【拿到相机 OK/NG 结果】才显示对应徽标（新的一轮开始前隐藏），不会空窗口乱显。\r\n保存后即时生效。");
            this.chkWindowOkNg.UseVisualStyleBackColor = true;
            // 
            // chkWindowIndex
            // 
            this.chkWindowIndex.AutoSize = true;
            this.chkWindowIndex.Location = new System.Drawing.Point(390, 219);
            this.chkWindowIndex.Name = "chkWindowIndex";
            this.chkWindowIndex.Size = new System.Drawing.Size(112, 24);
            this.chkWindowIndex.TabIndex = 17;
            this.chkWindowIndex.Text = "显示窗口编号";
            this.tip.SetToolTip(this.chkWindowIndex, "主界面每个显示窗口左上角是否显示【窗口编号】（半透明白底 + 深蓝灰字，辅助现场定位第几路）。\r\n默认勾选（与历史画面一致）；现场嫌编号碍眼可取消勾选，保存后即时" +
        "生效。");
            this.chkWindowIndex.UseVisualStyleBackColor = true;
            // 
            // chkWindowToolTip
            // 
            this.chkWindowToolTip.AutoSize = true;
            this.chkWindowToolTip.Location = new System.Drawing.Point(555, 219);
            this.chkWindowToolTip.Name = "chkWindowToolTip";
            this.chkWindowToolTip.Size = new System.Drawing.Size(84, 24);
            this.chkWindowToolTip.TabIndex = 18;
            this.chkWindowToolTip.Text = "悬停提示";
            this.tip.SetToolTip(this.chkWindowToolTip, "鼠标放到主界面任一显示窗口内停留片刻，是否弹出【双击放大/还原】气泡提示。\r\n默认勾选（方便新手操作员发现双击功能）；现场嫌气泡挡画面可取消勾选，保存后即时生效。" +
        "");
            this.chkWindowToolTip.UseVisualStyleBackColor = true;
            // 
            // chkAutoFit
            // 
            this.chkAutoFit.AutoSize = true;
            this.chkAutoFit.Location = new System.Drawing.Point(420, 61);
            this.chkAutoFit.Name = "chkAutoFit";
            this.chkAutoFit.Size = new System.Drawing.Size(70, 24);
            this.chkAutoFit.TabIndex = 33;
            this.chkAutoFit.Text = "自适应";
            this.tip.SetToolTip(this.chkAutoFit, resources.GetString("chkAutoFit.ToolTip"));
            this.chkAutoFit.UseVisualStyleBackColor = true;
            // 
            // lblCams
            // 
            this.lblCams.AutoSize = true;
            this.lblCams.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblCams.Location = new System.Drawing.Point(20, 296);
            this.lblCams.Name = "lblCams";
            this.lblCams.Size = new System.Drawing.Size(69, 19);
            this.lblCams.TabIndex = 20;
            this.lblCams.Text = "相机列表:";
            // 
            // gridCameras
            // 
            this.gridCameras.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridCameras.BackgroundColor = System.Drawing.Color.White;
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle7.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle7.Font = new System.Drawing.Font("微软雅黑", 10F);
            dataGridViewCellStyle7.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridCameras.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            this.gridCameras.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("微软雅黑", 10F);
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridCameras.DefaultCellStyle = dataGridViewCellStyle8;
            this.gridCameras.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridCameras.Location = new System.Drawing.Point(20, 322);
            this.gridCameras.Name = "gridCameras";
            this.gridCameras.RowHeadersVisible = false;
            this.gridCameras.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridCameras.Size = new System.Drawing.Size(900, 150);
            this.gridCameras.TabIndex = 21;
            // 
            // btnAddCam
            // 
            this.btnAddCam.Location = new System.Drawing.Point(20, 492);
            this.btnAddCam.Name = "btnAddCam";
            this.btnAddCam.Size = new System.Drawing.Size(100, 30);
            this.btnAddCam.TabIndex = 22;
            this.btnAddCam.Text = "添加一台";
            this.tip.SetToolTip(this.btnAddCam, "在列表末尾添加一台相机（默认值可直接改 IP / 端口 / FTP 上传目录）。");
            this.btnAddCam.UseVisualStyleBackColor = true;
            // 
            // btnDelCam
            // 
            this.btnDelCam.Location = new System.Drawing.Point(150, 492);
            this.btnDelCam.Name = "btnDelCam";
            this.btnDelCam.Size = new System.Drawing.Size(100, 30);
            this.btnDelCam.TabIndex = 23;
            this.btnDelCam.Text = "删除选中";
            this.tip.SetToolTip(this.btnDelCam, "删除选中的相机行；未选中时先点选要删的行。");
            this.btnDelCam.UseVisualStyleBackColor = true;
            // 
            // lblScannersTcp
            // 
            this.lblScannersTcp.AutoSize = true;
            this.lblScannersTcp.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblScannersTcp.Location = new System.Drawing.Point(20, 540);
            this.lblScannersTcp.Name = "lblScannersTcp";
            this.lblScannersTcp.Size = new System.Drawing.Size(120, 19);
            this.lblScannersTcp.TabIndex = 26;
            this.lblScannersTcp.Text = "扫码枪列表(TCP):";
            this.tip.SetToolTip(this.lblScannersTcp, "TCP 扫码枪列表：基恩士 SR 系列以太网扫码枪，一台一行。\r\n任何一台扫到的条码都会更新当前序列号（标题栏与存图目录同步）。\r\n\"启用\"不打勾则这台不接入（序" +
        "列号可双击标题栏序列号框手动输入，V1.12.17）。\r\nV1.12.8 起拆为独立的 TCP 表，不再与串口混在同一张表里。");
            // 
            // gridScannersTcp
            // 
            this.gridScannersTcp.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridScannersTcp.BackgroundColor = System.Drawing.Color.White;
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle9.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle9.Font = new System.Drawing.Font("微软雅黑", 10F);
            dataGridViewCellStyle9.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle9.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle9.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle9.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridScannersTcp.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle9;
            this.gridScannersTcp.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle10.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle10.Font = new System.Drawing.Font("微软雅黑", 10F);
            dataGridViewCellStyle10.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle10.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle10.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle10.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridScannersTcp.DefaultCellStyle = dataGridViewCellStyle10;
            this.gridScannersTcp.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridScannersTcp.Location = new System.Drawing.Point(20, 566);
            this.gridScannersTcp.Name = "gridScannersTcp";
            this.gridScannersTcp.RowHeadersVisible = false;
            this.gridScannersTcp.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridScannersTcp.Size = new System.Drawing.Size(900, 120);
            this.gridScannersTcp.TabIndex = 27;
            // 
            // btnAddScannerTcp
            // 
            this.btnAddScannerTcp.Location = new System.Drawing.Point(20, 700);
            this.btnAddScannerTcp.Name = "btnAddScannerTcp";
            this.btnAddScannerTcp.Size = new System.Drawing.Size(100, 30);
            this.btnAddScannerTcp.TabIndex = 28;
            this.btnAddScannerTcp.Text = "添加一台";
            this.tip.SetToolTip(this.btnAddScannerTcp, "添加一台 TCP 扫码枪（默认 19.87.6.100 / 9004 / LON，可直接改）。");
            this.btnAddScannerTcp.UseVisualStyleBackColor = true;
            // 
            // btnDelScannerTcp
            // 
            this.btnDelScannerTcp.Location = new System.Drawing.Point(150, 700);
            this.btnDelScannerTcp.Name = "btnDelScannerTcp";
            this.btnDelScannerTcp.Size = new System.Drawing.Size(100, 30);
            this.btnDelScannerTcp.TabIndex = 29;
            this.btnDelScannerTcp.Text = "删除选中";
            this.tip.SetToolTip(this.btnDelScannerTcp, "删除选中的 TCP 扫码枪行；未选中时先点选要删的行。");
            this.btnDelScannerTcp.UseVisualStyleBackColor = true;
            // 
            // lblScannersSerial
            // 
            this.lblScannersSerial.AutoSize = true;
            this.lblScannersSerial.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblScannersSerial.Location = new System.Drawing.Point(20, 748);
            this.lblScannersSerial.Name = "lblScannersSerial";
            this.lblScannersSerial.Size = new System.Drawing.Size(121, 19);
            this.lblScannersSerial.TabIndex = 30;
            this.lblScannersSerial.Text = "扫码枪列表(串口):";
            this.tip.SetToolTip(this.lblScannersSerial, "串口扫码枪列表：RS-232 串口扫码枪，一台一行。\r\n串口扫码枪上电即读码、无需触发指令（与 TCP 不同）。\r\n\"启用\"不打勾则这台不接入。");
            // 
            // gridScannersSerial
            // 
            this.gridScannersSerial.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridScannersSerial.BackgroundColor = System.Drawing.Color.White;
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle11.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle11.Font = new System.Drawing.Font("微软雅黑", 10F);
            dataGridViewCellStyle11.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle11.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle11.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle11.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridScannersSerial.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle11;
            this.gridScannersSerial.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle12.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle12.Font = new System.Drawing.Font("微软雅黑", 10F);
            dataGridViewCellStyle12.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle12.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle12.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle12.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridScannersSerial.DefaultCellStyle = dataGridViewCellStyle12;
            this.gridScannersSerial.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridScannersSerial.Location = new System.Drawing.Point(20, 774);
            this.gridScannersSerial.Name = "gridScannersSerial";
            this.gridScannersSerial.RowHeadersVisible = false;
            this.gridScannersSerial.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridScannersSerial.Size = new System.Drawing.Size(900, 120);
            this.gridScannersSerial.TabIndex = 31;
            // 
            // btnAddScannerSerial
            // 
            this.btnAddScannerSerial.Location = new System.Drawing.Point(20, 908);
            this.btnAddScannerSerial.Name = "btnAddScannerSerial";
            this.btnAddScannerSerial.Size = new System.Drawing.Size(100, 30);
            this.btnAddScannerSerial.TabIndex = 32;
            this.btnAddScannerSerial.Text = "添加一台";
            this.tip.SetToolTip(this.btnAddScannerSerial, "添加一台串口扫码枪（默认 COM3 / 115200 / 1 / None，可直接改）。");
            this.btnAddScannerSerial.UseVisualStyleBackColor = true;
            // 
            // btnDelScannerSerial
            // 
            this.btnDelScannerSerial.Location = new System.Drawing.Point(150, 908);
            this.btnDelScannerSerial.Name = "btnDelScannerSerial";
            this.btnDelScannerSerial.Size = new System.Drawing.Size(100, 30);
            this.btnDelScannerSerial.TabIndex = 33;
            this.btnDelScannerSerial.Text = "删除选中";
            this.tip.SetToolTip(this.btnDelScannerSerial, "删除选中的串口扫码枪行；未选中时先点选要删的行。");
            this.btnDelScannerSerial.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSave.Location = new System.Drawing.Point(750, 9);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 32);
            this.btnSave.TabIndex = 40;
            this.btnSave.Text = "保存";
            this.tip.SetToolTip(this.btnSave, "保存所有设置并写盘到 Config/appconfig.json，保存后即时生效（V1.6.0 免重启）。\r\n服务层按新配置自动重建，设备短暂断连后几秒内自动连回" +
        "。");
            this.btnSave.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(850, 9);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 41;
            this.btnCancel.Text = "取消";
            this.tip.SetToolTip(this.btnCancel, "放弃本次修改并关闭，不写盘。");
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // pnlScroll
            // 
            this.pnlScroll.AutoScroll = true;
            this.pnlScroll.Controls.Add(this.btnDelScannerSerial);
            this.pnlScroll.Controls.Add(this.btnAddScannerSerial);
            this.pnlScroll.Controls.Add(this.gridScannersSerial);
            this.pnlScroll.Controls.Add(this.lblScannersSerial);
            this.pnlScroll.Controls.Add(this.btnDelScannerTcp);
            this.pnlScroll.Controls.Add(this.btnAddScannerTcp);
            this.pnlScroll.Controls.Add(this.gridScannersTcp);
            this.pnlScroll.Controls.Add(this.lblScannersTcp);
            this.pnlScroll.Controls.Add(this.btnDelCam);
            this.pnlScroll.Controls.Add(this.btnAddCam);
            this.pnlScroll.Controls.Add(this.gridCameras);
            this.pnlScroll.Controls.Add(this.lblCams);
            this.pnlScroll.Controls.Add(this.chkAutoFit);
            this.pnlScroll.Controls.Add(this.chkWindowToolTip);
            this.pnlScroll.Controls.Add(this.chkWindowIndex);
            this.pnlScroll.Controls.Add(this.chkWindowOkNg);
            this.pnlScroll.Controls.Add(this.chkTitleOkNg);
            this.pnlScroll.Controls.Add(this.lblOkNg);
            this.pnlScroll.Controls.Add(this.btnEditPoints);
            this.pnlScroll.Controls.Add(this.lblPoints);
            this.pnlScroll.Controls.Add(this.txtFileNameTpl);
            this.pnlScroll.Controls.Add(this.lblFile);
            this.pnlScroll.Controls.Add(this.btnEditDirs);
            this.pnlScroll.Controls.Add(this.txtSaveDir);
            this.pnlScroll.Controls.Add(this.lblDir);
            this.pnlScroll.Controls.Add(this.nudCols);
            this.pnlScroll.Controls.Add(this.lblCols);
            this.pnlScroll.Controls.Add(this.nudRows);
            this.pnlScroll.Controls.Add(this.lblRows);
            this.pnlScroll.Controls.Add(this.nudPlcPort);
            this.pnlScroll.Controls.Add(this.lblPlcPort);
            this.pnlScroll.Controls.Add(this.btnModelConfig);
            this.pnlScroll.Controls.Add(this.txtPlcIp);
            this.pnlScroll.Controls.Add(this.lblPlcIp);
            this.pnlScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlScroll.Location = new System.Drawing.Point(0, 0);
            this.pnlScroll.Name = "pnlScroll";
            this.pnlScroll.Size = new System.Drawing.Size(960, 650);
            this.pnlScroll.TabIndex = 0;
            // 
            // pnlBottom
            // 
            this.pnlBottom.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(238)))), ((int)(((byte)(238)))));
            this.pnlBottom.Controls.Add(this.btnCancel);
            this.pnlBottom.Controls.Add(this.btnSave);
            this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBottom.Location = new System.Drawing.Point(0, 650);
            this.pnlBottom.Name = "pnlBottom";
            this.pnlBottom.Size = new System.Drawing.Size(960, 50);
            this.pnlBottom.TabIndex = 42;
            // 
            // tip
            // 
            this.tip.AutoPopDelay = 8000;
            this.tip.InitialDelay = 500;
            this.tip.ReshowDelay = 100;
            this.tip.ShowAlways = true;
            // 
            // SettingsForm
            // 
            this.AcceptButton = this.btnSave;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(960, 700);
            this.Controls.Add(this.pnlScroll);
            this.Controls.Add(this.pnlBottom);
            this.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "系统设置";
            ((System.ComponentModel.ISupportInitialize)(this.nudPlcPort)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudRows)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudCols)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCameras)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridScannersTcp)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridScannersSerial)).EndInit();
            this.pnlScroll.ResumeLayout(false);
            this.pnlScroll.PerformLayout();
            this.pnlBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        // 设计器声明的字段（视觉化拖拽所需；命名遵循匈牙利前缀规范）
        private Label lblPlcIp;
        private TextBox txtPlcIp;
        private Label lblPlcPort;
        private NumericUpDown nudPlcPort;
        private Button btnModelConfig;
        private Label lblRows;
        private NumericUpDown nudRows;
        private Label lblCols;
        private NumericUpDown nudCols;
        private Label lblDir;
        private TextBox txtSaveDir;
        private Button btnEditDirs;
        private Label lblFile;
        private TextBox txtFileNameTpl;
        private Label lblPoints;
        private Button btnEditPoints;
        private Label lblOkNg;
        private CheckBox chkTitleOkNg;
        private CheckBox chkWindowOkNg;
        private CheckBox chkWindowIndex;
        private CheckBox chkWindowToolTip;
        private CheckBox chkAutoFit;
        private Label lblCams;
        private DataGridView gridCameras;
        private Button btnAddCam;
        private Button btnDelCam;
        private Label lblScannersTcp;
        private DataGridView gridScannersTcp;
        private Button btnAddScannerTcp;
        private Button btnDelScannerTcp;
        private Label lblScannersSerial;
        private DataGridView gridScannersSerial;
        private Button btnAddScannerSerial;
        private Button btnDelScannerSerial;
        private Button btnSave;
        private Button btnCancel;
        private Panel pnlScroll;
        private Panel pnlBottom;
        private ToolTip tip;
    }
}
