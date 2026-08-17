using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Views
{
    /// <summary>
    /// MainForm 的 Visual Studio 窗体设计器分部文件（自动生成风格，可手动维护）。
    /// 把"静态、固定数量"的控件（标题栏信息字段、产品型号下拉、系统设置按钮、
    /// PLC 状态灯、底部状态栏、窗口矩阵容器）放进设计器，便于可视化拖拽排布；
    /// 运行时才确定的"动态控件"（每台相机一个状态灯、窗口矩阵里的 CameraDisplayControl）
    /// 仍在 MainForm.cs 中生成，不放进这里。
    /// 【重要】整体顺序请参考 MainForm.cs 类注释里的 ASCII 布局图。
    ///   ┌──────────────────────────────────────────────────────────────┐
    ///   │ 产品型号:[cmbModel▾] 序列号:[lblSerialTitle][lblSerial·只读框]    │
    ///   │   [btnManualSerial人工补录] | 总数:[lblTotal] OK:[lblOk]      │
    ///   │   NG:[lblNg] | [btnSettings系统设置]                          │
    ///   │          ●[lblPlcStatus] ●[lblScannerStatus] ●[相机灯]        │
    ///   ├──────────────────────────────────────────────────────────────┤
    ///   │  pnlWindowScroll（AutoScroll=true，行多超高时出竖直滚动条）       │
    ///   │            └─ gridCameraWindows（TableLayoutPanel 等分）        │
    ///   ├──────────────────────────────────────────────────────────────┤
    ///   │ lblStatus（状态文本，左下角）                                      │
    ///   └──────────────────────────────────────────────────────────────┘
    /// 说明：
    ///   - 标题栏面板 pnlTitleBar：Dock=Top，FixedHeight=48；内部字段用绝对坐标，
    ///     运行时由 MainForm.InitTitleBarRuntime/RelayoutTitleBar 按"显示开关"紧凑重排。
    ///   - 产品型号（cmbModel，V2.8 可选下拉）：候选恒预置 U171/Z121（配置候选去重合并），
    ///     操作员在标题栏直接下拉切换当前生产型号，切换即生效（重建协调器按新型号查相机映射表 +
    ///     写盘持久化），与系统设置窗体 PLC 区"产品型号"是同一个值。
    ///   - lblProductPrefix 的文案与各信息字段的 Visible 由 Display 配置控制（运行时设置）。
    ///   - 连接状态灯：PLC 灯在 Designer 中先 Add（Dock.Right 先加的靠左），扫码枪状态灯
    ///     紧跟其后（第 2 位，位于 PLC 右侧），相机动灯随后由 MainForm.cs 正序循环 Add（靠右）
    ///     ——相机1..相机N 依次排在扫码枪灯右侧（V1.7.1 起：相机1 在相机2 左边，相机3 继续往右）。
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
            this.lblScannerStatus = new System.Windows.Forms.Label();
            this.lblCamPlaceholder = new System.Windows.Forms.Label();
            this.btnSettings = new System.Windows.Forms.Button();
            this.btnToggleLanguage = new System.Windows.Forms.Button();
            this.btnManualSerial = new System.Windows.Forms.Button();
            this.lblSep2 = new System.Windows.Forms.Label();
            this.lblNg = new System.Windows.Forms.Label();
            this.lblOk = new System.Windows.Forms.Label();
            this.lblTotal = new System.Windows.Forms.Label();
            this.lblSep1 = new System.Windows.Forms.Label();
            this.lblSerial = new System.Windows.Forms.Label();
            this.lblSerialTitle = new System.Windows.Forms.Label();
            this.cmbModel = new System.Windows.Forms.ComboBox();
            this.lblProductPrefix = new System.Windows.Forms.Label();
            this.pnlStatusBar = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.gridCameraWindows = new System.Windows.Forms.TableLayoutPanel();
            this.pnlWindowScroll = new System.Windows.Forms.Panel();
            this.pnlTitleBar.SuspendLayout();
            this.pnlStatusBar.SuspendLayout();
            this.pnlWindowScroll.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlTitleBar
            // 
            this.pnlTitleBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(242)))), ((int)(((byte)(250)))));
            this.pnlTitleBar.Controls.Add(this.lblPlcStatus);
            // 扫码枪状态灯（V1.12.6）：紧跟 PLC 灯 Add（Dock.Right 先 Add 靠左）→ 位于 PLC 右侧、
            // 相机灯左侧；运行时相机灯最后 Add 排最右。未连接红色/已连接绿色由
            // MainForm.RefreshScannerStatus 订阅每台扫码枪的 ConnectionChanged 聚合刷新。
            this.pnlTitleBar.Controls.Add(this.lblScannerStatus);
            // 相机灯占位（灰色提示）：真实相机灯是运行时代码按相机台数动态生成的，
            // 设计器里看不到，所以用这个占位 Label 提醒"这里是相机灯区域"；
            // 运行时 InitTitleBarRuntime 生成真灯后会把占位隐藏（隐藏控件不占 Dock 空间）。
            this.pnlTitleBar.Controls.Add(this.lblCamPlaceholder);
            this.pnlTitleBar.Controls.Add(this.btnSettings);
            this.pnlTitleBar.Controls.Add(this.btnToggleLanguage);
            this.pnlTitleBar.Controls.Add(this.lblSep2);
            this.pnlTitleBar.Controls.Add(this.lblNg);
            this.pnlTitleBar.Controls.Add(this.lblOk);
            this.pnlTitleBar.Controls.Add(this.lblTotal);
            this.pnlTitleBar.Controls.Add(this.lblSep1);
            this.pnlTitleBar.Controls.Add(this.lblSerialTitle);
            this.pnlTitleBar.Controls.Add(this.lblSerial);
            this.pnlTitleBar.Controls.Add(this.btnManualSerial);
            this.pnlTitleBar.Controls.Add(this.cmbModel);
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
            // lblScannerStatus
            // 
            // 扫码枪连接状态灯（V1.12.6）：样式与 PLC/相机灯完全一致——"● 扫码枪"圆点灯，
            // 位于标题栏右上 PLC 灯右侧。Dock.Right 布局"先 Add 的靠左"：本控件在 Controls.Add
            // 顺序里紧跟 lblPlcStatus（第 2 位），运行时相机灯最后 Add 排最右，故顺序为
            // ●PLC | ●扫码枪 | ●相机N。初始灰色（150,150,150，同 PLC/相机灯设计器默认），
            // 已连接变绿/未连接变红由 MainForm.RefreshScannerStatus 根据每台扫码枪
            // ConnectionChanged 事件聚合刷新（全部启用都已连接才绿色，任一未连接即红）。
            this.lblScannerStatus.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblScannerStatus.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblScannerStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblScannerStatus.Location = new System.Drawing.Point(1292, 0);
            this.lblScannerStatus.Name = "lblScannerStatus";
            this.lblScannerStatus.Size = new System.Drawing.Size(96, 48);
            this.lblScannerStatus.TabIndex = 14;
            this.lblScannerStatus.Text = "● 扫码枪";
            this.lblScannerStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
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
            // btnToggleLanguage
            // 界面语言切换按钮（V2.15.0 国际化，V2.15.1 从设置窗体移到主界面标题栏）：
            // 排布在【系统设置】按钮右侧（RelayoutTitleBar 的 seq 数组里 btnSettings 之后即最右）。
            // 点击直接切换中/英文（中文界面 → English、英文界面 → 中文），立即热生效并写盘持久化。
            // 按钮文本 = "目标语言名"（语言名本身不翻译，自解释），由 ApplyLanguage() 按当前语言设置。
            // 外观与 btnSettings 完全一致：蓝底白字、Flat 无边框、微软雅黑 9F、88×30。
            // 运行期位置由 RelayoutTitleBar 重排（Designer 里的 Location 只是初始值）。
            // 
            this.btnToggleLanguage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnToggleLanguage.FlatAppearance.BorderSize = 0;
            this.btnToggleLanguage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggleLanguage.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnToggleLanguage.ForeColor = System.Drawing.Color.White;
            this.btnToggleLanguage.Location = new System.Drawing.Point(853, 9);
            this.btnToggleLanguage.Name = "btnToggleLanguage";
            this.btnToggleLanguage.Size = new System.Drawing.Size(88, 30);
            this.btnToggleLanguage.TabIndex = 10;
            this.btnToggleLanguage.Text = "English";
            this.btnToggleLanguage.UseVisualStyleBackColor = false;
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
            // 序列号显示框（V2.14.7 由 TextBox 换回 Label 只读框——TextBox 是 V1.12.19 为
            // "框内直录"引入的，直录已废弃，Label 更纯粹、不可聚焦不可编辑，扫码枪收码照样覆盖文本；
            // 外观延续历史 lblSerial：固定宽度（AutoSize=false）、单线边框像"空框"、MiddleLeft 文本）。
            // 扫码枪收码（OnSerialScanned）自动覆盖框内文本；手动补录用右侧【人工补录】按钮
            // （btnManualSerial）弹 SerialInputForm 录入对话框（预填当前 SN、点确定/取消），
            // 交互接线见 MainForm.SetupSerialEditor（构造时一次，热更不重复订阅）。
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
            this.lblSerial.BackColor = System.Drawing.Color.White;
            this.lblSerial.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnManualSerial
            // 人工补录按钮（V2.14.6）：位于序列号框右侧，点击弹 SerialInputForm 手动录入/修改
            // 序列号（V2.14.7 起为手动录 SN 的唯一入口，双击序列号框已取消）。
            // 风格参考 btnSettings（蓝底白字、Flat 无边框），
            // 高度 30 与系统设置按钮一致，RelayoutTitleBar 里与同行控件垂直居中排布。
            // 交互接线见 MainForm.SetupSerialEditor（构造时一次）。
            //
            this.btnManualSerial.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(152)))), ((int)(((byte)(219)))));
            this.btnManualSerial.FlatAppearance.BorderSize = 0;
            this.btnManualSerial.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnManualSerial.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnManualSerial.ForeColor = System.Drawing.Color.White;
            this.btnManualSerial.Location = new System.Drawing.Point(471, 9);
            this.btnManualSerial.Name = "btnManualSerial";
            this.btnManualSerial.Size = new System.Drawing.Size(88, 30);
            this.btnManualSerial.TabIndex = 25;
            this.btnManualSerial.Text = "人工补录";
            this.btnManualSerial.UseVisualStyleBackColor = false;
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
            // cmbModel
            // 
            // 产品型号下拉（V2.8）：操作员在标题栏直接选当前生产型号，切换即生效（重建协调器
            // 按新型号查相机映射表切程序 + 写盘持久化）。候选初始在 InitModelCombo 运行时填充，
            // 恒预置 U171/Z121（与配置 ProductModels 去重合并），
            // DropDownList 只能从清单选。
            // 运行坐标由 RelayoutTitleBar 按标题栏整行重排，这里只是设计器初始参照。
            this.cmbModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbModel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cmbModel.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.cmbModel.Location = new System.Drawing.Point(136, 10);
            this.cmbModel.Name = "cmbModel";
            this.cmbModel.Size = new System.Drawing.Size(110, 27);
            this.cmbModel.TabIndex = 24;
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
            // pnlWindowScroll
            // 
            // 窗口矩阵的滚动宿主（V2.14）：包裹 gridCameraWindows。
            // AutoScroll=true：行数多、矩阵超高放不下时，由本面板自动出竖直滚动条（右侧滑块），
            // 滚轮/滑块翻看；标题栏（Top）与状态栏（Bottom）挂在窗体上、不在本面板内，不随滚动。
            // "铺满 / 滚动"两种形态由 MainForm.BuildWindowGrid → ApplyGridScrollLayout 运行切换：
            //   铺满 = grid.Dock=Fill（行少，窗口尽量大占满本区域）；滚动 = grid.Dock=Top + 定高。
            // 底色与 grid 同浅蓝，滚动露出空隙时视觉连续。
            this.pnlWindowScroll.AutoScroll = true;
            this.pnlWindowScroll.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(250)))));
            this.pnlWindowScroll.Controls.Add(this.gridCameraWindows);
            this.pnlWindowScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlWindowScroll.Location = new System.Drawing.Point(0, 48);
            this.pnlWindowScroll.Name = "pnlWindowScroll";
            this.pnlWindowScroll.Size = new System.Drawing.Size(1400, 718);
            this.pnlWindowScroll.TabIndex = 13;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(245)))), ((int)(((byte)(250)))));
            this.ClientSize = new System.Drawing.Size(1400, 820);
            this.Controls.Add(this.pnlWindowScroll);
            this.Controls.Add(this.pnlStatusBar);
            this.Controls.Add(this.pnlTitleBar);
            this.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            // V1.11.0 默认全屏 + 禁用所有缩放手段（按钮 + 拖拽）：
            //   - FixedSingle：固定单线边框，Normal 状态下窗口边缘没有可调热区（不可拖拽）；
            //   - 注意：不能用 WindowState.Maximized！最大化状态会被 Windows 强行切换成
            //     可调整边框，边缘拖拽缩放照常开放（WndProc 拦截也挡不住系统这一层）。
            //     铺满屏幕由 MainForm.OnShown 里手动 Bounds=WorkingArea 实现（等效全屏）；
            //   - MaximizeBox=false：中间的"最大化/还原"按钮禁用（变灰不可点）；
            //   - MinimizeBox=true：最小化按钮保留可用；
            //   - 关闭按钮完整保留，客户可正常退出。
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.WindowState = System.Windows.Forms.FormWindowState.Normal;
            this.Text = "上位机控制中心";
            this.pnlTitleBar.ResumeLayout(false);
            this.pnlTitleBar.PerformLayout();
            this.pnlStatusBar.ResumeLayout(false);
            this.pnlStatusBar.PerformLayout();
            this.pnlWindowScroll.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        // 以下为设计器声明的字段（可视化拖拽所需）。命名遵循匈牙利前缀规范：
        // pnl=Panel / lbl=Label / cmb=ComboBox / btn=Button / grid=TableLayoutPanel。
        private Panel pnlTitleBar;
        private Label lblProductPrefix;
        private ComboBox cmbModel;
        private Label lblSerialTitle;
        private Label lblSerial;
        private Button btnManualSerial;   // 人工补录按钮（V2.14.6）：序列号框右侧，点击弹 SerialInputForm
        private Label lblSep1;
        private Label lblTotal;
        private Label lblOk;
        private Label lblNg;
        private Label lblSep2;
        private Button btnSettings;
        private Button btnToggleLanguage;
        private Label lblPlcStatus;
        private Label lblScannerStatus;
        private Label lblCamPlaceholder;
        private Panel pnlStatusBar;
        private Label lblStatus;
        private Panel pnlWindowScroll;   // 窗口矩阵滚动宿主（V2.14，AutoScroll=true，行多出滚动条）
        private TableLayoutPanel gridCameraWindows;
    }
}