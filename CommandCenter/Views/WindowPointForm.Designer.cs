using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// WindowPointForm 的窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 布局请对照 WindowPointForm.cs 类注释里的 ASCII 布局图：
    ///   顶部说明标签（lblHint，随"交换模式"提示实时切换文案）
    ///   窗口↔点位矩阵（pnlMatrix + tblMatrix，格子是运行时代码生成的 Button，不在设计器里）
    ///   相机程序映射区（grpProgram）：相机下拉 cmbCamera + 点位/程序号表格 dgvPrograms +
    ///     btnAddProg/btnDelProg 新增删除 + lblProgNote 说明（V1.12.25 同页混排新增）
    ///   底部：编辑点位 / 交换位置 / 恢复默认 / 确定 / 取消
    /// </summary>
    partial class WindowPointForm
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
            this.lblHint = new System.Windows.Forms.Label();
            this.pnlMatrix = new System.Windows.Forms.Panel();
            this.tblMatrix = new System.Windows.Forms.TableLayoutPanel();
            this.btnEditPoint = new System.Windows.Forms.Button();
            this.btnSwap = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnDisable = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpProgram = new System.Windows.Forms.GroupBox();
            this.lblProgNote = new System.Windows.Forms.Label();
            this.lblProgHint = new System.Windows.Forms.Label();
            this.btnAddProg = new System.Windows.Forms.Button();
            this.btnDelProg = new System.Windows.Forms.Button();
            this.cmbCamera = new System.Windows.Forms.ComboBox();
            this.lblCamera = new System.Windows.Forms.Label();
            this.lblModel = new System.Windows.Forms.Label();
            this.cmbModel = new System.Windows.Forms.ComboBox();
            this.dgvPrograms = new System.Windows.Forms.DataGridView();
            this.colStation = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colProgram = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.pnlMatrix.SuspendLayout();
            this.grpProgram.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrograms)).BeginInit();
            this.SuspendLayout();
            // 
            // lblHint
            // 
            this.lblHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.lblHint.Location = new System.Drawing.Point(20, 14);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(600, 42);
            this.lblHint.TabIndex = 0;
            this.lblHint.Text = "每个格子 = 主界面一个显示窗口。上方是【固定编号】；下方是它的【存图点位】。\r\n单击格子选中，点\"编辑点位\"改存图号；点\"交换位置\"可把两个窗口的内容互换（编" +
    "号固定）。";
            // 
            // pnlMatrix
            // 
            this.pnlMatrix.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlMatrix.Controls.Add(this.tblMatrix);
            this.pnlMatrix.Location = new System.Drawing.Point(20, 64);
            this.pnlMatrix.Name = "pnlMatrix";
            this.pnlMatrix.Size = new System.Drawing.Size(600, 296);
            this.pnlMatrix.TabIndex = 1;
            // 
            // tblMatrix
            // 
            this.tblMatrix.ColumnCount = 1;
            this.tblMatrix.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tblMatrix.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblMatrix.Location = new System.Drawing.Point(0, 0);
            this.tblMatrix.Name = "tblMatrix";
            this.tblMatrix.RowCount = 1;
            this.tblMatrix.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tblMatrix.Size = new System.Drawing.Size(598, 294);
            this.tblMatrix.TabIndex = 0;
            // 
            // grpProgram
            // 
            this.grpProgram.Controls.Add(this.lblProgNote);
            this.grpProgram.Controls.Add(this.lblProgHint);
            this.grpProgram.Controls.Add(this.btnAddProg);
            this.grpProgram.Controls.Add(this.btnDelProg);
            this.grpProgram.Controls.Add(this.cmbCamera);
            this.grpProgram.Controls.Add(this.lblCamera);
            this.grpProgram.Controls.Add(this.cmbModel);
            this.grpProgram.Controls.Add(this.lblModel);
            this.grpProgram.Controls.Add(this.dgvPrograms);
            this.grpProgram.Location = new System.Drawing.Point(20, 368);
            this.grpProgram.Name = "grpProgram";
            this.grpProgram.Size = new System.Drawing.Size(600, 228);
            this.grpProgram.TabIndex = 7;
            this.grpProgram.TabStop = false;
            this.grpProgram.Text = "相机程序映射（点位 → 相机程序号，每台相机各自一张表）";
            // 
            // lblCamera
            // 
            this.lblCamera.Location = new System.Drawing.Point(16, 34);
            this.lblCamera.Name = "lblCamera";
            this.lblCamera.Size = new System.Drawing.Size(60, 26);
            this.lblCamera.TabIndex = 8;
            this.lblCamera.Text = "相机：";
            this.lblCamera.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbCamera
            // 
            this.cmbCamera.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCamera.Location = new System.Drawing.Point(78, 32);
            this.cmbCamera.Name = "cmbCamera";
            this.cmbCamera.Size = new System.Drawing.Size(140, 27);
            this.cmbCamera.TabIndex = 9;
            // 
            // lblModel
            // 
            this.lblModel.Location = new System.Drawing.Point(224, 34);
            this.lblModel.Name = "lblModel";
            this.lblModel.Size = new System.Drawing.Size(54, 26);
            this.lblModel.TabIndex = 15;
            this.lblModel.Text = "型号：";
            this.lblModel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbModel
            // 
            this.cmbModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbModel.Location = new System.Drawing.Point(280, 32);
            this.cmbModel.Name = "cmbModel";
            this.cmbModel.Size = new System.Drawing.Size(122, 27);
            this.cmbModel.TabIndex = 16;
            // 
            // lblProgHint
            // 
            this.lblProgHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.lblProgHint.Location = new System.Drawing.Point(410, 30);
            this.lblProgHint.Name = "lblProgHint";
            this.lblProgHint.Size = new System.Drawing.Size(178, 34);
            this.lblProgHint.TabIndex = 10;
            this.lblProgHint.Text = "按“相机+型号”查表切程序；\r\n选“默认”查相机的旧映射表。";
            // 
            // dgvPrograms
            // 
            this.dgvPrograms.AllowUserToAddRows = false;
            this.dgvPrograms.AllowUserToDeleteRows = false;
            this.dgvPrograms.AllowUserToResizeRows = false;
            this.dgvPrograms.BackgroundColor = System.Drawing.Color.White;
            this.dgvPrograms.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPrograms.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colStation,
            this.colProgram});
            this.dgvPrograms.Location = new System.Drawing.Point(16, 72);
            this.dgvPrograms.Name = "dgvPrograms";
            this.dgvPrograms.RowHeadersVisible = false;
            this.dgvPrograms.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvPrograms.Size = new System.Drawing.Size(568, 116);
            this.dgvPrograms.TabIndex = 11;
            // 
            // colStation
            // 
            this.colStation.HeaderText = "点位（选择）";
            this.colStation.Name = "colStation";
            this.colStation.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // colProgram
            // 
            this.colProgram.HeaderText = "相机程序号（选择）";
            this.colProgram.Name = "colProgram";
            this.colProgram.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // btnAddProg
            // 
            this.btnAddProg.Location = new System.Drawing.Point(16, 198);
            this.btnAddProg.Name = "btnAddProg";
            this.btnAddProg.Size = new System.Drawing.Size(90, 30);
            this.btnAddProg.TabIndex = 12;
            this.btnAddProg.Text = "新增映射";
            this.btnAddProg.UseVisualStyleBackColor = true;
            // 
            // btnDelProg
            // 
            this.btnDelProg.Location = new System.Drawing.Point(114, 198);
            this.btnDelProg.Name = "btnDelProg";
            this.btnDelProg.Size = new System.Drawing.Size(100, 30);
            this.btnDelProg.TabIndex = 13;
            this.btnDelProg.Text = "删除选中行";
            this.btnDelProg.UseVisualStyleBackColor = true;
            // 
            // lblProgNote
            // 
            this.lblProgNote.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblProgNote.Location = new System.Drawing.Point(224, 202);
            this.lblProgNote.Name = "lblProgNote";
            this.lblProgNote.Size = new System.Drawing.Size(360, 26);
            this.lblProgNote.TabIndex = 14;
            this.lblProgNote.Text = "点位从下拉选（数量=窗口数）；程序号选'不切换'或相机实际程序号（0~127，数量/编号跟相机程序库走，与窗口数无关）";
            this.lblProgNote.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnEditPoint
            // 
            this.btnEditPoint.Location = new System.Drawing.Point(20, 614);
            this.btnEditPoint.Name = "btnEditPoint";
            this.btnEditPoint.Size = new System.Drawing.Size(100, 30);
            this.btnEditPoint.TabIndex = 2;
            this.btnEditPoint.Text = "编辑点位";
            this.btnEditPoint.UseVisualStyleBackColor = true;
            // 
            // btnSwap
            // 
            this.btnSwap.Location = new System.Drawing.Point(130, 614);
            this.btnSwap.Name = "btnSwap";
            this.btnSwap.Size = new System.Drawing.Size(100, 30);
            this.btnSwap.TabIndex = 3;
            this.btnSwap.Text = "交换位置";
            this.btnSwap.UseVisualStyleBackColor = true;
            // 
            // btnReset
            // 
            this.btnReset.Location = new System.Drawing.Point(240, 614);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(100, 30);
            this.btnReset.TabIndex = 4;
            this.btnReset.Text = "恢复默认";
            this.btnReset.UseVisualStyleBackColor = true;
            // 
            // btnDisable
            // 
            this.btnDisable.Location = new System.Drawing.Point(350, 614);
            this.btnDisable.Name = "btnDisable";
            this.btnDisable.Size = new System.Drawing.Size(70, 30);
            this.btnDisable.TabIndex = 15;
            this.btnDisable.Text = "禁用/启用";
            this.btnDisable.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(430, 614);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 32);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "确定";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(530, 614);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // WindowPointForm
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(640, 664);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnDisable);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnSwap);
            this.Controls.Add(this.btnEditPoint);
            this.Controls.Add(this.grpProgram);
            this.Controls.Add(this.pnlMatrix);
            this.Controls.Add(this.lblHint);
            this.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "WindowPointForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "窗口/点位与相机程序配置";
            this.pnlMatrix.ResumeLayout(false);
            this.grpProgram.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrograms)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        // 设计器声明的字段（命名遵循匈牙利前缀规范）
        private Label lblHint;
        private Panel pnlMatrix;
        private TableLayoutPanel tblMatrix;
        private Button btnEditPoint;
        private Button btnSwap;
        private Button btnReset;
        private Button btnDisable;
        private Button btnOk;
        private Button btnCancel;
        private GroupBox grpProgram;
        private Label lblProgNote;
        private Label lblProgHint;
        private Button btnAddProg;
        private Button btnDelProg;
        private ComboBox cmbCamera;
        private Label lblCamera;
        private Label lblModel;
        private ComboBox cmbModel;
        private DataGridView dgvPrograms;
        private DataGridViewComboBoxColumn colStation;
        private DataGridViewComboBoxColumn colProgram;
    }
}