using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// DirTreeEditForm 的窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 布局请对照 DirTreeEditForm.cs 类注释里的 ASCII 布局图：
    ///   根目录输入框 + 浏览按钮
    ///   目录层级 ListBox（可增删移）
    ///   层级名字/规则编辑框 + 占位符下拉插入
    ///   文件名规则编辑框
    ///   实时预览 Label
    ///   确定/取消按钮
    /// </summary>
    partial class DirTreeEditForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        private void InitializeComponent()
        {
            this.lblRootDir = new System.Windows.Forms.Label();
            this.txtSaveRootDir = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.lblLevels = new System.Windows.Forms.Label();
            this.lstLevels = new System.Windows.Forms.ListBox();
            this.lblLevelName = new System.Windows.Forms.Label();
            this.txtLevelName = new System.Windows.Forms.TextBox();
            this.lblPh = new System.Windows.Forms.Label();
            this.cmbPlaceholder = new System.Windows.Forms.ComboBox();
            this.btnInsertPh = new System.Windows.Forms.Button();
            this.btnAddLevel = new System.Windows.Forms.Button();
            this.btnInsertLevel = new System.Windows.Forms.Button();
            this.btnDeleteLevel = new System.Windows.Forms.Button();
            this.btnUp = new System.Windows.Forms.Button();
            this.btnDown = new System.Windows.Forms.Button();
            this.lblFileRule = new System.Windows.Forms.Label();
            this.txtFileNameTpl = new System.Windows.Forms.TextBox();
            this.lblNote = new System.Windows.Forms.Label();
            this.gbPreview = new System.Windows.Forms.GroupBox();
            this.tvPreview = new System.Windows.Forms.TreeView();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.gbPreview.SuspendLayout();
            this.SuspendLayout();
            //
            // lblRootDir
            //
            this.lblRootDir.AutoSize = true;
            this.lblRootDir.Location = new System.Drawing.Point(20, 26);
            this.lblRootDir.Name = "lblRootDir";
            this.lblRootDir.Size = new System.Drawing.Size(61, 19);
            this.lblRootDir.TabIndex = 0;
            this.lblRootDir.Text = "根目录:";
            //
            // txtSaveRootDir
            //
            this.txtSaveRootDir.Location = new System.Drawing.Point(90, 23);
            this.txtSaveRootDir.Name = "txtSaveRootDir";
            this.txtSaveRootDir.Size = new System.Drawing.Size(420, 25);
            this.txtSaveRootDir.TabIndex = 1;
            this.txtSaveRootDir.Text = "D:\\CommandCenter\\Images";
            //
            // btnBrowse
            //
            this.btnBrowse.Location = new System.Drawing.Point(520, 22);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(80, 28);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "浏览...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            //
            // lblLevels
            //
            this.lblLevels.AutoSize = true;
            this.lblLevels.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblLevels.Location = new System.Drawing.Point(20, 68);
            this.lblLevels.Name = "lblLevels";
            this.lblLevels.Size = new System.Drawing.Size(166, 19);
            this.lblLevels.TabIndex = 3;
            this.lblLevels.Text = "目录层级（从上到下逐级建目录）:";
            //
            // lstLevels
            //
            this.lstLevels.FormattingEnabled = true;
            this.lstLevels.ItemHeight = 17;
            this.lstLevels.Location = new System.Drawing.Point(20, 95);
            this.lstLevels.Name = "lstLevels";
            this.lstLevels.Size = new System.Drawing.Size(580, 140);
            this.lstLevels.TabIndex = 4;
            //
            // lblLevelName
            //
            this.lblLevelName.AutoSize = true;
            this.lblLevelName.Location = new System.Drawing.Point(20, 258);
            this.lblLevelName.Name = "lblLevelName";
            this.lblLevelName.Size = new System.Drawing.Size(141, 19);
            this.lblLevelName.TabIndex = 5;
            this.lblLevelName.Text = "当前层级名字/规则:";
            //
            // txtLevelName
            //
            this.txtLevelName.Location = new System.Drawing.Point(160, 255);
            this.txtLevelName.Name = "txtLevelName";
            this.txtLevelName.Size = new System.Drawing.Size(440, 25);
            this.txtLevelName.TabIndex = 6;
            //
            // lblPh
            //
            this.lblPh.AutoSize = true;
            this.lblPh.Location = new System.Drawing.Point(20, 296);
            this.lblPh.Name = "lblPh";
            this.lblPh.Size = new System.Drawing.Size(96, 19);
            this.lblPh.TabIndex = 7;
            this.lblPh.Text = "插入占位符:";
            //
            // cmbPlaceholder
            //
            this.cmbPlaceholder.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPlaceholder.Items.AddRange(new object[] {
                "{年月日}", "{年}", "{月}", "{日}", "{SN}", "{OKNG}", "{点位}", "{时间}"});
            this.cmbPlaceholder.Location = new System.Drawing.Point(160, 293);
            this.cmbPlaceholder.Name = "cmbPlaceholder";
            this.cmbPlaceholder.SelectedIndex = 0;
            this.cmbPlaceholder.Size = new System.Drawing.Size(120, 25);
            this.cmbPlaceholder.TabIndex = 8;
            this.cmbPlaceholder.Text = "{年月日}";
            //
            // btnInsertPh
            //
            this.btnInsertPh.Location = new System.Drawing.Point(290, 292);
            this.btnInsertPh.Name = "btnInsertPh";
            this.btnInsertPh.Size = new System.Drawing.Size(80, 28);
            this.btnInsertPh.TabIndex = 9;
            this.btnInsertPh.Text = "插入";
            this.btnInsertPh.UseVisualStyleBackColor = true;
            //
            // btnAddLevel
            //
            this.btnAddLevel.Location = new System.Drawing.Point(20, 338);
            this.btnAddLevel.Name = "btnAddLevel";
            this.btnAddLevel.Size = new System.Drawing.Size(100, 30);
            this.btnAddLevel.TabIndex = 10;
            this.btnAddLevel.Text = "添加层级";
            this.btnAddLevel.UseVisualStyleBackColor = true;
            //
            // btnInsertLevel
            //
            this.btnInsertLevel.Location = new System.Drawing.Point(130, 338);
            this.btnInsertLevel.Name = "btnInsertLevel";
            this.btnInsertLevel.Size = new System.Drawing.Size(110, 30);
            this.btnInsertLevel.TabIndex = 11;
            this.btnInsertLevel.Text = "插入到上方";
            this.btnInsertLevel.UseVisualStyleBackColor = true;
            //
            // btnDeleteLevel
            //
            this.btnDeleteLevel.Location = new System.Drawing.Point(250, 338);
            this.btnDeleteLevel.Name = "btnDeleteLevel";
            this.btnDeleteLevel.Size = new System.Drawing.Size(100, 30);
            this.btnDeleteLevel.TabIndex = 12;
            this.btnDeleteLevel.Text = "删除选中";
            this.btnDeleteLevel.UseVisualStyleBackColor = true;
            //
            // btnUp
            //
            this.btnUp.Location = new System.Drawing.Point(370, 338);
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(50, 30);
            this.btnUp.TabIndex = 13;
            this.btnUp.Text = "↑ 上移";
            this.btnUp.UseVisualStyleBackColor = true;
            //
            // btnDown
            //
            this.btnDown.Location = new System.Drawing.Point(430, 338);
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(50, 30);
            this.btnDown.TabIndex = 14;
            this.btnDown.Text = "↓ 下移";
            this.btnDown.UseVisualStyleBackColor = true;
            //
            // lblFileRule
            //
            this.lblFileRule.AutoSize = true;
            this.lblFileRule.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.lblFileRule.Location = new System.Drawing.Point(20, 388);
            this.lblFileRule.Name = "lblFileRule";
            this.lblFileRule.Size = new System.Drawing.Size(96, 19);
            this.lblFileRule.TabIndex = 15;
            this.lblFileRule.Text = "文件名规则:";
            //
            // txtFileNameTpl
            //
            this.txtFileNameTpl.Location = new System.Drawing.Point(130, 385);
            this.txtFileNameTpl.Name = "txtFileNameTpl";
            this.txtFileNameTpl.Size = new System.Drawing.Size(470, 25);
            this.txtFileNameTpl.TabIndex = 16;
            this.txtFileNameTpl.Text = "{点位}";
            //
            // lblNote
            //
            this.lblNote.AutoSize = true;
            this.lblNote.ForeColor = System.Drawing.Color.Gray;
            this.lblNote.Location = new System.Drawing.Point(20, 420);
            this.lblNote.Name = "lblNote";
            this.lblNote.Size = new System.Drawing.Size(571, 19);
            this.lblNote.TabIndex = 17;
            this.lblNote.Text = "占位符:{年月日} 整个目录名(2026年08月11日)  {SN}序列号  {OKNG}判成OK或NG目录  {点位}点位号  {时间}毫秒时间戳";
            //
            // gbPreview
            // 实时目录结构预览：按当前层级规则用示例数据展开成"文件夹树"，
            // 让现场一眼看到将来落盘的完整目录长什么样（OK/NG 两种分支各展示一棵子树）。
            //
            this.gbPreview.Controls.Add(this.tvPreview);
            this.gbPreview.Location = new System.Drawing.Point(20, 445);
            this.gbPreview.Name = "gbPreview";
            this.gbPreview.Size = new System.Drawing.Size(584, 150);
            this.gbPreview.TabIndex = 18;
            this.gbPreview.TabStop = false;
            this.gbPreview.Text = "实时预览（示例：今天日期 / SN-0001 / 点位1）";
            //
            // tvPreview
            //
            this.tvPreview.HideSelection = false;
            this.tvPreview.Location = new System.Drawing.Point(12, 24);
            this.tvPreview.Name = "tvPreview";
            this.tvPreview.PathSeparator = "\\";
            this.tvPreview.ShowLines = true;
            this.tvPreview.ShowPlusMinus = true;
            this.tvPreview.ShowRootLines = true;
            this.tvPreview.Size = new System.Drawing.Size(560, 112);
            this.tvPreview.TabIndex = 0;
            //
            // btnOk
            //
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(420, 610);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 32);
            this.btnOk.TabIndex = 19;
            this.btnOk.Text = "确定";
            this.btnOk.UseVisualStyleBackColor = true;
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(520, 610);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 20;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // DirTreeEditForm
            //
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 658);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.gbPreview);
            this.Controls.Add(this.lblNote);
            this.Controls.Add(this.txtFileNameTpl);
            this.Controls.Add(this.lblFileRule);
            this.Controls.Add(this.btnDown);
            this.Controls.Add(this.btnUp);
            this.Controls.Add(this.btnDeleteLevel);
            this.Controls.Add(this.btnInsertLevel);
            this.Controls.Add(this.btnAddLevel);
            this.Controls.Add(this.btnInsertPh);
            this.Controls.Add(this.cmbPlaceholder);
            this.Controls.Add(this.lblPh);
            this.Controls.Add(this.txtLevelName);
            this.Controls.Add(this.lblLevelName);
            this.Controls.Add(this.lstLevels);
            this.Controls.Add(this.lblLevels);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.txtSaveRootDir);
            this.Controls.Add(this.lblRootDir);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DirTreeEditForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "图片存储目录结构配置";
            this.gbPreview.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // 设计器声明的字段（命名遵循匈牙利前缀规范）
        private Label lblRootDir;
        private TextBox txtSaveRootDir;
        private Button btnBrowse;
        private Label lblLevels;
        private ListBox lstLevels;
        private Label lblLevelName;
        private TextBox txtLevelName;
        private Label lblPh;
        private ComboBox cmbPlaceholder;
        private Button btnInsertPh;
        private Button btnAddLevel;
        private Button btnInsertLevel;
        private Button btnDeleteLevel;
        private Button btnUp;
        private Button btnDown;
        private Label lblFileRule;
        private TextBox txtFileNameTpl;
        private Label lblNote;
        private GroupBox gbPreview;
        private TreeView tvPreview;
        private Button btnOk;
        private Button btnCancel;
    }
}
