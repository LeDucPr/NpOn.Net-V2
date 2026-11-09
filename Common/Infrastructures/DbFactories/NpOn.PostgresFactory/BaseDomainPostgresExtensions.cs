using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonBaseDomain.Attributes;
using Common.Extensions.NpOn.HandleFlow.Attributes;
using Common.Infrastructures.NpOn.PostgresExtCm.Results;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;

public class DomainFieldMetadata
{
    public string ColumnName { get; set; } = string.Empty;
    public MemberInfo Member { get; set; } = null!;
    public Type MemberType { get; set; } = null!;
    public bool IsPrimaryKey { get; set; }
    public Func<object, object?> FastGetter { get; set; } = null!;
}

public class DomainEntityMetadata
{
    public Type EntityType { get; set; } = null!;
    public TableLoaderAttribute? TableLoader { get; set; }
    public string TableName => TableLoader?.TableName ?? string.Empty;
    public List<DomainFieldMetadata> Fields { get; set; } = [];
    public List<DomainFieldMetadata> PrimaryKeys { get; set; } = [];
}

public static class BaseDomainExtensions
{
    // Thread-safe metadata cache for BaseDomain entities (compiled once via IL Emit, reused across all threads)
    private static readonly ConcurrentDictionary<Type, DomainEntityMetadata> EntityMetadataCache = new();

    private static DomainEntityMetadata GetOrCreateMetadata(Type type)
    {
        return EntityMetadataCache.GetOrAdd(type, CompileEntityMetadata);
    }

    private static DomainEntityMetadata CompileEntityMetadata(Type type)
    {
        var meta = new DomainEntityMetadata
        {
            EntityType = type,
            TableLoader = type.GetCustomAttribute<TableLoaderAttribute>(true)
        };

        var fields = new List<DomainFieldMetadata>();

        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var fa = p.GetCustomAttributes(true).OfType<FieldAttribute>().FirstOrDefault();
            if (fa != null && !string.IsNullOrWhiteSpace(fa.FieldName))
            {
                var isPk = p.GetCustomAttributes(typeof(PkAttribute), true).Any();
                fields.Add(new DomainFieldMetadata
                {
                    ColumnName = fa.FieldName,
                    Member = p,
                    MemberType = p.PropertyType,
                    IsPrimaryKey = isPk,
                    FastGetter = CompilePropertyGetter(type, p)
                });
            }
        }

        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var fa = f.GetCustomAttributes(true).OfType<FieldAttribute>().FirstOrDefault();
            if (fa != null && !string.IsNullOrWhiteSpace(fa.FieldName))
            {
                var isPk = f.GetCustomAttributes(typeof(PkAttribute), true).Any();
                fields.Add(new DomainFieldMetadata
                {
                    ColumnName = fa.FieldName,
                    Member = f,
                    MemberType = f.FieldType,
                    IsPrimaryKey = isPk,
                    FastGetter = CompileFieldGetter(type, f)
                });
            }
        }

        meta.Fields = fields;
        meta.PrimaryKeys = fields.Where(f => f.IsPrimaryKey).ToList();

        return meta;
    }

    private static Func<object, object?> CompilePropertyGetter(Type type, PropertyInfo prop)
    {
        var getMethod = prop.GetGetMethod(true);
        if (getMethod == null)
        {
            return instance => prop.GetValue(instance);
        }

        var dm = new DynamicMethod(
            $"FastGet_{type.Name}_{prop.Name}",
            typeof(object),
            [typeof(object)],
            type.Module,
            true);

        var il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        if (type.IsValueType)
            il.Emit(OpCodes.Unbox_Any, type);
        else
            il.Emit(OpCodes.Castclass, type);

        il.Emit(OpCodes.Callvirt, getMethod);
        if (prop.PropertyType.IsValueType)
            il.Emit(OpCodes.Box, prop.PropertyType);

        il.Emit(OpCodes.Ret);
        return (Func<object, object?>)dm.CreateDelegate(typeof(Func<object, object?>));
    }

    private static Func<object, object?> CompileFieldGetter(Type type, FieldInfo field)
    {
        var dm = new DynamicMethod(
            $"FastGet_{type.Name}_{field.Name}",
            typeof(object),
            [typeof(object)],
            type.Module,
            true);

        var il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        if (type.IsValueType)
            il.Emit(OpCodes.Unbox_Any, type);
        else
            il.Emit(OpCodes.Castclass, type);

        il.Emit(OpCodes.Ldfld, field);
        if (field.FieldType.IsValueType)
            il.Emit(OpCodes.Box, field.FieldType);

        il.Emit(OpCodes.Ret);
        return (Func<object, object?>)dm.CreateDelegate(typeof(Func<object, object?>));
    }

    public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsInsert(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var meta = GetOrCreateMetadata(type);
        if (meta.TableLoader == null || string.IsNullOrEmpty(meta.TableName))
            throw new Exception("Invalid table attribute");

        if (meta.Fields.Count == 0)
            throw new Exception($"Type {type.Name} has no [Field] mapped members");

        var parameters = new List<NpgsqlParameter>(domains.Count * meta.Fields.Count);
        var sql = new StringBuilder(domains.Count * meta.Fields.Count * 20);

        for (int i = 0; i < domains.Count; i++)
        {
            var cols = new List<string>();
            var paramNames = new List<string>();

            foreach (var field in meta.Fields)
            {
                var raw = field.FastGetter(domains[i]);
                if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, field.MemberType)))
                {
                    continue;
                }

                var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(raw);
                string param = $"@p_{i}_{cols.Count}";
                cols.Add(field.ColumnName);
                paramNames.Add(param);

                var p = new NpgsqlParameter(param, paramValue ?? DBNull.Value);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                parameters.Add(p);
            }

            if (cols.Count == 0) continue;

            sql.Append(
                $"INSERT INTO {meta.TableName} ({string.Join(",", cols)}) VALUES ({string.Join(",", paramNames)});");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsUpdate(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var meta = GetOrCreateMetadata(type);
        if (meta.PrimaryKeys.Count == 0)
            throw new Exception($"Type {type.Name} has no primary key");

        var parameters = new List<NpgsqlParameter>(domains.Count * meta.Fields.Count);
        var sql = new StringBuilder(domains.Count * meta.Fields.Count * 20);

        for (int i = 0; i < domains.Count; i++)
        {
            var setClauses = new List<string>();

            foreach (var field in meta.Fields)
            {
                if (field.IsPrimaryKey) continue;

                var raw = field.FastGetter(domains[i]);
                if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, field.MemberType)))
                {
                    continue;
                }

                var (val, npgType) = PostgresUtils.NormalizeForNpgsql(raw);
                string param = $"@v_{i}_{field.ColumnName}";
                var p = new NpgsqlParameter(param, val ?? DBNull.Value);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                parameters.Add(p);

                setClauses.Add($"{field.ColumnName} = {param}");
            }

            if (setClauses.Count == 0) continue;

            var pkConditions = new List<string>();
            for (int j = 0; j < meta.PrimaryKeys.Count; j++)
            {
                var pkField = meta.PrimaryKeys[j];
                string pkParam = $"@pk_{i}_{j}";
                var pkVal = pkField.FastGetter(domains[i]);
                if (pkVal == null)
                    throw new Exception(
                        $"Primary key value for {pkField.ColumnName} cannot be null in domain at index {i}");

                var (pkValue, pkType) = PostgresUtils.NormalizeForNpgsql(pkVal);
                var pkParamObj = new NpgsqlParameter(pkParam, pkValue);
                if (pkType != NpgsqlDbType.Unknown) pkParamObj.NpgsqlDbType = pkType;
                parameters.Add(pkParamObj);
                pkConditions.Add($"{pkField.ColumnName} = {pkParam}");
            }

            sql.Append(
                $"UPDATE {meta.TableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", pkConditions)};");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsMerge(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var meta = GetOrCreateMetadata(type);
        if (meta.PrimaryKeys.Count == 0)
            throw new Exception($"Type {type.Name} has no primary key");

        if (string.IsNullOrEmpty(meta.TableName))
            throw new Exception($"Type {type.Name} is missing TableLoaderAttribute");

        var parameters = new List<NpgsqlParameter>(domains.Count * meta.Fields.Count);
        var sql = new StringBuilder(domains.Count * meta.Fields.Count * 20);

        for (int i = 0; i < domains.Count; i++)
        {
            var domain = domains[i];
            var cols = new List<string>();
            var paramNames = new List<string>();

            foreach (var field in meta.Fields)
            {
                var raw = field.FastGetter(domain);
                if (!isUseDefaultWhenNull && (raw == null || IsDefaultValue(raw, field.MemberType)))
                {
                    continue;
                }

                var (val, npgType) = PostgresUtils.NormalizeForNpgsql(raw);
                string param = $"@p_{i}_{field.ColumnName}";

                cols.Add(field.ColumnName);
                paramNames.Add(param);

                var p = new NpgsqlParameter(param, val ?? DBNull.Value);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                parameters.Add(p);
            }

            if (cols.Count == 0) continue;

            sql.Append($"INSERT INTO {meta.TableName} ({string.Join(",", cols)}) VALUES ({string.Join(",", paramNames)})");
            sql.Append($" ON CONFLICT ({string.Join(",", meta.PrimaryKeys.Select(m => m.ColumnName))}) DO UPDATE SET ");

            var updateSetClauses = meta.Fields
                .Where(m => !m.IsPrimaryKey && cols.Contains(m.ColumnName))
                .Select(m => $"{m.ColumnName} = EXCLUDED.{m.ColumnName}");
            sql.Append(string.Join(", ", updateSetClauses));
            sql.Append(';');
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsDelete(
        this List<BaseDomain> domains, bool isUseDefaultWhenNull = false)
    {
        if (domains == null || domains.Count == 0)
            throw new Exception("Empty domain list");

        var type = domains[0].GetType();
        var meta = GetOrCreateMetadata(type);
        if (meta.PrimaryKeys.Count == 0)
            throw new Exception($"Type {type.Name} has no primary key");

        var parameters = new List<NpgsqlParameter>(domains.Count * meta.PrimaryKeys.Count);
        var sql = new StringBuilder(domains.Count * 32);

        if (meta.PrimaryKeys.Count == 1)
        {
            var pkField = meta.PrimaryKeys[0];
            var pkParams = new List<string>(domains.Count);
            for (int i = 0; i < domains.Count; i++)
            {
                var raw = pkField.FastGetter(domains[i]);
                if (raw == null)
                {
                    if (isUseDefaultWhenNull)
                    {
                        raw = pkField.MemberType.IsValueType
                            ? Activator.CreateInstance(pkField.MemberType)!
                            : string.Empty;
                    }
                    else
                    {
                        continue;
                    }
                }

                string pkParam = $"@pk_{i}";
                pkParams.Add(pkParam);
                var (val, npgType) = PostgresUtils.NormalizeForNpgsql(raw);
                var p = new NpgsqlParameter(pkParam, val);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                parameters.Add(p);
            }

            if (pkParams.Count == 0)
                throw new Exception("No valid primary key conditions generated");

            sql.Append($"DELETE FROM {meta.TableName} WHERE {pkField.ColumnName} IN ({string.Join(", ", pkParams)});");
            return (sql.ToString(), parameters);
        }

        sql.Append($"DELETE FROM {meta.TableName} WHERE ");

        var whereClauses = new List<string>();
        for (int i = 0; i < domains.Count; i++)
        {
            var domain = domains[i];
            var pkConditions = new List<string>();

            for (int j = 0; j < meta.PrimaryKeys.Count; j++)
            {
                var pkField = meta.PrimaryKeys[j];
                string pkParam = $"@pk_{i}_{j}";
                var raw = pkField.FastGetter(domain);

                if (raw == null)
                {
                    if (isUseDefaultWhenNull)
                    {
                        object defaultVal = pkField.MemberType.IsValueType
                            ? Activator.CreateInstance(pkField.MemberType)!
                            : string.Empty;
                        raw = defaultVal;
                    }
                    else
                    {
                        continue;
                    }
                }

                var (val, npgType) = PostgresUtils.NormalizeForNpgsql(raw);
                var p = new NpgsqlParameter(pkParam, val);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                parameters.Add(p);
                pkConditions.Add($"{pkField.ColumnName} = {pkParam}");
            }

            if (pkConditions.Count > 0)
            {
                whereClauses.Add($"({string.Join(" AND ", pkConditions)})");
            }
        }

        if (whereClauses.Count == 0)
            throw new Exception("No valid primary key conditions generated");

        sql.Append(string.Join(" OR ", whereClauses));

        return (sql.ToString(), parameters);
    }

    private static bool IsDefaultValue(object value, Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (!underlyingType.IsValueType)
            return false;

        if (underlyingType == typeof(Guid) && value is Guid g) return g == Guid.Empty;
        if (underlyingType == typeof(DateTime) && value is DateTime d) return d == DateTime.MinValue;

        return false;
    }
}
