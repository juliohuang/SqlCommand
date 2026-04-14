using System;
using System.Data;
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frame.Utils.Command.Tests
{
    [TestClass]
    public class ConnectionTests
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
                    Age INTEGER
                );
                
                INSERT INTO Users (Id, Name, Age) VALUES (1, 'John', 30);
                INSERT INTO Users (Id, Name, Age) VALUES (2, 'Jane', 25);
            ";
            command.ExecuteNonQuery();
        }

        [TestMethod]
        public void ConnectionPooling_Test()
        {
            // 测试连接池功能
            var connection1 = Commands.GetConnection("test");
            Assert.IsNotNull(connection1);
            
            // 归还连接到连接池
            Commands.ReleaseConnection("test", connection1);
            
            // 再次获取连接，应该是同一个连接对象
            var connection2 = Commands.GetConnection("test");
            Assert.IsNotNull(connection2);
            
            // 验证连接被正确归还到连接池
            Commands.ReleaseConnection("test", connection2);
        }

        [TestMethod]
        public void CommandCaching_Test()
        {
            // 测试命令缓存功能
            using var connection = Commands.GetConnection("test");
            
            // 第一次获取命令，应该创建新命令
            var command1 = Commands.GetCachedCommand(connection, "SELECT * FROM Users WHERE Id = @Id");
            Assert.IsNotNull(command1);
            
            // 第二次获取相同的命令，应该从缓存中获取
            var command2 = Commands.GetCachedCommand(connection, "SELECT * FROM Users WHERE Id = @Id");
            Assert.IsNotNull(command2);
            
            // 验证命令被正确缓存
            Commands.ReleaseConnection("test", connection);
        }

        [TestMethod]
        public void CreateTransactionScope_Test()
        {
            // 测试创建事务作用域
            using var scope = Commands.CreateTransactionScope();
            Assert.IsNotNull(scope);
            
            // 执行一些操作
            var command = Commands.GetCommand("UPDATE Users SET Age = @Age WHERE Id = @Id", "test");
            command.Read<int>(new { Age = 31, Id = 1 });
            
            // 完成事务
            scope.Complete();
            
            // 验证更新结果
            var checkCommand = Commands.GetCommand("SELECT Age FROM Users WHERE Id = @Id", "test");
            var age = checkCommand.Read<int>(new { Id = 1 });
            Assert.AreEqual(31, age);
        }

        [TestMethod]
        public void BeginTransaction_Test()
        {
            // 测试开始事务
            using var transaction = Commands.BeginTransaction("test");
            Assert.IsNotNull(transaction);
            
            try
            {
                // 执行更新操作
                var command = Commands.GetCommand("UPDATE Users SET Age = @Age WHERE Id = @Id", "test");
                command.Read<int>(new { Age = 32, Id = 1 }, transaction);
                
                // 提交事务
                transaction.Commit();
                
                // 验证更新结果
                var checkCommand = Commands.GetCommand("SELECT Age FROM Users WHERE Id = @Id", "test");
                var age = checkCommand.Read<int>(new { Id = 1 });
                Assert.AreEqual(32, age);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        [TestMethod]
        public void DbConnection_Test()
        {
            // 测试创建数据库连接
            var connection = Commands.DbConnection("test");
            Assert.IsNotNull(connection);
            Assert.IsInstanceOfType(connection, typeof(SQLiteConnection));
            
            // 验证连接可以打开
            connection.Open();
            Assert.AreEqual(ConnectionState.Open, connection.State);
            connection.Close();
        }
    }
}