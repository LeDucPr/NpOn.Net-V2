using System.Data;
using System.Net;
using System.Numerics;
using Cassandra;

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
        { typeof(ushort), DbType.UInt16 }
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

    public static Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType GetECassandraDbType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType.IsEnum)
            underlyingType = Enum.GetUnderlyingType(underlyingType);

        if (underlyingType == typeof(string)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Text;
        if (underlyingType == typeof(int)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Int;
        if (underlyingType == typeof(long)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Bigint;
        if (underlyingType == typeof(short)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.SmallInt;
        if (underlyingType == typeof(sbyte)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.TinyInt;
        if (underlyingType == typeof(byte[])) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Blob;
        if (underlyingType == typeof(bool)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Boolean;
        if (underlyingType == typeof(decimal)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Decimal;
        if (underlyingType == typeof(double)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Double;
        if (underlyingType == typeof(float)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Float;
        if (underlyingType == typeof(Guid)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Uuid;
        if (underlyingType == typeof(DateTime)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Timestamp;
        if (underlyingType == typeof(DateTimeOffset)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Timestamp;
        if (underlyingType == typeof(LocalDate)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Date;
        if (underlyingType == typeof(LocalTime)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Time;
        if (underlyingType == typeof(TimeSpan)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Time;
        if (underlyingType == typeof(IPAddress)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Inet;
        if (underlyingType == typeof(BigInteger)) return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Varint;

        return Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown;
    }

    public static (object? Value, Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType? DbType) NormalizeForCassandra(object? raw)
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
        
        return (raw, dbType != Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown ? dbType : null);
    }
}