using System.Data;
using System.Reflection;
using System.Text;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonBaseDomain.Attributes;
using Common.Extensions.NpOn.HandleFlow.Attributes;
using Common.Infrastructures.NpOn.MssqlExtCm.Results;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.DbFactories.NpOn.MssqlFactory;

public static class BaseDomainMssqlExtensions
{
    public static (string CommandText, List<SqlParameter> Parameters) ToMssqlParamsInsert
        (this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var tableAttr = type.GetCustomAttribute<TableLoaderAttribute>();
        if (tableAttr == null) throw new Exception("Invalid table attribute");

        var mappedMembers = GetFieldMappedMembers(type);
        if (mappedMembers.Count == 0)
            throw new Exception($"Type {type.Name} has no [Field] mapped members");

        var parameters = new List<SqlParameter>();
        var sql = new StringBuilder();

        for (int i = 0; i < domains.Count; i++)
        {
            var cols = new List<string>();
            var paramNames = new List<string>();

            foreach (var (columnName, member, memberType) in mappedMembers)
            {
                var raw = GetMemberValue(member, domains[i]);
                if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, memberType)))
                {
                    continue;
                }

                var (paramValue, sqlDbType) = MssqlUtils.NormalizeForMssql(raw);
                string param = $"@p_{i}_{cols.Count}";
                cols.Add($"[{columnName}]");
                paramNames.Add(param);

                var p = new SqlParameter(param, paramValue ?? DBNull.Value);
                if (sqlDbType != SqlDbType.Variant) p.SqlDbType = sqlDbType;
                parameters.Add(p);
            }

            if (cols.Count == 0) continue;

            sql.Append($"INSERT INTO [{tableAttr.TableName}] ({string.Join(",", cols)}) VALUES ({string.Join(",", paramNames)});");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<SqlParameter> Parameters) ToMssqlParamsUpdate(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();
        if (!pkMembers.Any()) throw new Exception($"Type {type.Name} has no primary key");

        var parameters = new List<SqlParameter>();
        var sql = new StringBuilder();
        var tableAttr = type.GetCustomAttribute<TableLoaderAttribute>();

        for (int i = 0; i < domains.Count; i++)
        {
            var setClauses = new List<string>();

            foreach (var (colName, member, memberType) in mappedMembers)
            {
                if (pkMembers.Any(pk => pk.ColumnName == colName)) continue;

                var raw = GetMemberValue(member, domains[i]);
                if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, memberType))) continue;

                var (val, sqlType) = MssqlUtils.NormalizeForMssql(raw);
                string param = $"@v_{i}_{colName.Replace(" ", "_")}";
                var p = new SqlParameter(param, val ?? DBNull.Value);
                if (sqlType != SqlDbType.Variant) p.SqlDbType = sqlType;
                parameters.Add(p);

                setClauses.Add($"[{colName}] = {param}");
            }

            if (setClauses.Count == 0) continue;

            var pkConditions = new List<string>();
            for (int j = 0; j < pkMembers.Count; j++)
            {
                var pkMember = pkMembers[j];
                string pkParam = $"@pk_{i}_{j}";
                var pkVal = GetMemberValue(pkMember.Member, domains[i]);
                if (pkVal == null) throw new Exception($"Primary key for {pkMember.ColumnName} cannot be null");

                var (pkValue, pkType) = MssqlUtils.NormalizeForMssql(pkVal);
                var p = new SqlParameter(pkParam, pkValue);
                if (pkType != SqlDbType.Variant) p.SqlDbType = pkType;
                parameters.Add(p);
                pkConditions.Add($"[{pkMember.ColumnName}] = {pkParam}");
            }

            sql.Append($"UPDATE [{tableAttr!.TableName}] SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", pkConditions)};");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<SqlParameter> Parameters) ToMssqlParamsMerge(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0) throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var tableAttr = type.GetCustomAttribute<TableLoaderAttribute>();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();
        
        var parameters = new List<SqlParameter>();
        var sql = new StringBuilder();

        for (int i = 0; i < domains.Count; i++)
        {
            var allCols = new List<string>();
            var allParams = new List<string>();
            var updateSets = new List<string>();
            var matchConditions = new List<string>();

            foreach (var (colName, member, memberType) in mappedMembers)
            {
                var raw = GetMemberValue(member, domains[i]);
                if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, memberType))) continue;

                var (val, sqlType) = MssqlUtils.NormalizeForMssql(raw);
                string param = $"@m_{i}_{colName.Replace(" ", "_")}";
                var p = new SqlParameter(param, val ?? DBNull.Value);
                if (sqlType != SqlDbType.Variant) p.SqlDbType = sqlType;
                parameters.Add(p);

                allCols.Add($"[{colName}]");
                allParams.Add(param);

                if (pkMembers.Any(pk => pk.ColumnName == colName))
                    matchConditions.Add($"target.[{colName}] = {param}");
                else
                    updateSets.Add($"target.[{colName}] = {param}");
            }

            if (matchConditions.Count == 0) continue;

            sql.AppendLine($"MERGE INTO [{tableAttr!.TableName}] AS target");
            sql.AppendLine($"USING (SELECT 1 as dual) AS source ON ({string.Join(" AND ", matchConditions)})");
            if (updateSets.Count > 0)
            {
                sql.AppendLine($"WHEN MATCHED THEN UPDATE SET {string.Join(", ", updateSets)}");
            }
            sql.AppendLine($"WHEN NOT MATCHED THEN INSERT ({string.Join(",", allCols)}) VALUES ({string.Join(",", allParams)});");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<SqlParameter> Parameters) ToMssqlParamsDelete(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0) throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var tableAttr = type.GetCustomAttribute<TableLoaderAttribute>();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();

        var parameters = new List<SqlParameter>();
        var sql = new StringBuilder();
        sql.Append($"DELETE FROM [{tableAttr!.TableName}] WHERE ");

        var orClauses = new List<string>();
        for (int i = 0; i < domains.Count; i++)
        {
            var pkConditions = new List<string>();
            for (int j = 0; j < pkMembers.Count; j++)
            {
                var pkMember = pkMembers[j];
                string pkParam = $"@d_{i}_{j}";
                var raw = GetMemberValue(pkMember.Member, domains[i]);
                if (raw == null) continue;

                var (val, sqlType) = MssqlUtils.NormalizeForMssql(raw);
                var p = new SqlParameter(pkParam, val);
                if (sqlType != SqlDbType.Variant) p.SqlDbType = sqlType;
                parameters.Add(p);
                pkConditions.Add($"[{pkMember.ColumnName}] = {pkParam}");
            }
            if (pkConditions.Any()) orClauses.Add($"({string.Join(" AND ", pkConditions)})");
        }

        sql.Append(string.Join(" OR ", orClauses));
        return (sql.ToString(), parameters);
    }

    private static List<(string ColumnName, MemberInfo Member, Type MemberType)> GetFieldMappedMembers(Type type)
    {
        var list = new List<(string, MemberInfo, Type)>();
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var fa = p.GetCustomAttributes(true).OfType<FieldAttribute>().FirstOrDefault();
            if (fa != null && !string.IsNullOrWhiteSpace(fa.FieldName)) list.Add((fa.FieldName, p, p.PropertyType));
        }
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var fa = f.GetCustomAttributes(true).OfType<FieldAttribute>().FirstOrDefault();
            if (fa != null && !string.IsNullOrWhiteSpace(fa.FieldName)) list.Add((fa.FieldName, f, f.FieldType));
        }
        return list;
    }

    private static object? GetMemberValue(MemberInfo member, object instance) => member switch { PropertyInfo pi => pi.GetValue(instance), FieldInfo fi => fi.GetValue(instance), _ => null };

    private static bool IsDefaultValue(object value, Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (!underlyingType.IsValueType) return false;
        if (underlyingType == typeof(Guid) && value is Guid g) return g == Guid.Empty;
        if (underlyingType == typeof(DateTime) && value is DateTime d) return d == DateTime.MinValue;
        return false;
    }
}
