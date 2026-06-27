using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace ChatApplicationServer.Network
{
    internal class Database
    {
        private readonly string _cs;
        public Database(string cs)
        {
            _cs = cs;
            InitSchema();
        }
        private void InitSchema()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Users')
                CREATE TABLE Users (
                    userId INT IDENTITY(1,1) PRIMARY KEY,
                    userName NVARCHAR(50) UNIQUE NOT NULL,
                    passwordHash NVARCHAR(200) NOT NULL,
                    createAt DATETIME2 DEFAULT SYSUTCDATETIME()
                );

                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Messages')
                CREATE TABLE Messages (
                    messageId INT IDENTITY(1,1) PRIMARY KEY,
                    roomName NVARCHAR(100) NOT NULL,
                    sender NVARCHAR(50) NOT NULL,
                    content NVARCHAR(MAX) NOT NULL,
                    sentAt DATETIME2 DEFAULT SYSUTCDATETIME()
                );";

            using var connect = new SqlConnection(_cs);
            connect.Open();
            using var command = new SqlCommand(sql, connect);
            command.ExecuteNonQuery();
            Console.WriteLine("Kết nối thành công, bảng đã sẵn sàng");
        }

        public bool Register(string userName, string password, out string error)
        {
            error = "";
            if (userName.Length < 3)
            {
                error = "Tên đăng nhập phải lớn hơn 2 ký tự";
                return false;
            }
            if (password.Length < 6)
            {
                error = "Mật khẩu phải lớn hơn 5 ký tự";
                return false;
            }

            using var connect = new SqlConnection(_cs);
            connect.Open();

            using (var check = new SqlCommand("SELECT COUNT(1) FROM Users WHERE userName=@u", connect))
            {
                check.Parameters.AddWithValue("@u", userName);
                if ((int)check.ExecuteScalar()! > 0)
                {
                    error = "Tên đăng nhập đã tồn tại";
                    return false;
                }

                string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

                using var insert = new SqlCommand("INSERT INTO Users (userName, passwordHash) VALUES (@u, @h)", connect);
                insert.Parameters.AddWithValue("@u", userName);
                insert.Parameters.AddWithValue("@h", hash);
                insert.ExecuteNonQuery();
                return true;
            }
        }

        public bool Login(string username, string password)
        {
            using var connect = new SqlConnection(_cs);
            connect.Open();
            using var command = new SqlCommand("SELECT passwordHash FROM Users WHERE userName=@u", connect);
            command.Parameters.AddWithValue("@u", username);

            if (command.ExecuteScalar() is not string storedHash) return false;

            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        public void SaveMessage(string room, string sender, string content)
        {
            using var connect = new SqlConnection(_cs);
            connect.Open();
            using var command = new SqlCommand("INSERT INTO Messages (roomName, sender, content) VALUES (@r, @s, @c)", connect);
            command.Parameters.AddWithValue("@r", room);
            command.Parameters.AddWithValue("@s", sender);
            command.Parameters.AddWithValue("@c", content);
            command.ExecuteNonQuery();
        }
        public List<string> GetRecentMessages(string room, int limit = 15)
        {
            var list = new List<string>();
            using var connect = new SqlConnection(_cs);
            connect.Open();
            using var command = new SqlCommand(@"
            SELECT sender, content FROM (
                SELECT TOP (@n) sender, content, sentAt
                FROM Messages WHERE roomName=@r ORDER BY sentAt DESC
            ) t ORDER BY t.sentAt ASC", connect);
            command.Parameters.AddWithValue("@n", limit);
            command.Parameters.AddWithValue("@r", room);

            using var r = command.ExecuteReader();
            while (r.Read()) list.Add($"[{room}] {r.GetString(0)}: {r.GetString(1)}");
            return list;
        }
    }
}
