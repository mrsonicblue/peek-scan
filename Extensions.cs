using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Peek.Scan
{
    public static class Extensions
    {
        private static string FormatName(string name)
        {
            return string.Join(".", name.Split('.').Select(s => "`" + s + "`"));
        }

        public static IEnumerable<T> Read<T>(this SqliteCommand cmd, Func<SqliteDataReader, T> select)
        {
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    yield return select(reader);
                }
            }
        }

        public static Dictionary<string, object> ToDictionary(this SqliteDataReader reader)
        {
            return Enumerable.Range(0, reader.FieldCount)
                .ToDictionary(i => reader.GetName(i), i => reader.GetProviderSpecificValue(i));
        }

        public static T ToEntity<T>(this SqliteDataReader reader)
            where T : new()
        {
            T result = new T();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var prop = typeof(T).GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    prop.SetValue(result, value, null);
                }
            }

            return result;
        }

        public static int ExecuteNonQuery(this SqliteConnection conn, string sql, Dictionary<string, object> param = null)
        {
            SqliteCommand cmd = new SqliteCommand(sql, conn);
            if (param != null)
            {
                cmd.Parameters.AddRange(param.Select(o => new SqliteParameter(o.Key, o.Value)).ToArray());
            }

            return cmd.ExecuteNonQuery();
        }

        public static int ExecuteNonQuery(this SqliteConnection conn, string sql, Action<Dictionary<string, object>> paramFunc = null)
        {
            return ExecuteNonQuery(conn, sql, BuildParams(paramFunc));
        }

        public static T SelectSingleValue<T>(this SqliteConnection conn, string sql, Dictionary<string, object> param = null)
        {
            SqliteCommand cmd = new SqliteCommand(sql, conn);
            if (param != null)
            {
                cmd.Parameters.AddRange(param.Select(o => new SqliteParameter(o.Key, o.Value)).ToArray());
            }

            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    return (T)reader.GetValue(0);
                }
            }

            return default(T);
        }

        public static T SelectSingleValue<T>(this SqliteConnection conn, string sql, Action<Dictionary<string, object>> paramFunc = null)
        {
            return SelectSingleValue<T>(conn, sql, BuildParams(paramFunc));
        }

        public static IEnumerable<T> Select<T>(this SqliteConnection conn, string sql, Dictionary<string, object> param = null)
            where T : new()
        {
            SqliteCommand cmd = new SqliteCommand(sql, conn);
            if (param != null)
            {
                cmd.Parameters.AddRange(param.Select(o => new SqliteParameter(o.Key, o.Value)).ToArray());
            }

            return cmd.Read(r => r.ToEntity<T>());
        }

        public static IEnumerable<T> Select<T>(this SqliteConnection conn, string sql, Action<Dictionary<string, object>> paramFunc = null)
            where T : new()
        {
            return Select<T>(conn, sql, BuildParams(paramFunc));
        }

        public static IEnumerable<T> SelectByValue<T>(this SqliteConnection conn, string tableName, string field, object value)
            where T : new()
        {
            SqliteCommand cmd = new SqliteCommand("", conn);
            StringBuilder sql = new StringBuilder();

            sql.Append("SELECT * FROM " + FormatName(tableName) + " WHERE " + FormatName(field));

            if (value != null)
            {
                sql.Append(" = @V");
                cmd.Parameters.AddWithValue("@V", value);
            }
            else
            {
                sql.Append(" IS NULL");
            }

            cmd.CommandText = sql.ToString();

            return cmd.Read(r => r.ToEntity<T>());
        }

        public static IEnumerable<T> SelectByValues<T>(this SqliteConnection conn, string tableName, Dictionary<string, object> fields)
            where T : new()
        {
            SqliteCommand cmd = new SqliteCommand("", conn);

            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT * FROM " + FormatName(tableName));

            if (fields.Count > 0)
            {
                sql.Append(" WHERE " + string.Join(" AND ", fields.Select((f, i) =>
                {
                    if (f.Value != null)
                    {
                        string key = "@V" + i;
                        cmd.Parameters.AddWithValue(key, f.Value);
                        return FormatName(f.Key) + " = " + key;
                    }
                    return FormatName(f.Key) + " IS NULL";
                })));
            }

            sql.Append(" ORDER BY ID");

            cmd.CommandText = sql.ToString();

            return cmd.Read(r => r.ToEntity<T>());
        }

        public static IEnumerable<T> SelectByValues<T>(this SqliteConnection conn, string tableName, Action<Dictionary<string, object>> paramFunc = null)
            where T : new()
        {
            return SelectByValues<T>(conn, tableName, BuildParams(paramFunc));
        }

        public static T Find<T>(this SqliteConnection conn, string sql, Dictionary<string, object> param = null)
            where T : new()
        {
            return Select<T>(conn, sql, param).FirstOrDefault();
        }

        public static T Find<T>(this SqliteConnection conn, string sql, Action<Dictionary<string, object>> paramFunc = null)
            where T : new()
        {
            return Select<T>(conn, sql, paramFunc).FirstOrDefault();
        }

        public static T FindByID<T>(this SqliteConnection conn, string tableName, int id)
            where T : new()
        {
            SqliteCommand cmd = new SqliteCommand("SELECT TOP 1 * FROM " + FormatName(tableName) + " WHERE ID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);

            return cmd.Read(r => r.ToEntity<T>()).FirstOrDefault();
        }

        public static T FindByValue<T>(this SqliteConnection conn, string tableName, string field, object value)
            where T : new()
        {
            return SelectByValue<T>(conn, tableName, field, value).FirstOrDefault();
        }

        public static T FindByValues<T>(this SqliteConnection conn, string tableName, Dictionary<string, object> fields)
            where T : new()
        {
            return SelectByValues<T>(conn, tableName, fields).FirstOrDefault();
        }

        public static T FindByValues<T>(this SqliteConnection conn, string tableName, Action<Dictionary<string, object>> paramFunc = null)
            where T : new()
        {
            return SelectByValues<T>(conn, tableName, paramFunc).FirstOrDefault();
        }

        public static int InsertOrUpdate(this SqliteConnection conn, string tableName, string idName, int? id, Dictionary<string, object> fields)
        {
            if (id == null)
                return Insert(conn, tableName, fields);

            Update(conn, tableName, idName, id.Value, fields);

            return id.Value;
        }

        public static int Insert(this SqliteConnection conn, string tableName, Dictionary<string, object> fields)
        {
            var pairs = fields.ToList(); // This is to ensure keys and values align

            SqliteCommand cmd = new SqliteCommand("", conn);

            StringBuilder sql = new StringBuilder();
            sql.Append("INSERT INTO " + FormatName(tableName));

            if (fields.Count == 0)
            {
                sql.Append(" DEFAULT VALUES");
            }
            else
            {
                sql.Append(" (");
                sql.Append(string.Join(",", pairs.Select(f => f.Key)));
                sql.Append(") VALUES (");
                sql.Append(string.Join(",", pairs.Select((f, i) =>
                {
                    string key = "@V" + i;
                    cmd.Parameters.AddWithValue(key, f.Value);
                    return key;
                })));
                sql.Append("); SELECT LAST_INSERT_ID();");
            }

            cmd.CommandText = sql.ToString();

            return Convert.ToInt32((ulong)cmd.ExecuteScalar());
        }

        public static int Insert(this SqliteConnection conn, string tableName, Action<Dictionary<string, object>> fieldsFunc = null)
        {
            return Insert(conn, tableName, BuildParams(fieldsFunc));
        }

        public static int Update(this SqliteConnection conn, string tableName, string idName, int id, Dictionary<string, object> fields)
        {
            if (fields.Count == 0)
                return 1;

            SqliteCommand cmd = new SqliteCommand("", conn);

            StringBuilder sql = new StringBuilder();
            sql.Append("UPDATE " + FormatName(tableName) + " SET ");

            sql.Append(string.Join(",", fields.Select((f, i) =>
            {
                string key = "@V" + i;
                cmd.Parameters.AddWithValue(key, f.Value);
                return f.Key + "=" + key;
            })));

            sql.Append(" WHERE " + FormatName(idName) + " = @ID");
            cmd.Parameters.AddWithValue("@ID", id);

            cmd.CommandText = sql.ToString();

            return cmd.ExecuteNonQuery();
        }

        public static int Update(this SqliteConnection conn, string tableName, string idName, int id, Action<Dictionary<string, object>> fieldsFunc = null)
        {
            return Update(conn, tableName, idName, id, BuildParams(fieldsFunc));
        }

        public static int Delete(this SqliteConnection conn, string tableName, int id)
        {
            SqliteCommand cmd = new SqliteCommand("DELETE FROM " + FormatName(tableName) + " WHERE ID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", id);

            return cmd.ExecuteNonQuery();
        }

        private static Dictionary<string, object> BuildParams(Action<Dictionary<string, object>> paramFunc)
        {
            var param = new Dictionary<string, object>();

            if (paramFunc != null)
                paramFunc(param);

            return param;
        }
    }
}
