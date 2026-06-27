using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatApplicationClient
{
    internal class ChatClient
    {
<<<<<<< HEAD
        static async Task Main(string[] args) //bắt đầu chương trình
        {
            Console.OutputEncoding = Encoding.UTF8; //đọc tiếng việt
            Console.Write("Nhập IP Server: ");
            string host = Console.ReadLine() is { Length: > 0 } h ? h : "127.0.0.1"; //Length: > 0 nghĩa là nếu người dùng nhập vào thì lấy giá trị đó, còn không thì mặc định là

            using var client = new TcpClient(); //Tạo socket
            await client.ConnectAsync(host, 9000); //kết nối đến sv port 9000 ip 127.0.0.1
            // Console.WriteLine("Đã kết nối. Gõ tin nhắn rồi Enter để gửi. Nhập /exit để thoát.");

            var stream = client.GetStream(); //Lấy luồng dữ liệu từ socket để đọc và ghi dữ liệu
            var reader = new StreamReader(stream); //Đọc dữ liệu từ luồng
            var writer = new StreamWriter(stream) { AutoFlush = true }; //Ghi dữ liệu vào luồng, AutoFlush = true nghĩa là tự động xóa bộ nhớ đệm sau khi ghi dữ liệu
            if (!await AuthenticateAsync(reader, writer)) return; //Nếu đăng nhập thất bại thì thoát chương trình
            Console.WriteLine("\nĐăng nhập thành công! Gõ /help để xem lệnh\n");
            PrintHelp(); //In lệnh hỗ trợ
            _ = Task.Run(async () => //Tạo một luồng mới để đọc dữ liệu từ server, nếu mất kết nối thì thoát chương trình
=======
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Write("Nhập IP Server: ");
            string host = Console.ReadLine() is { Length: > 0 } h ? h : "127.0.0.1";

            using var client = new TcpClient();
            await client.ConnectAsync(host, 9000);
            Console.WriteLine("Đã kết nối. Gõ tin nhắn rồi Enter để gửi. Nhập /exit để thoát.");

            var stream = client.GetStream();
            var reader = new StreamReader(stream);
            var writer = new StreamWriter(stream) { AutoFlush = true };

            _ = Task.Run(async () =>
>>>>>>> ad68fbfa3ee913988180e196468496936293216a
            {
                try
                {
                    string? line;
<<<<<<< HEAD
                    while ((line = await reader.ReadLineAsync()) != null) Console.WriteLine(line); //Liên tục đọc từng dòng từ server và in ra màn hình
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
                        case "/join": await writer.WriteLineAsync($"JOIN| {arg}"); break; //Gửi lệnh tham gia phòng kèm tên phòng lên server
                        case "/leave": await writer.WriteLineAsync("LEAVE"); break; //Gửi lệnh rời phòng lên server
                        case "/rooms": await writer.WriteLineAsync("ROOMS"); break; //Gửi lệnh lấy danh sách phòng từ server
                        case "/help": PrintHelp(); break; //Hiển thị danh sách lệnh hỗ trợ
                        case "/exit": return; //Thoát chương trình
                        default: Console.WriteLine("Câu lệnh không tồn tại. Gõ lệnh \"/help\" để xem câu lệnh"); break; //Nếu người dùng nhập vào lệnh không tồn tại thì in ra thông báo
                    }
                }
                else await writer.WriteLineAsync($"MESSAGE|{input}"); //Không / thì hiển thị tin nhắn bình thường
=======
                    while ((line = await reader.ReadLineAsync()) != null) Console.WriteLine(line);
                }
                catch { }
                Console.WriteLine("Mất kết nối tới Server");
                Environment.Exit(0);
            });
            string? input;
            while((input = Console.ReadLine()) != null)
            {
                if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase)) break;
                await writer.WriteLineAsync(input);
>>>>>>> ad68fbfa3ee913988180e196468496936293216a
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
                Console.WriteLine(status == "OK" ? $"{msg}" : $"{msg}"); //In thông báo ra màn hình

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
                    NewMethod(sb, key);
            }
            Console.WriteLine(); //Xuống dòng sau khi nhấn Enter
            return sb.ToString(); //Trả về mật khẩu đã nhập

            static void NewMethod(StringBuilder sb, ConsoleKeyInfo key)
            {
                sb.Append(key.KeyChar); //Thêm ký tự vào mật khẩu
                Console.Write("*"); //Hiển thị dấu * thay cho ký tự thật
            }
        }

        static void PrintHelp() => Console.WriteLine(
        """
        =================HELP=================
        /join <phòng>: tham gia vào nhóm chat
        /leave: rời khỏi phòng hiện tại
        /rooms: xem danh sách các phòng
        /help: xem các câu lệnh
        ======================================
        """); //In danh sách lệnh hỗ trợ ra màn hình
    }
}