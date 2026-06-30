namespace ChatApplicationClient.UI
{
    /// <summary>
    /// Bảng chọn emoji dạng popup nhỏ (theme nền sáng). Hiển thị lưới các emoji
    /// thông dụng chia theo vài nhóm; bấm vào một emoji sẽ phát sự kiện
    /// <see cref="EmojiSelected"/> kèm chuỗi Unicode của emoji đó.
    /// Popup tự đóng khi mất tiêu điểm (người dùng bấm ra ngoài).
    /// Đây thuần tuý là phần GIAO DIỆN: emoji chỉ là ký tự Unicode, không đụng
    /// tới protocol hay mã hóa.
    /// </summary>
    internal sealed class EmojiPickerForm : Form
    {
        /// <summary>Phát ra khi người dùng chọn một emoji (chuỗi Unicode của emoji).</summary>
        public event Action<string>? EmojiSelected;

        // Các nhóm emoji thông dụng: (tiêu đề nhóm, danh sách emoji). Chỉ dùng emoji
        // đơn giản (một mã điểm hoặc kèm bộ chọn biến thể) để hiển thị ổn định.
        private static readonly (string Title, string[] Emojis)[] Groups =
        {
            ("Mặt cười & cảm xúc", new[]
            {
                "😀","😃","😄","😁","😆","😅","😂","🤣","😊","😇",
                "🙂","😉","😌","😍","🥰","😘","😋","😎","🤩","🥳",
            }),
            ("Buồn & lo lắng", new[]
            {
                "😐","😕","🙁","😞","😔","😢","😭","😤","😠","😡",
                "🤔","😴","😪","😨","😱","😳",
            }),
            ("Cử chỉ tay", new[]
            {
                "👍","👎","👌","✌️","🤞","🙏","👏","🙌","💪","👋","🤝","✋",
            }),
            ("Tim & biểu tượng", new[]
            {
                "❤️","🧡","💛","💚","💙","💜","🖤","💔","💯","🔥",
                "⭐","✨","🎉","🎁","✅","❌",
            }),
        };

        public EmojiPickerForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Theme.Panel;
            ClientSize = new Size(380, 430);

            BuildUi();
        }

        // Vẽ một đường viền mảnh quanh popup cho hợp theme nền sáng.
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        // Người dùng bấm ra ngoài -> popup mất tiêu điểm -> tự đóng.
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        private void BuildUi()
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Panel,
                Padding = new Padding(10, 8, 10, 10),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
            };

            foreach (var (title, emojis) in Groups)
            {
                // Tiêu đề nhóm chiếm trọn một hàng riêng.
                var header = new Label
                {
                    Text = title.ToUpperInvariant(),
                    Font = Theme.FontSmallBold,
                    ForeColor = Theme.TextMuted,
                    AutoSize = false,
                    Size = new Size(320, 22),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = new Padding(2, 6, 2, 2),
                };
                flow.Controls.Add(header);
                flow.SetFlowBreak(header, true);

                Label? last = null;
                foreach (var emoji in emojis)
                {
                    last = CreateEmojiTile(emoji);
                    flow.Controls.Add(last);
                }
                // Ngắt dòng sau ô cuối nhóm để nhóm sau bắt đầu ở hàng mới.
                if (last != null) flow.SetFlowBreak(last, true);
            }

            Controls.Add(flow);
        }

        // Một ô emoji bấm được: Label vuông, đổi màu nền khi rê chuột.
        private Label CreateEmojiTile(string emoji)
        {
            var tile = new Label
            {
                Text = emoji,
                Font = Theme.FontEmojiLarge,
                UseCompatibleTextRendering = true,
                AutoSize = false,
                Size = new Size(40, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(2),
                Cursor = Cursors.Hand,
                BackColor = Theme.Panel,
            };
            tile.MouseEnter += (_, _) => tile.BackColor = Theme.Background;
            tile.MouseLeave += (_, _) => tile.BackColor = Theme.Panel;
            tile.Click += (_, _) => EmojiSelected?.Invoke(emoji);
            return tile;
        }
    }
}
