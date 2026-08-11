using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// SettingsForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 把"静态、数量与位置固定"的控件全部放进设计器，便于可视化拖拽：
    ///   PLC IP/端口、显示窗口行列、图片保存相关三个输入框、相机列表 DataGridView、
    ///   添加/删除相机、保存/取消 按钮。
    /// 这些控件都是固定布局（无运行时紧凑重排需求），设计器坐标即最终坐标。
    /// 【重要】整体顺序请参考 SettingsForm.cs 类注释里的 ASCII 布局图。
    ///   ┌────────────────────────────────────────────────────┐
    ///   │ PLC IP:   [txtPlcIp]   端口:[nudPlcPort]           │
    ///   │ 显示窗口行:[nudRows] 列:[nudCols]                   │
    ///   │ 图片保存根目录: [txtSaveDir]                         │
    ///   │ 目录结构: [btnEditDirs 配置目录结构…]               │
    ///   │    （上下各留 12px 空隙，避免与文件名模板行挤在一起）│
    ///   │ 文件名模板:     [txtFileNameTpl]                    │
    ///   │ 窗口点位: [btnEditPoints 窗口/点位配置…]            │
    ///   │ OK/NG显示: [√标题栏高亮]                            │
    ///   │ 相机列表:                                          │
    ///   │   ┌──────────────────────────────────────────────┐ │
    ///   │   │ gridCameras（DataGridView）                    │ │
    ///   │   └──────────────────────────────────────────────┘ │
    ///   │   [btnAddCam] [btnDelCam]       [btnSave] [btnCancel]│
    ///   └────────────────────────────────────────────────────┘
    /// 说明：
    ///   - 控件说明不占界面：原常驻灰字标签（lblDirPreview/lblHelp/lblPointsHelp）已删除，
    ///     统一改为 ToolTip 气泡（悬停按钮/标题/输入框 0.5 秒显示，Windows 标准延迟）。
    ///     其中"当前目录结构"是动态信息，实时挂在"配置目录结构..."按钮的 ToolTip 里。
    ///   - 控件的"显示内容"（IP/端口/行列/目录模板/相机行）由 SettingsForm.cs 运行时
    ///     从 AppConfig 填充（LoadFromConfig），设计器里的值只是可视化参照。
    ///   - gridCameras 的 4 个列由运行时代码添加（AddCameraColumns：相机IP/触发端口/FTP上传目录/
    ///     取图方式下拉框），不在设计器序列化，避免 DataGridView 列序列化代码冗长易错；
    ///     外观与行为在设计器里设置。
    ///   - 保存/取消按钮的 DialogResult 在设计器里设好，点保存时上层按 DialogResult 判断。
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
            this.lblCams = new System.Windows.Forms.Label();
            this.gridCameras = new System.Windows.Forms.DataGridView();
            this.btnAddCam = new System.Windows.Forms.Button();
            this.btnDelCam = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tip = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.nudPlcPort)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudRows)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudCols)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCameras)).BeginInit();
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
            this.txtPlcIp.Text = "192.168.1.100";
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
            // 图片保存根目录（绝对路径），右侧预留到窗体边缘（宽 570）
            //
            this.txtSaveDir.Location = new System.Drawing.Point(130, 102);
            this.txtSaveDir.Name = "txtSaveDir";
            this.txtSaveDir.Size = new System.Drawing.Size(570, 25);
            this.txtSaveDir.TabIndex = 9;
            this.txtSaveDir.Text = "D:\\CommandCenter\\Images";
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
            // 图片文件名模板。原右侧的占位符常驻标签（lblHelp）已删，说明并入悬停 ToolTip；
            // 因此输入框一路加宽到窗体右缘，与"图片保存根目录"对齐，更整齐。
            //
            this.txtFileNameTpl.Location = new System.Drawing.Point(130, 181);
            this.txtFileNameTpl.Name = "txtFileNameTpl";
            this.txtFileNameTpl.Size = new System.Drawing.Size(570, 25);
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
            // 操作方式说明并入悬停 ToolTip（原 lblPointsHelp 已删）。
            //
            this.btnEditPoints.Location = new System.Drawing.Point(130, 216);
            this.btnEditPoints.Name = "btnEditPoints";
            this.btnEditPoints.Size = new System.Drawing.Size(150, 30);
            this.btnEditPoints.TabIndex = 16;
            this.btnEditPoints.Text = "窗口/点位配置...";
            this.btnEditPoints.UseVisualStyleBackColor = true;
            //
            // lblOkNg
            // "OK/NG 显示" 配置行标题（标题栏 OK/NG 计数高亮开关的说明，与开关垂直居中）
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
            // 现场嫌"只显示带颜色数字不够醒目"，V1.5.0 默认开；关闭则回退普通彩色文字
            //
            this.chkTitleOkNg.AutoSize = true;
            this.chkTitleOkNg.Location = new System.Drawing.Point(130, 251);
            this.chkTitleOkNg.Name = "chkTitleOkNg";
            this.chkTitleOkNg.Size = new System.Drawing.Size(111, 23);
            this.chkTitleOkNg.TabIndex = 15;
            this.chkTitleOkNg.Text = "标题栏高亮";
            this.chkTitleOkNg.UseVisualStyleBackColor = true;
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
            // 列结构由 SettingsForm.cs 运行时 AddCameraColumns 添加，此处只设外观与编辑行为；
            // AllowUserToAddRows/DeleteRows 打开后可直接在表格里增删行。
            //
            this.gridCameras.AllowUserToAddRows = true;
            this.gridCameras.AllowUserToDeleteRows = true;
            this.gridCameras.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridCameras.BackgroundColor = System.Drawing.Color.White;
            this.gridCameras.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.gridCameras.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridCameras.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridCameras.Location = new System.Drawing.Point(20, 322);
            this.gridCameras.Name = "gridCameras";
            this.gridCameras.RowHeadersVisible = false;
            // 整行选择：点任意单元格都整行高亮 → SelectedRows 才有值，"删除选中"才好使
            this.gridCameras.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridCameras.Size = new System.Drawing.Size(680, 150);
            this.gridCameras.TabIndex = 21;
            //
            // btnAddCam
            // 添加一台默认相机行（默认值 192.168.1.1 / 8500 / 点位1 / FTP留空用全局）
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
            // btnSave
            // 保存：把界面值回写内存配置并返回 OK（上层写盘 + 热生效，V1.6.0 免重启）
            //
            this.btnSave.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSave.Location = new System.Drawing.Point(490, 492);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 32);
            this.btnSave.TabIndex = 24;
            this.btnSave.Text = "保存";
            this.btnSave.UseVisualStyleBackColor = true;
            //
            // btnCancel
            // 取消：直接关闭，不写盘；回车/ESC 快捷键见 AcceptButton/CancelButton。
            // 位置右边缘与上方控件（根目录框/模板框/相机网格，右边缘均=700）对齐，
            // 与"保存"之间留 30px 间隙（与"添加一台/删除选中"一致），悬停有说明即可。
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(610, 492);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 25;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // SettingsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(720, 632);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnDelCam);
            this.Controls.Add(this.btnAddCam);
            this.Controls.Add(this.gridCameras);
            this.Controls.Add(this.lblCams);
            this.Controls.Add(this.chkTitleOkNg);
            this.Controls.Add(this.lblOkNg);
            this.Controls.Add(this.btnEditPoints);
            this.Controls.Add(this.lblPoints);
            this.Controls.Add(this.txtFileNameTpl);
            this.Controls.Add(this.lblFile);
            this.Controls.Add(this.btnEditDirs);
            this.Controls.Add(this.txtSaveDir);
            this.Controls.Add(this.lblDir);
            this.Controls.Add(this.nudCols);
            this.Controls.Add(this.lblCols);
            this.Controls.Add(this.nudRows);
            this.Controls.Add(this.lblRows);
            this.Controls.Add(this.nudPlcPort);
            this.Controls.Add(this.lblPlcPort);
            this.Controls.Add(this.txtPlcIp);
            this.Controls.Add(this.lblPlcIp);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "系统设置";
            //
            // tip（ToolTip 气泡：悬停 0.5 秒出提示、停留 8 秒自动消失）
            // 悬停延迟 InitialDelay=500ms 是 Windows 工具提示的标准参数（行业惯例 0.4~0.7s），
            // ReshowDelay=100ms 表示在别的控件间快速移动时缩短再次弹出等待；
            // AutoPopDelay=8000ms 停留 8 秒自动消失，避免气泡挡住界面；
            // ShowAlways=true 窗体未激活时也显示，鼠标移上来就有。
            // 文本以 "?" 开头会显示帮助图标（Windows 惯例：?=帮助提示）。
            //
            this.tip.InitialDelay = 500;
            this.tip.ReshowDelay = 100;
            this.tip.AutoPopDelay = 8000;
            this.tip.ShowAlways = true;
            //
            // 悬停提示：按钮、标题、输入框都挂上，现场不用点开就知道每个控件干嘛的。
            // "配置目录结构..."按钮的 ToolTip 内容是动态的（当前目录结构），在 SettingsForm.cs 里刷新。
            //
            this.tip.SetToolTip(this.txtPlcIp,
                "PLC 的 IP 地址（汇川，Modbus TCP 从站）。\r\n与上位机同一网段、能 ping 通；保存后即时生效（自动按新 IP 重连）。");
            this.tip.SetToolTip(this.nudPlcPort,
                "PLC 通讯端口，默认 502（Modbus TCP 标准端口）。\r\n保存后即时生效（自动重连）。");
            this.tip.SetToolTip(this.nudRows,
                "主界面显示窗口的行数。窗口总数=行×列；保存后即时生效。\r\n新增窗口的存图点位默认=窗口编号，可在下方\"窗口/点位配置...\"里改。");
            this.tip.SetToolTip(this.nudCols,
                "主界面显示窗口的列数。窗口总数=行×列；保存后即时生效。\r\n新增窗口的存图点位默认=窗口编号，可在下方\"窗口/点位配置...\"里改。");
            this.tip.SetToolTip(this.txtSaveDir,
                "图片保存的根目录（绝对路径）。\r\n实际目录结构按\"配置目录结构...\"里的层级逐级创建。");
            this.tip.SetToolTip(this.btnEditDirs,
                "可视化编辑存图目录结构（目录层级列表 + 文件名规则），并实时预览 OK/NG 两条落盘路径。\r\n当前结构见下方动态提示。");
            this.tip.SetToolTip(this.txtFileNameTpl,
                "图片文件名规则，占位符会自动替换：\r\n{点位}→窗口点位号（如 1.png）  {SN}→序列号  {OKNG}→OK 或 NG\r\n{年}/{月}/{日}→日期  {时间}→毫秒时间戳；其余文字原样保留。\r\n目录结构里的层级同样支持这些占位符。");
            this.tip.SetToolTip(this.btnEditPoints,
                "可视化设置每个窗口的存图点位（默认点位=窗口编号）。\r\n点格子选中→\"编辑点位\"改存图号；\"交换位置\"互换两个窗口的内容（编号固定跟随格子）；\"恢复默认\"一键还原。\r\n改动随本次\"保存\"一起写盘。");
            this.tip.SetToolTip(this.btnAddCam,
                "在列表末尾添加一台相机（默认值可直接改 IP / 端口 / FTP 上传目录）。");
            this.tip.SetToolTip(this.chkTitleOkNg,
                "标题栏的 OK / NG 计数用\"实心彩色色块 + 白字\"高亮（绿底=OK、红底=NG），\r\n比普通彩色文字醒目得多。取消则回退彩色文字样式。保存后即时生效。");
            this.tip.SetToolTip(this.btnAddCam,
                "在列表末尾添加一台相机（默认值可直接改 IP / 端口 / FTP 上传目录）。");
            this.tip.SetToolTip(this.btnDelCam,
                "删除选中的相机行；未选中时先点选要删的行。");
            this.tip.SetToolTip(this.btnSave,
                "保存所有设置并写盘到 Config/appconfig.json，保存后即时生效（V1.6.0 免重启）。\r\n服务层按新配置自动重建，设备短暂断连后几秒内自动连回。");
            this.tip.SetToolTip(this.btnCancel,
                "放弃本次修改并关闭，不写盘。");
            ((System.ComponentModel.ISupportInitialize)(this.nudPlcPort)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudRows)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudCols)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridCameras)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // 设计器声明的字段（视觉化拖拽所需；命名遵循匈牙利前缀规范）
        private Label lblPlcIp;
        private TextBox txtPlcIp;
        private Label lblPlcPort;
        private NumericUpDown nudPlcPort;
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
        private Label lblCams;
        private DataGridView gridCameras;
        private Button btnAddCam;
        private Button btnDelCam;
        private Button btnSave;
        private Button btnCancel;
        private ToolTip tip;
    }
}