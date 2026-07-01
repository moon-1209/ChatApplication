using System.Drawing.Drawing2D;

namespace ChatApplicationClient.UI
{
    internal enum BubbleStyle { Mine, Other, System }

    /// <summary>
    /// Một bong bóng tin nhắn tự vẽ: bo góc, tự co giãn theo nội dung.
    /// - Mine: nền xanh nhạt, căn phải.
    /// - Other: nền xám nhạt, căn trái, có tên người gửi phía trên.
    /// - System: chữ xám nhạt in nghiêng, không nền (thông báo hệ thống).
    /// </summary>
    internal sealed partial class BubbleLabel : Control
    {
        private const int PadX = 12;
        private const int PadY = 8;
        private const int Radius = 12;

        public BubbleStyle Style { get; }
        public string Sender { get; }
        private readonly string _message;

        public BubbleLabel(BubbleStyle style, string sender, string message)
        {
            Style = style;
            Sender = sender;
            _message = message;
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
        }

        // Tin của mình/người khác dùng font hỗ trợ emoji để biểu tượng hiển thị đúng
        // (không thành ô vuông); thông báo hệ thống giữ kiểu chữ nghiêng như cũ.
        private Font MsgFont => Style == BubbleStyle.System ? Theme.FontSystem : Theme.FontEmoji;
        private bool HasHeader => Style == BubbleStyle.Other && !string.IsNullOrEmpty(Sender);

        /// <summary>Tính lại kích thước bong bóng dựa trên độ rộng tối đa cho phép.</summary>
        public void Measure(int maxBubbleWidth)
        {
            int innerMax = Math.Max(40, maxBubbleWidth - 2 * PadX);
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;

            Size text = TextRenderer.MeasureText(_message, MsgFont, new Size(innerMax, int.MaxValue), flags);

            if (Style == BubbleStyle.System)
            {
                // Thông báo hệ thống chiếm gần hết bề rộng, không có padding bong bóng
                Size = new Size(Math.Min(maxBubbleWidth, text.Width + 4), text.Height + 6);
                return;
            }

            int w = text.Width;
            int headerH = 0;
            if (HasHeader)
            {
                Size hs = TextRenderer.MeasureText(Sender, Theme.FontSmallBold, new Size(innerMax, int.MaxValue), flags);
                w = Math.Max(w, hs.Width);
                headerH = hs.Height + 2;
            }
            Size = new Size(w + 2 * PadX, headerH + text.Height + 2 * PadY);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;

            if (Style == BubbleStyle.System)
            {
                TextRenderer.DrawText(g, _message, Theme.FontSystem,
                    new Rectangle(2, 2, Width - 4, Height - 4), Theme.TextMuted,
                    flags | TextFormatFlags.HorizontalCenter);
                return;
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color back = Style == BubbleStyle.Mine ? Theme.BubbleMine : Theme.BubbleOther;
            using (var path = Theme.RoundedRect(rect, Radius))
            using (var brush = new SolidBrush(back))
                g.FillPath(brush, path);

            int y = PadY;
            if (HasHeader)
            {
                var hRect = new Rectangle(PadX, y, Width - 2 * PadX, Height);
                Size hs = TextRenderer.MeasureText(Sender, Theme.FontSmallBold, new Size(Width - 2 * PadX, int.MaxValue), flags);
                TextRenderer.DrawText(g, Sender, Theme.FontSmallBold,
                    new Rectangle(PadX, y, Width - 2 * PadX, hs.Height), Theme.Accent, flags);
                y += hs.Height + 2;
            }

            TextRenderer.DrawText(g, _message, MsgFont,
                new Rectangle(PadX, y, Width - 2 * PadX, Height - y - PadY), Theme.TextPrimary, flags);
        }
    }
}
