namespace ChatApplicationClient.UI
{
    /// <summary>
    /// Khung hiển thị tin nhắn có cuộn. Tự bố trí các bong bóng theo chiều dọc,
    /// căn phải cho tin của mình và căn trái cho tin của người khác.
    /// </summary>
    internal sealed partial class MessageListPanel : Panel
    {
        private const int Gap = 8;        // khoảng cách giữa các bong bóng
        private const int Side = 12;      // lề trái/phải
        private readonly List<BubbleLabel> _items = new();

        public MessageListPanel()
        {
            AutoScroll = true;
            BackColor = Theme.Background;
            DoubleBuffered = true;
            Padding = new Padding(0, 8, 0, 8);
        }

        public void AddMessage(BubbleStyle style, string sender, string text)
        {
            var bubble = new BubbleLabel(style, sender, text);
            _items.Add(bubble);
            Controls.Add(bubble);
            Relayout();
            ScrollToBottom();
        }

        public void Clear()
        {
            foreach (var b in _items) b.Dispose();
            _items.Clear();
            Controls.Clear();
            AutoScrollMinSize = Size.Empty;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        private void Relayout()
        {
            int avail = ClientSize.Width;
            int maxBubble = (int)(avail * 0.74);
            // Bố trí theo toạ độ logic rồi cộng AutoScrollPosition để khớp với vị trí cuộn hiện tại
            int y = Padding.Top;

            SuspendLayout();
            foreach (var b in _items)
            {
                b.Measure(maxBubble);
                int x = b.Style == BubbleStyle.Mine
                    ? avail - b.Width - Side
                    : Side;
                if (b.Style == BubbleStyle.System)
                    x = Side;

                b.Location = new Point(x + AutoScrollPosition.X, y + AutoScrollPosition.Y);
                y += b.Height + Gap;
            }
            ResumeLayout();
            AutoScrollMinSize = new Size(0, y + Padding.Bottom);
        }

        public void ScrollToBottom()
        {
            if (AutoScrollMinSize.Height <= ClientSize.Height) return;
            AutoScrollPosition = new Point(0, AutoScrollMinSize.Height);
        }
    }
}
