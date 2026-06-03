using System.Reflection;
using System.Text;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonBaseDomain.Attributes;
using Common.Extensions.NpOn.HandleFlow.Attributes;
using Common.Infrastructures.NpOn.Neo4jExtCm.Results;

namespace Common.Infrastructures.DbFactories.NpOn.Neo4jDbFactory;

public static class BaseDomainNeo4jExtensions
{
    public static (string CommandText, Dictionary<string, object?> Parameters) ToNeo4jParamsCreate
        (this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        if (!domains.TryGetSingleTableAttribute(out var tableLoader) || tableLoader == null)
            throw new Exception("Invalid table attribute");

        var type = domains[0].GetType();
        var mappedMembers = GetFieldMappedMembers(type);
        if (mappedMembers.Count == 0)
            throw new Exception($"Type {type.Name} has no [Field] mapped members");

        var parameters = new Dictionary<string, object?>();
        var cypher = new StringBuilder();

        var propsList = new List<Dictionary<string, object?>>();

        for (int i = 0; i < domains.Count; i++)
        {
            var props = new Dictionary<string, object?>();
            foreach (var (columnName, member, memberType) in mappedMembers)
            {
                var raw = GetMemberValue(member, domains[i]);
                if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, memberType)))
                    continue;

                props[columnName] = Neo4jUtils.NormalizeToCypherValue(raw);
            }
            if (props.Count > 0)
            {
                propsList.Add(props);
            }
        }

        if (propsList.Count == 0)
            return (string.Empty, parameters);

        string label = tableLoader.TableName;
        parameters.Add("props", propsList);
        
        cypher.Append($"UNWIND $props AS prop CREATE (n:`{label}`) SET n = prop;");

        return (cypher.ToString(), parameters);
    }

    public static (string CommandText, Dictionary<string, object?> Parameters) ToNeo4jParamsUpdate(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();
        
        if (!pkMembers.Any())
            throw new Exception($"Type {type.Name} has no primary key");

        var tableLoader = type.GetCustomAttribute<TableLoaderAttribute>();
        string label = tableLoader?.TableName ?? type.Name;
        
        var parameters = new Dictionary<string, object?>();
        var cypher = new StringBuilder();
        
        var updatesList = new List<Dictionary<string, object?>>();
        
        for (int i = 0; i < domains.Count; i++)
        {
            var updateItem = new Dictionary<string, object?>();
            var pks = new Dictionary<string, object?>();
            var props = new Dictionary<string, object?>();

            foreach (var (colName, member, memberType) in mappedMembers)
            {
                var raw = GetMemberValue(member, domains[i]);
                if (pkMembers.Any(pk => pk.ColumnName == colName))
                {
                    if (raw == null) throw new Exception("Primary key value cannot be null.");
                    pks[colName] = Neo4jUtils.NormalizeToCypherValue(raw);
                }
                else
                {
                    if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, memberType)))
                        continue;
                    props[colName] = Neo4jUtils.NormalizeToCypherValue(raw);
                }
            }

            if (props.Count > 0)
            {
                updateItem["pks"] = pks;
                updateItem["props"] = props;
                updatesList.Add(updateItem);
            }
        }

        if (updatesList.Count == 0) return (string.Empty, parameters);

        parameters.Add("updates", updatesList);
        
        var matchConditions = string.Join(" AND ", pkMembers.Select(pk => $"n.`{pk.ColumnName}` = update.pks.`{pk.ColumnName}`"));
        cypher.Append($"UNWIND $updates AS update MATCH (n:`{label}`) WHERE {matchConditions} SET n += update.props;");

        return (cypher.ToString(), parameters);
    }

    public static (string CommandText, Dictionary<string, object?> Parameters) ToNeo4jParamsMerge(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();
        
        if (!pkMembers.Any())
            throw new Exception($"Type {type.Name} has no primary key");

        var tableLoader = type.GetCustomAttribute<TableLoaderAttribute>();
        string label = tableLoader?.TableName ?? type.Name;

        var parameters = new Dictionary<string, object?>();
        var cypher = new StringBuilder();

        var mergesList = new List<Dictionary<string, object?>>();
        for (int i = 0; i < domains.Count; i++)
        {
            var pks = new Dictionary<string, object?>();
            var allProps = new Dictionary<string, object?>();

            foreach (var (colName, member, memberType) in mappedMembers)
            {
                var raw = GetMemberValue(member, domains[i]);
                if (pkMembers.Any(pk => pk.ColumnName == colName))
                {
                    pks[colName] = Neo4jUtils.NormalizeToCypherValue(raw);
                }
                else if (isUseDefaultWhenNull || (raw != null && !IsDefaultValue(raw, memberType)))
                {
                    allProps[colName] = Neo4jUtils.NormalizeToCypherValue(raw);
                }
            }

            mergesList.Add(new Dictionary<string, object?> { ["pks"] = pks, ["props"] = allProps });
        }

        if (mergesList.Count == 0) return (string.Empty, parameters);
        
        parameters.Add("merges", mergesList);

        var mergeProperties = string.Join(", ", pkMembers.Select(pk => $"`{pk.ColumnName}`: merge.pks.`{pk.ColumnName}`"));
        cypher.Append($"UNWIND $merges AS merge MERGE (n:`{label}` {{ {mergeProperties} }}) SET n += merge.props;");

        return (cypher.ToString(), parameters);
    }

    public static (string CommandText, Dictionary<string, object?> Parameters) ToNeo4jParamsDelete(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var mappedMembers = GetFieldMappedMembers(type);
        var pkMembers = mappedMembers.Where(m => m.Member.GetCustomAttributes(typeof(PkAttribute), true).Any()).ToList();

        if (!pkMembers.Any())
            throw new Exception($"Type {type.Name} has no primary key");

        var tableLoader = type.GetCustomAttribute<TableLoaderAttribute>();
        string label = tableLoader?.TableName ?? type.Name;

        var parameters = new Dictionary<string, object?>();
        var cypher = new StringBuilder();
        
        var deletesList = new List<Dictionary<string, object?>>();
        foreach (var domain in domains)
        {
            var pks = new Dictionary<string, object?>();
            foreach (var pk in pkMembers)
            {
                var raw = GetMemberValue(pk.Member, domain);
                if (raw != null)
                {
                    pks[pk.ColumnName] = Neo4jUtils.NormalizeToCypherValue(raw);
                }
            }
            if (pks.Count == pkMembers.Count)
            {
                deletesList.Add(pks);
            }
        }

        if (deletesList.Count == 0) return (string.Empty, parameters);

        parameters.Add("deletes", deletesList);
        
        var matchConditions = string.Join(" AND ", pkMembers.Select(pk => $"n.`{pk.ColumnName}` = item.`{pk.ColumnName}`"));
        cypher.Append($"UNWIND $deletes AS item MATCH (n:`{label}`) WHERE {matchConditions} DETACH DELETE n;");

        return (cypher.ToString(), parameters);
    }

    private static bool TryGetSingleTableAttribute(
        this IEnumerable<BaseDomain> domains,
        out TableLoaderAttribute? tableAttr)
    {
        tableAttr = null;
        var validDomains = domains.Where(x => x is BaseDomain).ToList();
        if (!validDomains.Any()) return false;

        var attrs = validDomains.Select(x => x.GetType().GetCustomAttributes(typeof(TableLoaderAttribute), true).FirstOrDefault() as TableLoaderAttribute).ToList();
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
