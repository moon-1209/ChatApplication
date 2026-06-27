using ChatApplicationServer.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatApplicationServer
{
    internal class Program
    {
        static readonly ConcurrentDictionary<int, ClientHandle> _clients = new();
        static int _nextId = 0;
        static Database _db = null!;
        static async Task Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            string cs = Environment.GetEnvironmentVariable("CHAT_DB") ?? @"Server=.;Database=ChatApplicationDB;Trusted_Connection=True;TrustServerCertificate=True;";
            _db = new Database(cs);

            var listener = new TcpListener(IPAddress.Any, 9000);
            listener.Start();
            Console.WriteLine("Server: Đang lắng nghe tại cổng 9000...");
            while (true)
            {
                TcpClient tcp = await listener.AcceptTcpClientAsync();
                int id = Interlocked.Increment(ref _nextId);

                var c = new ClientHandle(id, tcp);
                _clients[id] = c;
                Console.WriteLine($"Server: Client #{id} đã kết nối ({_clients.Count}) người online");

                _ = HandleClientAsync(c);
            }
        }
        static async Task HandleClientAsync(ClientHandle c)
        {
            try
            {
                string? line;

                while ((line = await c.Reader.ReadLineAsync()) != null) await ProcessCommandAsync(c, line);
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

        static async Task ProcessCommandAsync(ClientHandle c, string line)
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
                await c.SendAsync("ERROR| Bạn cần phải đăng nhập trước");
                return;
            }

            switch (command)
            {
                case "ACCOUNT":
                    c.Account = string.IsNullOrWhiteSpace(rest) ? c.Account : rest.Trim();
                    await c.SendAsync($"Tên của bạn: {c.Account}");
                    break;
                case "JOIN":
                    await JoinRoomAsync(c, rest.Trim());
                    break;
                case "LEAVE":
                    await LeaveRoomAsync(c);
                    break;
                case "ROOMS":
                    await SendRoomListAsync(c);
                    break;
                case "MESSAGE":
                    if (c.Room is null) await c.SendAsync("Bạn chưa tham gia phòng chat nào. Sử dụng /join <tên phòng>.");
                    else await BroadcastToRoomAsync(c.Room, $"{c.Room} {c.Account}: {rest}", exceptId: c.Id);
                    break;
                default:
                    await c.SendAsync("Lệnh không hợp lệ");
                    break;
            }
        }

        static async Task HandleAuthAsync(ClientHandle c, string command, string rest)
        {
            var a = rest.Split('|', 2);
            string user = a.Length > 0 ? a[0] : "";
            string pass = a.Length > 1 ? a[1] : "";

            if (user.Length == 0 || pass.Length == 0)
            {
                await c.SendAsync("ERROR| Thiếu tên đăng nhập hoặc mật khẩu.");
                return;
            }

            if (command == "REGISTER")
            {
                if (_db.Register(user, pass, out string err))
                    await c.SendAsync("OK| Đăng ký thành công! Hãy đăng nhập.");
                else
                    await c.SendAsync($"ERROR| {err}");
            }
            else
            {
                if (_db.Login(user, pass))
                {
                    c.Authenticated = true;
                    c.Account = user;
                    await c.SendAsync($"OK| Xin chào {user}, bạn đã đăng nhập thành công");
                    Console.WriteLine($"SERVER| {user} đăng nhập (client #{c.Id}).");
                }
                else await c.SendAsync("ERROR| Sai tên đăng nhập hoặc mật khẩu.");
            }
        }

        static async Task JoinRoomAsync(ClientHandle c, string room)
        {
            if (string.IsNullOrWhiteSpace(room))
            {
                await c.SendAsync("Tên phòng trống");
                return;
            }
            if (c.Room != null) await LeaveRoomAsync(c);
            c.Room = room;
            await c.SendAsync($"Đã vào phòng {room}");

            var history = _db.GetRecentMessages(room);
            if (history.Count > 0)
            {
                await c.SendAsync("==== Lịch sử gần đây ====");
                foreach (var h in history) await c.SendAsync(h);
                await c.SendAsync("=========================");
            }

            await BroadcastToRoomAsync(room, $"{c.Account} đã tham gia phòng chat", exceptId: c.Id);
        }

        static async Task LeaveRoomAsync(ClientHandle c)
        {
            if (c.Room is null) return;
            string oldRoom = c.Room;
            c.Room = null;
            await BroadcastToRoomAsync(oldRoom, $"{c.Account} đã rời khỏi phòng chat", exceptId: c.Id);
            await c.SendAsync($"Đã rời khỏi phòng {oldRoom}");
        }

        static async Task SendRoomListAsync(ClientHandle c)
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
