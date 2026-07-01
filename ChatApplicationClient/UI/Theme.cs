using System.Drawing.Drawing2D;

namespace ChatApplicationClient.UI
{
    /// <summary>
    /// Bảng màu và font dùng chung cho toàn ứng dụng – THEME NỀN SÁNG.
    /// Tập trung màu sắc tại một nơi để giao diện đồng nhất, hiện đại, dễ đọc.
    /// </summary>
    internal static class Theme
    {
        // Nền chính cửa sổ: xám rất nhạt
        public static readonly Color Background = ColorTranslator.FromHtml("#F7F8FA");
        // Nền panel/khung phụ: trắng
        public static readonly Color Panel = ColorTranslator.FromHtml("#FFFFFF");
        // Viền nhạt
        public static readonly Color Border = ColorTranslator.FromHtml("#E3E6EA");
        // Màu nhấn chính (nút, header, tiêu đề)
        public static readonly Color Accent = ColorTranslator.FromHtml("#3B82F6");
        public static readonly Color AccentHover = ColorTranslator.FromHtml("#2F74E0");
        // Chữ trên nút nhấn
        public static readonly Color OnAccent = Color.White;
        // Bong bóng tin của mình: xanh nhạt, căn phải
        public static readonly Color BubbleMine = ColorTranslator.FromHtml("#DCEBFF");
        // Bong bóng tin của người khác: xám nhạt, căn trái
        public static readonly Color BubbleOther = ColorTranslator.FromHtml("#EEF1F4");
        // Chữ chính: xám đậm gần đen (KHÔNG dùng đen tuyền)
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#1F2937");
        // Thông báo hệ thống: xám nhạt
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#6B7280");

        public static readonly Font Font = new("Segoe UI", 10F);
        public static readonly Font FontSmall = new("Segoe UI", 8.5F);
        public static readonly Font FontSmallBold = new("Segoe UI", 8.5F, FontStyle.Bold);
        public static readonly Font FontTitle = new("Segoe UI Semibold", 14F);
        public static readonly Font FontSystem = new("Segoe UI", 9F, FontStyle.Italic);
        // Font hỗ trợ emoji (Segoe UI Emoji): dùng cho control hiển thị tin nhắn và
        // ô nhập để emoji/biểu tượng không bị hiển thị thành ô vuông (tofu).
        public static readonly Font FontEmoji = new("Segoe UI Emoji", 10F);
        // Bản cỡ lớn dùng cho các ô emoji trong bảng chọn và nút mở bảng chọn.
        public static readonly Font FontEmojiLarge = new("Segoe UI Emoji", 15F);

        /// <summary>Tạo đường viền bo góc cho việc vẽ bong bóng/nút.</summary>
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Định kiểu nút phẳng, bo góc, màu nhấn – chữ trắng.</summary>
        public static void StylePrimaryButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Accent;
            b.ForeColor = OnAccent;
            b.Font = new Font("Segoe UI Semibold", 10F);
            b.Cursor = Cursors.Hand;
            b.Height = 38;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            // Bo góc nhẹ cho nút
            b.Resize += (_, _) => b.Region = new Region(RoundedRect(new Rectangle(0, 0, b.Width, b.Height), 8));
        }

        /// <summary>Nút phụ: nền trắng, viền nhạt, chữ tối.</summary>
        public static void StyleSecondaryButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = Panel;
            b.ForeColor = TextPrimary;
            b.Font = Font;
            b.Cursor = Cursors.Hand;
            b.Height = 34;
            b.FlatAppearance.MouseOverBackColor = Background;
        }

        /// <summary>Ô nhập liệu có khung bo nhẹ (đặt TextBox trong panel viền).</summary>
        public static Panel WrapInput(TextBox tb)
        {
            var p = new Panel
            {
                BackColor = Panel,
                Padding = new Padding(10, 8, 10, 8),
                Height = tb.PreferredHeight + 16
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Border);
                using var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 8);
                e.Graphics.DrawPath(pen, path);
            };
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = Panel;
            tb.ForeColor = TextPrimary;
            tb.Font = Font;
            tb.Dock = DockStyle.Fill;
            p.Controls.Add(tb);
            return p;
        }
    }
}
