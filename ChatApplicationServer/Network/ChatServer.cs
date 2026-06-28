using ChatApplicationServer.Data;
using ChatApplicationServer.Security;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ChatApplicationServer.Network
{
    internal class Program
    {
        static readonly ConcurrentDictionary<int, ClientHandler> _clients = new();
        static int _nextId = 0;
        static Database _db = null!;
        const string CertPass = "ChatCert123";
        static async Task Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            string cs = Environment.GetEnvironmentVariable("CHAT_DB") ?? @"Server=.;Database=ChatApplicationDB;Trusted_Connection=True;TrustServerCertificate=True;";
            _db = new Database(cs);

            var cert = CertificateHelper.Load("server.pfx", CertPass);

            var listener = new TcpListener(IPAddress.Any, 9000);
            listener.Start();
            Console.WriteLine("Server: Đang lắng nghe tại cổng 9000...");

            while (true)
            {
                var tcp = await listener.AcceptTcpClientAsync();
                _ = AcceptAsync(tcp, cert);
            }
        }

        static async Task AcceptAsync(TcpClient tcp, X509Certificate2 cert)
        {
            ClientHandler? c = null;
            try
            {
                var ssl = new SslStream(tcp.GetStream(), false);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = cert,
                    EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
                    ClientCertificateRequired = false
                });
                int id = Interlocked.Increment(ref _nextId);
                c = new ClientHandler(id, ssl);
                _clients[id] = c;
                Console.WriteLine($"Server: Client #{id} kết nối ({ssl.SslProtocol}).");
                await HandleClientAsync(c);
            }
            catch (AuthenticationException ex) { Console.WriteLine($"Server: TLS lỗi: {ex.Message}"); }
            catch (Exception ex) { Console.WriteLine($"Server: Lỗi: {ex.Message}"); }
            finally
            {
                if (c != null)
                {
                    _clients.TryRemove(c.Id, out _);
                    string? room = c.Room;
                    c.Dispose();
                    if (room != null) { await BroadcastToRoomAsync(room, $"{c.Account} đã rời phòng.", c.Id); await PushKeysAsync(room); }
                }
                tcp.Dispose();
            }
        }

        static async Task PushKeysAsync(string room)
        {
            var members = _clients.Values.Where(x => x.Room == room && x.PublicKey != null).ToList();
            string dir = string.Join("|", members.Select(m => $"{m.Account}={m.PublicKey}"));
            foreach (var m in members)
                await m.SendAsync($"KEYS|{dir}");
        }
        static async Task HandleClientAsync(ClientHandler c)
        {
            try
            {
                string? line;
                while ((line = await c.Reader.ReadLineAsync()) != null)
                {
                    try
                    {
                        await ProcessCommandAsync(c, line);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Server: Lỗi xử lý #{c.Id}: {ex}");
                        try { 
                            await c.SendAsync("ERROR|Server gặp lỗi."); 
                        }
                        catch { }
                    }
                }
            }
            catch (IOException) { }
            finally
            {
                _clients.TryRemove(c.Id, out _);
                if (c.Room != null) await BroadcastToRoomAsync(c.Room, $"{c.Account} đã rời khỏi phòng.", exceptId: c.Id);
                c.Dispose();
                Console.WriteLine($"Client #{c.Id} đã rời ({_clients.Count} người online).");
            }
        }
        static async Task ProcessCommandAsync(ClientHandler c, string line)
        {
            int sep = line.IndexOf('|');
            string command = (sep < 0 ? line : line[..sep]).ToUpperInvariant();
            string rest = sep < 0 ? "" : line[(sep + 1)..];

            if (command is "REGISTER" or "LOGIN")
            {
                await HandleAuthAsync(c, command, rest);
                return;
            }

            if (!c.Authenticated)
            {
                await c.SendAsync("ERROR|Bạn cần phải đăng nhập trước");
                return;
            }

            switch (command)
            {
                case "ACCOUNT":
                    c.Account = string.IsNullOrWhiteSpace(rest) ? c.Account : rest.Trim();
                    await c.SendAsync($"Tên của bạn: {c.Account}");
                    break;
                case "JOIN":
                    var a = rest.Split('|', 2);
                    string room = a[0].Trim();
                    if (a.Length > 1) c.PublicKey = a[1];
                    await JoinRoomAsync(c, room);
                    break;
                case "LEAVE":
                    await LeaveRoomAsync(c);
                    break;
                case "ROOMS":
                    await SendRoomListAsync(c);
                    break;
                case "MESSAGE":
                    if (c.Room is null) await c.SendAsync("Bạn chưa tham gia phòng chat nào. Sử dụng /join <tên phòng>.");
                    else await BroadcastToRoomAsync(c.Room, $"RELAY|{c.Account}|{rest}", exceptId: c.Id);
                    break;
                default:
                    await c.SendAsync("Lệnh không hợp lệ");
                    break;
            }
        }

        static async Task HandleAuthAsync(ClientHandler c, string command, string rest)
        {
            var a = rest.Split('|', 2);
            string user = a.Length > 0 ? a[0] : "";
            string pass = a.Length > 1 ? a[1] : "";

            if (user.Length == 0 || pass.Length == 0)
            {
                await c.SendAsync("ERROR|Thiếu tên đăng nhập hoặc mật khẩu.");
                return;
            }

            if (command == "REGISTER")
            {
                if (_db.Register(user, pass, out string err)) await c.SendAsync("OK|Đăng ký thành công! Hãy đăng nhập.");
                else await c.SendAsync($"ERROR|{err}");
            }
            else
            {
                if (_db.Login(user, pass))
                {
                    c.Authenticated = true;
                    c.Account = user;
                    await c.SendAsync($"OK|Xin chào {user}, bạn đã đăng nhập thành công");
                    Console.WriteLine($"SERVER: {user} đăng nhập (client #{c.Id}).");
                }
                else await c.SendAsync("ERROR|Sai tên đăng nhập hoặc mật khẩu.");
            }
        }

        static async Task JoinRoomAsync(ClientHandler c, string room)
        {
            if (string.IsNullOrWhiteSpace(room))
            {
                await c.SendAsync("Tên phòng trống");
                return;
            }
            if (c.Room != null) await LeaveRoomAsync(c);
            c.Room = room;

            var history = _db.GetRecentMessages(room);
            if (history.Count > 0)
            {
                await c.SendAsync("==== Lịch sử gần đây ====");
                foreach (var h in history) await c.SendAsync(h);
                await c.SendAsync("=========================");
            }

            await BroadcastToRoomAsync(room, $"{c.Account} đã tham gia phòng chat", exceptId: c.Id);
            await PushKeysAsync(room);
        }

        static async Task LeaveRoomAsync(ClientHandler c)
        {
            if (c.Room is null) return;
            string oldRoom = c.Room;
            c.Room = null;
            await BroadcastToRoomAsync(oldRoom, $"{c.Account} đã rời khỏi phòng chat", exceptId: c.Id);
            await c.SendAsync($"Đã rời khỏi phòng {oldRoom}");
            await PushKeysAsync(oldRoom);
        }

        static async Task SendRoomListAsync(ClientHandler c)
        {
            var groups = _clients.Values.Where(x => x.Room != null).GroupBy(x => x.Room)
                                        .Select(g => $"{g.Key} ({g.Count()} người)").ToList();

            string body = groups.Count > 0 ? string.Join("\n", groups) : " (chưa có phòng nào)";
            await c.SendAsync($"Danh sách phòng: \n {body}");
        }

        static async Task BroadcastToRoomAsync(string room, string msg, int exceptId)
        {
            foreach (var c in _clients.Values)
            {
                if (c.Room == room && c.Id != exceptId) await c.SendAsync(msg);
            }
        }
    }
}