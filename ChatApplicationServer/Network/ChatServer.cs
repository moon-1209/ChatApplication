using System;//Khai báo thư viện cơ bản
using System.Collections.Concurrent;//Dùng ConcurrentDictionary đồng bộ hóa ngầm,nhiều luồng hoạt động không sợ xung đột
using System.Collections.Generic;
using System.IO;//ĐỌc ghi dl
using System.Linq;
using System.Net;
using System.Net.Sockets;//LTM để tạo server TCP IP ...
using System.Text;
using System.Threading;
using System.Threading.Tasks;//Hỗ trợ đa luồng 

namespace ChatApplicationServer
{
    internal class ChatServer
    {
        static readonly ConcurrentDictionary<int, ClientHandle> _clients = new();//Dùng ConcurrentDictionary để lưu trữ các client đang kết nối, đảm bảo thread-safe khi truy cập từ nhiều luồng
        static int _nextId = 0;
        static async Task Main()
        {
            Console.OutputEncoding = Encoding.UTF8; // Đọc tiếng Việt có dấu
            var listener = new TcpListener(IPAddress.Any, 9000);//Tạo server lắng nghe all IPAddress tại cổng 9000
            listener.Start();//Bắt đầu mở cho client kết nối
            Console.WriteLine("Server: Đang lắng nghe tại cổng 9000...");
            while (true)//vòng lặp vh
            {
                TcpClient tcp = await listener.AcceptTcpClientAsync();//CHờ và nhận kết nối
                int id = Interlocked.Increment(ref _nextId);//Tạo ID ( tăng dần )

                var handle = new ClientHandle(id, tcp);//Tạo đối tượng Cleint -> quản lý kết nối của client
                _clients[id] = handle;//Thêm client vào danh sách đang kết nối
                Console.WriteLine($"Server: Client #{id} đã kết nối ({_clients.Count} người online");

                _ = HandleClientAsync(handle);//Phân Luồng xử lý cleint( nhiều người kết nối cùng lúc )
            }
        }
        static async Task HandleClientAsync(ClientHandle c) //Xử lý giao tiếp với client
        {
            await BroadcastAsync($"Client #{c.Id} đã tham gia vào phòng chat.", exceptId: c.Id);

            try
            {
                string? line;

                while ((line = await c.Reader.ReadLineAsync()) != null)//Đợi và đọc từng dòng tin nhắn từ client, nếu client ngắt kết nối sẽ trả về null
                {
                    Console.WriteLine($"Client #{c.Id} {line}");//In ra Sv
                    await BroadcastAsync($"Client: {c.Id}: {line}", exceptId: c.Id);//Sv phát cho các cleint khác(trừ client gửi)
                }
            }
            catch (IOException) { }
            finally
            {
                _clients.TryRemove(c.Id, out _);//Xóa client khỏi danh sách khi ngắt kết nối
                c.Dispose();//Giải phóng tài nguyên
                Console.WriteLine($"Client #{c.Id} đã rời ({_clients.Count} người online.");//Thông báo
                await BroadcastAsync($"Client #{c.Id} đã rời khỏi phòng.", exceptId: c.Id);//Báo cho người khác
            }
        }
        static async Task BroadcastAsync(string msg, int exceptId)//Hàm phát tin nhắn đến tất cả client ngoại trừ client có ID exceptId
        {
            foreach (var c in _clients.Values)//Duyệt all client đang onl
            {
                if (c.Id == exceptId) continue;//nếu là client gửi thì bỏ qua
                await c.SendAsync(msg);//Gửi tin đến client đó
            }
        }
    }
    class ClientHandle : IDisposable//Lớp quản lý kết nối của client, bao gồm ID, luồng đọc và ghi, và phương thức gửi tin nhắn
    {
        public int Id { get; }
        public StreamReader Reader { get; }//Dùng để đọc tin nhắn từ client

        private readonly TcpClient _tcp;//Lưu kết nối mạng
        private readonly StreamWriter _writer;//Dùng để gửi tin nhắn đến client
        private readonly SemaphoreSlim _writerLock = new(1, 1);//Khóa dữ liệu(đồng bộ hóa)

        public ClientHandle(int id, TcpClient tcp)//Tạo đối tượng quản lý client
        {
            Id = id;
            _tcp = tcp;
            var stream = tcp.GetStream();
            Reader = new StreamReader(stream);
            _writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };
        }

        public async Task SendAsync(string msg)//Gửi tin nhắn tới client
        {
            await _writerLock.WaitAsync();//Đảm bảo 1 lần chỉ có 1 luồng được phép ghi dữ liệu
            try
            {
                await _writer.WriteLineAsync(msg);
            }
            catch { }
            finally
            {
                _writerLock.Release();//Giải phóng khóa sau khi gửi xong, cho phép luồng khác có thể gửi tin nhắn tiếp theo
            }
        }
        public void Dispose() => _tcp.Dispose();//Đóng kết nối mạng khi kh sử dụng, giải phóng tn
    }
}
