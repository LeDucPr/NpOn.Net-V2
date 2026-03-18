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
}