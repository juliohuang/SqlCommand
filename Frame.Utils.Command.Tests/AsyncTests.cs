using System;
using System.Data;
using System.Data.SQLite;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frame.Utils.Command.Tests
{
    [TestClass]
    public class AsyncTests
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
        public async Task ReadAsync_Test()
        {
            var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "test");
            var user = await command.ReadAsync<User>(new { Id = 1 });

            Assert.IsNotNull(user);
            Assert.AreEqual(1, user.Id);
            Assert.AreEqual("John", user.Name);
            Assert.AreEqual(30, user.Age);
        }

        [TestMethod]
        public async Task ReadAsync_List_Test()
        {
            var command = Commands.GetCommand("SELECT * FROM Users", "test");
            var users = await command.ReadAsync<System.Collections.Generic.List<User>>();

            Assert.IsNotNull(users);
            Assert.AreEqual(2, users.Count);
        }

        [TestMethod]
        public async Task ReadAsync_JsonField_Test()
        {
            var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "test");
            var user = await command.ReadAsync<UserWithJson>(new { Id = 1 });

            Assert.IsNotNull(user);
            Assert.AreEqual(1, user.Id);
            Assert.IsNotNull(user.Data);
            Assert.AreEqual("john@example.com", user.Data.Email);
        }

        [TestMethod]
        public async Task ProcessAsync_Test()
        {
            // 创建测试存储过程
            using var connection = new SQLiteConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE PROCEDURE InsertUser(@Name TEXT, @Age INTEGER, @Data TEXT)
                BEGIN
                    INSERT INTO Users (Name, Age, Data) VALUES (@Name, @Age, @Data);
                    SELECT last_insert_rowid();
                END;
            ";
            command.ExecuteNonQuery();

            // 准备参数
            var parameters = new SQLiteParameter[]
            {
                new SQLiteParameter("@Name", "Bob"),
                new SQLiteParameter("@Age", 35),
                new SQLiteParameter("@Data", "{\"Email\": \"bob@example.com\", \"Phone\": \"5555555555\"}")
            };

            // 执行存储过程
            var processCommand = Commands.GetCommand("InsertUser", "test");
            var result = await processCommand.ProcessAsync(parameters);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(long));
        }

        [TestMethod]
        public async Task MultipleAsyncOperations_Test()
        {
            // 并行执行多个异步操作
            var tasks = new[]
            {
                Task.Run(async () => {
                    var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "test");
                    return await command.ReadAsync<User>(new { Id = 1 });
                }),
                Task.Run(async () => {
                    var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "test");
                    return await command.ReadAsync<User>(new { Id = 2 });
                }),
                Task.Run(async () => {
                    var command = Commands.GetCommand("SELECT COUNT(*) FROM Users", "test");
                    return await command.ReadAsync<int>();
                })
            };

            // 等待所有任务完成
            var results = await Task.WhenAll(tasks);

            // 验证结果
            var user1 = results[0] as User;
            var user2 = results[1] as User;
            var count = (int)results[2];

            Assert.IsNotNull(user1);
            Assert.AreEqual(1, user1.Id);
            Assert.IsNotNull(user2);
            Assert.AreEqual(2, user2.Id);
            Assert.AreEqual(2, count);
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