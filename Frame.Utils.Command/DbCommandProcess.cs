using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;

namespace Frame.Utils.Command
{
    public static class DbCommandProcess
    {
        /// <summary>
        /// </summary>
        /// <param name="watch"></param>
        /// <param name="command"></param>
        public static void Process(this Stopwatch watch, IDbCommand command)
        {
            //watch.Process(null, entity => { entity.Command = Prase(command); });
        }

        /// <summary>
        /// </summary>
        /// <param name="exception"></param>
        /// <param name="command"></param>
        public static void Process(this Exception exception, IDbCommand command)
        {
          //  exception.Process(null, entity => { entity.Command = Prase(command); });
        }

        /// <summary>
        /// </summary>
        /// <param name="traceLevel"></param>
        /// <param name="command"></param>
        public static void Process(this TraceLevel traceLevel, IDbCommand command)
        {
            //traceLevel.Process(null, entity => { entity.Command = Prase(command); });
        }

        public static string Prase(IDbCommand command)
        {
            try
            {
                if (command == null) return "";
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(string.Format("DbCommand[CommandType.{0}]", command.CommandType));

                foreach (DbParameter parameter in command.Parameters)
                {
                    string dbType = DBType(parameter);

                    stringBuilder.AppendLine(string.Format("declare {0} {1}", parameter.ParameterName, dbType));
                }

                foreach (DbParameter parameter in command.Parameters)
                {
                    switch (parameter.DbType)
                    {
                        case DbType.Boolean:
                            stringBuilder.AppendFormat("set {0} = {1}", parameter.ParameterName,
                                Convert.ToBoolean(parameter.Value) ? 1 : 0);
                            break;
                        case DbType.Currency:
                            stringBuilder.AppendFormat("set {0} = '{1}'", parameter.ParameterName, parameter.Value);
                            break;
                        case DbType.Date:
                        case DbType.DateTime:
                        case DbType.DateTime2:
                            DateTime dateTime;
                            try
                            {
                                dateTime = Convert.ToDateTime(parameter.Value);
                                stringBuilder.AppendFormat("set {0}=  convert(datetime,'{1:yyyy-MM-dd HH:mm:ss}')",
                                    parameter.ParameterName, dateTime);
                            }
                            catch (Exception)
                            {
                                
                             // throw;
                            }
                         
                            break;
                        case DbType.Decimal:
                            stringBuilder.AppendFormat("set {0} = {1}", parameter.ParameterName, parameter.Value);
                            break;
                        case DbType.Double:
                            stringBuilder.AppendFormat("set {0} = {1}", parameter.ParameterName, parameter.Value);
                            break;
                        case DbType.Guid:
                            break;
                        case DbType.UInt16:
                        case DbType.UInt32:
                        case DbType.UInt64:
                        case DbType.Int16:
                        case DbType.Int32:
                        case DbType.Int64:
                            stringBuilder.AppendFormat("set {0} = {1}", parameter.ParameterName, parameter.Value);
                            break;
                        case DbType.AnsiString:
                        case DbType.Binary:
                        case DbType.Byte:
                        case DbType.Object:
                        case DbType.SByte:
                        case DbType.Single:
                            stringBuilder.AppendFormat("set {0} = {1}", parameter.ParameterName, "??" + parameter.Value);
                            break;
                        case DbType.String:
                            stringBuilder.AppendFormat("set {0} = '{1}'", parameter.ParameterName, parameter.Value);
                            break;
                        case DbType.Time:
                            break;
                        case DbType.VarNumeric:
                            break;
                        case DbType.AnsiStringFixedLength:
                            break;
                        case DbType.StringFixedLength:
                            break;
                        case DbType.Xml:
                            break;
                        case DbType.DateTimeOffset:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    stringBuilder.AppendLine();
                }
                stringBuilder.AppendLine(command.CommandText);
                return stringBuilder.ToString();
            }
            catch (Exception ex)
            {
               // ex.Process();
                return "SQL解析失败";
            }
        }

        public static string DBType(this DbParameter parameter)
        {
            string dbType;
            switch (parameter.DbType)
            {
                case DbType.Date:
                case DbType.DateTime:
                case DbType.DateTime2:
                    dbType = "datetime";
                    break;
                case DbType.Decimal:
                case DbType.Currency:
                    dbType = "decimal(18,4)";
                    break;
                case DbType.Double:
                case DbType.Single:
                    dbType = "float";
                    break;
                case DbType.Guid:
                    dbType = "uniqueidentifier";
                    break;
                case DbType.UInt16:
                case DbType.UInt32:
                case DbType.UInt64:
                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                case DbType.Byte:
                case DbType.SByte:
                    dbType = "int";
                    break;
                case DbType.Boolean:
                    dbType = "bit";
                    break;
                case DbType.AnsiString:
                case DbType.String:
                    dbType = "nvarchar(256)";
                    break;
                case DbType.Binary:
                case DbType.Object:
                case DbType.Time:
                case DbType.VarNumeric:
                case DbType.AnsiStringFixedLength:
                case DbType.StringFixedLength:
                case DbType.Xml:
                case DbType.DateTimeOffset:
                    dbType = parameter.DbType.ToString();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return dbType;
        }
    }
}