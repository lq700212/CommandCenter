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
    ///   │ 目录结构: [btnEditDirs 配置目录结构…] [lblDirPreview] │
    ///   │ 文件名模板:     [txtFileNameTpl]  (lblHelp 灰字提示) │
    ///   │ 相机列表:                                          │
    ///   │   ┌──────────────────────────────────────────────┐ │
    ///   │   │ gridCameras（DataGridView）                    │ │
    ///   │   └──────────────────────────────────────────────┘ │
    ///   │   [btnAddCam] [btnDelCam]      [btnSave] [btnCancel]│
    ///   └────────────────────────────────────────────────────┘
    /// 说明：
    ///   - 控件的"显示内容"（IP/端口/行列/目录模板/相机行）由 SettingsForm.cs 运行时
    ///     从 AppConfig 填充（LoadFromConfig），设计器里的值只是可视化参照。
    ///   - gridCameras 的 4 个列由运行时代码添加（AddCameraColumns），不在设计器序列化，
    ///     避免 DataGridView 列序列化代码冗长易错；外观与行为在设计器里设置。
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
            this.lblDirPreview = new System.Windows.Forms.Label();
            this.lblFile = new System.Windows.Forms.Label();
            this.txtFileNameTpl = new System.Windows.Forms.TextBox();
            this.lblHelp = new System.Windows.Forms.Label();
            this.lblCams = new System.Windows.Forms.Label();
            this.gridCameras = new System.Windows.Forms.DataGridView();
            this.btnAddCam = new System.Windows.Forms.Button();
            this.btnDelCam = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
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
            // 打开"图片存储目录结构配置"对话框（DirTreeEditForm），可视化编辑目录层级与文件名规则
            //
            this.btnEditDirs.Location = new System.Drawing.Point(130, 139);
            this.btnEditDirs.Name = "btnEditDirs";
            this.btnEditDirs.Size = new System.Drawing.Size(160, 30);
            this.btnEditDirs.TabIndex = 11;
            this.btnEditDirs.Text = "配置目录结构...";
            this.btnEditDirs.UseVisualStyleBackColor = true;
            //
            // lblDirPreview
            // 只读展示当前目录结构（层级名/规则用 / 拼接），点按钮进可视化对话框改
            //
            this.lblDirPreview.AutoSize = true;
            this.lblDirPreview.ForeColor = System.Drawing.Color.Gray;
            this.lblDirPreview.Location = new System.Drawing.Point(300, 145);
            this.lblDirPreview.Name = "lblDirPreview";
            this.lblDirPreview.Size = new System.Drawing.Size(300, 19);
            this.lblDirPreview.TabIndex = 11;
            this.lblDirPreview.Text = "{年月日}/{SN}/{OKNG}";
            //
            // lblFile
            //
            this.lblFile.AutoSize = true;
            this.lblFile.Location = new System.Drawing.Point(20, 172);
            this.lblFile.Name = "lblFile";
            this.lblFile.Size = new System.Drawing.Size(96, 19);
            this.lblFile.TabIndex = 12;
            this.lblFile.Text = "文件名模板:";
            //
            // txtFileNameTpl
            // 图片文件名模板；宽 200，右侧留位给占位符提示（lblHelp）
            //
            this.txtFileNameTpl.Location = new System.Drawing.Point(130, 169);
            this.txtFileNameTpl.Name = "txtFileNameTpl";
            this.txtFileNameTpl.Size = new System.Drawing.Size(200, 25);
            this.txtFileNameTpl.TabIndex = 13;
            this.txtFileNameTpl.Text = "{点位}";
            //
            // lblHelp
            // 模板占位符速查提示，灰字，不动手改
            //
            this.lblHelp.AutoSize = true;
            this.lblHelp.ForeColor = System.Drawing.Color.Gray;
            this.lblHelp.Location = new System.Drawing.Point(340, 172);
            this.lblHelp.Name = "lblHelp";
            this.lblHelp.Size = new System.Drawing.Size(413, 19);
            this.lblHelp.TabIndex = 14;
            this.lblHelp.Text = "占位符:{年}{月}{日}{SN}{OKNG}{点位}{时间}，其余文字原样保留；目录模板用 / 分层";
            //
            // lblCams
            // 相机列表标题，加粗醒目
            //
            this.lblCams.AutoSize = true;
            this.lblCams.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblCams.Location = new System.Drawing.Point(20, 211);
            this.lblCams.Name = "lblCams";
            this.lblCams.Size = new System.Drawing.Size(84, 19);
            this.lblCams.TabIndex = 15;
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
            this.gridCameras.Location = new System.Drawing.Point(20, 237);
            this.gridCameras.Name = "gridCameras";
            this.gridCameras.RowHeadersVisible = false;
            // 整行选择：点任意单元格都整行高亮 → SelectedRows 才有值，"删除选中"才好使
            this.gridCameras.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridCameras.Size = new System.Drawing.Size(680, 170);
            this.gridCameras.TabIndex = 16;
            //
            // btnAddCam
            // 添加一台默认相机行（默认值 192.168.1.1 / 8500 / 点位1 / FTP留空用全局）
            //
            this.btnAddCam.Location = new System.Drawing.Point(20, 417);
            this.btnAddCam.Name = "btnAddCam";
            this.btnAddCam.Size = new System.Drawing.Size(100, 30);
            this.btnAddCam.TabIndex = 17;
            this.btnAddCam.Text = "添加一台";
            this.btnAddCam.UseVisualStyleBackColor = true;
            //
            // btnDelCam
            // 删除当前选中的相机行
            //
            this.btnDelCam.Location = new System.Drawing.Point(130, 417);
            this.btnDelCam.Name = "btnDelCam";
            this.btnDelCam.Size = new System.Drawing.Size(100, 30);
            this.btnDelCam.TabIndex = 18;
            this.btnDelCam.Text = "删除选中";
            this.btnDelCam.UseVisualStyleBackColor = true;
            //
            // btnSave
            // 保存：把界面值回写内存配置并返回 OK（上层写盘 + 提示重启）
            //
            this.btnSave.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSave.Location = new System.Drawing.Point(300, 417);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 32);
            this.btnSave.TabIndex = 19;
            this.btnSave.Text = "保存";
            this.btnSave.UseVisualStyleBackColor = true;
            //
            // btnCancel
            // 取消：直接关闭，不写盘；回车/ESC 快捷键见 AcceptButton/CancelButton
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(400, 417);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 20;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // SettingsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AcceptButton = this.btnSave;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(720, 560);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnDelCam);
            this.Controls.Add(this.btnAddCam);
            this.Controls.Add(this.gridCameras);
            this.Controls.Add(this.lblCams);
            this.Controls.Add(this.lblHelp);
            this.Controls.Add(this.txtFileNameTpl);
            this.Controls.Add(this.lblFile);
            this.Controls.Add(this.lblDirPreview);
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
        private Label lblDirPreview;
        private Label lblFile;
        private TextBox txtFileNameTpl;
        private Label lblHelp;
        private Label lblCams;
        private DataGridView gridCameras;
        private Button btnAddCam;
        private Button btnDelCam;
        private Button btnSave;
        private Button btnCancel;
    }
}