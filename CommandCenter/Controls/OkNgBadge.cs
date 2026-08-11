using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Controls
{
    /// <summary>
    /// OK/NG 徽标控件：自绘一个矩形框，框线与框内文字同色。
    /// ┌────────┐
    /// │  OK    │  ← OK = 绿色框 + 绿色字（现场习惯：OK 即绿）
    /// │  NG    │  ← NG = 红色框 + 红色字
    /// └────────┘
    /// 说明：
    ///   - 用 TextRenderer(GDI) 自绘文字，清晰且支持后续扩展抗锯齿；
    ///   - 颜色通过 IsOk 属性切换，无需外部传参；
    ///   - 尺寸建议 52x24，可由调用方按需调整。
    /// </summary>
    public class OkNgBadge : Control
    {
        private bool _isOk = true;

        /// <summary>true=OK(绿色)，false=NG(红色)</summary>
        public bool IsOk
        {
            get => _isOk;
            set { _isOk = value; Invalidate(); } // 颜色变化后立即重绘
        }

        public OkNgBadge()
        {
            // 徽标不参与字体缩放，保持固定逻辑尺寸；值为继承自父窗体的状态即可
            Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold);
            DoubleBuffered = true;
        }

        /// <summary>
        /// 自绘：先画白底 + 彩色矩形框，再居中画彩色文字。
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Color accent = IsOk ? Color.Green : Color.Red; // OK=绿，NG=红

            Graphics g = e.Graphics;
            Rectangle rect = ClientRectangle;

            // 白底，避免与相机深灰底混色，突出徽标
            using (var white = new SolidBrush(Color.White))
                g.FillRectangle(white, rect);

            // 矩形框线（2px，让边框醒目）
            using (var pen = new Pen(accent, 2f))
                g.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 3, rect.Height - 3);

            // 框内文字：与框同色（OK 绿字 / NG 红字），垂直水平居中
            TextRenderer.DrawText(g,
                IsOk ? "OK" : "NG",
                Font,
                new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4),
                accent,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}