# Frame.Utils.Command 安装使用文档

## 1. 项目介绍

Frame.Utils.Command 是一个 .NET Core 3.1 数据库操作工具库，提供了简洁易用的 SQL 命令执行和结果映射功能。

### 主要功能

- **SQL 命令执行与结果映射
- **JSON 格式字段支持
- **事务处理优化**
- **连接池管理**
- **命令缓存**
- **异步操作支持**

## 2. 安装

### 2.1 环境要求

- .NET Core 3.1 或更高版本
- 支持的数据库：
  - SQL Server
  - SQLite
  - PostgreSQL (通过 Npgsql)
  - MySQL (通过 MySqlConnector)
  - 其他支持 ADO.NET 的数据库

### 2.2 项目类型集成

#### ASP.NET Core Web 项目

在 Startup.cs 或 Program.cs 中配置：

```csharp
// 在 Program.cs (ASP.NET Core 6+)
var builder = WebApplication.CreateBuilder(args);

// 配置 Frame.Utils.Command
Frame.Utils.Command.Commands.Connections = new Dictionary<string, string>
{
    { "main", builder.Configuration.GetConnectionString("DefaultConnection") }
};

Frame.Utils.Command.Commands.Configs = new Dictionary<string, CommandConfig>
{
    { "main", new CommandConfig { provider = typeof(SqlConnection) } }
};
```

#### 控制台应用

在 Program.cs 的 Main 方法或启动类中配置：

```csharp
class Program
{
    static void Main(string[] args)
    {
        // 配置 Frame.Utils.Command
        Commands.Connections = new Dictionary<string, string>
        {
            { "main", "Server=localhost;Database=YourDatabase;User Id=YourUser;Password=YourPassword;" }
        };

        Commands.Configs = new Dictionary<string, CommandConfig>
        {
            { "main", new CommandConfig { provider = typeof(SqlConnection) } }
        };

        // 开始使用
        RunApplication();
    }
}
```

#### 多数据库项目

支持同时连接多个数据库：

```csharp
// 配置多个数据库
Commands.Connections = new Dictionary<string, string>
{
    { "main", "Server=localhost;Database=MainDB;User Id=user;Password=pass;" },
    { "logs", "Server=localhost;Database=LogDB;User Id=user;Password=pass;" },
    { "cache", "Data Source=cache.db" }
};

Commands.Configs = new Dictionary<string, CommandConfig>
{
    { "main", new CommandConfig { provider = typeof(SqlConnection) } },
    { "logs", new CommandConfig { provider = typeof(SqlConnection) } },
    { "cache", new CommandConfig { provider = typeof(SQLiteConnection) } }
};

// 使用时指定数据库名称
var mainCommand = Commands.GetCommand("SELECT * FROM Users", "main");
var logCommand = Commands.GetCommand("SELECT * FROM Logs", "logs");
```

### 2.3 安装方式

#### 方式一：直接引用项目

将 `Frame.Utils.Command` 项目添加到你的解决方案中，然后在你的项目中添加项目引用。

```xml
<ProjectReference Include="path/to/Frame.Utils.Command.csproj" />
```

#### 方式二：编译为 DLL 引用

1. 编译项目：
```bash
dotnet build
```

2. 将编译生成的 `Frame.Utils.Command.dll` 引用到你的项目中。

#### 方式三：NuGet 包（如果可用）

如果将项目打包为 NuGet 包，可以通过以下方式安装：

```bash
dotnet add package Frame.Utils.Command
```

### 2.4 依赖项

项目依赖以下 NuGet 包：
- Microsoft.Extensions.Configuration.Abstractions (5.0.0)
- Newtonsoft.Json (12.0.3)
- System.Configuration.ConfigurationManager (5.0.0)

根据你使用的数据库，还需要安装相应的数据库驱动：
- SQL Server: `System.Data.SqlClient` 或 `Microsoft.Data.SqlClient`
- SQLite: `System.Data.SQLite`
- PostgreSQL: `Npgsql`
- MySQL: `MySqlConnector`

## 3. 快速开始

### 3.1 初始化配置

在使用前，需要先配置数据库连接：

```csharp
using Frame.Utils.Command;

// 配置连接字符串
Commands.Connections = new Dictionary<string, string>
{
    { "main", "Server=localhost;Database=YourDatabase;User Id=YourUser;Password=YourPassword;" }
};

// 配置提供者类型
Commands.Configs = new Dictionary<string, CommandConfig>
{
    { "main", new CommandConfig { provider = typeof(SqlConnection) }
};
```

### 3.2 基本使用

#### 执行查询并映射到对象

```csharp
// 创建命令
var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "main");

// 执行查询并映射到 User 对象
var user = command.Read<User>(new { Id = 1 });
```

#### 执行查询并映射到列表

```csharp
// 创建命令
var command = Commands.GetCommand("SELECT * FROM Users", "main");

// 执行查询并映射到 List<User>
var users = command.Read<List<User>>();
```

## 4. 高级功能

### 4.1 JSON 格式字段支持

库支持自动处理 JSON 格式字段：

```csharp
public class UserData
{
    public string Email { get; set; }
    public string Phone { get; set; }
}

public class UserWithJson
{
    public int Id { get; set; }
    public string Name { get; set; }
    public UserData Data { get; set; }
}

// 查询时会自动将 JSON 字符串反序列化为 UserData 对象
var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "main");
var user = command.Read<UserWithJson>(new { Id = 1 });

// 插入时会自动将对象序列化为 JSON 字符串
var newUser = new UserWithJson
{
    Name = "Alice",
    Data = new UserData { Email = "alice@example.com", Phone = "1234567890" }
};
var insertCommand = Commands.GetCommand("INSERT INTO Users (Name, Data) VALUES (@Name, @Data)", "main");
insertCommand.CommandType = CommandType.Text;
insertCommand.Read<int>(newUser);
```

### 4.2 事务处理

#### 使用 TransactionScope

```csharp
using (var scope = Commands.CreateTransactionScope())
{
    // 在这里执行多个数据库操作
    
    scope.Complete();
}
```

#### 使用 ExecuteWithTransaction

```csharp
Commands.ExecuteWithTransaction(transaction =>
{
    // 插入数据
    var command1 = Commands.GetCommand("INSERT INTO Users (Name, Age) VALUES (@Name, @Age)", "main");
    command1.Read<int>(new { Name = "Bob", Age = 30 }, transaction);
    
    // 更新数据
    var command2 = Commands.GetCommand("UPDATE Users SET Age = @Age WHERE Id = @Id", "main");
    command2.Read<int>(new { Id = 1, Age = 31 }, transaction);
}, "main");
```

### 4.3 异步操作

```csharp
// 异步读取单个对象
var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "main");
var user = await command.ReadAsync<User>(new { Id = 1 });

// 异步读取列表
var listCommand = Commands.GetCommand("SELECT * FROM Users", "main");
var users = await command.ReadAsync<List<User>>();
```

## 5. API 文档

### 5.1 Commands 类

| 方法 | 说明 |
|------|------|
| `GetCommand(string sql, string dbName = "main")` | 创建命令对象 |
| `BeginTransaction(string dbName = "main")` | 开始事务 |
| `CreateTransactionScope()` | 创建事务作用域 |
| `ExecuteWithTransaction(Action<IDbTransaction> action, string dbName = "main")` | 执行带事务的操作 |
| `GetConnection(string name)` | 从连接池获取连接 |
| `ReleaseConnection(string name, IDbConnection connection)` | 归还连接到连接池 |
| `GetCachedCommand(IDbConnection connection, string sql)` | 获取缓存的命令 |

### 5.2 Command 类

| 属性 | 说明 |
|------|------|
| `Id` | 命令 ID |
| `DbName` | 数据库名称 |
| `Text` | 命令文本 |
| `CommandType` | 命令类型 |
| `Snake` | 是否使用蛇形命名 |

### 5.3 CommandExtend 类

| 方法 | 说明 |
|------|------|
| `Read<T>(object paras = null, IDbTransaction transaction = null)` | 读取数据并映射到类型 T |
| `ReadAsync<T>(object paras = null, IDbTransaction transaction = null)` | 异步读取数据并映射到类型 T |
| `Process(IDataParameter[] parameters, IDbTransaction transaction = null)` | 处理存储过程 |

## 6. 测试

项目包含完整的单元测试，使用 SQLite 内存数据库进行测试：

```bash
# 运行测试
dotnet test
```

测试覆盖以下功能：
- 基本查询和对象映射
- JSON 字段处理
- 事务处理
- 异步操作

## 7. 性能优化建议

1. **使用连接池**：库已内置连接池管理，确保高效使用数据库连接
2. **使用命令缓存**：常用 SQL 命令会被自动缓存，减少解析开销
3. **使用异步操作**：在高并发场景下，使用异步方法可以提高性能
4. **合理使用事务**：确保事务范围尽可能小，减少锁持时间

## 8. 实际应用场景

### 8.1 电商平台订单系统

```csharp
public class OrderService
{
    public async Task<Order> CreateOrderAsync(OrderDto orderDto)
    {
        var order = new Order();
        Commands.ExecuteWithTransaction(transaction =>
        {
            // 保存订单
            var orderCommand = Commands.GetCommand(
                "INSERT INTO Orders (UserId, TotalAmount, Status) VALUES (@UserId, @TotalAmount, @Status); SELECT SCOPE_IDENTITY();", 
                "main");
            var orderId = orderCommand.Read<int>(new { 
                UserId = orderDto.UserId, 
                TotalAmount = orderDto.TotalAmount, 
                Status = "Pending" 
            }, transaction);
            
            // 保存订单项
            foreach (var item in orderDto.Items)
            {
                var itemCommand = Commands.GetCommand(
                    "INSERT INTO OrderItems (OrderId, ProductId, Quantity, Price) VALUES (@OrderId, @ProductId, @Quantity, @Price)", 
                    "main");
                itemCommand.Read<int>(new { 
                    OrderId = orderId, 
                    ProductId = item.ProductId, 
                    Quantity = item.Quantity, 
                    Price = item.Price 
                }, transaction);
            }
            
            order.Id = orderId;
        }, "main");
        
        return await GetOrderAsync(order.Id);
    }
}
```

### 8.2 日志记录系统

```csharp
public class LogService
{
    public async Task LogAsync(LogEntry log)
    {
        var command = Commands.GetCommand(
            "INSERT INTO Logs (Level, Message, Data, CreatedAt) VALUES (@Level, @Message, @Data, @CreatedAt)", 
            "logs");
        await command.ReadAsync<int>(new { 
            Level = log.Level, 
            Message = log.Message, 
            Data = log.Data, 
            CreatedAt = DateTime.Now 
        });
    }
}

public class LogEntry
{
    public string Level { get; set; }
    public string Message { get; set; }
    public object Data { get; set; } // 会自动序列化为 JSON
    public DateTime CreatedAt { get; set; }
}
```

### 8.3 缓存系统

```csharp
public class CacheService
{
    public async Task<T> GetAsync<T>(string key)
    {
        var command = Commands.GetCommand(
            "SELECT Value FROM Cache WHERE Key = @Key AND ExpiresAt > @Now", 
            "cache");
        var result = await command.ReadAsync<CacheItem>(new { Key = key, Now = DateTime.Now });
        
        if (result != null && !string.IsNullOrEmpty(result.Value))
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(result.Value);
        }
        
        return default;
    }
    
    public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        var command = Commands.GetCommand(
            "INSERT OR REPLACE INTO Cache (Key, Value, ExpiresAt) VALUES (@Key, @Value, @ExpiresAt)", 
            "cache");
        await command.ReadAsync<int>(new { 
            Key = key, 
            Value = Newtonsoft.Json.JsonConvert.SerializeObject(value), 
            ExpiresAt = DateTime.Now.Add(expiration) 
        });
    }
}

public class CacheItem
{
    public string Key { get; set; }
    public string Value { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

## 9. 最佳实践

### 9.1 配置管理最佳实践

- **集中配置**：将数据库连接配置放在独立的配置文件中
- **环境隔离**：使用不同的配置文件区分开发、测试和生产环境
- **敏感信息保护**：不要在代码中硬编码密码，使用环境变量或密钥管理服务

### 9.2 性能最佳实践

- **使用连接池**：确保正确配置连接池大小，避免连接泄漏
- **合理使用缓存**：对于频繁执行的 SQL 命令，利用命令缓存功能
- **批量操作**：尽可能使用批量插入/更新，减少数据库往返次数
- **索引优化**：确保查询字段有合适的索引

### 9.3 安全最佳实践

- **参数化查询**：始终使用参数化查询，防止 SQL 注入
- **最小权限原则**：数据库用户只授予必要的权限
- **连接字符串加密**：生产环境中加密存储连接字符串

### 9.4 错误处理最佳实践

```csharp
try
{
    var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "main");
    var user = await command.ReadAsync<User>(new { Id = userId });
    return user;
}
catch (SQLiteException ex)
{
    // 处理 SQLite 特定错误
    logger.LogError(ex, "Database error occurred");
    throw new ApplicationException("数据库操作失败", ex);
}
catch (Exception ex)
{
    // 处理其他错误
    logger.LogError(ex, "Unexpected error");
    throw;
}
```

## 10. 注意事项

1. 确保在使用前正确配置数据库连接
2. 注意处理异常处理，确保异常时事务正确回滚
3. 在使用异步方法时，确保使用 `await` 关键字
4. 对于长时间运行的查询，注意设置适当的命令超时时间
5. 连接池大小默认限制为 100，可根据实际需求调整
6. 避免在事务中执行耗时过长的操作，以免影响数据库性能
7. 定期监控连接池使用情况，确保没有连接泄漏
8. 对于包含大量数据的查询，考虑分页处理
