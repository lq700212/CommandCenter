using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// MainForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 把"静态、固定数量"的控件（标题栏信息字段、配方下拉框、系统设置按钮、
    /// PLC 状态灯、底部状态栏、窗口矩阵容器）放进设计器，便于可视化拖拽排布；
    /// 运行时才确定的"动态控件"（每台相机一个状态灯、窗口矩阵里的 CameraDisplayControl）
    /// 仍在 MainForm.cs 中生成，不放进这里。
    /// 【重要】整体顺序请参考 MainForm.cs 类注释里的 ASCII 布局图。
    ///   ┌──────────────────────────────────────────────────────────────┐
    ///   │ 产品型号:[cmbRecipe▾] 序列号:[lblSerialTitle][lblSerial框]    │
    ///   │  | 总数:[lblTotal] OK:[lblOk] NG:[lblNg]                     │
    ///   │          | [btnSettings系统设置]    ●[lblPlcStatus]          │
    ///   ├──────────────────────────────────────────────────────────────┤
    ///   │                 gridCameraWindows（TableLayoutPanel 等分）      │
    ///   ├──────────────────────────────────────────────────────────────┤
    ///   │ lblStatus（状态文本，左下角）                                      │
    ///   └──────────────────────────────────────────────────────────────┘
    /// 说明：
    ///   - 标题栏面板 pnlTitleBar：Dock=Top，FixedHeight=48；内部字段用绝对坐标，
    ///     运行时由 MainForm.InitTitleBarRuntime/RelayoutTitleBar 按"显示开关"紧凑重排。
    ///   - lblProductPrefix 的文案与各信息字段的 Visible 由 Display 配置控制（运行时设置）。
    ///   - 连接状态灯：PLC 灯在 Designer 中先 Add（Dock.Right 先加的靠左），
    ///     相机动灯随后由 MainForm.cs 循环 Add（靠右），与历史实测布局一致。
    ///   - gridCameraWindows 只做容器，行列数量、百分比分格、窗口填充全部由
    ///     MainForm.BuildWindowGrid 运行时重建，保证改 Rows/Columns 配置即可生效。
    /// </summary>
    partial class MainForm
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
            this.pnlTitleBar = new System.Windows.Forms.Panel();
            this.lblPlcStatus = new System.Windows.Forms.Label();
            this.lblCamPlaceholder = new System.Windows.Forms.Label();
            this.btnSettings = new System.Windows.Forms.Button();
            this.lblSep2 = new System.Windows.Forms.Label();
            this.lblNg = new System.Windows.Forms.Label();
            this.lblOk = new System.Windows.Forms.Label();
            this.lblTotal = new System.Windows.Forms.Label();
            this.lblSep1 = new System.Windows.Forms.Label();
            this.lblSerial = new System.Windows.Forms.Label();
            this.lblSerialTitle = new System.Windows.Forms.Label();
            this.cmbRecipe = new System.Windows.Forms.ComboBox();
            this.lblProductPrefix = new System.Windows.Forms.Label();
            this.pnlStatusBar = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.gridCameraWindows = new System.Windows.Forms.TableLayoutPanel();
            this.pnlTitleBar.SuspendLayout();
            this.pnlStatusBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlTitleBar
            // 
            this.pnlTitleBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(242)))), ((int)(((byte)(250)))));
            this.pnlTitleBar.Controls.Add(this.lblPlcStatus);
            // 相机灯占位（灰色提示）：真实相机灯是运行时代码按相机台数动态生成的，
            // 设计器里看不到，所以用这个占位 Label 提醒"这里是相机灯区域"；
            // 运行时 InitTitleBarRuntime 生成真灯后会把占位隐藏（隐藏控件不占 Dock 空间）。
            this.pnlTitleBar.Controls.Add(this.lblCamPlaceholder);
            this.pnlTitleBar.Controls.Add(this.btnSettings);
            this.pnlTitleBar.Controls.Add(this.lblSep2);
            this.pnlTitleBar.Controls.Add(this.lblNg);
            this.pnlTitleBar.Controls.Add(this.lblOk);
            this.pnlTitleBar.Controls.Add(this.lblTotal);
            this.pnlTitleBar.Controls.Add(this.lblSep1);
            this.pnlTitleBar.Controls.Add(this.lblSerialTitle);
            this.pnlTitleBar.Controls.Add(this.lblSerial);
            this.pnlTitleBar.Controls.Add(this.cmbRecipe);
            this.pnlTitleBar.Controls.Add(this.lblProductPrefix);
            this.pnlTitleBar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitleBar.Location = new System.Drawing.Point(0, 0);
            this.pnlTitleBar.Name = "pnlTitleBar";
            this.pnlTitleBar.Padding = new System.Windows.Forms.Padding(12, 0, 12, 0);
            this.pnlTitleBar.Size = new System.Drawing.Size(1400, 48);
            this.pnlTitleBar.TabIndex = 0;
            // 
            // lblPlcStatus
            // 
            this.lblPlcStatus.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblPlcStatus.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblPlcStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblPlcStatus.Location = new System.Drawing.Point(1292, 0);
            this.lblPlcStatus.Name = "lblPlcStatus";
            this.lblPlcStatus.Size = new System.Drawing.Size(96, 48);
            this.lblPlcStatus.TabIndex = 10;
            this.lblPlcStatus.Text = "● PLC";
            this.lblPlcStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblCamPlaceholder
            // 相机连接状态灯的"设计器占位"：样式与动态相机灯一致（Dock=Right、96px、灰色）；
            // 仅用于在 VS 设计器里提示这块区域，运行时被隐藏，不参与实际布局。
            //
            this.lblCamPlaceholder.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblCamPlaceholder.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblCamPlaceholder.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblCamPlaceholder.Location = new System.Drawing.Point(1392, 0);
            this.lblCamPlaceholder.Name = "lblCamPlaceholder";
            this.lblCamPlaceholder.Size = new System.Drawing.Size(96, 48);
            this.lblCamPlaceholder.TabIndex = 13;
            this.lblCamPlaceholder.Text = "● 相机（运行时生成）";
            this.lblCamPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnSettings
            // 
            this.btnSettings.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnSettings.FlatAppearance.BorderSize = 0;
            this.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSettings.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnSettings.ForeColor = System.Drawing.Color.White;
            this.btnSettings.Location = new System.Drawing.Point(757, 9);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(88, 30);
            this.btnSettings.TabIndex = 9;
            this.btnSettings.Text = "系统设置";
            this.btnSettings.UseVisualStyleBackColor = false;
            // 
            // lblSep2
            // 
            this.lblSep2.AutoSize = true;
            this.lblSep2.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblSep2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblSep2.Location = new System.Drawing.Point(722, 12);
            this.lblSep2.Name = "lblSep2";
            this.lblSep2.Size = new System.Drawing.Size(14, 19);
            this.lblSep2.TabIndex = 8;
            this.lblSep2.Text = "|";
            // 
            // lblNg
            // 
            this.lblNg.AutoSize = true;
            this.lblNg.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblNg.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(229)))), ((int)(((byte)(72)))), ((int)(((byte)(77)))));
            this.lblNg.Location = new System.Drawing.Point(650, 12);
            this.lblNg.Name = "lblNg";
            this.lblNg.Size = new System.Drawing.Size(50, 19);
            this.lblNg.TabIndex = 7;
            this.lblNg.Text = "NG: 0";
            // 
            // lblOk
            // 
            this.lblOk.AutoSize = true;
            this.lblOk.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblOk.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(158)))), ((int)(((byte)(107)))));
            this.lblOk.Location = new System.Drawing.Point(576, 12);
            this.lblOk.Name = "lblOk";
            this.lblOk.Size = new System.Drawing.Size(48, 19);
            this.lblOk.TabIndex = 6;
            this.lblOk.Text = "OK: 0";
            // 
            // lblTotal
            // 
            this.lblTotal.AutoSize = true;
            this.lblTotal.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblTotal.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblTotal.Location = new System.Drawing.Point(490, 12);
            this.lblTotal.Name = "lblTotal";
            this.lblTotal.Size = new System.Drawing.Size(56, 19);
            this.lblTotal.TabIndex = 5;
            this.lblTotal.Text = "总数: 0";
            // 
            // lblSep1
            // 
            this.lblSep1.AutoSize = true;
            this.lblSep1.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblSep1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblSep1.Location = new System.Drawing.Point(455, 12);
            this.lblSep1.Name = "lblSep1";
            this.lblSep1.Size = new System.Drawing.Size(14, 19);
            this.lblSep1.TabIndex = 4;
            this.lblSep1.Text = "|";
            // 
            // lblSerialTitle
            // 序列号标题：独立于显示框，作为"序列号:"框的标题文字显示在框外（左侧）。
            // 与其它信息标签同字号同色，垂直居中位置由 RelayoutTitleBar 统一计算。
            //
            this.lblSerialTitle.AutoSize = true;
            this.lblSerialTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblSerialTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblSerialTitle.Location = new System.Drawing.Point(252, 12);
            this.lblSerialTitle.Name = "lblSerialTitle";
            this.lblSerialTitle.Size = new System.Drawing.Size(61, 19);
            this.lblSerialTitle.TabIndex = 24;
            this.lblSerialTitle.Text = "序列号:";
            // 
            // lblSerial
            // 序列号显示框：固定宽度、不随内容伸缩（AutoSize=false），加单线边框像一个"空框"。
            // 框内只放序列号值（"序列号:"标题由 lblSerialTitle 显示在框外左侧），
            // 没有序列号则框内留空；宽度固定避免前后字段跳动。
            //
            this.lblSerial.AutoSize = false;
            this.lblSerial.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblSerial.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblSerial.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblSerial.Location = new System.Drawing.Point(341, 12);
            this.lblSerial.Name = "lblSerial";
            this.lblSerial.Size = new System.Drawing.Size(220, 24);
            this.lblSerial.TabIndex = 3;
            this.lblSerial.Text = "";
            this.lblSerial.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbRecipe
            // 
            this.cmbRecipe.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRecipe.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cmbRecipe.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.cmbRecipe.Location = new System.Drawing.Point(136, 10);
            this.cmbRecipe.Name = "cmbRecipe";
            this.cmbRecipe.Size = new System.Drawing.Size(180, 27);
            this.cmbRecipe.TabIndex = 2;
            // 
            // lblProductPrefix
            // 
            this.lblProductPrefix.AutoSize = true;
            this.lblProductPrefix.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblProductPrefix.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblProductPrefix.Location = new System.Drawing.Point(12, 12);
            this.lblProductPrefix.Name = "lblProductPrefix";
            this.lblProductPrefix.Size = new System.Drawing.Size(73, 19);
            this.lblProductPrefix.TabIndex = 1;
            this.lblProductPrefix.Text = "产品型号:";
            // 
            // pnlStatusBar
            // 
            this.pnlStatusBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(242)))), ((int)(((byte)(250)))));
            this.pnlStatusBar.Controls.Add(this.lblStatus);
            this.pnlStatusBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlStatusBar.Location = new System.Drawing.Point(0, 766);
            this.pnlStatusBar.Name = "pnlStatusBar";
            this.pnlStatusBar.Size = new System.Drawing.Size(1400, 54);
            this.pnlStatusBar.TabIndex = 11;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(73)))), ((int)(((byte)(94)))));
            this.lblStatus.Location = new System.Drawing.Point(14, 15);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(88, 20);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "正在初始化...";
            // 
            // gridCameraWindows
            // 
            this.gridCameraWindows.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(250)))));
            this.gridCameraWindows.ColumnCount = 1;
            this.gridCameraWindows.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridCameraWindows.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCameraWindows.Location = new System.Drawing.Point(0, 48);
            this.gridCameraWindows.Name = "gridCameraWindows";
            this.gridCameraWindows.Padding = new System.Windows.Forms.Padding(6);
            this.gridCameraWindows.RowCount = 1;
            this.gridCameraWindows.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.gridCameraWindows.Size = new System.Drawing.Size(1400, 718);
            this.gridCameraWindows.TabIndex = 12;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(1400, 820);
            this.Controls.Add(this.gridCameraWindows);
            this.Controls.Add(this.pnlStatusBar);
            this.Controls.Add(this.pnlTitleBar);
            this.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CommandCenter - 相机/PLC 命令中心";
            this.pnlTitleBar.ResumeLayout(false);
            this.pnlTitleBar.PerformLayout();
            this.pnlStatusBar.ResumeLayout(false);
            this.pnlStatusBar.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        // 以下为设计器声明的字段（可视化拖拽所需）。命名遵循匈牙利前缀规范：
        // pnl=Panel / lbl=Label / cmb=ComboBox / btn=Button / grid=TableLayoutPanel。
        private Panel pnlTitleBar;
        private Label lblProductPrefix;
        private ComboBox cmbRecipe;
        private Label lblSerialTitle;
        private Label lblSerial;
        private Label lblSep1;
        private Label lblTotal;
        private Label lblOk;
        private Label lblNg;
        private Label lblSep2;
        private Button btnSettings;
        private Label lblPlcStatus;
        private Label lblCamPlaceholder;
        private Panel pnlStatusBar;
        private Label lblStatus;
        private TableLayoutPanel gridCameraWindows;
    }
}