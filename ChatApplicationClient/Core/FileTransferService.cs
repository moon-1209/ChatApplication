using ChatApplicationClient.Security;
using System.Collections.Concurrent;

namespace ChatApplicationClient.Core
{
    internal class FileTransferService
    {
        const int ChunkSize = 48 * 1024;
        private readonly Func<string, Task> _send;
        private readonly CryptoService _crypto;
        private readonly ConcurrentDictionary<string, Incoming> _incoming = new();

        public FileTransferService(Func<string, Task> send, CryptoService crypto)
        {
            _send = send;
            _crypto = crypto;
        }

        sealed class Incoming
        {
            public string Sender = "", FileName = "", WrappedKeyB64 = "", IvB64 = "";
            public int TotalChunks;
            public readonly ConcurrentDictionary<int, byte[]> Chunks = new();
        }

        public async Task SendFileAsync(string recipient, string recipientPublicKeyB64, string path)
        {
            if(!File.Exists(path))
            {
                Console.WriteLine($"Không tìm thấy File: {path}");
                return;
            }
            string fileName = Path.GetFileName(path);
            byte[] data = await File.ReadAllBytesAsync(path);
            byte[] aesKey = CryptoService.NewAesKey();
            var (cipher, iv) = CryptoService.AesEncryptBytes(data, aesKey);
            string wrappedKey = CryptoService.WrapKey(aesKey, recipientPublicKeyB64);
            string ivB64 = Convert.ToBase64String(iv);

            string transferId = Guid.NewGuid().ToString();
            int total = (int)Math.Ceiling(cipher.Length / (double)ChunkSize);
            Console.WriteLine($"Gửi '{fileName} ({data.Length:NO} byte, {total} mảnh) đến {recipient}");

            await _send($"FILE|{recipient}|{transferId}|{fileName}|{data.Length}|{total}|{wrappedKey}|{ivB64}");

            for (int i = 0; i < total; ++i)
            {
                int offset = i * ChunkSize;
                int len = Math.Min(ChunkSize, cipher.Length - offset);
                string chunkB64 = Convert.ToBase64String(cipher, offset, len);
                await _send($"FILECHUNK|{recipient}|{transferId}|{i}|{chunkB64}");

                if (total >= 5 && (i + 1) % (total / 5) == 0) Console.WriteLine($"{(i + 1) * 100 / total}%");
            }

            await _send($"FILEEND|{recipient}|{transferId}");
            Console.WriteLine($"Đã gửi thành công '{fileName}'");
        }

        public void HandleFilePacket(string type, string rest)
        {
            switch (type)
            {
                case "FILE":
                    {
                        var p = rest.Split('|');
                        if (p.Length < 7) return;
                        _incoming[p[1]] = new Incoming
                        {
                            Sender = p[0],
                            FileName = p[2],
                            TotalChunks = int.Parse(p[4]),
                            WrappedKeyB64 = p[5],
                            IvB64 = p[6]
                        };
                        Console.WriteLine($"⬇ Đang nhận '{p[2]}' từ {p[0]} ({p[4]} mảnh)...");
                        break;
                    }
                case "FILECHUNK":
                    {
                        var p = rest.Split('|', 3);
                        if (p.Length < 3 || !_incoming.TryGetValue(p[0], out var inc)) return;
                        inc.Chunks[int.Parse(p[1])] = Convert.FromBase64String(p[2]);
                        break;
                    }
                case "FILEEND":
                    {
                        if (_incoming.TryRemove(rest, out var inc))
                            _ = Task.Run(() => SaveAsync(inc));
                        break;
                    }
            }
        }

        private async Task SaveAsync(Incoming inc)
        {
            try
            {
                using var ms = new MemoryStream();
                for (int i = 0; i < inc.TotalChunks; i++)
                {
                    if (!inc.Chunks.TryGetValue(i, out var chunk))
                    {
                        Console.WriteLine($"File '{inc.FileName}' thiếu mảnh {i}.");
                        return;
                    }
                    ms.Write(chunk, 0, chunk.Length);
                }

                byte[] aesKey = _crypto.UnwrapKey(inc.WrappedKeyB64);
                byte[] plain = CryptoService.AesDecryptBytes(ms.ToArray(), aesKey, Convert.FromBase64String(inc.IvB64));

                Directory.CreateDirectory("received");
                string path = Path.Combine("received", inc.FileName);
                await File.WriteAllBytesAsync(path, plain);

                Console.WriteLine($"Đã nhận và giải mã '{inc.FileName}' thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi nhận file: {ex.Message}");
            }
        }
    }
}
