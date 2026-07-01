using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatApplicationServer.Network
{
    internal class ClientHandler : IDisposable
    {
        public string Account { get; set; }
        public string? Room { get; set; }
        public int Id { get; }

        public string? PublicKey { get; set; }
        public bool Authenticated { get; set; }
        public StreamReader Reader { get; }

        private readonly SslStream _ssl;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writerLock = new(1, 1);

        public ClientHandler(int id, SslStream ssl)
        {
            Account = $"User{id}";
            Id = id;
            _ssl = ssl;
            Reader = new StreamReader(ssl);
            _writer = new StreamWriter(ssl)
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
        
        public void Dispose() => _ssl.Dispose();
    }
}
