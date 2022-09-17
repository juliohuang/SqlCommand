using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Frame.Utils.Command
{
    public static partial class CommandExtend
    {
        private static readonly Regex Regex = new Regex("@[0-9a-zA-Z#_]+", RegexOptions.None);
        private static readonly Regex Regex2 = new Regex("\\$[0-9a-zA-Z#_]+", RegexOptions.None);

        private static void ReadAll(this Command command, IList result, Type memberType,
            Dictionary<string, object> paras = null, string[] includes = null, IDbTransaction transaction = null)
        {
            if (memberType.IsArray)
                Command(command, (dbCommand, list) =>
                {
                    var reader = dbCommand.ExecuteReader();
                    var count = reader.FieldCount;
                    var elementType = memberType.GetElementType();
                    while (reader.Read())
                    {
                        var array = Array.CreateInstance(elementType, count);
                        for (var i = 0; i < count; i++) array.SetValue(reader[i], i);

                        list.Add(array);
                    }

                    return list;
                }, result, paras, transaction);

            if (memberType.IsClass && memberType != typeof(string))
            {
                var hashtables = new Dictionary<string, Hashtable>();
                if (includes != null && includes.Length > 0)
                    foreach (var include in includes)
                    {
                        var strings = include.Split(".".ToCharArray());
                        if (strings.Length > 1)
                        {
                            if (!hashtables.ContainsKey(strings[1]))
                                hashtables.Add(strings[1], new Hashtable());
                        }
                        else
                        {
                            if (!hashtables.ContainsKey("@Key"))
                                hashtables.Add("@Key", new Hashtable());
                        }
                    }


                if (memberType.IsGenericType && memberType == typeof(Dictionary<string, object>))
                {
                    ReadData<List<string>> readData = (reader, list) =>
                    {
                        var readToObject = new Dictionary<string, object>();

                        ReadToMap(reader, list, readToObject);

                        result.Add(readToObject);
                    };
                    Command(command, dbCommand =>
                    {
                        var reader = dbCommand.ExecuteReader();
                        var schemaColumns = SchemaColumns(reader.GetSchemaTable(), false);

                        while (reader.Read()) readData(reader, schemaColumns);
                    }, paras, transaction);
                }
                else
                {
                    PreRead<List<List<PropertyInfo>>> preRead = schemaTable => SchemaList(schemaTable, memberType);
                    ReadData<List<List<PropertyInfo>>> readData = (reader, list) =>
                    {
                        var readToObject = ReadToObject(reader, list, memberType, command.Snake);
                        foreach (var hashtable1 in hashtables)
                        {
                            var value = hashtable1.Key == "@Key" ? reader[0] : reader[hashtable1.Key];
                            if (!hashtable1.Value.Contains(value))
                                hashtable1.Value.Add(value, readToObject);
                        }

                        result.Add(readToObject);
                    };
                    if (includes != null && includes.Length > 0)
                        Command(command, dbCommand =>
                        {
                            var reader = dbCommand.ExecuteReader();
                            var data = preRead(reader.GetSchemaTable());
                            while (reader.Read()) readData(reader, data);

                            foreach (var name in includes)
                            {
                                if (!reader.NextResult()) break;

                                var strings = name.Split(".".ToCharArray());
                                var propertyInfo = memberType.GetProperty(strings[0]);
                                var type = propertyInfo.PropertyType.GetGenericArguments()[0];
                                PreRead<List<List<PropertyInfo>>> preRead2 =
                                    schemaTable => SchemaList(schemaTable, type);
                                ReadData<List<List<PropertyInfo>>> readData2 = (dataReader, list) =>
                                {
                                    var key =
                                        dataReader[1];

                                    var s = strings.Length > 1
                                        ? strings[1]
                                        : "@Key";
                                    var obj =
                                        hashtables[s][key];
                                    if (obj == null) return;
                                    if (!(propertyInfo
                                        .GetValue(obj,
                                            new object[0
                                            ]) is IList o))
                                    {
                                        o =
                                            Activator
                                                    .CreateInstance
                                                    (propertyInfo
                                                        .PropertyType)
                                                as IList;
                                        propertyInfo
                                            .SetValue(obj, o,
                                                new object[0
                                                ]);
                                    }

                                    o?.Add(
                                        ReadToObject(
                                            dataReader, list,
                                            type, command.Snake));
                                };

                                var data2 = preRead2(reader.GetSchemaTable());
                                while (reader.Read()) readData2(reader, data2);
                            }
                        }, paras, transaction);
                    else
                        Read(command, preRead, readData, paras, false, transaction);
                }
            }
            else
            {
                var t = result;
                ReadOne(command, reader => t.Add(Convert.ChangeType(reader[0], memberType)), paras, false,
                    transaction);
            }
        }


        private static T ReadToObject<T>(IDataReader reader, List<List<PropertyInfo>> list, bool snake)
        {
            var type = typeof(T);
            var hasEmptyConstructor = type.GetConstructors().Any(c => c.GetParameters().Length == 0);
            if (hasEmptyConstructor)
            {
                var readToObject = ReadToObject(reader, list, type, snake);
                return (T) readToObject;
            }

            var constructorInfo = typeof(T).GetConstructors().First();
            var objects = ReadToArray(reader) as object[];
            return (T) constructorInfo.Invoke(objects);
        }

        private static object ReadToObject(IDataReader reader, List<List<PropertyInfo>> list, Type type, bool snake)
        {
            var constructors = type.GetConstructors();

            var hasEmptyConstructor = constructors.Any(c => c.GetParameters().Length == 0);

            if (hasEmptyConstructor)
            {
                var result = Activator.CreateInstance(type);
                ReadToObject(reader, list, result, snake);
                return result;
            }

            var constructorInfo = constructors.First();
            var objects = ReadToArray(reader) as object[];
            return constructorInfo.Invoke(objects);
        }

        private static void ReadToObject(IDataReader reader, List<List<PropertyInfo>> list, object t, bool snake)
        {
            foreach (var property in list)
            {
                if (property == null || property.Count == 0) break;
                var name = string.Join("_", property.Select(c => c.Name));
//                if (snake)
//                {
//                    name = Snake( name);
//                }

                object o;
                try
                {
                    o = reader[name];
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    o = DBNull.Value;
                }

                if (o is DBNull) continue;
                var obj = t;

                for (var index = 0; index < property.Count; index++)
                {
                    var propertyInfo = property[index];

                    var propertyType = propertyInfo.PropertyType;

                    if (propertyType.IsGenericType &&
                        propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        propertyType = propertyType.GetGenericArguments()[0];

                    if (index == property.Count - 1)
                    {
                        SetPropertyValue(obj, o, propertyInfo);
                    }
                    else
                    {
                        var propertyValue = propertyInfo.GetValue(obj, new object[0]);
                        if (propertyValue == null)
                        {
                            var instance = Activator.CreateInstance(propertyType);
                            propertyInfo.SetValue(obj, instance, new object[0]);
                            obj = instance;
                        }
                        else
                        {
                            obj = propertyValue;
                        }
                    }
                }
            }
        }


        private static void ReadToMap(IDataReader reader, List<string> list, IDictionary<string, object> t)
        {
            foreach (var property in list)
            {
                object o;
                try
                {
                    o = reader[property];
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    o = DBNull.Value;
                }

                if (o is DBNull) continue;

                t.Add(property, o);
            }
        }

        private static string Snake(string camel)
        {
            return Regex.Replace(LowStart(camel), @"([A-Z])", "_$1").ToLower();
        }

        public static string LowStart( string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;
            return raw.Substring(0, 1).ToLower() + raw.Substring(1);
        }

        /// <summary>
        ///     对象转参数对象
        /// </summary>
        /// <param name="command"></param>
        /// <param name="paras"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        private static Dictionary<string, object> ToDictionary(Command command, object paras, string prefix = "@")
        {
            if (paras == null)
                return new Dictionary<string, object>();

            var matches = Regex.Matches(command.Text);

            var paraNames = (from ma in matches select ma.Value).Distinct().ToList();

            if (paraNames.Count == 0)
            {
                var objects =
                    paras.GetType()
                        .GetProperties()
                        .Where(propertyInfo => propertyInfo.CanRead)
                        .ToDictionary(propertyInfo => prefix + propertyInfo.Name,
                            delegate(PropertyInfo propertyInfo)
                            {
                                try
                                {
                                    return propertyInfo.GetValue(paras, new object[0]);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }

                                return null;
                            });

                return objects;
            }

            var dictionary = new Dictionary<string, object>();
            if (paras is IDictionary dictionary1)
                foreach (var s in paraNames)
                {
                    var value = paras;
                    var strings = s.Substring(1);
                    dictionary.Add(strings, dictionary1[strings]);
                }
            else
                foreach (var s in paraNames)
                {
                    var value = paras;
                    var strings = s.Substring(1).Split('_');
                    foreach (var s1 in strings)
                    {
                        if (value == null) continue;
                        var propertyInfo = value.GetType().GetProperties().FirstOrDefault(c => c.Name == s1);
                        if (propertyInfo != null)
                        {
                            value = propertyInfo.GetValue(value, new object[0]);

                            if (propertyInfo.PropertyType.IsEnum &&
                                TypeDescriptor.GetConverter(propertyInfo.PropertyType).ToString() !=
                                typeof(EnumConverter).FullName)
                                value = TypeDescriptor.GetConverter(propertyInfo.PropertyType)
                                    .ConvertTo(value, typeof(string));
                        }
                        else
                        {
                            goto Ignore;
                        }
                    }

                    dictionary.Add(s, value);
                    Ignore: ;
                }

            return dictionary;
        }

        private static T ReadRow<T>(this Command command, Dictionary<string, object> paras = null,
            IDbTransaction transaction = null)
        {
            var result = default(T);
            var type = typeof(T);
            if (type.IsClass && type != typeof(string))
            {
                if (type.IsArray)
                    ReadOne(command, reader => result = (T) ReadToArray(reader), paras, true, transaction);
                else if (type.GetInterfaces().Contains(typeof(IDictionary)))
                    Read(command, SchemaColumns, (reader, list) =>
                        {
                            var instance = Activator.CreateInstance<T>();
                            var a = instance as IDictionary;
                            foreach (var s in list) a?.Add(s, reader[s]);

                            result = instance;
                        },
                        paras, true, transaction);
                else
                    Read(command, schemaTable => SchemaList(schemaTable, type),
                        (reader, list) => result = ReadToObject<T>(reader, list, command.Snake), paras, true,
                        transaction);
            }
            else
            {
                Command(command, dbCommand =>
                {
                    var scalar = dbCommand.ExecuteScalar();
                    result = scalar != null && scalar != DBNull.Value
                        ? (T) Convert.ChangeType(scalar, type)
                        : default;
                }, paras, transaction);
            }

            return result;
        }

        private static object ReadToArray(IDataReader reader)
        {
            var array = new object[reader.FieldCount];
            for (var index = 0; index < array.Length; index++)
            {
                var data = reader[index];

                array.SetValue(data != DBNull.Value ? data : null, index);
            }

            return array;
        }

        private static List<List<PropertyInfo>> SchemaList(DataTable schemaTable, Type type)
        {
            var columns = SchemaColumns(schemaTable);
            var propertyInfos = Array.FindAll(type.GetProperties(), c => c.CanRead).ToList();
            var propertyLists =
                columns.Select(column => GetPropertyList(column, propertyInfos)).Where(t => t != null && t.Count > 0);
            return propertyLists.ToList();
        }

        private static List<string> SchemaColumns(DataTable schemaTable)
        {
            return SchemaColumns(schemaTable, true);
        }

        private static List<string> SchemaColumns(DataTable schemaTable, bool toLower)
        {
            var columnNames = from DataRow row in schemaTable.Rows
                select toLower ? row["ColumnName"].ToString().ToLower() : row["ColumnName"].ToString();
            return columnNames.ToList();
        }


        private static List<PropertyInfo> GetPropertyList(string column, List<PropertyInfo> propertyInfos)
        {
            //var contains = column.Contains("_");
            var infos = new List<PropertyInfo>();
            // if (contains)
            // {
            //     var indexOf = column.IndexOf("_", StringComparison.Ordinal);
            //     var propertyInfo =
            //         propertyInfos.Find(
            //             c =>
            //                 string.Compare(c.Name, column.Substring(0, indexOf),
            //                     StringComparison.CurrentCultureIgnoreCase) == 0);
            //     if (propertyInfo == null) return infos;
            //     infos.Add(propertyInfo);
            //     var substring = column.Substring(indexOf + 1);
            //     var list =
            //         Array.FindAll(propertyInfo.PropertyType.GetProperties(), c => c.CanRead).ToList();
            //     var propertyList = GetPropertyList(substring, list);
            //     if (propertyList.Count == 0)
            //         return propertyList;
            //     infos.AddRange(propertyList);
            // }
            // else
            column = column.Replace("_", "");
            {
                var propertyInfo =
                    propertyInfos.Find(c => string.Compare(c.Name, column, StringComparison.OrdinalIgnoreCase) == 0);
                if (propertyInfo != null)
                    infos.Add(propertyInfo);
            }

            return infos;
        }

        private static void SetPropertyValue(this object t, object o, PropertyInfo propertyInfo)
        {
            if (o is DBNull) return;
            if (o == null || !propertyInfo.CanWrite) return;

            var conversionType = propertyInfo.PropertyType;
            if (conversionType.IsArray)
            {
                const StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries;
                var strings = o.ToString().Split(new[] {','}, options);
                var elementType = conversionType.GetElementType();
                var instance = Array.CreateInstance(elementType, strings.Length);
                for (var index = 0; index < strings.Length; index++)
                {
                    var s = strings[index];
                    if (!string.IsNullOrEmpty(s))
                        instance.SetValue(Convert.ChangeType(s, elementType), index);
                }

                propertyInfo.SetValue(t, instance, new object[0]);
                return;
            }

            if (conversionType.IsGenericType
                && conversionType.GetGenericTypeDefinition() == typeof(List<>))
            {
                const StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries;
                var strings = o.ToString().Split(new[] {','}, options);
                var elementType = conversionType.GetGenericArguments()[0];

                var instance = Activator.CreateInstance(conversionType) as IList;
                if (instance != null)
                    foreach (var s in strings.Where(s => !string.IsNullOrEmpty(s)))
                        if (elementType == typeof(List<int>))
                        {
                            //todo
                        }
                        else
                        {
                            try
                            {
                                instance.Add(Convert.ChangeType(s, elementType));
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(elementType);
                                throw;
                            }
                        }

                propertyInfo.SetValue(t, instance, new object[0]);
                return;
            }


            if (conversionType.IsGenericType &&
                conversionType.GetGenericTypeDefinition() == typeof(Nullable<>))
                conversionType = conversionType.GetGenericArguments()[0];
            var value = conversionType.IsEnum
                ? TypeDescriptor.GetConverter(propertyInfo.PropertyType).ToString() !=
                  typeof(EnumConverter).FullName
                    ? TypeDescriptor.GetConverter(propertyInfo.PropertyType).ConvertFrom(o)
                    : Enum.ToObject(conversionType, o)
                : Convert.ChangeType(o, conversionType);
            propertyInfo.SetValue(t, value, new object[0]);
        }

        public static T Read<T>(this Command command, object paras = null, IDbTransaction transaction = null)
        {
            var type = typeof(T);
            var args = ToDictionary(command, paras);
            if (type.IsArray)
            {
                var instance = default(T);
                ReadOne(command, reader => instance = (T) ReadToArray(reader), args, true,
                    transaction);
                return instance;
            }

            var interfaces = type.GetInterfaces();
            if (interfaces.Contains(typeof(IDictionary)))
            {
                var instance = Activator.CreateInstance<T>();
                var dictionary = instance as IDictionary;
                var dictionary1 = args;
                ReadToDictionary(command, dictionary, type, dictionary1);
                return instance;
            }

            if (type.IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition == typeof(List<>))
                {
                    var instance = Activator.CreateInstance<T>();
                    ReadAll(command, paras, instance as IList, type.GetGenericArguments()[0]);
                    return instance;
                }

                if (typeDefinition == typeof(Tuple<,>)
                    || typeDefinition == typeof(Tuple<,,>)
                    || typeDefinition == typeof(Tuple<,,,>)
                    || typeDefinition == typeof(Tuple<,,,,>)
                    || typeDefinition == typeof(Tuple<,,,,,>)
                    || typeDefinition == typeof(Tuple<,,,,,,>)
                    || typeDefinition == typeof(ValueTuple<,,>)
                    || typeDefinition == typeof(KeyValuePair<,>))
                {
                    var genericArguments = type.GetGenericArguments();
                    if (genericArguments.Any(c => c.IsGenericType && c.GetGenericTypeDefinition() == typeof(List<>)))
                    {
                        var a = new object[type.GetGenericArguments().Length];
                        Command(command, dbCommand =>
                        {
                            using var reader = dbCommand.ExecuteReader();
                            for (var index = 0; index < genericArguments.Length; index++)
                            {
                                if (index > 0)
                                    reader.NextResult();

                                var genericArgument = genericArguments[index];
                                if (genericArgument.IsGenericType &&
                                    genericArgument.GetGenericTypeDefinition() == typeof(List<>))
                                {
                                    var list1 = Activator.CreateInstance(genericArgument) as IList;
                                    var memberType = genericArgument.GetGenericArguments()[0];

                                    var data = SchemaList(reader.GetSchemaTable(), memberType);
                                    while (reader.Read())
                                        list1.Add(ReadToObject(reader, data, memberType, command.Snake));


                                    a[index] = list1;
                                }
                                else if (!genericArgument.IsClass || genericArgument == typeof(string))
                                {
                                    if (reader.Read())
                                    {
                                        var o = reader[0];
                                        if (!(o is DBNull)) a[index] = Convert.ChangeType(o, genericArgument);
                                    }
                                }
                                else
                                {
                                    if (reader.Read())
                                    {
                                        var schemaList = SchemaList(reader.GetSchemaTable(), genericArgument);
                                        a[index] = ReadToObject(reader, schemaList, genericArgument, command.Snake);
                                    }
                                }
                            }

                            reader.Close();
                        }, args, transaction);

                        return (T) Activator.CreateInstance(type, a);
                    }
                }
            }


            if (type.BaseType == typeof(ValueTuple))
            {
                var constructorInfo = type.GetConstructors().First();
                object t = null;
                Command(command, dbCommand =>
                {
                    using var reader = dbCommand.ExecuteReader();
                    var objects = ReadToArray(reader) as object[];
                    t = (T) constructorInfo.Invoke(objects);
                    reader.Close();
                });

                return (T) t;
            }

            if (!type.IsClass ||
                type == typeof(string))
            {
                var instance = default(T);
                ReadOne(command, reader =>
                {
                    var o = reader[0];
                    if (o is DBNull)
                        instance = default;
                    else
                        instance = (T) Convert.ChangeType(o, typeof(T));
                }, args, true, transaction);

                return instance;
            }

            var instance2 = default(T);
            ReadOne(command, reader =>
            {
                var schemaList = SchemaList(reader.GetSchemaTable(), typeof(T));
                instance2 = ReadToObject<T>(reader, schemaList, command.Snake);
            }, args, true, transaction);

            return instance2;
        }

        private static void ReadToDictionary(this Command command, IDictionary result, Type dictionaryType,
            Dictionary<string, object> paras = null,
            IDbTransaction transaction = null)
        {
            Type keyType = null;
            Type valueType = null;
            var needExtend = false;

            Type type1 = null;
            if (dictionaryType.IsGenericType)
            {
                var arguments = dictionaryType.GetGenericArguments();
                if (arguments.Length == 2)
                {
                    keyType = arguments[0];
                    valueType = arguments[1];
                    if (arguments[1].IsClass && arguments[1] != typeof(string))
                        needExtend = true;
                    if (arguments[1].IsArray) type1 = arguments[1];
                }
            }

            if (!needExtend)
            {
                ReadOne(command, reader =>
                {
                    var key = keyType == null ? reader[0] : Convert.ChangeType(reader[0], keyType);
                    if (result.Contains(key)) return;
                    var value = valueType == null
                        ? reader[1]
                        : Convert.ChangeType(reader[1], valueType);
                    result.Add(key, value);
                }, paras, false, transaction);
            }
            else
            {
                if (type1 != null)
                {
                    ReadData<List<List<PropertyInfo>>> readData = (reader, list) =>
                    {
                        var fieldCount = reader.FieldCount;
                        var objects = new object[fieldCount];
                        reader.GetValues(objects);
                        var key = objects[0];
                        if (result.Contains(key)) return;
                        var array = Array.CreateInstance(type1.GetElementType(), fieldCount - 1);
                        for (var index = 1; index < objects.Length; index++) array.SetValue(objects[index], index - 1);

                        result.Add(Convert.ChangeType(key, keyType), array);
                    };
                    Read(command, schemaTable => SchemaList(schemaTable, valueType), readData, paras, false,
                        transaction);
                }
                else
                {
                    ReadData<List<List<PropertyInfo>>> readData = (reader, list) =>
                    {
                        var key = reader[0];
                        if (result.Contains(key)) return;
                        var instance = Activator.CreateInstance(valueType);
                        ReadToObject(reader, list, instance, command.Snake);
                        result.Add(Convert.ToString(key), instance);
                    };
                    Read(command, schemaTable => SchemaList(schemaTable, valueType), readData, paras, false,
                        transaction);
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="paras"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        private static bool Exec(this Command command, Dictionary<string, object> paras = null,
            IDbTransaction transaction = null)
        {
            return Command(
                command,
                (dbCommand, result) => dbCommand.ExecuteNonQuery() > 0,
                false,
                paras,
                transaction);
        }

        /// <summary>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="commandProcess"></param>
        /// <param name="paras"></param>
        /// <param name="transaction"></param>
        private static void Command(this Command command, CommandProcess commandProcess,
            Dictionary<string, object> paras = null, IDbTransaction transaction = null)
        {
            var connection = transaction != null
                ? transaction.Connection
                : Commands.DbConnection(command.DbName ?? "main");

            var dbCommand = connection.CreateCommand();


            dbCommand.CommandType = command.CommandType;
            //dbCommand.CommandTimeout = 3000000;
            if (paras != null && paras.Count > 0) MakeParameters(dbCommand, paras);

            dbCommand.CommandText = command.Text;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (transaction != null)
                    dbCommand.Transaction = transaction;
                else
                    connection.OpenIfClose();

                commandProcess(dbCommand);
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                ex.Process(dbCommand);
                throw;
            }
            finally
            {
                stopwatch.Process(dbCommand);
                if (transaction == null)
                    connection.CloseIfOpen();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="commandProcess"></param>
        /// <param name="result"></param>
        /// <param name="paras"></param>
        /// <param name="transaction"></param>
        private static T Command<T>(this Command command, CommandProcess<T> commandProcess, T result,
            Dictionary<string, object> paras = null, IDbTransaction transaction = null)
        {
            var connection = transaction != null
                ? transaction.Connection
                : Commands.DbConnection(command.DbName ?? "main");

            var dbCommand = connection.CreateCommand();


            dbCommand.CommandText = command.Text;
            dbCommand.CommandType = command.CommandType;
            dbCommand.CommandTimeout = 30000;


            MakeParameters(dbCommand, paras);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (transaction != null)
                    dbCommand.Transaction = transaction;
                else
                    connection.OpenIfClose();

                return commandProcess(dbCommand, result);
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                ex.Process(dbCommand);
                throw;
            }
            finally
            {
                stopwatch.Process(dbCommand);
                if (transaction == null)
                    connection.CloseIfOpen();
            }
        }

        private static void MakeParameters(IDbCommand dbCommand, Dictionary<string, object> paras)
        {
            if (paras == null || paras.Count <= 0) return;
            foreach (var (key, value) in paras)
            {
                var parameter = dbCommand.CreateParameter();
                parameter.ParameterName = key;
                parameter.Value = value ?? DBNull.Value;
               

                dbCommand.Parameters.Add(parameter);
            }
        }

        private static void Read<T>(this Command command, PreRead<T> preRead, ReadData<T> readData,
            Dictionary<string, object> paras, bool oneRow, IDbTransaction transaction = null)
        {
            Command(command, dbCommand =>
            {
                using var reader = dbCommand.ExecuteReader();

                var data = preRead(reader.GetSchemaTable());
                while (reader.Read())
                {
                    readData(reader, data);
                    if (oneRow) break;
                }

                reader.Close();
            }, paras, transaction);
        }

        private static void ReadOne(this Command command, ReadData readData, Dictionary<string, object> paras,
            bool oneRow,
            IDbTransaction transaction = null)
        {
            Command(command, dbCommand =>
            {
                using var reader = dbCommand.ExecuteReader();
                while (reader.Read())
                {
                    readData(reader);
                    if (oneRow) break;
                }

                reader.Close();
            }, paras, transaction);
        }

        /// <summary>
        ///     处理存储过程
        /// </summary>
        /// <param name="command"></param>
        /// <param name="parameters"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public static object Process(this Command command, IDataParameter[] parameters,
            IDbTransaction transaction = null)
        {
            var connection = transaction != null
                ? transaction.Connection
                : Commands.DbConnection(command.DbName ?? "main");

            var dbCommand = connection.CreateCommand();

            dbCommand.CommandText = command.Text;
            dbCommand.CommandType = CommandType.StoredProcedure;

            foreach (var dataParameter in parameters) dbCommand.Parameters.Add(dataParameter);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (transaction != null)
                    dbCommand.Transaction = transaction;
                else
                    connection.OpenIfClose();
                var dbParameter = dbCommand.CreateParameter();
                dbParameter.ParameterName = "RetVal";
                dbParameter.Direction = ParameterDirection.ReturnValue;
                dbCommand.Parameters.Add(dbParameter);
                dbCommand.ExecuteNonQuery();
                return dbParameter.Value;
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                ex.Process(dbCommand);
                throw;
            }
            finally
            {
                stopwatch.Process(dbCommand);
                if (transaction == null)
                    connection.CloseIfOpen();
            }
        }
    }
}