using System.Data;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.Neo4jExtCm.Results;

public static class Neo4jUtils
{
    public static object? NormalizeNeo4jValue(object? value)
    {
        if (value == null) return null;

        return value switch
        {
            Neo4j.Driver.INode node => NormalizeNode(node),
            Neo4j.Driver.IRelationship rel => NormalizeRelationship(rel),
            Neo4j.Driver.IPath path => NormalizePath(path),
            Neo4j.Driver.ZonedDateTime zdt => zdt.ToDateTimeOffset(),
            Neo4j.Driver.LocalDateTime ldt => new DateTime(ldt.Year, ldt.Month, ldt.Day,
                ldt.Hour, ldt.Minute, ldt.Second, ldt.Nanosecond / 1000000),
            Neo4j.Driver.LocalDate ld => new DateOnly(ld.Year, ld.Month, ld.Day),
            Neo4j.Driver.LocalTime lt => new TimeOnly(lt.Hour, lt.Minute, lt.Second,
                lt.Nanosecond / 1000000),
            Neo4j.Driver.Point point => new Dictionary<string, object?>
            {
                ["srid"] = point.SrId,
                ["x"] = point.X,
                ["y"] = point.Y,
                ["z"] = point.Z
            },
            Neo4j.Driver.Duration dur => TimeSpan.FromSeconds(
                dur.Seconds + dur.Months * 30.436875 * 86400 + dur.Days * 86400)
                + TimeSpan.FromTicks(dur.Nanos / 100),
            IList<object> list => list.Select(NormalizeNeo4jValue).ToList(),
            IDictionary<string, object> map => map.ToDictionary(kv => kv.Key, kv => NormalizeNeo4jValue(kv.Value)),
            _ => value
        };
    }

    private static Dictionary<string, object?> NormalizeNode(Neo4j.Driver.INode node)
    {
        var dict = new Dictionary<string, object?>();
        dict["_neo4j_element_id"] = node.ElementId;
        dict["_neo4j_labels"] = node.Labels.ToList();
        foreach (var prop in node.Properties)
        {
            dict[prop.Key] = NormalizeNeo4jValue(prop.Value);
        }
        return dict;
    }

    private static Dictionary<string, object?> NormalizeRelationship(Neo4j.Driver.IRelationship rel)
    {
        var dict = new Dictionary<string, object?>
        {
            ["_neo4j_element_id"] = rel.ElementId,
            ["_neo4j_type"] = rel.Type,
            ["_neo4j_start_element_id"] = rel.StartNodeElementId,
            ["_neo4j_end_element_id"] = rel.EndNodeElementId
        };
        foreach (var prop in rel.Properties)
        {
            dict[prop.Key] = NormalizeNeo4jValue(prop.Value);
        }
        return dict;
    }

    private static Dictionary<string, object?> NormalizePath(Neo4j.Driver.IPath path)
    {
        return new Dictionary<string, object?>
        {
            ["_neo4j_nodes"] = path.Nodes.Select(n => (object?)NormalizeNode(n)).ToList(),
            ["_neo4j_relationships"] = path.Relationships.Select(r => (object?)NormalizeRelationship(r)).ToList()
        };
    }

    public static object? NormalizeToCypherValue(object? raw)
    {
        if (raw == null || raw == DBNull.Value) return null;

        if (raw.GetType().IsEnum)
            return Convert.ChangeType(raw, Enum.GetUnderlyingType(raw.GetType()));

        if (raw is Guid g)
            return g.ToString();

        if (raw is DateTime dt)
            return dt.ToUniversalTime().ToString("O");

        if (raw is DateTimeOffset dto)
            return dto.ToString("O");

        return raw;
    }

    public static ENeo4jDbType GetENeo4jDbType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType.IsEnum)
            underlyingType = Enum.GetUnderlyingType(underlyingType);

        if (underlyingType == typeof(string)) return ENeo4jDbType.String;
        if (underlyingType == typeof(int) || underlyingType == typeof(long) || underlyingType == typeof(short)) return ENeo4jDbType.Integer;
        if (underlyingType == typeof(bool)) return ENeo4jDbType.Boolean;
        if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float)) return ENeo4jDbType.Float;
        if (underlyingType == typeof(byte[])) return ENeo4jDbType.Bytes;
        if (underlyingType == typeof(DateTime)) return ENeo4jDbType.LocalDateTime;
        if (underlyingType == typeof(DateTimeOffset)) return ENeo4jDbType.DateTime;
        if (underlyingType == typeof(DateOnly)) return ENeo4jDbType.Date;
        if (underlyingType == typeof(TimeOnly) || underlyingType == typeof(TimeSpan)) return ENeo4jDbType.Time;

        if (underlyingType.IsArray) return ENeo4jDbType.List;

        if (underlyingType.IsGenericType)
        {
            var genDef = underlyingType.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>) || genDef == typeof(IEnumerable<>))
                return ENeo4jDbType.List;
            if (genDef == typeof(Dictionary<,>) || genDef == typeof(IDictionary<,>))
                return ENeo4jDbType.Map;
        }

        return ENeo4jDbType.Unknown;
    }

    public static Type InferDotNetType(object? value)
    {
        if (value == null) return typeof(object);

        return value switch
        {
            bool => typeof(bool),
            int or long => typeof(long),
            float or double => typeof(double),
            string => typeof(string),
            Neo4j.Driver.INode => typeof(Dictionary<string, object?>),
            Neo4j.Driver.IRelationship => typeof(Dictionary<string, object?>),
            Neo4j.Driver.IPath => typeof(Dictionary<string, object?>),
            Neo4j.Driver.ZonedDateTime => typeof(DateTimeOffset),
            Neo4j.Driver.LocalDateTime => typeof(DateTime),
            Neo4j.Driver.LocalDate => typeof(DateOnly),
            Neo4j.Driver.LocalTime => typeof(TimeOnly),
            Neo4j.Driver.Point => typeof(Dictionary<string, object?>),
            IList<object> => typeof(List<object?>),
            IDictionary<string, object> => typeof(Dictionary<string, object?>),
            _ => value.GetType()
        };
    }

    private static readonly Dictionary<Type, DbType> TypeMap = new()
    {
        [typeof(string)] = DbType.String,
        [typeof(bool)] = DbType.Boolean,
        [typeof(int)] = DbType.Int32,
        [typeof(long)] = DbType.Int64,
        [typeof(float)] = DbType.Single,
        [typeof(double)] = DbType.Double,
        [typeof(decimal)] = DbType.Decimal,
        [typeof(DateTime)] = DbType.DateTime,
        [typeof(DateTimeOffset)] = DbType.DateTimeOffset,
        [typeof(DateOnly)] = DbType.Date,
        [typeof(TimeOnly)] = DbType.Time,
        [typeof(Guid)] = DbType.Guid,
        [typeof(byte[])] = DbType.Binary,
        [typeof(object)] = DbType.Object,
    };

    public static DbType ToDbType(this Type type)
    {
        var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;
        return nonNullableType.IsEnum ? DbType.Int32 : TypeMap.GetValueOrDefault(nonNullableType, DbType.Object);
    }
}
