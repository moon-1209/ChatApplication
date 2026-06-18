using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatApplicationServer.Network
{
    internal class ClientHandle : IDisposable
    {
        public string Account { get; set; }
        public string? Room { get; set; }
        public int Id { get; }
        public StreamReader Reader { get; }

        private readonly TcpClient _tcp;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writerLock = new(1, 1);

        public ClientHandle(int id, TcpClient tcp)
        {
            Account = $"User{id}";
            Id = id;
            _tcp = tcp;
            var stream = tcp.GetStream();
            Reader = new StreamReader(stream);
            _writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };
        }

        public async Task SendAsync(string msg) {
            await _writerLock.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(msg);
            }
            catch { }
            finally
            {
                _writerLock.Release();
            }
        }
        
        public void Dispose() => _tcp.Dispose();
    }
}
