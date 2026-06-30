using ChatApplicationClient.Core;

namespace ChatApplicationClient.UI
{
    /// <summary>
    /// Màn hình kết nối: nhập IP server + tên hiển thị, nút Kết nối.
    /// Khi TLS handshake thành công sẽ mở màn hình đăng nhập/đăng ký.
    /// </summary>
    internal sealed partial class ConnectForm : Form
    {
        private readonly TextBox _txtHost = new() { Text = "127.0.0.1" };
        private readonly TextBox _txtName = new() { Text = "" };
        private readonly Button _btnConnect = new() { Text = "Kết nối" };
        private readonly Label _lblStatus = new();

        public ConnectForm()
        {
            Text = "Chat - Kết nối";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(420, 360);
            BackColor = Theme.Background;
            Font = Theme.Font;

            var title = new Label
            {
                Text = "💬 Ứng dụng Chat",
                Font = Theme.FontTitle,
                ForeColor = Theme.Accent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 70
            };

            var card = new Panel
            {
                BackColor = Theme.Panel,
                Location = new Point(30, 90),
                Size = new Size(360, 230),
                Padding = new Padding(24)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Pen(Theme.Border);
                using var path = Theme.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12);
                e.Graphics.DrawPath(pen, path);
            };

            var lblHost = MakeLabel("Địa chỉ IP server");
            var lblNm = MakeLabel("Tên hiển thị");
            var hostBox = Theme.WrapInput(_txtHost);
            var nameBox = Theme.WrapInput(_txtName);
            Theme.StylePrimaryButton(_btnConnect);
            _btnConnect.Dock = DockStyle.Bottom;

            _lblStatus.ForeColor = Color.IndianRed;
            _lblStatus.AutoSize = false;
            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 24;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            // Xếp các control trong card (đặt thủ công cho gọn gàng)
            lblHost.Location = new Point(24, 18); lblHost.Size = new Size(312, 20);
            hostBox.Location = new Point(24, 40); hostBox.Width = 312;
            lblNm.Location = new Point(24, 90); lblNm.Size = new Size(312, 20);
            nameBox.Location = new Point(24, 112); nameBox.Width = 312;
            _lblStatus.Location = new Point(24, 158); _lblStatus.Width = 312; _lblStatus.Dock = DockStyle.None;
            _btnConnect.Location = new Point(24, 182); _btnConnect.Width = 312; _btnConnect.Dock = DockStyle.None;

            card.Controls.AddRange(new Control[] { lblHost, hostBox, lblNm, nameBox, _lblStatus, _btnConnect });
            Controls.Add(card);
            Controls.Add(title);

            _btnConnect.Click += async (_, _) => await ConnectAsync();
            AcceptButton = _btnConnect;
        }

        private static Label MakeLabel(string text) => new()
        {
            Text = text,
            ForeColor = Theme.TextMuted,
            Font = Theme.FontSmall,
            AutoSize = false
        };

        private async Task ConnectAsync()
        {
            string host = string.IsNullOrWhiteSpace(_txtHost.Text) ? "127.0.0.1" : _txtHost.Text.Trim();
            string name = string.IsNullOrWhiteSpace(_txtName.Text) ? "User" : _txtName.Text.Trim();

            _btnConnect.Enabled = false;
            _lblStatus.ForeColor = Theme.TextMuted;
            _lblStatus.Text = "Đang kết nối...";

            var conn = new ChatClient();
            try
            {
                await conn.ConnectAsync(host, name);
            }
            catch (Exception ex)
            {
                conn.Dispose();
                _lblStatus.ForeColor = Color.IndianRed;
                _lblStatus.Text = ex.Message;
                _btnConnect.Enabled = true;
                return;
            }

            // Sang màn hình đăng nhập (giữ kết nối đã mở)
            Hide();
            using (var login = new LoginForm(conn))
            {
                var result = login.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    // Đăng nhập thành công -> mở cửa sổ chat chính
                    var chat = new ChatForm(conn);
                    chat.FormClosed += (_, _) => Close();
                    chat.Show();
                    return;
                }
            }

            // Người dùng huỷ/đăng nhập thất bại -> đóng kết nối và quay lại
            conn.Dispose();
            Show();
            _btnConnect.Enabled = true;
            _lblStatus.Text = "";
        }
    }
}
