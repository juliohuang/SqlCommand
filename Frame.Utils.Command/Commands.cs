using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Transactions;

namespace Frame.Utils.Command
{
    /// <summary>
    ///     Commands
    /// </summary>
    public class Commands : List<Command>
    {
        public static Dictionary<string, string> Connections;

        public static Dictionary<string, CommandConfig> Configs;

        // 连接池管理
        private static readonly Dictionary<string, Stack<IDbConnection>> ConnectionPool = new Dictionary<string, Stack<IDbConnection>>();
        private static readonly object ConnectionPoolLock = new object();

        // 命令缓存
        private static readonly Dictionary<string, IDbCommand> CommandCache = new Dictionary<string, IDbCommand>();
        private static readonly object CommandCacheLock = new object();

        // public static bool snake { get; set; }


        public static Command GetCommand(string sql, string dbName = "main")
        {
            var snake = false;
            if (Configs.TryGetValue(dbName, out var config)) snake = config.snake;
            return new Command {DbName = dbName, Text = sql, Snake = snake};
        }


        public static IDbTransaction BeginTransaction(string dbName = "main")
        {
            var connection = GetConnection(dbName);
            connection.OpenIfClose();
            return connection.BeginTransaction();
        }

        /// <summary>
        /// 创建事务作用域
        /// </summary>
        /// <returns></returns>
        public static TransactionScope CreateTransactionScope()
        {
            return new TransactionScope(TransactionScopeOption.Required, new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TransactionManager.DefaultTimeout
            });
        }

        /// <summary>
        /// 执行带事务的操作
        /// </summary>
        /// <param name="action">操作</param>
        /// <param name="dbName">数据库名称</param>
        public static void ExecuteWithTransaction(Action<IDbTransaction> action, string dbName = "main")
        {
            using var connection = GetConnection(dbName);
            connection.OpenIfClose();
            using var transaction = connection.BeginTransaction();
            try
            {
                action(transaction);
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                // 归还连接到连接池
                ReleaseConnection(dbName, connection);
            }
        }

        /// <summary>
        /// 从连接池获取连接
        /// </summary>
        /// <param name="name">数据库名称</param>
        /// <returns></returns>
        public static IDbConnection GetConnection(string name)
        {
            lock (ConnectionPoolLock)
            {
                if (!ConnectionPool.ContainsKey(name))
                {
                    ConnectionPool[name] = new Stack<IDbConnection>();
                }

                if (ConnectionPool[name].Count > 0)
                {
                    return ConnectionPool[name].Pop();
                }

                // 创建新连接
                var connection = DbConnection(name);
                return connection;
            }
        }

        /// <summary>
        /// 归还连接到连接池
        /// </summary>
        /// <param name="name">数据库名称</param>
        /// <param name="connection">连接</param>
        public static void ReleaseConnection(string name, IDbConnection connection)
        {
            if (connection == null) return;

            try
            {
                // 确保连接关闭
                connection.CloseIfOpen();

                lock (ConnectionPoolLock)
                {
                    if (!ConnectionPool.ContainsKey(name))
                    {
                        ConnectionPool[name] = new Stack<IDbConnection>();
                    }

                    // 限制连接池大小
                    if (ConnectionPool[name].Count < 100) // 可配置
                    {
                        ConnectionPool[name].Push(connection);
                    }
                }
            }
            catch (Exception)
            {
                // 连接已损坏，不归还
                connection.Dispose();
            }
        }

        /// <summary>
        /// 获取缓存的命令
        /// </summary>
        /// <param name="connection">连接</param>
        /// <param name="sql">SQL语句</param>
        /// <returns></returns>
        public static IDbCommand GetCachedCommand(IDbConnection connection, string sql)
        {
            var cacheKey = $"{connection.GetType().FullName}:{sql}";

            lock (CommandCacheLock)
            {
                if (CommandCache.TryGetValue(cacheKey, out var command))
                {
                    // 清除之前的参数
                    command.Parameters.Clear();
                    command.Connection = connection;
                    return command;
                }

                // 创建新命令并缓存
                command = connection.CreateCommand();
                command.CommandText = sql;
                CommandCache[cacheKey] = command;
                return command;
            }
        }

        public static IDbConnection DbConnection(string name)
        {
            var settings = Connections[name];

            // default SqlServer

            var connectionString = settings;
            Type type=null;
            if (Configs.TryGetValue(name, out var config))
                type = config.provider;// ?? typeof(SqlConnection);
            //else
            //    type = typeof(SqlConnection);

           // Debug.Assert(type != null, "type != null");
           if(type==null)
                return null;

            var instance = Activator.CreateInstance(type, connectionString);
            return instance as IDbConnection;
        }

        public static T Procedure<T>()
        {
            return default;
        }
    }
}