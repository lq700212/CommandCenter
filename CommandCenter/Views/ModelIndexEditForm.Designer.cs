using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// ModelIndexEditForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护/设计器微调）。
    /// 把窗体外观（横幅/表格/确定/取消按钮的布局、颜色、字体）全部放进设计器，
    /// 业务逻辑（预载型号、确定写回、取消关闭）在 ModelIndexEditForm.cs 中。
    /// 【界面布局】（风格对齐 LoginForm/SerialInputForm：顶部蓝色横幅 + 白底 + 蓝色主按钮）
    ///   ┌──────────────────────────────────────────────┐
    ///   │▓ 产品型号配置（pnlHeader 蓝色横幅，白字居中）▓│
    ///   ├──────────────────────────────────────────────┤
    ///   │          [btnAdd新增][btnDelete删除选中]     │
    ///   │  ┌─────────┬───────┬──────────────────────┐  │
    ///   │  │ 选中(□) │ 序号  │ 型号名称（全居中）     │  │
    ///   │  ├─────────┼───────┼──────────────────────┤  │
    ///   │  │  ☑/□    │   2   │   U171                │  │
    ///   │  └─────────┴───────┴──────────────────────┘  │
    ///   │                                              │
    ///   │ [btnCancel 取 消]          [btnOk 确 定]    │
    ///   └──────────────────────────────────────────────┘
    ///   对齐规则（V2.14.14，同 SerialInputForm）：
    ///     · 确定按钮【右】、取消按钮【左】；
    ///     · 取消左边缘与表格左边缘对齐、确定右边缘与表格右边缘对齐（两边按钮都贴表格边缘）。
    /// </summary>
    partial class ModelIndexEditForm
    {
        private System.ComponentModel.IContainer components = null;

        /// <summary>清理正在使用的资源。</summary>
        /// <param name="disposing">为 true 时释放托管资源（含 COM 等）；为 false 时只释放非托管资源。</param>
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
            this.grid = new System.Windows.Forms.DataGridView();
            this.colSel = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colModel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.pnlHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 顶部蓝色横幅（对齐 LoginForm.pnlHeader 同款主蓝）：固定高 52、深蓝底，
            // 品牌感横幅，一眼看出"这是个配置对话框"。
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.pnlHeader.Controls.Add(this.lblBanner);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(440, 52);
            this.pnlHeader.TabIndex = 0;
            // 
            // lblBanner
            // 横幅内标题：白色粗体、水平垂直居中，文字"产品型号配置"。
            // 
            this.lblBanner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblBanner.Font = new System.Drawing.Font("Microsoft YaHei", 14F, System.Drawing.FontStyle.Bold);
            this.lblBanner.ForeColor = System.Drawing.Color.White;
            this.lblBanner.Location = new System.Drawing.Point(0, 0);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.Size = new System.Drawing.Size(440, 52);
            this.lblBanner.TabIndex = 0;
            this.lblBanner.Text = "产品型号配置";
            this.lblBanner.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // grid
            // 型号↔PLC序号映射表格（V2.14.14）：三列——选中、序号、型号名称。
            //   · 前几行默认预载当前已有型号与序号（LoadFromConfig 填充）；
            //   · 单元格可直接编辑；
            //   · 增加行只能走【新增】按钮（btnAdd，V2.14.29）——**不开放 DataGridView 自带的
            //     自动新行（AllowUserToAddRows=false）**：自动"* 新行"在编辑一半时被删除会抛
            //     "无法删除未提交的新行"异常，且现场容易误点出空行，故统一由按钮显式加行；
            //     删除走【删除选中】按钮（勾选列多选 / 选中行单选多选 / Delete 键）；
            //   · 整行选中（FullRowSelect）+ 支持多选（Ctrl/Shift）、勾选列可快速多选；
            //   · "全列内容居中"（V2.14.25）：表头与单元格横竖都居中。
            // 确定写回 plc.modelIndexes 并持久化；取消关闭不保存。
            // 
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = true;
            this.grid.BackgroundColor = System.Drawing.Color.White;
            this.grid.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.grid.ColumnHeadersDefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSel,
            this.colIndex,
            this.colModel});
            this.grid.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.grid.Location = new System.Drawing.Point(28, 104);
            this.grid.MultiSelect = true;
            this.grid.Name = "grid";
            this.grid.RowHeadersVisible = false;
            this.grid.RowTemplate.Height = 27;
            this.grid.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.grid.Size = new System.Drawing.Size(384, 186);
            this.grid.TabIndex = 1;
            // 
            // colSel
            // 选中列（V2.14.25）：DataGridViewCheckBoxColumn，勾选=待删除的行（多选删除用）。
            //   · 表头文字"选中"，单元格居中；
            //   · 只作"删除前标记"用，不参与确定写回（OnOk 只收集序号+型号名）；
            //   · 另支持单选删除：鼠标/方向键选中行后点 btnDelete 或按 Delete 键。
            // 
            this.colSel.HeaderText = "选中";
            this.colSel.Name = "colSel";
            this.colSel.Width = 56;
            // 
            // colIndex
            // 序号列：该型号在 PLC 40007 里对应的序号（0~65535，0=不写序号）。
            // 
            this.colIndex.HeaderText = "序号";
            this.colIndex.Name = "colIndex";
            this.colIndex.Width = 128;
            // 
            // colModel
            // 型号名称列：与 AppConfig.ProductModel/ProductModels 对应（如 "Z121"）。
            // 
            this.colModel.HeaderText = "型号名称";
            this.colModel.Name = "colModel";
            this.colModel.Width = 200;
            // 
            // btnOk
            // 确定按钮（蓝色主按钮，对齐 LoginForm.btnLogin/SerialInputForm.btnOk）：
            // 蓝底白字、Flat 无边框、粗体。位置在右下：右边缘与表格右边缘对齐（贴表格边缘）。
            // 点击后收集表格→写回 plc.modelIndexes（见 ModelIndexEditForm.cs OnOk）。
            // 
            this.btnOk.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnOk.FlatAppearance.BorderSize = 0;
            this.btnOk.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOk.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.btnOk.ForeColor = System.Drawing.Color.White;
            this.btnOk.Location = new System.Drawing.Point(308, 312);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(104, 38);
            this.btnOk.TabIndex = 3;
            this.btnOk.Text = "确  定";
            this.btnOk.UseVisualStyleBackColor = false;
            // 
            // btnCancel
            // 取消按钮（白底描边次按钮，对齐 SerialInputForm.btnCancel）：
            // 浅灰蓝底、深蓝灰字、Flat 无边框。位置在左下：左边缘与表格左边缘对齐（贴表格边缘）。
            // 点击后直接关闭窗体、不保存任何修改（见 ModelIndexEditForm.cs）。
            // 
            this.btnCancel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(240)))), ((int)(((byte)(245)))));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.FlatAppearance.BorderSize = 0;
            this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancel.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.btnCancel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.btnCancel.Location = new System.Drawing.Point(28, 312);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(104, 38);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "取  消";
            this.btnCancel.UseVisualStyleBackColor = false;
            // 
            // btnAdd
            // 新增按钮（V2.14.29，放在【删除选中】左边）：
            // 加新型号必须显式点它——不再用 DataGridView 自带"* 新行"（编辑一半删除会抛
            // "无法删除未提交的新行"）。点击后在表格末尾追加一行空行（序号 0、型号名空，
            // 见 ModelIndexEditForm.cs BtnAdd_Click），填好后点【确定】写回。
            // 样式：蓝色系（与确定主蓝同族），与红色删除按钮形成"蓝=加、红=删"的对比印象。
            // 
            this.btnAdd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnAdd.FlatAppearance.BorderSize = 0;
            this.btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAdd.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.btnAdd.ForeColor = System.Drawing.Color.White;
            this.btnAdd.Location = new System.Drawing.Point(160, 68);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(108, 30);
            this.btnAdd.TabIndex = 6;
            this.btnAdd.Text = "新  增";
            this.btnAdd.UseVisualStyleBackColor = false;
            // 
            // btnDelete
            // 删除按钮（V2.14.25，红色系破坏性操作，放表格右上方、【新增】按钮右边）：
            // 浅红底白字、Flat 无边框，一眼区别于蓝/灰按钮。
            // 点击删除"勾选的行"；没有勾选但有选中行时删除选中行（单选/多选删除）；
            // 都没有则提示（见 ModelIndexEditForm.cs BtnDelete_Click）。
            // 
            this.btnDelete.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(108)))), ((int)(((byte)(108)))));
            this.btnDelete.FlatAppearance.BorderSize = 0;
            this.btnDelete.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDelete.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.btnDelete.ForeColor = System.Drawing.Color.White;
            this.btnDelete.Location = new System.Drawing.Point(276, 68);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(136, 30);
            this.btnDelete.TabIndex = 5;
            this.btnDelete.Text = "删除选中";
            this.btnDelete.UseVisualStyleBackColor = false;
            // 
            // ModelIndexEditForm
            // 窗体：FixedDialog 固定边框（禁拉伸）、相对父窗体居中弹出、白底、
            // KeyPreview=true 让窗体先收到按键（回车/Esc 兜底）。
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AcceptButton = this.btnOk;
            this.BackColor = System.Drawing.Color.White;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(440, 362);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.grid);
            this.Controls.Add(this.pnlHeader);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ModelIndexEditForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "产品型号配置";
            this.pnlHeader.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private Panel pnlHeader;
        private Label lblBanner;
        private DataGridView grid;
        private DataGridViewCheckBoxColumn colSel;
        private DataGridViewTextBoxColumn colIndex;
        private DataGridViewTextBoxColumn colModel;
        private Button btnOk;
        private Button btnCancel;
        private Button btnAdd;
        private Button btnDelete;
    }
}
