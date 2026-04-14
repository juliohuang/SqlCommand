using System;
using System.Data;
using System.Data.SQLite;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Frame.Utils.Command.Tests
{
    [TestClass]
    public class AdditionalTests
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
                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT,
                    Price DECIMAL(10, 2),
                    InStock BOOLEAN,
                    LastUpdated DATETIME
                );
                
                INSERT INTO Products (Id, Name, Price, InStock, LastUpdated) VALUES (1, 'Product 1', 19.99, 1, '2024-01-01 10:00:00');
                INSERT INTO Products (Id, Name, Price, InStock, LastUpdated) VALUES (2, 'Product 2', 29.99, 0, '2024-01-02 11:00:00');
            ";
            command.ExecuteNonQuery();
        }

        [TestMethod]
        public void Exec_Test()
        {
            // 测试更新操作
            var command = Commands.GetCommand("UPDATE Products SET Price = @Price WHERE Id = @Id", "test");
            var result = command.Exec(new { Price = 24.99, Id = 1 });
            
            Assert.IsTrue(result);
            
            // 验证更新结果
            var checkCommand = Commands.GetCommand("SELECT Price FROM Products WHERE Id = @Id", "test");
            var price = checkCommand.Read<decimal>(new { Id = 1 });
            Assert.AreEqual(24.99m, price);
        }

        [TestMethod]
        public async Task ExecAsync_Test()
        {
            // 测试异步更新操作
            var command = Commands.GetCommand("UPDATE Products SET InStock = @InStock WHERE Id = @Id", "test");
            var result = await command.ExecAsync(new { InStock = 1, Id = 2 });
            
            Assert.IsTrue(result);
            
            // 验证更新结果
            var checkCommand = Commands.GetCommand("SELECT InStock FROM Products WHERE Id = @Id", "test");
            var inStock = checkCommand.Read<bool>(new { Id = 2 });
            Assert.IsTrue(inStock);
        }

        [TestMethod]
        public void ParameterizedQuery_Test()
        {
            // 测试参数化查询
            var command = Commands.GetCommand("SELECT * FROM Products WHERE Price > @MinPrice AND InStock = @InStock", "test");
            var products = command.Read<System.Collections.Generic.List<Product>>(new { MinPrice = 20.00, InStock = 1 });
            
            Assert.IsNotNull(products);
            Assert.AreEqual(1, products.Count);
            Assert.AreEqual("Product 2", products[0].Name);
        }

        [TestMethod]
        public void DifferentReturnTypes_Test()
        {
            // 测试不同类型的返回值
            var command = Commands.GetCommand("SELECT COUNT(*) FROM Products", "test");
            var count = command.Read<int>();
            Assert.AreEqual(2, count);
            
            command = Commands.GetCommand("SELECT Price FROM Products WHERE Id = @Id", "test");
            var price = command.Read<decimal>(new { Id = 1 });
            Assert.AreEqual(19.99m, price);
            
            command = Commands.GetCommand("SELECT Name FROM Products WHERE Id = @Id", "test");
            var name = command.Read<string>(new { Id = 1 });
            Assert.AreEqual("Product 1", name);
            
            command = Commands.GetCommand("SELECT InStock FROM Products WHERE Id = @Id", "test");
            var inStock = command.Read<bool>(new { Id = 1 });
            Assert.IsTrue(inStock);
            
            command = Commands.GetCommand("SELECT LastUpdated FROM Products WHERE Id = @Id", "test");
            var lastUpdated = command.Read<DateTime>(new { Id = 1 });
            Assert.IsNotNull(lastUpdated);
        }

        [TestMethod]
        public async Task DifferentReturnTypesAsync_Test()
        {
            // 测试异步不同类型的返回值
            var command = Commands.GetCommand("SELECT COUNT(*) FROM Products", "test");
            var count = await command.ReadAsync<int>();
            Assert.AreEqual(2, count);
            
            command = Commands.GetCommand("SELECT Price FROM Products WHERE Id = @Id", "test");
            var price = await command.ReadAsync<decimal>(new { Id = 1 });
            Assert.AreEqual(19.99m, price);
            
            command = Commands.GetCommand("SELECT Name FROM Products WHERE Id = @Id", "test");
            var name = await command.ReadAsync<string>(new { Id = 1 });
            Assert.AreEqual("Product 1", name);
        }

        [TestMethod]
        public void ErrorHandling_Test()
        {
            // 测试错误处理
            var command = Commands.GetCommand("SELECT * FROM NonExistentTable", "test");
            
            Assert.ThrowsException<Exception>(() =>
            {
                var result = command.Read<System.Collections.Generic.List<Product>>();
            });
        }

        [TestMethod]
        public async Task ErrorHandlingAsync_Test()
        {
            // 测试异步错误处理
            var command = Commands.GetCommand("SELECT * FROM NonExistentTable", "test");
            
            await Assert.ThrowsExceptionAsync<Exception>(async () =>
            {
                var result = await command.ReadAsync<System.Collections.Generic.List<Product>>();
            });
        }

        [TestMethod]
        public void TransactionRollback_Test()
        {
            // 测试事务回滚
            try
            {
                Commands.ExecuteWithTransaction(transaction =>
                {
                    // 插入新记录
                    var command1 = Commands.GetCommand("INSERT INTO Products (Id, Name, Price, InStock, LastUpdated) VALUES (@Id, @Name, @Price, @InStock, @LastUpdated)", "test");
                    command1.Read<int>(new { Id = 3, Name = "Product 3", Price = 39.99, InStock = 1, LastUpdated = DateTime.Now }, transaction);
                    
                    // 故意抛出异常
                    throw new Exception("Test rollback");
                }, "test");
            }
            catch (Exception)
            {
                // 异常被捕获，事务应该已回滚
            }
            
            // 验证记录未被插入
            var command = Commands.GetCommand("SELECT COUNT(*) FROM Products", "test");
            var count = command.Read<int>();
            Assert.AreEqual(2, count);
        }

        public class Product
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public bool InStock { get; set; }
            public DateTime LastUpdated { get; set; }
        }
    }
}