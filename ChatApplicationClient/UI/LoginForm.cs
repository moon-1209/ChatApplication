using ChatApplicationClient.Core;

namespace ChatApplicationClient.UI
{
    /// <summary>
    /// Màn hình đăng nhập/đăng ký: username + password (ẩn ký tự),
    /// hai nút Đăng nhập / Đăng ký, hiển thị thông báo OK/ERROR từ server.
    /// Trả về DialogResult.OK khi đăng nhập thành công.
    /// </summary>
    internal sealed partial class LoginForm : Form
    {
        private readonly ChatClient _conn;
        private readonly TextBox _txtUser = new();
        private readonly TextBox _txtPass = new() { UseSystemPasswordChar = true };
        private readonly Button _btnLogin = new() { Text = "Đăng nhập" };
        private readonly Button _btnRegister = new() { Text = "Đăng ký" };
        private readonly Label _lblMsg = new();

        public LoginForm(ChatClient conn)
        {
            _conn = conn;

            Text = "Chat - Đăng nhập";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            ClientSize = new Size(420, 380);
            BackColor = Theme.Background;
            Font = Theme.Font;

            var title = new Label
            {
                Text = "Đăng nhập tài khoản",
                Font = Theme.FontTitle,
                ForeColor = Theme.Accent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 64
            };

            var card = new Panel
            {
                BackColor = Theme.Panel,
                Location = new Point(30, 80),
                Size = new Size(360, 270)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var pen = new Pen(Theme.Border);
                using var path = Theme.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12);
                e.Graphics.DrawPath(pen, path);
            };

            var lblUser = MakeLabel("Tên đăng nhập");
            var lblPass = MakeLabel("Mật khẩu");
            var userBox = Theme.WrapInput(_txtUser);
            var passBox = Theme.WrapInput(_txtPass);

            Theme.StylePrimaryButton(_btnLogin);
            Theme.StyleSecondaryButton(_btnRegister);

            _lblMsg.AutoSize = false;
            _lblMsg.TextAlign = ContentAlignment.MiddleLeft;
            _lblMsg.ForeColor = Color.IndianRed;

            lblUser.Location = new Point(24, 18); lblUser.Size = new Size(312, 20);
            userBox.Location = new Point(24, 40); userBox.Width = 312;
            lblPass.Location = new Point(24, 90); lblPass.Size = new Size(312, 20);
            passBox.Location = new Point(24, 112); passBox.Width = 312;
            _lblMsg.Location = new Point(24, 158); _lblMsg.Size = new Size(312, 22);
            _btnLogin.Location = new Point(24, 186); _btnLogin.Width = 150;
            _btnRegister.Location = new Point(186, 186); _btnRegister.Width = 150;

            card.Controls.AddRange(new Control[] { lblUser, userBox, lblPass, passBox, _lblMsg, _btnLogin, _btnRegister });
            Controls.Add(card);
            Controls.Add(title);

            _btnLogin.Click += async (_, _) => await DoAuthAsync(register: false);
            _btnRegister.Click += async (_, _) => await DoAuthAsync(register: true);
            AcceptButton = _btnLogin;
        }

        private static Label MakeLabel(string text) => new()
        {
            Text = text,
            ForeColor = Theme.TextMuted,
            Font = Theme.FontSmall,
            AutoSize = false
        };

        private async Task DoAuthAsync(bool register)
        {
            string user = _txtUser.Text.Trim();
            string pass = _txtPass.Text;
            if (user.Length == 0 || pass.Length == 0)
            {
                _lblMsg.ForeColor = Color.IndianRed;
                _lblMsg.Text = "Vui lòng nhập đủ tên đăng nhập và mật khẩu.";
                return;
            }

            // Vô hiệu hóa cả hai nút khi đang chờ phản hồi để tránh bấm nhiều lần
            _btnLogin.Enabled = _btnRegister.Enabled = false;
            try
            {
                var (result, message) = await _conn.AuthenticateAsync(register, user, pass);
                switch (result)
                {
                    case AuthResult.LoginSuccess:
                        // Đăng nhập thành công: đóng dialog với OK.
                        // Phiên (gửi ACCOUNT + vòng lặp nhận) sẽ được ChatForm khởi động
                        // SAU khi đã đăng ký event để không bỏ lỡ gói tin đầu tiên.
                        DialogResult = DialogResult.OK;
                        Close();
                        break;

                    case AuthResult.RegisterSuccess:
                        // Đăng ký thành công: KHÔNG chuyển màn hình, ở lại để người dùng đăng nhập.
                        _lblMsg.ForeColor = Color.SeaGreen;
                        _lblMsg.Text = message;
                        break;

                    default: // AuthResult.Failed -> hiển thị đúng lý do lỗi server trả về
                        _lblMsg.ForeColor = Color.IndianRed;
                        _lblMsg.Text = message;
                        break;
                }
            }
            catch (Exception ex)
            {
                _lblMsg.ForeColor = Color.IndianRed;
                _lblMsg.Text = ex.Message;
            }
            finally
            {
                // Bật lại nút sau khi đã có kết quả (kể cả khi lỗi)
                _btnLogin.Enabled = _btnRegister.Enabled = true;
            }
        }
    }
}
