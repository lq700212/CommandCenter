using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// WindowPointForm 的窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 布局请对照 WindowPointForm.cs 类注释里的 ASCII 布局图：
    ///   顶部说明标签（lblHint，随"交换模式"提示实时切换文案）
    ///   矩阵容器（pnlMatrix + 内部 tblMatrix，格子是运行时代码生成的 Button，不在设计器里）
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
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pnlMatrix.SuspendLayout();
            this.SuspendLayout();
            //
            // lblHint
            // 操作说明：常驻提示 + 进入"交换位置"模式时切换成引导文字（见 WindowPointForm.cs）
            //
            this.lblHint.AutoSize = false;
            this.lblHint.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            this.lblHint.Location = new System.Drawing.Point(20, 14);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(600, 42);
            this.lblHint.TabIndex = 0;
            this.lblHint.Text = "每个格子 = 主界面一个显示窗口。左上角是【固定编号】；下方是它的【存图点位】。\r\n单击格子选中，点\"编辑点位\"改存图号；点\"交换位置\"可把两个窗口的内容互换（编号固定）。";
            //
            // pnlMatrix
            // 矩阵容器：表格由运行时代码按 Rows×Cols 生成（格子是 Button，随点位值变化实时刷新文字）
            //
            this.pnlMatrix.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlMatrix.Controls.Add(this.tblMatrix);
            this.pnlMatrix.Location = new System.Drawing.Point(20, 64);
            this.pnlMatrix.Name = "pnlMatrix";
            this.pnlMatrix.Size = new System.Drawing.Size(600, 370);
            this.pnlMatrix.TabIndex = 1;
            //
            // tblMatrix
            //
            this.tblMatrix.ColumnCount = 1;
            this.tblMatrix.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblMatrix.Location = new System.Drawing.Point(0, 0);
            this.tblMatrix.Name = "tblMatrix";
            this.tblMatrix.RowCount = 1;
            this.tblMatrix.Size = new System.Drawing.Size(598, 368);
            this.tblMatrix.TabIndex = 0;
            //
            // btnEditPoint
            //
            this.btnEditPoint.Location = new System.Drawing.Point(20, 448);
            this.btnEditPoint.Name = "btnEditPoint";
            this.btnEditPoint.Size = new System.Drawing.Size(100, 30);
            this.btnEditPoint.TabIndex = 2;
            this.btnEditPoint.Text = "编辑点位";
            this.btnEditPoint.UseVisualStyleBackColor = true;
            //
            // btnSwap
            //
            this.btnSwap.Location = new System.Drawing.Point(130, 448);
            this.btnSwap.Name = "btnSwap";
            this.btnSwap.Size = new System.Drawing.Size(100, 30);
            this.btnSwap.TabIndex = 3;
            this.btnSwap.Text = "交换位置";
            this.btnSwap.UseVisualStyleBackColor = true;
            //
            // btnReset
            //
            this.btnReset.Location = new System.Drawing.Point(240, 448);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(100, 30);
            this.btnReset.TabIndex = 4;
            this.btnReset.Text = "恢复默认";
            this.btnReset.UseVisualStyleBackColor = true;
            //
            // btnOk
            //
            this.btnOk.Location = new System.Drawing.Point(430, 448);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 32);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "确定";
            this.btnOk.UseVisualStyleBackColor = true;
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(530, 448);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // WindowPointForm
            //
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 496);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnSwap);
            this.Controls.Add(this.btnEditPoint);
            this.Controls.Add(this.pnlMatrix);
            this.Controls.Add(this.lblHint);
            this.Font = new System.Drawing.Font("Microsoft YaHei", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "WindowPointForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "窗口与存图点位配置";
            this.pnlMatrix.ResumeLayout(false);
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
        private Button btnOk;
        private Button btnCancel;
    }
}
