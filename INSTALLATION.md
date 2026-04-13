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
  - 其他支持 ADO.NET 的数据库

### 2.2 安装方式

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

2. 将编译生成的 `Frame.Utils.Command.dll 引用到你的项目中。

### 2.3 依赖项

项目依赖以下 NuGet 包：
- Microsoft.Extensions.Configuration.Abstractions (5.0.0)
- Newtonsoft.Json (12.0.3)
- System.Configuration.ConfigurationManager (5.0.0)

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

## 8. 注意事项

1. 确保在使用前正确配置数据库连接
2. 注意处理异常处理，确保异常时事务正确回滚
3. 在使用异步方法时，确保使用 `await` 关键字
4. 对于长时间运行的查询，注意设置适当的命令超时时间
