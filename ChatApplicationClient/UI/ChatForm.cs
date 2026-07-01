using ChatApplicationClient.Core;
using System.Text.RegularExpressions;

namespace ChatApplicationClient.UI
{
    /// <summary>
    /// Cửa sổ chat chính: danh sách phòng, khung tin nhắn (bong bóng), ô nhập,
    /// nút gửi, nút gửi file (kèm tiến độ). Toàn bộ event từ luồng nhận nền
    /// được marshal về UI thread bằng BeginInvoke trước khi cập nhật giao diện.
    /// </summary>
    internal sealed partial class  ChatForm : Form
    {
        private readonly ChatClient _conn;

        // Cột trái – phòng & thành viên
        private readonly TextBox _txtRoom = new();
        private readonly Button _btnJoin = new() { Text = "Tham gia" };
        private readonly Button _btnLeave = new() { Text = "Rời phòng" };
        private readonly Button _btnRefresh = new() { Text = "⟳ Làm mới danh sách" };
        private readonly ListBox _lstRooms = new();
        private readonly ListBox _lstMembers = new();

        // Khu chat
        private readonly Label _lblRoomHeader = new();
        private readonly MessageListPanel _messages = new();
        private readonly TextBox _txtInput = new();
        private readonly Button _btnSend = new() { Text = "Gửi" };
        private readonly Button _btnFile = new() { Text = "📎 File" };
        private readonly Button _btnEmoji = new() { Text = "😀" };
        private readonly ProgressBar _progress = new() { Visible = false };
        private readonly Label _lblProgress = new() { Visible = false };

        // Bảng chọn emoji (popup) và thời điểm nó đóng gần nhất – dùng để nút emoji
        // hoạt động như công tắc bật/tắt (tránh mở lại ngay khi cú bấm vừa đóng nó).
        private EmojiPickerForm? _emojiPicker;
        private DateTime _emojiClosedAt;

        private static readonly Regex RoomLine =
            new(@"^\s*(.+?)\s*\((\d+)\s*người\)\s*$", RegexOptions.Compiled);

        public ChatForm(ChatClient conn)
        {
            _conn = conn;

            Text = $"Chat - {conn.Account}";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(940, 620);
            MinimumSize = new Size(760, 480);
            BackColor = Theme.Background;
            Font = Theme.Font;

            BuildLayout();
            WireEvents();
            UpdateRoomHeader();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Đã đăng ký event xong -> bắt đầu phiên (gửi ACCOUNT + vòng lặp nhận),
            // sau đó nạp danh sách phòng.
            try
            {
                await _conn.StartSessionAsync();
                await RefreshRoomsAsync();
            }
            catch (Exception ex)
            {
                _messages.AddMessage(BubbleStyle.System, "", $"Lỗi khởi tạo phiên: {ex.Message}");
            }
        }

        // =================== DỰNG GIAO DIỆN ===================

        private void BuildLayout()
        {
            // ----- Cột trái -----
            var left = new Panel { Dock = DockStyle.Left, Width = 260, BackColor = Theme.Panel, Padding = new Padding(14) };
            left.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, left.Width - 1, 0, left.Width - 1, left.Height);
            };

            var lblRooms = new Label { Text = "PHÒNG CHAT", Font = Theme.FontSmallBold, ForeColor = Theme.TextMuted, Dock = DockStyle.Top, Height = 22 };

            var joinRow = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(0, 4, 0, 4) };
            var roomBox = Theme.WrapInput(_txtRoom);
            _txtRoom.PlaceholderText = "Tên phòng...";
            roomBox.Dock = DockStyle.Fill;
            Theme.StylePrimaryButton(_btnJoin);
            _btnJoin.Dock = DockStyle.Right;
            _btnJoin.Width = 90;
            _btnJoin.Height = 36;
            joinRow.Controls.Add(roomBox);
            joinRow.Controls.Add(_btnJoin);

            Theme.StyleSecondaryButton(_btnLeave);
            _btnLeave.Dock = DockStyle.Top;
            _btnLeave.Margin = new Padding(0, 6, 0, 0);

            Theme.StyleSecondaryButton(_btnRefresh);
            _btnRefresh.Dock = DockStyle.Top;

            StyleList(_lstRooms);
            _lstRooms.Dock = DockStyle.Fill;
            _lstRooms.DisplayMember = "Display";

            var lblMembers = new Label { Text = "THÀNH VIÊN TRONG PHÒNG", Font = Theme.FontSmallBold, ForeColor = Theme.TextMuted, Dock = DockStyle.Top, Height = 22, Padding = new Padding(0, 4, 0, 0) };
            StyleList(_lstMembers);
            _lstMembers.Dock = DockStyle.Bottom;
            _lstMembers.Height = 150;

            // Thứ tự Add quyết định cách dock xếp chồng
            left.Controls.Add(_lstRooms);     // Fill
            left.Controls.Add(lblMembers);
            left.Controls.Add(_lstMembers);
            left.Controls.Add(_btnRefresh);
            left.Controls.Add(_btnLeave);
            left.Controls.Add(joinRow);
            left.Controls.Add(lblRooms);

            // ----- Khu chat (phải) -----
            var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };

            _lblRoomHeader.Dock = DockStyle.Top;
            _lblRoomHeader.Height = 52;
            _lblRoomHeader.BackColor = Theme.Panel;
            _lblRoomHeader.ForeColor = Theme.TextPrimary;
            _lblRoomHeader.Font = new Font("Segoe UI Semibold", 12F);
            _lblRoomHeader.TextAlign = ContentAlignment.MiddleLeft;
            _lblRoomHeader.Padding = new Padding(16, 0, 0, 0);

            _messages.Dock = DockStyle.Fill;

            // Thanh tiến độ truyền file
            var progressRow = new Panel { Dock = DockStyle.Bottom, Height = 0, Padding = new Padding(16, 0, 16, 0) };
            _progress.Dock = DockStyle.Bottom;
            _progress.Height = 6;
            _lblProgress.Dock = DockStyle.Bottom;
            _lblProgress.Height = 18;
            _lblProgress.ForeColor = Theme.TextMuted;
            _lblProgress.Font = Theme.FontSmall;
            progressRow.Controls.Add(_progress);
            progressRow.Controls.Add(_lblProgress);
            progressRow.Visible = false;
            _progressRow = progressRow;

            // Hàng nhập liệu
            var inputBar = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Theme.Panel, Padding = new Padding(12) };
            inputBar.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, inputBar.Width, 0);
            };
            var inputWrap = Theme.WrapInput(_txtInput);
            _txtInput.PlaceholderText = "Nhập tin nhắn... (Enter để gửi)";
            // Dùng font hỗ trợ emoji cho ô nhập để emoji vừa gõ/chèn hiển thị đúng.
            _txtInput.Font = Theme.FontEmoji;
            inputWrap.Dock = DockStyle.Fill;

            Theme.StyleSecondaryButton(_btnFile);
            _btnFile.Dock = DockStyle.Right;
            _btnFile.Width = 80;
            _btnFile.Height = 40;

            Theme.StylePrimaryButton(_btnSend);
            _btnSend.Dock = DockStyle.Right;
            _btnSend.Width = 90;
            _btnSend.Height = 40;

            var rightButtons = new Panel { Dock = DockStyle.Right, Width = 180, Padding = new Padding(8, 0, 0, 0) };
            rightButtons.Controls.Add(_btnSend);
            var fileHolder = new Panel { Dock = DockStyle.Right, Width = 88, Padding = new Padding(0, 0, 4, 0) };
            fileHolder.Controls.Add(_btnFile);
            rightButtons.Controls.Add(fileHolder);

            // Nút mở bảng chọn emoji – đặt ngay bên trái ô nhập, kiểu nút phụ.
            Theme.StyleSecondaryButton(_btnEmoji);
            _btnEmoji.Font = Theme.FontEmojiLarge;
            _btnEmoji.Dock = DockStyle.Fill;
            var emojiHolder = new Panel { Dock = DockStyle.Left, Width = 52, Padding = new Padding(0, 0, 8, 0) };
            emojiHolder.Controls.Add(_btnEmoji);

            // Thứ tự Add quyết định cách dock: Fill thêm trước (chiếm phần giữa),
            // rồi Right (Gửi/File) và Left (emoji) bám hai mép.
            inputBar.Controls.Add(inputWrap);     // Fill
            inputBar.Controls.Add(rightButtons);  // Right
            inputBar.Controls.Add(emojiHolder);   // Left

            main.Controls.Add(_messages);     // Fill
            main.Controls.Add(inputBar);      // Bottom
            main.Controls.Add(progressRow);   // Bottom
            main.Controls.Add(_lblRoomHeader);// Top

            Controls.Add(main);
            Controls.Add(left);
        }

        private Panel _progressRow = null!;

        private static void StyleList(ListBox lb)
        {
            lb.BorderStyle = BorderStyle.None;
            lb.BackColor = Theme.Panel;
            lb.ForeColor = Theme.TextPrimary;
            lb.Font = Theme.Font;
            lb.IntegralHeight = false;
            lb.ItemHeight = 26;
        }

        // =================== GẮN SỰ KIỆN ===================

        private void WireEvents()
        {
            _conn.OnMessageReceived += HandleMessage;
            _conn.OnSystemMessage += HandleSystem;
            _conn.OnKeysUpdated += HandleKeys;
            _conn.OnFileReceived += HandleFileReceived;
            _conn.OnFileProgress += HandleFileProgress;
            _conn.OnDisconnected += HandleDisconnected;

            _btnJoin.Click += async (_, _) => await JoinAsync(_txtRoom.Text);
            _btnLeave.Click += async (_, _) => await LeaveAsync();
            _btnRefresh.Click += async (_, _) => await RefreshRoomsAsync();
            _btnSend.Click += async (_, _) => await SendAsync();
            _btnFile.Click += async (_, _) => await SendFileAsync();
            _btnEmoji.Click += (_, _) => ToggleEmojiPicker();

            _lstRooms.DoubleClick += async (_, _) =>
            {
                if (_lstRooms.SelectedItem is RoomItem r) await JoinAsync(r.Name);
            };

            _txtInput.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendAsync();
                }
            };

            FormClosed += (_, _) => _conn.Dispose();
        }

        // =================== HÀNH ĐỘNG ===================

        private async Task JoinAsync(string room)
        {
            room = room.Trim();
            if (room.Length == 0) return;
            _messages.Clear();
            _lstMembers.Items.Clear();
            await _conn.JoinRoomAsync(room);
            _txtRoom.Clear();
            UpdateRoomHeader();
            _messages.AddMessage(BubbleStyle.System, "", $"Bạn đã tham gia phòng \"{room}\".");
        }

        private async Task LeaveAsync()
        {
            if (_conn.CurrentRoom is null)
            {
                _messages.AddMessage(BubbleStyle.System, "", "Bạn chưa ở trong phòng nào.");
                return;
            }
            string room = _conn.CurrentRoom;
            await _conn.LeaveRoomAsync();
            _lstMembers.Items.Clear();
            UpdateRoomHeader();
            _messages.AddMessage(BubbleStyle.System, "", $"Bạn đã rời phòng \"{room}\".");
        }

        private async Task RefreshRoomsAsync()
        {
            _lstRooms.Items.Clear();
            await _conn.RequestRoomsAsync();
        }

        private async Task SendAsync()
        {
            string text = _txtInput.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_conn.CurrentRoom is null)
            {
                _messages.AddMessage(BubbleStyle.System, "", "Hãy tham gia một phòng trước khi gửi tin.");
                return;
            }
            _txtInput.Clear();
            try { await _conn.SendMessageAsync(text); }
            catch (Exception ex) { _messages.AddMessage(BubbleStyle.System, "", $"Lỗi gửi: {ex.Message}"); }
        }

        private async Task SendFileAsync()
        {
            if (_conn.CurrentRoom is null)
            {
                _messages.AddMessage(BubbleStyle.System, "", "Hãy tham gia một phòng trước khi gửi file.");
                return;
            }
            using var dlg = new OpenFileDialog { Title = "Chọn file để gửi" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try { await _conn.SendFileAsync(dlg.FileName); }
            catch (Exception ex) { _messages.AddMessage(BubbleStyle.System, "", $"Lỗi gửi file: {ex.Message}"); }
        }

        // =================== EMOJI (chỉ là phần hiển thị) ===================

        /// <summary>Mở/đóng bảng chọn emoji khi bấm nút 😀.</summary>
        private void ToggleEmojiPicker()
        {
            if (_emojiPicker is { Visible: true })
            {
                _emojiPicker.Close();
                return;
            }
            // Nếu popup vừa đóng (do chính cú bấm này làm nó mất tiêu điểm) thì
            // không mở lại ngay, để nút hoạt động như một công tắc bật/tắt.
            if ((DateTime.UtcNow - _emojiClosedAt).TotalMilliseconds < 250) return;
            OpenEmojiPicker();
        }

        /// <summary>Hiện bảng chọn emoji ngay phía trên nút emoji, cạnh ô nhập.</summary>
        private void OpenEmojiPicker()
        {
            var picker = new EmojiPickerForm();
            picker.EmojiSelected += InsertEmoji;
            picker.FormClosed += (_, _) =>
            {
                _emojiClosedAt = DateTime.UtcNow;
                _emojiPicker = null;
            };
            _emojiPicker = picker;

            // Mặc định đặt popup phía trên nút; nếu chạm mép trên màn hình thì lật xuống dưới,
            // đồng thời kẹp lại trong vùng làm việc để không tràn ra ngoài.
            Point anchor = _btnEmoji.PointToScreen(Point.Empty);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int x = Math.Min(anchor.X, area.Right - picker.Width - 8);
            if (x < area.Left + 8) x = area.Left + 8;
            int y = anchor.Y - picker.Height - 6;
            if (y < area.Top + 8) y = anchor.Y + _btnEmoji.Height + 6;
            picker.Location = new Point(x, y);

            picker.Show(this);
        }

        /// <summary>
        /// Chèn emoji vào vị trí con trỏ trong ô nhập (giữ nguyên text đang gõ dở),
        /// rồi focus lại ô nhập. Emoji chỉ là ký tự Unicode nên đi thẳng vào TextBox
        /// và sẽ được mã hóa/truyền như text bình thường khi bấm Gửi.
        /// </summary>
        private void InsertEmoji(string emoji)
        {
            _txtInput.SelectedText = emoji; // thay phần đang chọn / chèn tại con trỏ, đẩy con trỏ ra sau
            _txtInput.Focus();
        }

        private void UpdateRoomHeader()
        {
            _lblRoomHeader.Text = _conn.CurrentRoom is { } r
                ? $"#  {r}"
                : "Chưa tham gia phòng nào";
            _btnLeave.Enabled = _conn.CurrentRoom != null;
        }

        // =================== XỬ LÝ EVENT (marshal về UI thread) ===================

        private void Ui(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }

        private void HandleMessage(string sender, string text, bool isMine)
            => Ui(() => _messages.AddMessage(isMine ? BubbleStyle.Mine : BubbleStyle.Other, isMine ? "" : sender, text));

        private void HandleSystem(string line) => Ui(() =>
        {
            // Tách dòng danh sách phòng "tên (N người)" để đổ vào ListBox bên trái
            var m = RoomLine.Match(line);
            if (m.Success)
            {
                string name = m.Groups[1].Value.Trim();
                string display = $"{name}   ·   {m.Groups[2].Value} người";
                bool exists = _lstRooms.Items.Cast<RoomItem>().Any(r => r.Name == name);
                if (!exists) _lstRooms.Items.Add(new RoomItem(name, display));
                return; // không hiển thị dòng này như thông báo chat
            }

            // Ẩn dòng tiêu đề "Danh sách phòng:" để tránh nhiễu khung chat khi làm mới
            if (line.StartsWith("Danh sách phòng")) return;

            _messages.AddMessage(BubbleStyle.System, "", line);
        });

        private void HandleKeys(IReadOnlyList<string> members) => Ui(() =>
        {
            _lstMembers.Items.Clear();
            foreach (var name in members)
                _lstMembers.Items.Add(name == _conn.Account ? $"{name} (bạn)" : name);
        });

        private void HandleFileReceived(string fileName, string path) => Ui(() =>
            _messages.AddMessage(BubbleStyle.System, "", $"📥 Đã nhận file \"{fileName}\" – lưu tại: {path}"));

        private void HandleFileProgress(string info, int percent) => Ui(() =>
        {
            if (percent < 0)
            {
                _progressRow.Visible = false;
                _progress.Visible = false;
                _lblProgress.Visible = false;
                return;
            }
            _progressRow.Visible = true;
            _progressRow.Height = 30;
            _progress.Visible = true;
            _lblProgress.Visible = true;
            _progress.Value = Math.Clamp(percent, 0, 100);
            _lblProgress.Text = $"{info}  ({percent}%)";
        });

        private void HandleDisconnected() => Ui(() =>
        {
            _messages.AddMessage(BubbleStyle.System, "", "⚠ Mất kết nối tới server.");
            _btnSend.Enabled = _btnFile.Enabled = _btnJoin.Enabled = false;
        });

        /// <summary>Mục phòng trong danh sách (Name dùng để JOIN, Display để hiển thị).</summary>
        private sealed record RoomItem(string Name, string Display);
    }
}
