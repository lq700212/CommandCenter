using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CommandCenter.Controls
{
    /// <summary>
    /// ToolTip 提示标记辅助类：给"已挂 ToolTip 悬停说明"的控件旁边自动添加一个小问号 "?"，
    /// 提醒操作员"这里悬停有说明可看"。悬停问号本身也显示同一 ToolTip。
    ///
    /// 【为什么加】
    ///   ToolTip 气泡只在鼠标悬停时才出现，界面上没有任何常驻标识告诉操作员"哪个控件有说明"，
    ///   现场往往没人知道要去悬停。补一个问号图标后，用户一看就知道"这里有提示"。
    ///   "?" 是 Windows 帮助/提示的标准符号（系统设置、VS 选项对话框等都用它），符合业内惯例。
    ///
    /// 【用法】
    ///   在窗体构造（InitializeComponent 之后）调用一次：
    ///       TipMarker.AttachAll(this, tip);
    ///   它会自动遍历窗体所有控件，凡是有 ToolTip 文本的都在其旁边放一个问号标记。
    ///   若某个控件的 ToolTip 文本之后会被动态刷新（如 SettingsForm.btnEditDirs），
    ///   刷新后调用 TipMarker.Sync(btnEditDirs, tip) 即可让问号标记同步新文本。
    ///
    /// 【位置算法】
    ///   依次尝试 右侧 → 左侧 → 上方 → 下方，取第一个"不超出父容器、不与其它可见控件重叠"的位置；
    ///   四个方向都放不下就放弃该控件（返回 null），避免问号压住界面上的正经控件。
    /// </summary>
    public static class TipMarker
    {
        /// <summary>问号标记边长（像素），固定小尺寸不随字体缩放，视觉统一。</summary>
        private const int MarkSize = 16;

        /// <summary>问号与宿主控件的间隙（像素）。</summary>
        private const int Gap = 2;

        /// <summary>问号颜色：标准"帮助蓝"，比纯黑醒目又不刺眼。</summary>
        private static readonly Color MarkColor = Color.FromArgb(0, 102, 204);

        /// <summary>GroupBox 标题栏高度估算（像素）：问号不能压到容器顶部的标题文字上。</summary>
        private const int GroupBoxHeaderHeight = 22;

        /// <summary>
        /// 给 root 下所有已挂 ToolTip 的控件统一添加问号标记（含 GroupBox 等容器内层控件）。
        /// 先收集再添加，避免在遍历 Controls 集合的同时往里加控件导致枚举异常。
        /// </summary>
        /// <param name="root">要扫描的窗体/容器（一般传 this）。</param>
        /// <param name="tip">窗体上那个 ToolTip 组件。</param>
        public static void AttachAll(Control root, ToolTip tip)
        {
            var targets = new List<Control>();
            Collect(root, targets);
            foreach (Control c in targets)
            {
                string text = tip.GetToolTip(c);
                if (!string.IsNullOrEmpty(text))
                    AttachTo(c, tip, text);
            }
        }

        /// <summary>
        /// 给单个控件挂问号标记。若该控件已挂过（Tag 中存有标记）则只更新提示文本，避免重复添加。
        /// </summary>
        /// <param name="host">带 ToolTip 的宿主控件。</param>
        /// <param name="tip">窗体上那个 ToolTip 组件。</param>
        /// <param name="text">要显示在问号上的 ToolTip 文本（一般与宿主一致）。</param>
        /// <returns>创建的问号 Label；四个方向都放不下时返回 null。</returns>
        public static Label AttachTo(Control host, ToolTip tip, string text)
        {
            if (host == null || host.Parent == null) return null;

            // 已挂过标记：复用并刷新文本（动态 ToolTip 场景，见 Sync）
            if (host.Tag is Label existing && !existing.IsDisposed)
            {
                tip.SetToolTip(existing, text);
                return existing;
            }

            var mark = new Label
            {
                Text = "?",
                AutoSize = false,
                Size = new Size(MarkSize, MarkSize),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = MarkColor,
                Font = new Font(host.Font.FontFamily, 9F, FontStyle.Bold),
                Cursor = Cursors.Help,          // 鼠标悬停变成问号，暗示"这里有帮助"
                Margin = Padding.Empty
            };

            Point? loc = FindSpot(host, MarkSize);
            if (loc == null)
            {
                mark.Dispose();
                return null;
            }

            mark.Location = loc.Value;
            host.Parent.Controls.Add(mark);
            mark.BringToFront();                // 保证问号浮在其它控件之上，不被遮挡
            host.Tag = mark;                    // 用 Tag 记下标记，方便 Sync 同步 / 防重复
            tip.SetToolTip(mark, text);
            return mark;
        }

        /// <summary>
        /// 同步：宿主控件的 ToolTip 文本被代码动态刷新后调用，让问号标记的文本跟着变。
        /// 例：SettingsForm.RefreshDirPreview 每次更新 btnEditDirs 的提示后调用本方法。
        /// </summary>
        /// <param name="host">ToolTip 文本刚被更新过的宿主控件。</param>
        /// <param name="tip">窗体上那个 ToolTip 组件。</param>
        public static void Sync(Control host, ToolTip tip)
        {
            if (host?.Tag is Label mark && !mark.IsDisposed)
                tip.SetToolTip(mark, tip.GetToolTip(host));
        }

        /// <summary>递归收集 root 下的所有后代控件（含嵌套容器里的）。</summary>
        private static void Collect(Control parent, List<Control> result)
        {
            foreach (Control c in parent.Controls)
            {
                result.Add(c);
                Collect(c, result);
            }
        }

        /// <summary>
        /// 为问号找一个不冲突的位置：依次试 右→左→上→下，
        /// 返回第一个合法位置；全放不下返回 null。
        /// </summary>
        private static Point? FindSpot(Control host, int size)
        {
            int midY = host.Top + (host.Height - size) / 2;                 // 垂直居中
            int centerX = host.Left + (host.Width - size) / 2;              // 水平居中

            Point[] candidates =
            {
                new Point(host.Right + Gap, midY),                          // 1. 右侧
                new Point(host.Left - size - Gap, midY),                    // 2. 左侧
                new Point(centerX, host.Top - size - Gap),                  // 3. 上方
                new Point(centerX, host.Bottom + Gap)                       // 4. 下方
            };

            foreach (Point p in candidates)
                if (CanPlace(host, p, size))
                    return p;
            return null;
        }

        /// <summary>检查 (loc) 放一个 size×size 的问号是否合法：不超父容器边界、不与其它可见控件重叠。</summary>
        private static bool CanPlace(Control host, Point loc, int size)
        {
            Control parent = host.Parent;
            if (parent == null) return false;

            Rectangle rc = new Rectangle(loc, new Size(size, size));
            if (!parent.ClientRectangle.Contains(rc)) return false;         // 越界

            // 父容器是 GroupBox 时，顶部标题栏区域（有标题文字）不允许放问号，
            // 否则会压在"实时预览（示例：…）"这类 GroupBox 标题上
            if (parent is GroupBox && rc.Top < GroupBoxHeaderHeight) return false;

            foreach (Control c in parent.Controls)
            {
                if (c == host || !c.Visible) continue;                       // 宿主自己不算，不可见的不算
                if (c.Bounds.IntersectsWith(rc)) return false;              // 压到别的控件
            }
            return true;
        }
    }
}
