using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Xml.Serialization;
using Microsoft.Extensions.Configuration;

namespace Frame.Utils.Command
{
    /// <summary>
    ///     Commands
    /// </summary>

    public class Commands : List<Command>
    {

        public static Dictionary<string, string> connections;


        public static Type Provider { get; set; }
        
        public static Command GetCommand(string sql, string dbName = "main", bool precompiled = false)
        {
            return new Command {DbName = dbName, Text = sql, Precompiled = precompiled};
        }


        public static IDbTransaction BeginTransaction(string dbName = "main")
        {
            var connection = DbConnection(dbName);
            connection.OpenIfClose();
            return connection.BeginTransaction();
        }

        public static IDbConnection DbConnection(string name)
        {
            
           
            var settings =  connections[name];

            // default SqlServer
            string providerName = null;
            var connectionString = settings;

            var type = string.IsNullOrEmpty(providerName) ? Provider : Type.GetType(providerName);
            Debug.Assert(type != null, "type != null");

            var instance = Activator.CreateInstance(type, connectionString);
            return instance as IDbConnection;
        }

        public static T Procedure<T>()
        {
            return default(T);
        }
    }
}