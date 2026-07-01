using ChatApplicationClient.Core;
using ChatApplicationClient.Security;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

    internal enum AuthResult
    {
        LoginSuccess,
        RegisterSuccess,
        Failed
    }
    internal sealed class ChatClient : IDisposable
    {
        private readonly CryptoService _crypto = new();
        private readonly Dictionary<string, string> _roomKeys = new();
        private readonly object _keysLock = new();

        private TcpClient? _client;
        private SslStream? _ssl;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private FileTransferService? _ft;

        private string _account = "User";

        public string Account => _account;

        public string? CurrentRoom { get; private set; }

        public event Action<string, string, bool>? OnMessageReceived;

        public event Action<string>? OnSystemMessage;

        public event Action<IReadOnlyList<string>>? OnKeysUpdated;

        public event Action<string, string>? OnFileReceived;

        public event Action<string, int>? OnFileProgress;

        public event Action? OnDisconnected;

        public async Task ConnectAsync(string host, string displayName)
        {
            _account = string.IsNullOrWhiteSpace(displayName) ? "User" : displayName.Trim();

            string certPath = Path.Combine(AppContext.BaseDirectory, "server.cer");
            if (!File.Exists(certPath)) throw new FileNotFoundException("Thiếu 'server.cer' cạnh file thực thi.");

            var pinned = X509CertificateLoader.LoadCertificateFromFile(certPath);

            _client = new TcpClient();
            await _client.ConnectAsync(host, 9000);

            _ssl = new SslStream(_client.GetStream(), false, (sender, cert, chain, errors) =>
                cert != null && CryptographicOperations.FixedTimeEquals(SHA256.HashData(cert.GetRawCertData()),SHA256.HashData(pinned.GetRawCertData())));

            try
            {
                await _ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "ChatServer",
                    EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12
                });
            }
            catch (AuthenticationException)
            {
                _ssl.Dispose();
                throw new AuthenticationException("Chứng chỉ không khớp, có thể bị giả mạo, từ chối kết nối.");
            }

            _reader = new StreamReader(_ssl);
            _writer = new StreamWriter(_ssl) { AutoFlush = true };

            _ft = new FileTransferService(SendLineAsync, _crypto);
            _ft.OnLog += msg => OnSystemMessage?.Invoke(msg);
            _ft.OnProgress += (info, pct) => OnFileProgress?.Invoke(info, pct);
            _ft.OnFileSaved += (name, path) => OnFileReceived?.Invoke(name, path);
        }

        public async Task<(AuthResult result, string message)> AuthenticateAsync(bool register, string user, string pass)
        {
            if (_writer is null || _reader is null) throw new InvalidOperationException("Chưa kết nối.");

            string command = register ? "REGISTER" : "LOGIN";
            await _writer.WriteLineAsync($"{command}|{user}|{pass}");

            string? resp = await _reader.ReadLineAsync();
            if (resp is null) throw new IOException("Mất kết nối khi xác thực.");

            var p = resp.Split('|', 2);
            string status = p[0];
            string msg = p.Length > 1 ? p[1] : "";

            AuthResult result;
            if (status == "OK")
                result = register ? AuthResult.RegisterSuccess : AuthResult.LoginSuccess;
            else
                result = AuthResult.Failed;

            return (result, msg);
        }

        public Task<(AuthResult result, string message)> LoginAsync(string user, string pass) => AuthenticateAsync(false, user, pass);

        public Task<(AuthResult result, string message)> RegisterAsync(string user, string pass) => AuthenticateAsync(true, user, pass);
        public async Task StartSessionAsync()
        {
            if (_writer is null || _reader is null) throw new InvalidOperationException("Chưa kết nối.");
            await _writer.WriteLineAsync($"ACCOUNT|{_account}");
            _ = Task.Run(ReceiveLoopAsync);
        }

        public async Task JoinRoomAsync(string room)
        {
            lock (_keysLock) _roomKeys.Clear();
            CurrentRoom = room;
            await SendLineAsync($"JOIN|{room}|{_crypto.ExportPublicKey()}");
        }

        public async Task LeaveRoomAsync()
        {
            CurrentRoom = null;
            lock (_keysLock) _roomKeys.Clear();
            await SendLineAsync("LEAVE");
        }

        public Task RequestRoomsAsync() => SendLineAsync("ROOMS");

        public async Task SendMessageAsync(string text)
        {
            Dictionary<string, string> recipients;
            lock (_keysLock)
                recipients = _roomKeys.Where(kv => kv.Key != _account)
                                      .ToDictionary(kv => kv.Key, kv => kv.Value);

            byte[] aesKey = CryptoService.NewAesKey();
            var (cipher, iv) = CryptoService.AesEncrypt(Encoding.UTF8.GetBytes(text), aesKey);
            var slots = recipients.Select(r => $"{r.Key}:{CryptoService.WrapKey(aesKey, r.Value)}");

            await SendLineAsync($"MESSAGE|{iv}|{cipher}|{string.Join(",", slots)}");

            OnMessageReceived?.Invoke(_account, text, true);
        }

        public async Task SendFileAsync(string path)
        {
            if (_ft is null) return;

            Dictionary<string, string> recipients;
            lock (_keysLock)
                recipients = _roomKeys.Where(kv => kv.Key != _account)
                                      .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (recipients.Count == 0)
            {
                OnSystemMessage?.Invoke("Không có ai khác trong phòng để gửi file.");
                return;
            }

            foreach (var r in recipients)
                await _ft.SendFileAsync(r.Key, r.Value, path);
        }

        private async Task ReceiveLoopAsync()
        {
        try
        {
            string? line;
            while (_reader != null && (line = await _reader.ReadLineAsync()) != null)
                HandleIncoming(line);
        }
        catch { }

            OnDisconnected?.Invoke();
        }

        private void HandleIncoming(string line)
        {
            int sep = line.IndexOf('|');
            string type = sep < 0 ? line : line[..sep];
            string rest = sep < 0 ? "" : line[(sep + 1)..];

            switch (type)
            {
                case "KEYS":
                    {
                        List<string> members;
                        lock (_keysLock)
                        {
                            _roomKeys.Clear();
                            foreach (var entry in rest.Split('|', StringSplitOptions.RemoveEmptyEntries))
                            {
                                int eq = entry.IndexOf('=');
                                if (eq > 0) _roomKeys[entry[..eq]] = entry[(eq + 1)..];
                            }
                            members = _roomKeys.Keys.ToList();
                        }
                        OnKeysUpdated?.Invoke(members);
                        break;
                    }
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
                            OnMessageReceived?.Invoke(sender, text, false);
                        }
                        catch { OnMessageReceived?.Invoke(sender, "không giải mã được", false); }
                        break;
                    }
                case "FILE":
                case "FILECHUNK":
                case "FILEEND":
                    _ft?.HandleFilePacket(type, rest);
                    break;
                case "OK":
                case "ERROR":
                    OnSystemMessage?.Invoke(rest.Length > 0 ? rest : line);
                    break;
                default:
                    OnSystemMessage?.Invoke(line);
                    break;
            }
        }

        private async Task SendLineAsync(string s)
        {
            if (_writer is null) return;
            await _writeLock.WaitAsync();
            try { await _writer.WriteLineAsync(s); }
            catch { }
            finally { _writeLock.Release(); }
        }

        public void Dispose()
        {
            try { _ssl?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }
            _crypto.Dispose();
        }
    }