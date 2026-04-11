using System.Reflection;
using System.Text;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonBaseDomain.Attributes;
using Common.Extensions.NpOn.HandleFlow.Attributes;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;

public static class BaseDomainClickHouseExtensions
{
    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsInsert(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var tableAttr = type.GetCustomAttribute<TableLoaderAttribute>();
        if (tableAttr == null) throw new Exception("Invalid table attribute");

        var mappedMembers = GetFieldMappedMembers(type);
        if (mappedMembers.Count == 0)
            throw new Exception($"Type {type.Name} has no [Field] mapped members");

        var parameters = new List<INpOnDbCommandParam>();
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

                string paramName = $"p_{i}_{cols.Count}";
                cols.Add($"\"{columnName}\"");
                paramNames.Add($"@{paramName}");

                parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = paramName,
                    ParamValue = raw ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            if (cols.Count == 0) continue;

            sql.Append($"INSERT INTO {tableAttr.TableName} ({string.Join(",", cols)}) VALUES ({string.Join(",", paramNames)}); ");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsUpdate(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();
        if (!pkMembers.Any()) throw new Exception($"Type {type.Name} has no primary key");

        var parameters = new List<INpOnDbCommandParam>();
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

                string paramName = $"v_{i}_{colName.Replace(" ", "_")}";
                parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = paramName,
                    ParamValue = raw ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });

                setClauses.Add($"\"{colName}\" = @{paramName}");
            }

            if (setClauses.Count == 0) continue;

            var pkConditions = new List<string>();
            for (int j = 0; j < pkMembers.Count; j++)
            {
                var pkMember = pkMembers[j];
                string pkParamName = $"pk_{i}_{j}";
                var pkVal = GetMemberValue(pkMember.Member, domains[i]);
                if (pkVal == null) throw new Exception($"Primary key for {pkMember.ColumnName} cannot be null");

                parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = pkParamName,
                    ParamValue = pkVal,
                    ParamType = EClickHouseDbType.Unknown
                });
                pkConditions.Add($"\"{pkMember.ColumnName}\" = @{pkParamName}");
            }

            // ClickHouse Mutation Syntax
            sql.Append($"ALTER TABLE {tableAttr!.TableName} UPDATE {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", pkConditions)}; ");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsMerge(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        // For ClickHouse, Merge is implemented as Insert (Upsert behavior in ReplacingMergeTree etc.)
        return ToClickHouseParamsInsert(domains, isUseDefaultWhenNull);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsDelete(
        this List<BaseDomain> domains)
    {
        if (domains == null || domains.Count == 0) throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var tableAttr = type.GetCustomAttribute<TableLoaderAttribute>();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();

        if (!pkMembers.Any()) throw new Exception($"Type {type.Name} has no primary key");

        var parameters = new List<INpOnDbCommandParam>();
        var sql = new StringBuilder();

        foreach (var domain in domains)
        {
            var pkConditions = new List<string>();
            for (int j = 0; j < pkMembers.Count; j++)
            {
                var pkMember = pkMembers[j];
                string pkParamName = $"pk_{parameters.Count}";
                var raw = GetMemberValue(pkMember.Member, domain);
                if (raw == null) continue;

                parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = pkParamName,
                    ParamValue = raw,
                    ParamType = EClickHouseDbType.Unknown
                });
                pkConditions.Add($"\"{pkMember.ColumnName}\" = @{pkParamName}");
            }

            if (pkConditions.Any())
            {
                // ClickHouse Mutation Syntax
                sql.Append($"ALTER TABLE {tableAttr!.TableName} DELETE WHERE {string.Join(" AND ", pkConditions)}; ");
            }
        }

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
