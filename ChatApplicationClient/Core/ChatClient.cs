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
            {
                try
                {
                    string? line;
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
            }
        }
    }
}
