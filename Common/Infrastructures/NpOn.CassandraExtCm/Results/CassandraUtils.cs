using System.Data;
using System.Net;
using System.Numerics;
using Cassandra;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Results;

public static class CassandraUtils
{
    private static readonly Dictionary<Type, DbType> TypeMap = new()
    {
        { typeof(string), DbType.String },
        { typeof(char), DbType.StringFixedLength },
        { typeof(int), DbType.Int32 },
        { typeof(long), DbType.Int64 }, // CQL bigint / counter
        { typeof(short), DbType.Int16 }, // CQL smallint
        { typeof(sbyte), DbType.SByte }, // CQL tinyint (8-bit signed)
        { typeof(byte), DbType.Byte },
        { typeof(decimal), DbType.Decimal },
        { typeof(double), DbType.Double },
        { typeof(float), DbType.Single },
        { typeof(DateTime), DbType.DateTime },
        { typeof(DateTimeOffset), DbType.DateTimeOffset }, // CQL timestamp
        { typeof(TimeSpan), DbType.Time },
        { typeof(Guid), DbType.Guid }, // CQL uuid / timeuuid
        { typeof(bool), DbType.Boolean },
        { typeof(byte[]), DbType.Binary }, // CQL blob
        { typeof(BigInteger), DbType.VarNumeric }, // CQL varint
        { typeof(IPAddress), DbType.String }, // CQL inet
        { typeof(LocalDate), DbType.Date }, // CQL date
        { typeof(LocalTime), DbType.Time }, // CQL time
        { typeof(uint), DbType.UInt32 },
        { typeof(ulong), DbType.UInt64 },
        { typeof(ushort), DbType.UInt16 },
        { typeof(TimeUuid), DbType.Guid }, // CQL timeuuid
        { typeof(Duration), DbType.Object } // CQL duration
    };

    public static object? NormalizeCassandraValue(this object? value)
    {
        if (value == null || value == DBNull.Value) return null;

        return value switch
        {
            LocalDate ld => new DateTime(ld.Year, ld.Month, ld.Day),
            LocalTime lt => new TimeSpan(lt.TotalNanoseconds / 100),
            _ => value
        };
    }

    public static string GetCqlTypeName(this CqlColumn column) => column.TypeCode.ToString().ToLowerInvariant();

    public static DbType GetDbType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType.IsEnum)
            underlyingType = Enum.GetUnderlyingType(underlyingType);
        return TypeMap.GetValueOrDefault(underlyingType, DbType.Object);
    }

    public static ECassandraDbType GetECassandraDbType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType.IsEnum)
            underlyingType = Enum.GetUnderlyingType(underlyingType);

        if (underlyingType == typeof(string)) return ECassandraDbType.Text;
        if (underlyingType == typeof(int)) return ECassandraDbType.Int;
        if (underlyingType == typeof(long)) return ECassandraDbType.Bigint;
        if (underlyingType == typeof(short)) return ECassandraDbType.SmallInt;
        if (underlyingType == typeof(sbyte)) return ECassandraDbType.TinyInt;
        if (underlyingType == typeof(bool)) return ECassandraDbType.Boolean;
        if (underlyingType == typeof(decimal)) return ECassandraDbType.Decimal;
        if (underlyingType == typeof(double)) return ECassandraDbType.Double;
        if (underlyingType == typeof(float)) return ECassandraDbType.Float;
        if (underlyingType == typeof(Guid)) return ECassandraDbType.Uuid;
        if (underlyingType == typeof(DateTime)) return ECassandraDbType.Timestamp;
        if (underlyingType == typeof(DateTimeOffset)) return ECassandraDbType.Timestamp;
        if (underlyingType == typeof(LocalDate)) return ECassandraDbType.Date;
        if (underlyingType == typeof(LocalTime)) return ECassandraDbType.Time;
        if (underlyingType == typeof(TimeSpan)) return ECassandraDbType.Time;
        if (underlyingType == typeof(IPAddress)) return ECassandraDbType.Inet;
        if (underlyingType == typeof(BigInteger)) return ECassandraDbType.Varint;
        if (underlyingType == typeof(TimeUuid)) return ECassandraDbType.Timeuuid;
        if (underlyingType == typeof(Duration)) return ECassandraDbType.Duration;

        if (underlyingType.IsArray)
        {
            if (underlyingType == typeof(byte[])) return ECassandraDbType.Blob;
            return ECassandraDbType.List;
        }

        if (underlyingType.IsGenericType)
        {
            var genDef = underlyingType.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IList<>) || genDef == typeof(IEnumerable<>))
                return ECassandraDbType.List;
            if (genDef == typeof(Dictionary<,>) || genDef == typeof(IDictionary<,>))
                return ECassandraDbType.Map;
            if (genDef == typeof(HashSet<>) || genDef == typeof(ISet<>))
                return ECassandraDbType.Set;
            if (typeof(System.Runtime.CompilerServices.ITuple).IsAssignableFrom(underlyingType))
                return ECassandraDbType.Tuple;
        }

        return ECassandraDbType.Unknown;
    }

    public static (object? Value, ECassandraDbType? DbType) NormalizeForCassandra(object? raw)
    {
        if (raw == null || raw == DBNull.Value) return (null, null);

        var type = raw.GetType();
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType.IsEnum)
        {
            underlyingType = Enum.GetUnderlyingType(underlyingType);
            raw = Convert.ChangeType(raw, underlyingType);
        }

        var dbType = GetECassandraDbType(underlyingType);
        
        // Convert unsupported types to string or appropriate types before sending down to Cassandra wrapper
        if (raw is char c) raw = c.ToString();
        else if (raw is uint u) raw = (long)u;
        else if (raw is ulong ul) raw = (long)ul;
        else if (raw is ushort us) raw = (int)us;
        else if (raw is byte b) raw = (sbyte)b;
        
        return (raw, dbType != ECassandraDbType.Unknown ? dbType : null);
    }
}