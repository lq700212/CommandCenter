using System;
using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Controls
{
    /// <summary>
    /// 相机显示控件：负责显示一路相机/一个检测点的画面与结果。
    /// ┌────────────────────────────────────┐
    /// │  编号                                 │  ← 左上：窗口编号
    /// │                                        │
    /// │             [ 图像显示区 ]            │  ← 中间：PictureBox，Zoom 居中显示照片
    /// │                                        │
    /// └────────────────────────────────────┘
    /// 说明：
    ///   - 图像区用 PictureBox.SizeMode=Zoom，等比缩放不裁剪；
    ///   - 控件默认浅蓝底（空态），收到图片后自动切图片显示；
    ///   - 本控件不显示存图点位（避免主界面信息冗余）；点位归属由
    ///     ProductionCoordinator 按"窗口→点位"映射计算，只通过设置界面的
    ///     WindowPointForm 查询/比对（见 DisplayConfig.WindowStationMap）。
    ///   - V1.9.5：去掉右下角 OK/NG 徽标（现场嫌占画面），判定状态仍由
    ///     主流程记录（IsOk/SetOkNgStatus 保留接口，只是不再叠加显示在画面上）。
    ///   - V2.10.3：OK/NG 徽标改为【可配置显隐】——由 MainForm 按 DisplayConfig.WindowOkNgVisible
    ///     控制（V2.14.24 起默认开启），勾选后右下角叠加自绘矩形框 OK/NG，颜色随配置 OK/NG 色。
    ///   - V2.14.24：徽标【拿到相机结果才显示】——新的一轮清窗/空窗口未接图时隐藏（宁缺毋滥），
    ///     只有本窗口点位拿到相机 OK/NG 判定（SetOkNgStatus）才显示对应徽标；新一轮开始（ResetResult）
    ///     复位结果态、徽标随图片一起清掉，杜绝上一轮结果残留误报。
    /// </summary>
    public class CameraDisplayControl : UserControl
    {
        /// <summary>图像显示区</summary>
        private PictureBox _pictureBox;

        /// <summary>窗口编号（从 1 开始，辅助现场定位第几路）</summary>
        private readonly Label _windowIndexLabel;

        /// <summary>当前结果：true=OK，false=NG</summary>
        private bool _isOk = true;

        /// <summary>
        /// 本窗口点位是否已拿到相机 OK/NG 结果（V2.14.24）。
        /// false（新一轮清窗后/空窗口未接图）= 徽标隐藏——宁可不显示，也不能拿"上上轮"的结果冒充；
        /// true 表示最近一轮相机判定已回到本窗口（SetOkNgStatus 最后一次调用后）。
        /// </summary>
        private bool _hasResult = false;

        /// <summary>
        /// 是否开启"窗口徽标"的显示开关（V2.14.24，由 MainForm 按 DisplayConfig.WindowOkNgVisible 注入）。
        /// 徽标最终显隐 = 本开关 && _hasResult（见 UpdateBadgeVisibility）。
        /// </summary>
        private bool _windowOkNgVisible = false;

        /// <summary>右下角 OK/NG 徽标（V2.10.3，默认隐藏，由"开关 && 已拿到结果"共同决定显隐）</summary>
        private readonly OkNgBadge _badge;

        /// <summary>窗口编号（1 起）</summary>
        private int _windowIndex;

        /// <summary>悬停提示（V2.10.7）：提示操作员可双击放大/还原该窗口。</summary>
        private readonly ToolTip _toolTip;

        /// <summary>悬停气泡提示文案（V2.10.7，开关关闭后再开启时恢复用）。</summary>
        private const string DoubleClickTipText = "双击放大（全屏查看）；再双击还原";

        /// <summary>
        /// 构造：创建图像区、编号标签并摆好位置。
        /// 使用 Dock 覆盖思路做简易布局。
        /// </summary>
        public CameraDisplayControl()
        {
            // 整体容器底色：淡蓝（未拍照/空态，与小清新浅色主题一致）
            BackColor = Color.FromArgb(220, 231, 243);
            Size = new Size(240, 180);

            // ① 图像显示区：占满整个控件，Zoom 等比显示图片
            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(45, 45, 45), // 深灰底，无图时直观看出"空"
                Padding = new Padding(0)
            };
            Controls.Add(_pictureBox);

            // ② 窗口编号标签：左上角悬浮（浅色主题 → 半透明白底 + 深蓝灰字）
            _windowIndexLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(52, 73, 94),
                BackColor = Color.FromArgb(255, 255, 255, 200),
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                Text = "1",
                Location = new Point(4, 4)
            };
            Controls.Add(_windowIndexLabel);

            // ③ 右下角 OK/NG 徽标（V2.10.3；V2.14.24 起"拿到结果才显示"）：叠加在画面上方、
            //    初始隐藏，由 MainForm 按配置调用 SetOkNgVisible 注入开关，本窗口拿到相机判定
            //    （SetOkNgStatus）后才显示对应徽标。Anchor=Bottom|Right → 控件缩放时始终贴右下角且间距固定。
            _badge = new OkNgBadge
            {
                Size = new Size(52, 24),
                Location = new Point(Size.Width - 52 - 6, Size.Height - 24 - 6),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Visible = false
            };
            Controls.Add(_badge);

            // 控件尺寸变化时，保证编号标签与 OK/NG 徽标始终浮在最上层（不被图片盖住）
            Resize += (s, e) =>
            {
                _windowIndexLabel.BringToFront();
                _badge.BringToFront();
            };

            // 双击放大/还原（V1.12.15）：
            // ★ 直接订阅"子控件"的 MouseDoubleClick，而非依赖父控件的冒泡（血泪教训）——
            //   初次实现用重写 OnDoubleClick：DoubleClick 事件不支持冒泡，双击落在图像区
            //   （PictureBox 占满整窗）时不触发，导致"双击没反应"；二次改为订阅本 UserControl
            //   的 MouseDoubleClick，实测部分环境冒泡仍不稳定，同样没生效。
            //   图像区 PictureBox 用 Dock=Fill 占满整个窗口，双击无论点在哪必落其上，所以
            //   直接订阅 PictureBox（及其上的编号标签）的 MouseDoubleClick 是必然命中的方式，
            //   完全绕开冒泡的不确定性。
            // 左键双击才触发 WindowDoubleClicked（右键双击不入此）。
            var handler = new MouseEventHandler(HandleDoubleClick);
            _pictureBox.MouseDoubleClick += handler;
            _windowIndexLabel.MouseDoubleClick += handler;

            // ④ 悬停提示（V2.10.7）：提示操作员"双击放大/还原"。
            //   ★ 挂到"真实命中双击"的同一批子控件上（PictureBox Dock=Fill 占满整窗、
            //   编号标签覆盖左上角），与双击订阅同批，保证鼠标悬停到任意位置都有提示；
            //   不要只挂 UserControl 自身——鼠标实际落在子控件上，单独挂父控件会提示不出来。
            //   显隐由配置 DisplayConfig.WindowToolTipVisible 控制（V2.10.8），构造默认显示。
            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_pictureBox, DoubleClickTipText);
            _toolTip.SetToolTip(_windowIndexLabel, DoubleClickTipText);
        }

        /// <summary>
        /// 子控件双击统一入口（V1.12.15）：左键双击 → 通知主窗体放大/还原全屏。
        /// </summary>
        private void HandleDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                WindowDoubleClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 设置窗口编号并显示到左上角。
        /// </summary>
        /// <param name="index">从 1 开始的编号</param>
        public void SetWindowIndex(int index)
        {
            _windowIndex = index;
            _windowIndexLabel.Text = index.ToString();
        }

        /// <summary>
        /// 设置左上角窗口编号标签是否显示（V2.10.4，由主窗体按配置调用）。
        /// 默认显示（true）；配置关掉后隐藏，画面更干净。
        /// </summary>
        /// <param name="visible">true=显示窗口编号，false=隐藏</param>
        public void SetWindowIndexVisible(bool visible)
        {
            _windowIndexLabel.Visible = visible;
        }

        /// <summary>
        /// 设置悬停气泡提示是否显示（V2.10.8，由主窗体按配置调用）。
        /// 默认显示（true）；配置关掉后移除气泡，画面更干净。
        /// </summary>
        /// <param name="visible">true=悬停显示气泡提示，false=关闭提示</param>
        public void SetToolTipVisible(bool visible)
        {
            if (visible)
            {
                // 恢复提示（文本是常量，与构造时保持一致）
                _toolTip.SetToolTip(_pictureBox, DoubleClickTipText);
                _toolTip.SetToolTip(_windowIndexLabel, DoubleClickTipText);
            }
            else
            {
                // 移除提示：SetToolTip 传 null 即解除该控件的气泡
                _toolTip.SetToolTip(_pictureBox, null);
                _toolTip.SetToolTip(_windowIndexLabel, null);
            }
        }

        /// <summary>
        /// 设置或清除要显示的照片。
        /// </summary>
        /// <param name="image">照片；传 null 表示清空回到深灰空态</param>
        public void SetImage(Image image)
        {
            if (_pictureBox.Image != null && !ReferenceEquals(_pictureBox.Image, _pictureBox.InitialImage))
                _pictureBox.Image?.Dispose();
            _pictureBox.Image = image;
            _pictureBox.BackColor = Color.FromArgb(45, 45, 45);
        }

        /// <summary>
        /// 设置检测结果（V2.10.3；V2.14.24 起"拿到结果才显示徽标"）：
        /// 记录本窗口点位已拿到相机判定，并同步到 OK/NG 徽标——有结果后徽标随开关显示、随结果变色。
        /// </summary>
        /// <param name="isOk">true=OK，false=NG</param>
        public void SetOkNgStatus(bool isOk)
        {
            _isOk = isOk;
            _hasResult = true;          // 本窗口点位拿到相机结果：解除"无结果隐藏"状态
            _badge.IsOk = isOk;
            UpdateBadgeVisibility();
        }

        /// <summary>当前结果：true=OK，false=NG</summary>
        public bool IsOk => _isOk;

        /// <summary>
        /// 复位"结果态"（V2.14.24，新一轮开始清窗时由 MainForm 调用）：
        /// 本窗口点位还没拿到新一轮的相机结果，徽标隐藏——避免上一轮的 OK/NG 残留误导现场。
        /// 注意只复位结果态、不影响 _isOk 与已显示的图片（清图由调用方 SetImage(null) 负责）。
        /// </summary>
        public void ResetResult()
        {
            _hasResult = false;
            UpdateBadgeVisibility();
        }

        /// <summary>
        /// 设置右下角 OK/NG 徽标开关（V2.10.3，由主窗体按配置调用）。
        /// 最终显隐 = 本开关 && 本窗口已拿到结果（_hasResult）——没结果时开关开了也不显示。
        /// </summary>
        public void SetOkNgVisible(bool visible)
        {
            _windowOkNgVisible = visible;
            UpdateBadgeVisibility();
        }

        /// <summary>按"开关 && 已拿到相机结果"刷新徽标显隐（V2.14.24 唯一判定点）。</summary>
        private void UpdateBadgeVisibility()
        {
            _badge.Visible = _hasResult && _windowOkNgVisible;
        }

        /// <summary>设置徽标 OK/NG 颜色（V2.10.3，跟随 display.okColorName/ngColorName）。</summary>
        public void SetOkNgColors(Color ok, Color ng)
        {
            _badge.OkColor = ok;
            _badge.NgColor = ng;
        }

        /// <summary>当前窗口编号</summary>
        public int WindowIndex => _windowIndex;

        /// <summary>
        /// 鼠标左键双击事件（V1.12.15：供主窗体放大/还原全屏使用）。
        /// 只在 UI 线程触发；主窗体据此把本窗口放大到全屏、再次双击还原。
        /// </summary>
        public event EventHandler WindowDoubleClicked;

        /// <summary>
        /// 资源释放：释放控件持有的图片句柄，避免句柄泄漏。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pictureBox?.Image?.Dispose();
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}