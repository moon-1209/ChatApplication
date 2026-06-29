using ChatApplicationClient.Core;
using ChatApplicationClient.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ChatApplicationClient
{
    internal class ChatClient
    {
        static readonly CryptoService _crypto = new();
        static string _account = "User";
        static readonly Dictionary<string, string> _roomKeys = new();
        static readonly object _keysLock = new();

        static StreamWriter _writer = null!;
        static readonly SemaphoreSlim _writeLock = new(1, 1);
        static FileTransferService _ft = null!;
        static async Task Main(string[] args) //bắt đầu chương trình
        {
            Console.OutputEncoding = Encoding.UTF8;//đọc tiếng việt
            Console.Write("Nhập IP Server: ");
            string host = Console.ReadLine() is { Length: > 0 } h ? h : "127.0.0.1";//Length: > 0 nghĩa là nếu người dùng nhập vào thì lấy giá trị đó, còn không thì mặc định là

            Console.Write("Nhập tên hiển thị: ");
            _account = Console.ReadLine() is { Length: > 0 } n ? n : "User";

            string certPath = Path.Combine(AppContext.BaseDirectory, "server.cer");

            if (!File.Exists(certPath))
            {
                Console.WriteLine("Thiếu 'server.cer'");
                return;
            }

            var pinned = X509CertificateLoader.LoadCertificateFromFile(certPath);

            using var client = new TcpClient();//Tạo socket
            await client.ConnectAsync(host, 9000);//kết nối đến sv port 9000 ip 127.0.0.1

            var ssl = new SslStream(client.GetStream(), false, (sender, cert, chain, errors) =>
                cert != null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(cert.GetRawCertData()), SHA256.HashData(pinned.GetRawCertData())));

            try
            {
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "ChatServer",
                    EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12
                });
            }
            catch (AuthenticationException)
            {
                Console.WriteLine("Chứng chỉ không khớp có thể bị giả mạo, từ chối kết nối");
                return;
            }

            var reader = new StreamReader(ssl);//Đọc dữ liệu từ luồng
            _writer = new StreamWriter(ssl) { AutoFlush = true };//Ghi dữ liệu vào luồng, AutoFlush = true nghĩa là tự động xóa bộ nhớ đệm sau khi ghi dữ liệu
            _ft = new FileTransferService(SendLineAsync, _crypto);

            if (!await AuthenticateAsync(reader, _writer)) return;
            Console.WriteLine("\nĐăng nhập thành công! Gõ /help để xem lệnh\n");
            await _writer.WriteLineAsync($"ACCOUNT|{_account}");
            PrintHelp();

            _ = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null) HandleIncoming(line); //Liên tục đọc từng dòng từ server và in ra màn hình
                }
                catch { } //Bỏ qua lỗi khi kết nối bị đóng đột ngột
                Console.WriteLine("Mất kết nối tới Server");
                Environment.Exit(0); //Thoát chương trình
            });

            string? input;
            while ((input = Console.ReadLine()) != null) //Đọc dữ liệu từ bàn phím và vòng lặp tn
            {
                if (input.StartsWith('/')) //Nếu người dùng nhập vào bắt đầu bằng dấu / thì là câu lệnh
                {
                    var parts = input.Split(' ', 2); //tách lệnh
                    string command = parts[0].ToLowerInvariant(); //Chuyển về chữ thường
                    string arg = parts.Length > 1 ? parts[1] : ""; //Lấy tham số từ lệnh

                    switch (command)
                    {
                        case "/join": //Gửi lệnh tham gia phòng kèm tên phòng lên server
                            lock (_keysLock) _roomKeys.Clear();
                            await _writer.WriteLineAsync($"JOIN|{arg}|{_crypto.ExportPublicKey()}");
                            break;
                        case "/leave": await _writer.WriteLineAsync("LEAVE"); break; //Gửi lệnh rời phòng lên server
                        case "/rooms": await _writer.WriteLineAsync("ROOMS"); break; //Gửi lệnh lấy danh sách phòng từ server
                        case "/sendfile": StartFileSend(arg); break;
                        case "/help": PrintHelp(); break; //Hiển thị danh sách lệnh hỗ trợ
                        case "/exit": return; //Thoát chương trình
                        default: Console.WriteLine("Câu lệnh không tồn tại. Gõ lệnh \"/help\" để xem câu lệnh"); break; //Nếu người dùng nhập vào lệnh không tồn tại thì in ra thông báo
                    }
                }
                else await SendEncryptedAsync(input); //Không / thì hiển thị tin nhắn bình thường
            }
        }

        static async Task SendLineAsync(string s)
        {
            await _writeLock.WaitAsync();
            try
            { 
                await _writer.WriteLineAsync(s);
            }
            catch { }
            finally
            {
                _writeLock.Release();
            }
        }

        static void StartFileSend(string arg)
        {
            var fa = arg.Split(' ', 2);
            if (fa.Length < 2)
            {
                Console.WriteLine("Sử dụng: /sendfile <tên người nhận> <đường dẫn file>");
                return; 
            }
            string rcpt = fa[0].Trim();
            string path = fa[1].Trim().Trim('"');

            string? pub;
            lock (_keysLock) _roomKeys.TryGetValue(rcpt, out pub);
            if (pub is null) { Console.WriteLine($"Chưa có khóa của '{rcpt}' (người nhận phải ở cùng phòng)."); return; }

            _ = _ft.SendFileAsync(rcpt, pub, path);
        }

        static async Task SendEncryptedAsync(string text)
        {
            Dictionary<string, string> recipients;
            lock (_keysLock) recipients = _roomKeys.Where(kv => kv.Key != _account).ToDictionary(kv => kv.Key, kv => kv.Value);

            byte[] aesKey = CryptoService.NewAesKey();
            var (cipher, iv) = CryptoService.AesEncrypt(Encoding.UTF8.GetBytes(text), aesKey);
            var slots = recipients.Select(r => $"{r.Key}:{CryptoService.WrapKey(aesKey, r.Value)}");

            await SendLineAsync($"MESSAGE|{iv}|{cipher}|{string.Join(",", slots)}");
        }

        static void HandleIncoming(string line)
        {
            int sep = line.IndexOf('|');
            string type = sep < 0 ? line : line[..sep];
            string rest = sep < 0 ? "" : line[(sep + 1)..];

            switch (type)
            {
                case "KEYS":
                    lock (_keysLock)
                    {
                        _roomKeys.Clear();
                        foreach (var entry in rest.Split('|', StringSplitOptions.RemoveEmptyEntries))
                        {
                            int eq = entry.IndexOf('=');
                            if (eq > 0) _roomKeys[entry[..eq]] = entry[(eq + 1)..];
                        }
                    }
                    break;
                case "RELAY":
                    {
                        var p = rest.Split('|');
                        if (p.Length < 4) return;
                        string sender = p[0], iv = p[1], cipher = p[2], envelope = p[3];

                        string? myWrapped = null;
                        foreach (var slot in envelope.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            int colon = slot.IndexOf(':');
                            if (colon > 0 && slot[..colon] == _account) { myWrapped = slot[(colon + 1)..]; break; }
                        }
                        if (myWrapped is null) return;
                        try
                        {
                            byte[] aesKey = _crypto.UnwrapKey(myWrapped);
                            string text = Encoding.UTF8.GetString(CryptoService.AesDecrypt(cipher, aesKey, iv));
                            Console.WriteLine($"{sender}: {text}");
                        }
                        catch { Console.WriteLine($"{sender}: không giải mã được"); }
                        break;
                    }
                    case "FILE":
                    case "FILECHUNK":
                    case "FILEEND":
                        _ft.HandleFilePacket(type, rest);
                        break;
                default:
                    Console.WriteLine(line);
                    break;
            }
        }
        static async Task<bool> AuthenticateAsync(StreamReader reader, StreamWriter writer)
        {
            while (true) //Lặp cho đến khi đăng nhập thành công hoặc người dùng thoát
            {
                Console.Write("\n1: Đăng nhập.\n2: Đăng ký.\nq: Thoát.\nLựa chọn: ");
                string choice = (Console.ReadLine() ?? "").Trim().ToLowerInvariant(); //Đọc lựa chọn, xóa khoảng trắng và chuyển về chữ thường
                if (choice == "q") return false; //Người dùng chọn thoát

                Console.Write("Tên đăng nhập: ");
                string user = Console.ReadLine() ?? ""; //Đọc tên đăng nhập, nếu null thì dùng chuỗi rỗng
                Console.Write("Mật khẩu: ");
                string pass = ReadPassword(); //Đọc mật khẩu ẩn ký tự

                string command = choice == "2" ? "REGISTER" : "LOGIN"; //Xác định lệnh gửi lên server: đăng ký hoặc đăng nhập
                await writer.WriteLineAsync($"{command}|{user}|{pass}"); //Gửi lệnh kèm thông tin lên server

                string? resp = await reader.ReadLineAsync(); //Chờ phản hồi từ server
                if (resp is null)
                {
                    Console.WriteLine("Mất kết nối."); //Server ngắt kết nối bất ngờ
                    return false;
                }

                var p = resp.Split('|', 2); //Tách phản hồi thành status và message
                string status = p[0]; //Lấy trạng thái: OK hoặc ERROR
                string msg = p.Length > 1 ? p[1] : ""; //Lấy nội dung thông báo từ server
                Console.WriteLine(msg); //In thông báo ra màn hình

                if (status == "OK" && command == "LOGIN") return true; //Đăng nhập thành công thì trả về true
            }
        }

        static string ReadPassword()
        {
            var sb = new StringBuilder(); //Dùng StringBuilder để ghép từng ký tự mật khẩu
            ConsoleKeyInfo key;
            while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter) //Đọc từng phím, intercept: true để không hiển thị ký tự ra màn hình
            {
                if (key.Key == ConsoleKey.Backspace && sb.Length > 0) //Nếu nhấn Backspace và còn ký tự
                {
                    sb.Length--; //Xóa ký tự cuối trong bộ nhớ
                    Console.Write("\b \b"); //Xóa dấu * vừa hiển thị trên màn hình
                }
                else if (!char.IsControl(key.KeyChar)) //Bỏ qua các phím điều khiển (Tab, Ctrl,...)
                {
                    sb.Append(key.KeyChar); //Thêm ký tự vào mật khẩu
                    Console.Write("*");//Hiển thị dấu * thay cho ký tự thật
                }    
            }
            Console.WriteLine(); //Xuống dòng sau khi nhấn Enter
            return sb.ToString(); //Trả về mật khẩu đã nhập
        }

        static void PrintHelp() => Console.WriteLine(//Help command ở lệnh trên
        """
        =================HELP=================
        /join <phòng>: tham gia vào nhóm chat
        /leave: rời khỏi phòng hiện tại
        /rooms: xem danh sách các phòng
        /sendfile <người nhận> <file>: gửi file cho người cùng phòng
        /exit: thoát ứng dụng
        /help: xem các câu lệnh
        ======================================
        """); //In danh sách lệnh hỗ trợ ra màn hình
    }
}