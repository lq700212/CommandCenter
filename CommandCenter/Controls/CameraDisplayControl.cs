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
    /// </summary>
    public class CameraDisplayControl : UserControl
    {
        /// <summary>图像显示区</summary>
        private PictureBox _pictureBox;

        /// <summary>窗口编号（从 1 开始，辅助现场定位第几路）</summary>
        private readonly Label _windowIndexLabel;

        /// <summary>当前结果：true=OK，false=NG</summary>
        private bool _isOk = true;

        /// <summary>窗口编号（1 起）</summary>
        private int _windowIndex;

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

            // 控件尺寸变化时，保证编号标签始终浮在最上层（不被图片盖住）
            Resize += (s, e) => _windowIndexLabel.BringToFront();
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
        /// 设置检测结果（V1.9.5：仅记录状态，画面上已不叠加 OK/NG 徽标）。
        /// </summary>
        /// <param name="isOk">true=OK，false=NG</param>
        public void SetOkNgStatus(bool isOk)
        {
            _isOk = isOk;
        }

        /// <summary>当前结果：true=OK，false=NG</summary>
        public bool IsOk => _isOk;

        /// <summary>当前窗口编号</summary>
        public int WindowIndex => _windowIndex;

        /// <summary>
        /// 资源释放：释放控件持有的图片句柄，避免句柄泄漏。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _pictureBox?.Image?.Dispose();
            base.Dispose(disposing);
        }
    }
}