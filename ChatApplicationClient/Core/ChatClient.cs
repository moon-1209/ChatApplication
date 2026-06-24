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
        static async Task Main(string[] args)//bắt đầu chương trình
        {
            Console.OutputEncoding = Encoding.UTF8;//đọc tiếng việt
            Console.Write("Nhập IP Server: ");
            string host = Console.ReadLine() is { Length: > 0 } h ? h : "127.0.0.1";//Length: > 0 nghĩa là nếu người dùng nhập vào thì lấy giá trị đó, còn không thì mặc định là

            using var client = new TcpClient();//Tạo socket
            await client.ConnectAsync(host, 9000);//kết nối đến sv port 9000 ip 127.0.0.1
            Console.WriteLine("Đã kết nối. Gõ tin nhắn rồi Enter để gửi. Nhập /exit để thoát.");

            var stream = client.GetStream();//Lấy luồng dữ liệu từ socket để đọc và ghi dữ liệu
            var reader = new StreamReader(stream);//Đọc dữ liệu từ luồng
            var writer = new StreamWriter(stream) { AutoFlush = true };//Ghi dữ liệu vào luồng, AutoFlush = true nghĩa là tự động xóa bộ nhớ đệm sau khi ghi dữ liệu

            _ = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null) Console.WriteLine(line);
                }
                catch { }
                Console.WriteLine("Mất kết nối tới Server");
                Environment.Exit(0);//Thoát chương trình
            });//Tạo một luồng mới để đọc dữ liệu từ server, nếu mất kết nối thì thoát chương trình
            string? input;
            while ((input = Console.ReadLine()) != null)//Đọc dữ liệu từ bàn phím và vòng lặp tn
            {
                if (input.StartsWith('/'))//Nếu người dùng nhập vào bắt đầu bằng dấu / thì là câu lệnh
                {
                    var parts = input.Split(' ', 2);//tách lệnh
                    string command = parts[0].ToLowerInvariant();//Chuyển về chữ thường
                    string arg = parts.Length > 1 ? parts[1] : "";//Lấy tham số từ lệnh

                    switch (command)
                    {
                        case "/join": await writer.WriteLineAsync($"JOIN| {arg}"); break;
                        case "/leave": await writer.WriteLineAsync("LEAVE"); break;
                        case "/rooms": await writer.WriteLineAsync("ROOMS"); break;
                        case "/help": PrintHelp(); break;
                        case "/exit": return;
                        default: Console.WriteLine("Câu lệnh không tồn tại. Gõ lệnh \"/help\" để xem câu lệnh"); break;//Nếu người dùng nhập vào lệnh không tồn tại thì in ra thông báo
                    }
                }
                else await writer.WriteLineAsync($"MESSAGE|{input}");//Không / thì hiển thị tin nhắn bình thường
            }
        }

        static void PrintHelp() => Console.WriteLine(//Help command ở lệnh trên
        """
        =================HELP=================
        /join <phòng>: tham gia vào nhóm chat
        /leave: rời khỏi phòng hiện tại
        /rooms: xem danh sách các phòng
        /help: xem các câu lệnh
        ======================================
        """);
    }
}
