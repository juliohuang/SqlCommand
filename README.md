# Frame.Utils.Command

Frame.Utils.Command 是一个 .NET Core 3.1 数据库操作工具库，提供了简洁易用的 SQL 命令执行和结果映射功能。

## 主要功能

- **SQL 命令执行与结果映射**：支持将查询结果自动映射到对象、列表或基本类型
- **JSON 格式字段支持**：自动处理 JSON 格式字段的序列化和反序列化
- **事务处理优化**：提供 TransactionScope 和 ExecuteWithTransaction 两种事务管理方式
- **连接池管理**：内置连接池，提高数据库连接的使用效率
- **命令缓存**：缓存常用 SQL 命令，减少解析开销
- **异步操作支持**：提供完整的异步方法，支持并行执行
- **多数据库支持**：兼容 SQL Server、SQLite、PostgreSQL、MySQL 等支持 ADO.NET 的数据库

## 快速开始

### 安装

1. **直接引用项目**：将 `Frame.Utils.Command` 项目添加到你的解决方案中
2. **编译为 DLL 引用**：编译项目后引用生成的 DLL 文件
3. **NuGet 包**：如果将项目打包为 NuGet 包，可以通过 `dotnet add package Frame.Utils.Command` 安装

### 配置

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
    { "main", new CommandConfig { provider = typeof(SqlConnection) } }
};
```

### 基本使用

```csharp
// 执行查询并映射到对象
var command = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "main");
var user = command.Read<User>(new { Id = 1 });

// 执行查询并映射到列表
var listCommand = Commands.GetCommand("SELECT * FROM Users", "main");
var users = listCommand.Read<List<User>>();

// 异步操作
var asyncCommand = Commands.GetCommand("SELECT * FROM Users WHERE Id = @Id", "main");
var userAsync = await asyncCommand.ReadAsync<User>(new { Id = 1 });

// 执行非查询命令
var execCommand = Commands.GetCommand("UPDATE Users SET Age = @Age WHERE Id = @Id", "main");
var result = execCommand.Exec(new { Age = 31, Id = 1 });

// 事务处理
Commands.ExecuteWithTransaction(transaction =>
{
    var insertCommand = Commands.GetCommand("INSERT INTO Users (Name, Age) VALUES (@Name, @Age)", "main");
    insertCommand.Read<int>(new { Name = "Bob", Age = 30 }, transaction);
    
    var updateCommand = Commands.GetCommand("UPDATE Users SET Age = @Age WHERE Id = @Id", "main");
    updateCommand.Read<int>(new { Id = 1, Age = 31 }, transaction);
}, "main");
```

## 文档

详细的安装和使用说明请参考 [INSTALLATION.md](file:///workspace/INSTALLATION.md) 文件。

## 测试

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
- 错误处理
- 不同类型的返回值

## 性能优化

1. **使用连接池**：库已内置连接池管理，确保高效使用数据库连接
2. **使用命令缓存**：常用 SQL 命令会被自动缓存，减少解析开销
3. **使用异步操作**：在高并发场景下，使用异步方法可以提高性能
4. **合理使用事务**：确保事务范围尽可能小，减少锁持时间

## 依赖项

- Microsoft.Extensions.Configuration.Abstractions (5.0.0)
- Newtonsoft.Json (12.0.3)
- System.Configuration.ConfigurationManager (5.0.0)

根据你使用的数据库，还需要安装相应的数据库驱动：
- SQL Server: `System.Data.SqlClient` 或 `Microsoft.Data.SqlClient`
- SQLite: `System.Data.SQLite`
- PostgreSQL: `Npgsql`
- MySQL: `MySqlConnector`

## 许可证

MIT 许可证