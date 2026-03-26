using System.Reflection;
using System.Text;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonBaseDomain.Attributes;
using Common.Extensions.NpOn.HandleFlow.Attributes;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using System.Collections.Generic;
using System.Linq;
using System;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Infrastructures.DbFactories.NpOn.CassandraFactory;

public static class BaseDomainExtensions
{
    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToCassandraParamsInsert(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        if (!domains.TryGetSingleTableAttribute(out var tableLoader) || tableLoader == null)
            throw new Exception("Invalid table attribute");

        var type = domains[0].GetType();
        if (domains.Any(d => d.GetType() != type))
            throw new Exception("All domains must be of the same type");

        var mappedMembers = GetFieldMappedMembers(type);
        if (mappedMembers.Count == 0)
            throw new Exception($"Type {type.Name} has no [Field] mapped members");

        var parameters = new List<INpOnDbCommandParam>();
        var sql = new StringBuilder();

        // Use BATCH for multiple inserts
        if (domains.Count > 1) sql.Append("BEGIN BATCH ");

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

                string param = $"?";
                cols.Add(columnName);
                paramNames.Add(param);
                
                var (val, cassType) = Common.Infrastructures.NpOn.CassandraExtCm.Results.CassandraUtils.NormalizeForCassandra(raw);
                var p = new NpOnDbCommandParam<Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType>
                {
                    ParamName = $"p_{i}_{cols.Count}",
                    ParamValue = val ?? DBNull.Value,
                    ParamType = cassType ?? Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown
                };
                parameters.Add(p);
            }

            if (cols.Count == 0) continue;

            sql.Append($"INSERT INTO {tableLoader.TableName} ({string.Join(",", cols)}) VALUES ({string.Join(",", paramNames)}); ");
        }

        if (domains.Count > 1) sql.Append("APPLY BATCH;");

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToCassandraParamsUpdate(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();
        
        if (!pkMembers.Any())
            throw new Exception($"Type {type.Name} has no primary key");

        var tableName = type.GetCustomAttribute<TableLoaderAttribute>()?.TableName;
        var parameters = new List<INpOnDbCommandParam>();
        var sql = new StringBuilder();

        if (domains.Count > 1) sql.Append("BEGIN BATCH ");

        for (int i = 0; i < domains.Count; i++)
        {
            var setClauses = new List<string>();

            foreach (var (colName, member, memberType) in mappedMembers)
            {
                if (pkMembers.Any(pk => pk.ColumnName == colName))
                    continue;

                var raw = GetMemberValue(member, domains[i]);
                if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, memberType)))
                {
                    continue;
                }

                setClauses.Add($"{colName} = ?");
                var (val, cassType) = Common.Infrastructures.NpOn.CassandraExtCm.Results.CassandraUtils.NormalizeForCassandra(raw);
                parameters.Add(new NpOnDbCommandParam<Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType>
                {
                    ParamName = $"v_{i}_{colName}",
                    ParamValue = val ?? DBNull.Value,
                    ParamType = cassType ?? Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown
                });
            }

            if (setClauses.Count == 0) continue;

            var pkConditions = new List<string>();
            foreach (var pkMember in pkMembers)
            {
                var pkVal = GetMemberValue(pkMember.Member, domains[i]);
                if (pkVal == null)
                    throw new Exception($"Primary key value for {pkMember.ColumnName} cannot be null");

                var (pkValue, pkType) = Common.Infrastructures.NpOn.CassandraExtCm.Results.CassandraUtils.NormalizeForCassandra(pkVal);
                pkConditions.Add($"{pkMember.ColumnName} = ?");
                parameters.Add(new NpOnDbCommandParam<Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType>
                {
                    ParamName = $"pk_{i}_{pkMember.ColumnName}",
                    ParamValue = pkValue ?? DBNull.Value,
                    ParamType = pkType ?? Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown
                });
            }

            sql.Append($"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", pkConditions)}; ");
        }

        if (domains.Count > 1) sql.Append("APPLY BATCH;");

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToCassandraParamsMerge(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        // In Cassandra, INSERT behaves as UPSERT, so Merge is effectively identical to Insert in most cases
        // except when trying to update specific columns while leaving others intact (which requires UPDATE).
        // Since we don't have ON CONFLICT in Cassandra, we will just use INSERT.
        return ToCassandraParamsInsert(domains, isUseDefaultWhenNull);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToCassandraParamsDelete(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();

        if (!pkMembers.Any())
            throw new Exception($"Type {type.Name} has no primary key");

        var parameters = new List<INpOnDbCommandParam>();
        var sql = new StringBuilder();
        var tableName = type.GetCustomAttribute<TableLoaderAttribute>()?.TableName;

        if (domains.Count > 1) sql.Append("BEGIN BATCH ");

        for (int i = 0; i < domains.Count; i++)
        {
            var pkConditions = new List<string>();

            foreach (var pkMember in pkMembers)
            {
                var raw = GetMemberValue(pkMember.Member, domains[i]);

                if (raw == null)
                {
                    if (isUseDefaultWhenNull)
                    {
                        raw = pkMember.MemberType.IsValueType ? Activator.CreateInstance(pkMember.MemberType)! : string.Empty;
                    }
                    else
                    {
                        continue;
                    }
                }

                var (pkValue, pkType) = Common.Infrastructures.NpOn.CassandraExtCm.Results.CassandraUtils.NormalizeForCassandra(raw);
                pkConditions.Add($"{pkMember.ColumnName} = ?");
                parameters.Add(new NpOnDbCommandParam<Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType>
                {
                    ParamName = $"pk_{i}_{pkMember.ColumnName}",
                    ParamValue = pkValue ?? DBNull.Value,
                    ParamType = pkType ?? Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown
                });
            }

            if (pkConditions.Any())
            {
                sql.Append($"DELETE FROM {tableName} WHERE {string.Join(" AND ", pkConditions)}; ");
            }
        }

        if (domains.Count > 1) sql.Append("APPLY BATCH;");

        return (sql.ToString(), parameters);
    }

    private static bool TryGetSingleTableAttribute(
        this IEnumerable<BaseDomain> domains,
        out TableLoaderAttribute? tableAttr)
    {
        tableAttr = null;
        var validDomains = domains.Where(x => x is BaseDomain).ToList();
        if (!validDomains.Any()) return false;

        var attrs = validDomains
            .Select(x => x.GetType().GetCustomAttributes(typeof(TableLoaderAttribute), true).FirstOrDefault() as TableLoaderAttribute)
            .ToList();

        if (attrs.Any(a => a == null)) return false;

        var distinctTables = attrs.Select(a => a!.TableName).Distinct().ToList();
        if (distinctTables.Count != 1) return false;

        tableAttr = attrs.First();
        return true;
    }

    private static List<(string ColumnName, MemberInfo Member, Type MemberType)> GetFieldMappedMembers(Type type)
    {
        var list = new List<(string, MemberInfo, Type)>();

        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var fa = p.GetCustomAttributes(true).OfType<FieldAttribute>().FirstOrDefault();
            if (fa != null && !string.IsNullOrWhiteSpace(fa.FieldName))
                list.Add((fa.FieldName, p, p.PropertyType));
        }

        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var fa = f.GetCustomAttributes(true).OfType<FieldAttribute>().FirstOrDefault();
            if (fa != null && !string.IsNullOrWhiteSpace(fa.FieldName))
                list.Add((fa.FieldName, f, f.FieldType));
        }

        return list;
    }

    private static object? GetMemberValue(MemberInfo member, object instance)
    {
        return member switch
        {
            PropertyInfo pi => pi.GetValue(instance),
            FieldInfo fi => fi.GetValue(instance),
            _ => null
        };
    }

    private static bool IsDefaultValue(object value, Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (!underlyingType.IsValueType) return false;
        if (underlyingType == typeof(Guid) && value is Guid g) return g == Guid.Empty;
        if (underlyingType == typeof(DateTime) && value is DateTime d) return d == DateTime.MinValue;
        return false;
    }
}
