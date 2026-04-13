using System;
using System.Data;
using System.Data.SQLite;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frame.Utils.Command.Tests
{
    [TestClass]
    public class CommandExtendTests
    {
        private const string ConnectionString = "Data Source=:memory:;Version=3;New=True;";

        [TestInitialize]
        public void TestInitialize()
        {
            // 初始化连接配置
            Commands.Connections = new System.Collections.Generic.Dictionary<string, string>
            {
                { "test", ConnectionString }
            };

            Commands.Configs = new System.Collections.Generic.Dictionary<string, CommandConfig>
            {
                { "test", new CommandConfig { provider = typeof(SQLiteConnection) } }
            };

            // 创建测试表
            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT,
                    Age INTEGER,
                    Data TEXT
                );
                
                INSERT INTO Users (Id, Name, Age, Data) VALUES (1, 'John', 30, '{""Email"": ""john@example.com"", ""Phone"": ""1234567890""}');
                INSERT INTO Users (Id, Name, Age, Data) VALUES (2, 'Jane', 25, '{""Email"": ""jane@example.com"", ""Phone"": ""0987654321""}');
            ";
            command.ExecuteNonQuery();
        }

        [TestMethod]
        public void Read_SingleObject_Test()
        {
            var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "test");
            var user = command.Read<User>(new { Id = 1 });

            Assert.IsNotNull(user);
            Assert.AreEqual(1, user.Id);
            Assert.AreEqual("John", user.Name);
            Assert.AreEqual(30, user.Age);
        }

        [TestMethod]
        public void Read_List_Test()
        {
            var command = Commands.GetCommand("SELECT * FROM Users", "test");
            var users = command.Read<System.Collections.Generic.List<User>>();

            Assert.IsNotNull(users);
            Assert.AreEqual(2, users.Count);
        }

        [TestMethod]
        public void Read_JsonField_Test()
        {
            var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "test");
            var user = command.Read<UserWithJson>(new { Id = 1 });

            Assert.IsNotNull(user);
            Assert.AreEqual(1, user.Id);
            Assert.IsNotNull(user.Data);
            Assert.AreEqual("john@example.com", user.Data.Email);
        }

        [TestMethod]
        public void Execute_WithTransaction_Test()
        {
            Commands.ExecuteWithTransaction(transaction =>
            {
                var command1 = Commands.GetCommand("INSERT INTO Users (Id, Name, Age, Data) VALUES (@Id, @Name, @Age, @Data)", "test");
                command1.CommandType = CommandType.Text;
                command1.Read<int>(new { Id = 3, Name = "Bob", Age = 35, Data = "{}" }, transaction);

                var command2 = Commands.GetCommand("UPDATE Users SET Age = @Age WHERE Id = @Id", "test");
                command2.CommandType = CommandType.Text;
                command2.Read<int>(new { Id = 1, Age = 31 }, transaction);
            }, "test");

            // 验证事务执行结果
            var command = Commands.GetCommand("SELECT Age FROM Users WHERE Id = @Id", "test");
            var age = command.Read<int>(new { Id = 1 });
            Assert.AreEqual(31, age);

            var countCommand = Commands.GetCommand("SELECT COUNT(*) FROM Users", "test");
            var count = command.Read<int>();
            Assert.AreEqual(3, count);
        }

        [TestMethod]
        public async Task ReadAsync_Test()
        {
            var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "test");
            var user = await command.ReadAsync<User>(new { Id = 1 });

            Assert.IsNotNull(user);
            Assert.AreEqual(1, user.Id);
            Assert.AreEqual("John", user.Name);
        }

        [TestMethod]
        public async Task ReadAsync_List_Test()
        {
            var command = Commands.GetCommand("SELECT * FROM Users", "test");
            var users = await command.ReadAsync<System.Collections.Generic.List<User>>();

            Assert.IsNotNull(users);
            Assert.AreEqual(2, users.Count);
        }

        public class User
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
            public string Data { get; set; }
        }

        public class UserWithJson
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
            public UserData Data { get; set; }
        }

        public class UserData
        {
            public string Email { get; set; }
            public string Phone { get; set; }
        }
    }
}