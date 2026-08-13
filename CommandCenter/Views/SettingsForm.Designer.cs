using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// SettingsForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 把"静态、数量与位置固定"的控件全部放进设计器，便于可视化拖拽：
    ///   PLC IP/端口、显示窗口行列、图片保存相关三个输入框、相机列表 DataGridView、
    ///   添加/删除相机、扫码枪列表 DataGridView（V1.8.1）、添加/删除扫码枪、保存/取消 按钮。
    /// 这些控件都是固定布局（无运行时紧凑重排需求），设计器坐标即最终坐标。
    /// 【重要】整体顺序请参考 SettingsForm.cs 类注释里的 ASCII 布局图。
    ///   ┌──────────────────────────────────────────────────────────┐
    ///   │ PLC IP:   [txtPlcIp]   端口:[nudPlcPort]                 │
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
            // components 容器必须先初始化：ToolTip 等组件要挂到它上面统一自动释放
            this.components = new System.ComponentModel.Container();
            this.lblPlcIp = new System.Windows.Forms.Label();
            this.txtPlcIp = new System.Windows.Forms.TextBox();
            this.lblPlcPort = new System.Windows.Forms.Label();
            this.nudPlcPort = new System.Windows.Forms.NumericUpDown();
            this.lblModel = new System.Windows.Forms.Label();
            this.cmbModel = new System.Windows.Forms.ComboBox();
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
            this.lblPlcIp.Size = new System.Drawing.Size(61, 19);
            this.lblPlcIp.TabIndex = 0;
            this.lblPlcIp.Text = "PLC IP:";
            //
            // txtPlcIp
            // PLC IP 地址（EditorBrowsable 保持默认，值由 LoadFromConfig 从配置填充）
            //
            this.txtPlcIp.Location = new System.Drawing.Point(130, 18);
            this.txtPlcIp.Name = "txtPlcIp";
            this.txtPlcIp.Size = new System.Drawing.Size(150, 25);
            this.txtPlcIp.TabIndex = 1;
            this.txtPlcIp.Text = "19.87.6.1";
            //
            // lblPlcPort
            //
            this.lblPlcPort.AutoSize = true;
            this.lblPlcPort.Location = new System.Drawing.Point(296, 21);
            this.lblPlcPort.Name = "lblPlcPort";
            this.lblPlcPort.Size = new System.Drawing.Size(46, 19);
            this.lblPlcPort.TabIndex = 2;
            this.lblPlcPort.Text = "端口:";
            //
            // nudPlcPort
            // PLC 通讯端口（Modbus TCP），范围校验 1~65535
            //
            this.nudPlcPort.Location = new System.Drawing.Point(346, 18);
            this.nudPlcPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.nudPlcPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.nudPlcPort.Name = "nudPlcPort";
            this.nudPlcPort.Size = new System.Drawing.Size(70, 25);
            this.nudPlcPort.TabIndex = 3;
            this.nudPlcPort.Value = new decimal(new int[] { 502, 0, 0, 0 });
            //
            // lblModel
            //
            this.lblModel.AutoSize = true;
            this.lblModel.Location = new System.Drawing.Point(446, 21);
            this.lblModel.Name = "lblModel";
            this.lblModel.Size = new System.Drawing.Size(61, 19);
            this.lblModel.TabIndex = 31;
            this.lblModel.Text = "产品型号:";
            //
            // cmbModel
            // 固定产品型号（V2.7 协议）：每次扫码完成上位机写入 PLC 40007~40011，最多 10 字符。
            // V2.8 起为可编辑下拉：候选项=预置三型号 U171/U172/Z121（DefaultProductModels）∪ 配置已有
            // ProductModels（去重合并，配置缺字段也能直接选到三型号），也可手动输入新型号；
            // 保存时若不在候选则自动加入。型号同时决定点位→程序号查哪张表。
            //
            this.cmbModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.cmbModel.Location = new System.Drawing.Point(521, 18);
            this.cmbModel.MaxLength = 10;
            this.cmbModel.Name = "cmbModel";
            this.cmbModel.Size = new System.Drawing.Size(170, 25);
            this.cmbModel.TabIndex = 32;
            this.cmbModel.Text = "";
            //
            // lblRows
            //
            this.lblRows.AutoSize = true;
            this.lblRows.Location = new System.Drawing.Point(20, 63);
            this.lblRows.Name = "lblRows";
            this.lblRows.Size = new System.Drawing.Size(96, 19);
            this.lblRows.TabIndex = 4;
            this.lblRows.Text = "显示窗口行:";
            //
            // nudRows
            // 显示窗口行数（1~10），决定矩阵几行
            //
            this.nudRows.Location = new System.Drawing.Point(130, 60);
            this.nudRows.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.nudRows.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.nudRows.Name = "nudRows";
            this.nudRows.Size = new System.Drawing.Size(70, 25);
            this.nudRows.TabIndex = 5;
            this.nudRows.Value = new decimal(new int[] { 4, 0, 0, 0 });
            //
            // lblCols
            //
            this.lblCols.AutoSize = true;
            this.lblCols.Location = new System.Drawing.Point(200, 63);
            this.lblCols.Name = "lblCols";
            this.lblCols.Size = new System.Drawing.Size(34, 19);
            this.lblCols.TabIndex = 6;
            this.lblCols.Text = "列:";
            //
            // nudCols
            // 显示窗口列数（1~10），决定矩阵几列
            //
            this.nudCols.Location = new System.Drawing.Point(230, 60);
            this.nudCols.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.nudCols.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.nudCols.Name = "nudCols";
            this.nudCols.Size = new System.Drawing.Size(70, 25);
            this.nudCols.TabIndex = 7;
            this.nudCols.Value = new decimal(new int[] { 7, 0, 0, 0 });
            //
            // lblDir
            //
            this.lblDir.AutoSize = true;
            this.lblDir.Location = new System.Drawing.Point(20, 105);
            this.lblDir.Name = "lblDir";
            this.lblDir.Size = new System.Drawing.Size(96, 19);
            this.lblDir.TabIndex = 8;
            this.lblDir.Text = "图片保存根目录:";
            //
            // txtSaveDir
            // 图片保存根目录（绝对路径），右侧预留到滚动面板边缘（宽 790，窗体加宽后右缘 920）
            //
            this.txtSaveDir.Location = new System.Drawing.Point(130, 102);
            this.txtSaveDir.Name = "txtSaveDir";
            this.txtSaveDir.Size = new System.Drawing.Size(790, 25);
            this.txtSaveDir.TabIndex = 9;
            this.txtSaveDir.Text = "E:\\Images";
            //
            // btnEditDirs
            // 打开"图片存储目录结构配置"对话框（DirTreeEditForm），可视化编辑目录层级与文件名规则。
            // 当前目录结构（动态）显示在该按钮的 ToolTip 里，界面不再放常驻灰字标签。
            //
            this.btnEditDirs.Location = new System.Drawing.Point(130, 139);
            this.btnEditDirs.Name = "btnEditDirs";
            this.btnEditDirs.Size = new System.Drawing.Size(160, 30);
            this.btnEditDirs.TabIndex = 11;
            this.btnEditDirs.Text = "配置目录结构...";
            this.btnEditDirs.UseVisualStyleBackColor = true;
            //
            // lblFile
            //
            this.lblFile.AutoSize = true;
            this.lblFile.Location = new System.Drawing.Point(20, 184);
            this.lblFile.Name = "lblFile";
            this.lblFile.Size = new System.Drawing.Size(96, 19);
            this.lblFile.TabIndex = 12;
            this.lblFile.Text = "文件名模板:";
            //
            // txtFileNameTpl
            // 图片文件名模板。原右侧的占位符常驻标签已删，说明并入悬停 ToolTip；
            // 输入框一路加宽到面板右缘（宽 790），与"图片保存根目录"对齐。
            //
            this.txtFileNameTpl.Location = new System.Drawing.Point(130, 181);
            this.txtFileNameTpl.Name = "txtFileNameTpl";
            this.txtFileNameTpl.Size = new System.Drawing.Size(790, 25);
            this.txtFileNameTpl.TabIndex = 13;
            this.txtFileNameTpl.Text = "{点位}";
            //
            // lblPoints
            // 窗口→存图点位 配置标题（点位默认=窗口编号，可在可视化矩阵里自定义）
            //
            this.lblPoints.AutoSize = true;
            this.lblPoints.Location = new System.Drawing.Point(20, 220);
            this.lblPoints.Name = "lblPoints";
            this.lblPoints.Size = new System.Drawing.Size(96, 19);
            this.lblPoints.TabIndex = 15;
            this.lblPoints.Text = "窗口点位:";
            //
            // btnEditPoints
            // 打开"窗口与存图点位配置"对话框（WindowPointForm），可视化改每个窗口的存图点位、
            // 交换窗口位置；默认点位=窗口编号，改动随本次"保存"一起写盘。
            //
            this.btnEditPoints.Location = new System.Drawing.Point(130, 216);
            this.btnEditPoints.Name = "btnEditPoints";
            this.btnEditPoints.Size = new System.Drawing.Size(150, 30);
            this.btnEditPoints.TabIndex = 16;
            this.btnEditPoints.Text = "窗口/点位配置...";
            this.btnEditPoints.UseVisualStyleBackColor = true;
            //
            // lblOkNg
            // "OK/NG 显示" 配置行标题
            //
            this.lblOkNg.AutoSize = true;
            this.lblOkNg.Location = new System.Drawing.Point(20, 253);
            this.lblOkNg.Name = "lblOkNg";
            this.lblOkNg.Size = new System.Drawing.Size(96, 19);
            this.lblOkNg.TabIndex = 14;
            this.lblOkNg.Text = "OK/NG显示:";
            //
            // chkTitleOkNg
            // 标题栏 OK/NG 计数高亮开关：实心彩色色块 + 白字（绿底=OK、红底=NG），
            // V1.5.0 默认开；关闭则回退普通彩色文字
            //
            this.chkTitleOkNg.AutoSize = true;
            this.chkTitleOkNg.Location = new System.Drawing.Point(130, 251);
            this.chkTitleOkNg.Name = "chkTitleOkNg";
            this.chkTitleOkNg.Size = new System.Drawing.Size(111, 23);
            this.chkTitleOkNg.TabIndex = 15;
            this.chkTitleOkNg.Text = "标题栏高亮";
            this.chkTitleOkNg.UseVisualStyleBackColor = true;
            //
            // chkWindowOkNg
            // 主界面窗口右下角 OK/NG 徽标显示开关（V2.10.3）：每格相机画面上叠加自绘
            // 矩形框 OK/NG（随 okColorName/ngColorName 配色）。默认关（V1.9.5 曾整体移除，
            // 保持现状画面干净），勾选后保存即时生效。
            //
            this.chkWindowOkNg.AutoSize = true;
            this.chkWindowOkNg.Location = new System.Drawing.Point(255, 251);
            this.chkWindowOkNg.Name = "chkWindowOkNg";
            this.chkWindowOkNg.Size = new System.Drawing.Size(102, 23);
            this.chkWindowOkNg.TabIndex = 16;
            this.chkWindowOkNg.Text = "窗口徽标";
            this.chkWindowOkNg.UseVisualStyleBackColor = true;
            //
            // chkWindowIndex
            // 主界面窗口左上角"窗口编号"显示开关（V2.10.4）：每格相机画面左上角悬浮半透明白底
            // + 深蓝灰字的编号（辅助现场定位第几路）。默认开（与历史行为一致）；勾掉后隐藏，
            // 画面更干净。与"窗口/点位配置..."按钮同处一行、垂直居中对齐。
            //
            this.chkWindowIndex.AutoSize = true;
            this.chkWindowIndex.Location = new System.Drawing.Point(290, 219);
            this.chkWindowIndex.Name = "chkWindowIndex";
            this.chkWindowIndex.Size = new System.Drawing.Size(102, 23);
            this.chkWindowIndex.TabIndex = 17;
            this.chkWindowIndex.Text = "显示窗口编号";
            this.chkWindowIndex.UseVisualStyleBackColor = true;
            //
            // chkWindowToolTip
            // 主界面窗口"悬停气泡提示"显示开关（V2.10.8）：鼠标放到任一显示窗口内停留片刻，
            // 气泡提示"双击放大（全屏查看）；再双击还原"，方便新手发现双击功能。默认开；
            // 现场觉得气泡挡画面可取消勾选。位于"显示窗口编号"右侧、垂直居中对齐。
            //
            this.chkWindowToolTip.AutoSize = true;
            this.chkWindowToolTip.Location = new System.Drawing.Point(402, 219);
            this.chkWindowToolTip.Name = "chkWindowToolTip";
            this.chkWindowToolTip.Size = new System.Drawing.Size(90, 23);
            this.chkWindowToolTip.TabIndex = 18;
            this.chkWindowToolTip.Text = "悬停提示";
            this.chkWindowToolTip.UseVisualStyleBackColor = true;
            //
            // chkAutoFit
            // 显示窗口矩阵"自适应"开关（V2.12.0）：勾选后窗口行列按当前型号 + 各相机点位表自动铺排，
            // 行/列输入框自动置灰；配合 tooltip 明示自适下不可用的功能（见 UpdateAutoFitUi）。
            // 与"显示窗口行/列"同一行、紧跟列框右侧，垂直居中。
            //
            this.chkAutoFit.AutoSize = true;
            this.chkAutoFit.Location = new System.Drawing.Point(320, 61);
            this.chkAutoFit.Name = "chkAutoFit";
            this.chkAutoFit.Size = new System.Drawing.Size(90, 23);
            this.chkAutoFit.TabIndex = 33;
            this.chkAutoFit.Text = "自适应";
            this.chkAutoFit.UseVisualStyleBackColor = true;
            //
            // lblCams
            // 相机列表标题，加粗醒目
            //
            this.lblCams.AutoSize = true;
            this.lblCams.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblCams.Location = new System.Drawing.Point(20, 296);
            this.lblCams.Name = "lblCams";
            this.lblCams.Size = new System.Drawing.Size(84, 19);
            this.lblCams.TabIndex = 20;
            this.lblCams.Text = "相机列表:";
            //
            // gridCameras
            // 相机清单：一行一台相机（行数=台数）。
            // 列结构由 SettingsForm.cs 运行时 SetupCameraGridColumns 添加，此处只设外观与编辑行为。
            //
            this.gridCameras.AllowUserToAddRows = true;
            this.gridCameras.AllowUserToDeleteRows = true;
            this.gridCameras.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridCameras.BackgroundColor = System.Drawing.Color.White;
            // 表头与单元格内容居中（V2.12.6 相机表含 PLC 索引列，现场习惯居中看）
            this.gridCameras.ColumnHeadersDefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.gridCameras.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.gridCameras.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.gridCameras.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridCameras.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridCameras.Location = new System.Drawing.Point(20, 322);
            this.gridCameras.Name = "gridCameras";
            this.gridCameras.RowHeadersVisible = false;
            this.gridCameras.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridCameras.Size = new System.Drawing.Size(900, 150);
            this.gridCameras.TabIndex = 21;
            //
            // btnAddCam
            // 添加一台默认相机行
            //
            this.btnAddCam.Location = new System.Drawing.Point(20, 492);
            this.btnAddCam.Name = "btnAddCam";
            this.btnAddCam.Size = new System.Drawing.Size(100, 30);
            this.btnAddCam.TabIndex = 22;
            this.btnAddCam.Text = "添加一台";
            this.btnAddCam.UseVisualStyleBackColor = true;
            //
            // btnDelCam
            // 删除当前选中的相机行
            //
            this.btnDelCam.Location = new System.Drawing.Point(150, 492);
            this.btnDelCam.Name = "btnDelCam";
            this.btnDelCam.Size = new System.Drawing.Size(100, 30);
            this.btnDelCam.TabIndex = 23;
            this.btnDelCam.Text = "删除选中";
            this.btnDelCam.UseVisualStyleBackColor = true;
            //
            // lblScannersTcp
            // 扫码枪列表(TCP)标题（V1.12.8 拆表），加粗醒目，与"相机列表"同风格。
            // TCP 表只配网络参数：IP/端口/触发指令，方式固定 Tcp、不再有"方式"下拉列。
            //
            this.lblScannersTcp.AutoSize = true;
            this.lblScannersTcp.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblScannersTcp.Location = new System.Drawing.Point(20, 540);
            this.lblScannersTcp.Name = "lblScannersTcp";
            this.lblScannersTcp.Size = new System.Drawing.Size(118, 19);
            this.lblScannersTcp.TabIndex = 26;
            this.lblScannersTcp.Text = "扫码枪列表(TCP):";
            //
            // gridScannersTcp
            // TCP 扫码枪清单：一行一台扫码枪。
            // 列结构由 SettingsForm.cs 运行时 SetupScannerGridColumns 添加（启用/IP/端口/触发指令）。
            //
            this.gridScannersTcp.AllowUserToAddRows = true;
            this.gridScannersTcp.AllowUserToDeleteRows = true;
            this.gridScannersTcp.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridScannersTcp.BackgroundColor = System.Drawing.Color.White;
            // 表头与单元格内容居中（与相机列表一致）
            this.gridScannersTcp.ColumnHeadersDefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.gridScannersTcp.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.gridScannersTcp.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.gridScannersTcp.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridScannersTcp.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridScannersTcp.Location = new System.Drawing.Point(20, 566);
            this.gridScannersTcp.Name = "gridScannersTcp";
            this.gridScannersTcp.RowHeadersVisible = false;
            this.gridScannersTcp.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridScannersTcp.Size = new System.Drawing.Size(900, 120);
            this.gridScannersTcp.TabIndex = 27;
            //
            // btnAddScannerTcp
            // 添加一台默认 TCP 扫码枪行（默认值：19.87.6.100 / 9004 / LON）
            //
            this.btnAddScannerTcp.Location = new System.Drawing.Point(20, 700);
            this.btnAddScannerTcp.Name = "btnAddScannerTcp";
            this.btnAddScannerTcp.Size = new System.Drawing.Size(100, 30);
            this.btnAddScannerTcp.TabIndex = 28;
            this.btnAddScannerTcp.Text = "添加一台";
            this.btnAddScannerTcp.UseVisualStyleBackColor = true;
            //
            // btnDelScannerTcp
            // 删除当前选中的 TCP 扫码枪行
            //
            this.btnDelScannerTcp.Location = new System.Drawing.Point(150, 700);
            this.btnDelScannerTcp.Name = "btnDelScannerTcp";
            this.btnDelScannerTcp.Size = new System.Drawing.Size(100, 30);
            this.btnDelScannerTcp.TabIndex = 29;
            this.btnDelScannerTcp.Text = "删除选中";
            this.btnDelScannerTcp.UseVisualStyleBackColor = true;
            //
            // lblScannersSerial
            // 扫码枪列表(串口)标题（V1.12.8 拆表），加粗醒目。
            // 串口表只配串口参数：串口名/波特率/停止位/校验位，方式固定 Serial。
            //
            this.lblScannersSerial.AutoSize = true;
            this.lblScannersSerial.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblScannersSerial.Location = new System.Drawing.Point(20, 748);
            this.lblScannersSerial.Name = "lblScannersSerial";
            this.lblScannersSerial.Size = new System.Drawing.Size(134, 19);
            this.lblScannersSerial.TabIndex = 30;
            this.lblScannersSerial.Text = "扫码枪列表(串口):";
            //
            // gridScannersSerial
            // 串口扫码枪清单：一行一台扫码枪。
            // 列结构由 SettingsForm.cs 运行时 SetupScannerGridColumns 添加（启用/串口名/波特率/停止位/校验位）。
            //
            this.gridScannersSerial.AllowUserToAddRows = true;
            this.gridScannersSerial.AllowUserToDeleteRows = true;
            this.gridScannersSerial.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridScannersSerial.BackgroundColor = System.Drawing.Color.White;
            // 表头与单元格内容居中（与相机列表一致）
            this.gridScannersSerial.ColumnHeadersDefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.gridScannersSerial.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.gridScannersSerial.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.gridScannersSerial.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridScannersSerial.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridScannersSerial.Location = new System.Drawing.Point(20, 774);
            this.gridScannersSerial.Name = "gridScannersSerial";
            this.gridScannersSerial.RowHeadersVisible = false;
            this.gridScannersSerial.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridScannersSerial.Size = new System.Drawing.Size(900, 120);
            this.gridScannersSerial.TabIndex = 31;
            //
            // btnAddScannerSerial
            // 添加一台默认串口扫码枪行（默认值：COM3 / 115200 / 1 / None）
            //
            this.btnAddScannerSerial.Location = new System.Drawing.Point(20, 908);
            this.btnAddScannerSerial.Name = "btnAddScannerSerial";
            this.btnAddScannerSerial.Size = new System.Drawing.Size(100, 30);
            this.btnAddScannerSerial.TabIndex = 32;
            this.btnAddScannerSerial.Text = "添加一台";
            this.btnAddScannerSerial.UseVisualStyleBackColor = true;
            //
            // btnDelScannerSerial
            // 删除当前选中的串口扫码枪行
            //
            this.btnDelScannerSerial.Location = new System.Drawing.Point(150, 908);
            this.btnDelScannerSerial.Name = "btnDelScannerSerial";
            this.btnDelScannerSerial.Size = new System.Drawing.Size(100, 30);
            this.btnDelScannerSerial.TabIndex = 33;
            this.btnDelScannerSerial.Text = "删除选中";
            this.btnDelScannerSerial.UseVisualStyleBackColor = true;
            //
            // pnlScroll
            // 可滚动内容面板：包裹所有配置控件，超出可视高度自动出竖滚动条（V1.12.8）。
            // Dock=Fill 使面板填满 pnlBottom 以上的全部窗体空间；
            // AutoScroll=true 按子控件的最远坐标自动计算滚动范围，无需手动设 AutoScrollMinSize。
            // 【为什么不用整个窗体的 AutoScroll】底部保存/取消按钮需要固定不随滚动，
            //   放在独立的 pnlBottom(Dock=Bottom) 里，与滚动面板分离。
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
            this.pnlScroll.Controls.Add(this.cmbModel);
            this.pnlScroll.Controls.Add(this.lblModel);
            this.pnlScroll.Controls.Add(this.txtPlcIp);
            this.pnlScroll.Controls.Add(this.lblPlcIp);
            this.pnlScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlScroll.Name = "pnlScroll";
            //
            // btnSave
            // 保存：把界面值回写内存配置并返回 OK（上层写盘 + 热生效，V1.6.0 免重启）
            // 固定在底部 pnlBottom，不随内容滚动，始终可见可点。
            //
            this.btnSave.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSave.Location = new System.Drawing.Point(750, 9);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 32);
            this.btnSave.TabIndex = 40;
            this.btnSave.Text = "保存";
            this.btnSave.UseVisualStyleBackColor = true;
            //
            // btnCancel
            // 取消：直接关闭，不写盘；回车/ESC 快捷键见 AcceptButton/CancelButton。
            // 与"保存"右侧对齐，两者间留 10px 间隙；窗体加宽后仍贴右缘（960-90-20=850）。
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(850, 9);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 41;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // pnlBottom
            // 底部按钮栏：固定不滚动，放保存/取消两个按钮。
            // Dock=Bottom + 比 pnlScroll 后加入 Controls → 先占底部 50px，pnlScroll 填满剩余。
            // 浅灰背景与上方内容区形成自然分隔，无需额外分隔线控件。
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
            // SettingsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(960, 700);
            // 先加 pnlScroll(Dock=Fill) 再加 pnlBottom(Dock=Bottom)：
            // WinForms 按倒序 z-index 处理 Dock，后加入的 pnlBottom 先占底部，pnlScroll 再填满剩余。
            this.Controls.Add(this.pnlScroll);
            this.Controls.Add(this.pnlBottom);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "系统设置";
            //
            // tip（ToolTip 气泡：悬停 0.5 秒出提示、停留 8 秒自动消失）
            //
            this.tip.InitialDelay = 500;
            this.tip.ReshowDelay = 100;
            this.tip.AutoPopDelay = 8000;
            this.tip.ShowAlways = true;
            //
            // 悬停提示：按钮、标题、输入框都挂上，现场不用点开就知道每个控件干嘛的。
            //
            this.tip.SetToolTip(this.txtPlcIp,
                "上位机从站监听绑定 IP（V1.12.11 起 PLC 做主站、上位机做从站）。\r\n填 0.0.0.0 监听所有网卡，或填本机指定 IP（如 19.87.6.230）；\r\n保存后即时生效（自动重启从站监听）。");
            this.tip.SetToolTip(this.nudPlcPort,
                "上位机从站监听端口（Modbus TCP 标准 502，需与汇川主站通讯指令里的端口一致）。\r\n保存后即时生效（自动重启从站监听）。");
            this.tip.SetToolTip(this.nudRows,
                "主界面显示窗口的行数。窗口总数=行×列；保存后即时生效。\r\n新增窗口的存图点位默认=窗口编号，可在下方\"窗口/点位配置...\"里改。\r\n勾选\"自适应\"后本框自动置灰（行数由相机点位表自动计算）。");
            this.tip.SetToolTip(this.nudCols,
                "主界面显示窗口的列数。窗口总数=行×列；保存后即时生效。\r\n新增窗口的存图点位默认=窗口编号，可在下方\"窗口/点位配置...\"里改。\r\n勾选\"自适应\"后本框自动置灰（列数由相机点位表自动计算）。");
            this.tip.SetToolTip(this.txtSaveDir,
                "图片保存的根目录（绝对路径）。\r\n实际目录结构按\"配置目录结构...\"里的层级逐级创建。");
            this.tip.SetToolTip(this.btnEditDirs,
                "可视化编辑存图目录结构（目录层级列表 + 文件名规则），并实时预览 OK/NG 两条落盘路径。\r\n当前结构见下方动态提示。");
            this.tip.SetToolTip(this.txtFileNameTpl,
                "图片文件名规则，占位符会自动替换：\r\n{点位}→窗口点位号（如 1.png）  {SN}→序列号  {OKNG}→OK 或 NG\r\n{年}/{月}/{日}→日期  {时间}→毫秒时间戳；其余文字原样保留。\r\n目录结构里的层级同样支持这些占位符。");
            this.tip.SetToolTip(this.btnEditPoints,
                "可视化设置每个窗口的存图点位（默认点位=窗口编号）。\r\n点格子选中→\"编辑点位\"改存图号；\"交换位置\"互换两个窗口的内容（编号固定跟随格子）；\"恢复默认\"一键还原。\r\n改动随本次\"保存\"一起写盘。\r\n勾选\"自适应\"后仅【禁用/启用】窗口与相机程序映射可用，点位编辑/交换/恢复 自动锁定。");
            this.tip.SetToolTip(this.btnAddCam,
                "在列表末尾添加一台相机（默认值可直接改 IP / 端口 / FTP 上传目录）。");
            this.tip.SetToolTip(this.chkTitleOkNg,
                "标题栏的 OK / NG 计数用\"实心彩色色块 + 白字\"高亮（绿底=OK、红底=NG），\r\n比普通彩色文字醒目得多。取消则回退彩色文字样式。保存后即时生效。");
            this.tip.SetToolTip(this.chkWindowOkNg,
                "主界面每个显示窗口右下角叠加一个【矩形框 OK/NG 徽标】（样子同标题栏色块，\r\n颜色随 \"OK颜色/NG颜色\" 配置）。默认关闭（保持画面干净），需要实时看每格结果时可勾选。\r\n保存后即时生效。");
            this.tip.SetToolTip(this.chkWindowIndex,
                "主界面每个显示窗口左上角是否显示【窗口编号】（半透明白底 + 深蓝灰字，辅助现场定位第几路）。\r\n默认勾选（与历史画面一致）；现场嫌编号碍眼可取消勾选，保存后即时生效。");
            this.tip.SetToolTip(this.chkWindowToolTip,
                "鼠标放到主界面任一显示窗口内停留片刻，是否弹出【双击放大/还原】气泡提示。\r\n默认勾选（方便新手操作员发现双击功能）；现场嫌气泡挡画面可取消勾选，保存后即时生效。");
            this.tip.SetToolTip(this.chkAutoFit,
                "勾选【自适应】后主界面窗口矩阵【不再手动指定行列】，而是按当前产品型号 + 各相机\r\n\"点位→程序号\"表自动铺排（窗口总数=各相机点位和、前上相机后下相机）。\r\n\r\n【同时锁定以下功能（相关输入/按钮自动置灰，避免误操作）】\r\n· 显示窗口 行/列 输入框（行列由系统自动算）；\r\n· 窗口/点位配置里的【编辑点位】【交换位置】【恢复默认】（点位由相机点位表决定，不可手改）；\r\n仍可用：【禁用/启用】窗口、相机程序映射（点位→程序号）。");
            this.tip.SetToolTip(this.btnDelCam,
                "删除选中的相机行；未选中时先点选要删的行。");
            this.tip.SetToolTip(this.lblScannersTcp,
                "TCP 扫码枪列表：基恩士 SR 系列以太网扫码枪，一台一行。\r\n任何一台扫到的条码都会更新当前序列号（标题栏与存图目录同步）。\r\n\"启用\"不打勾则这台不接入（序列号可双击标题栏序列号框手动输入，V1.12.17）。\r\nV1.12.8 起拆为独立的 TCP 表，不再与串口混在同一张表里。");
            this.tip.SetToolTip(this.lblScannersSerial,
                "串口扫码枪列表：RS-232 串口扫码枪，一台一行。\r\n串口扫码枪上电即读码、无需触发指令（与 TCP 不同）。\r\n\"启用\"不打勾则这台不接入。");
            this.tip.SetToolTip(this.btnAddScannerTcp,
                "添加一台 TCP 扫码枪（默认 19.87.6.100 / 9004 / LON，可直接改）。");
            this.tip.SetToolTip(this.btnDelScannerTcp,
                "删除选中的 TCP 扫码枪行；未选中时先点选要删的行。");
            this.tip.SetToolTip(this.btnAddScannerSerial,
                "添加一台串口扫码枪（默认 COM3 / 115200 / 1 / None，可直接改）。");
            this.tip.SetToolTip(this.btnDelScannerSerial,
                "删除选中的串口扫码枪行；未选中时先点选要删的行。");
            this.tip.SetToolTip(this.btnSave,
                "保存所有设置并写盘到 Config/appconfig.json，保存后即时生效（V1.6.0 免重启）。\r\n服务层按新配置自动重建，设备短暂断连后几秒内自动连回。");
            this.tip.SetToolTip(this.btnCancel,
                "放弃本次修改并关闭，不写盘。");
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
        private Label lblModel;
        private ComboBox cmbModel;
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
