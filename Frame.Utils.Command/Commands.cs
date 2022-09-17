using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace Frame.Utils.Command
{
    /// <summary>
    ///     Commands
    /// </summary>
    public class Commands : List<Command>
    {
        public static Dictionary<string, string> Connections;

        public static Dictionary<string, CommandConfig> Configs;

        // public static bool snake { get; set; }


        public static Command GetCommand(string sql, string dbName = "main")
        {
            var snake = false;
            if (Configs.TryGetValue(dbName, out var config)) snake = config.snake;
            return new Command {DbName = dbName, Text = sql, Snake = snake};
        }


        public static IDbTransaction BeginTransaction(string dbName = "main")
        {
            var connection = DbConnection(dbName);
            connection.OpenIfClose();
            return connection.BeginTransaction();
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