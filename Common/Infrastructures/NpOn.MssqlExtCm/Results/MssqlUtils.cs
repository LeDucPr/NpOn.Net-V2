using System.Data;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Results;

public static class MssqlUtils
{
    private static readonly Dictionary<Type, DbType> TypeMap = new()
    {
        // (String Types)
        [typeof(string)] = DbType.String,
        [typeof(char[])] = DbType.StringFixedLength,
        [typeof(char)] = DbType.StringFixedLength,
        // (Integer Types) 
        [typeof(byte)] = DbType.Byte,
        [typeof(sbyte)] = DbType.SByte,
        [typeof(short)] = DbType.Int16,
        [typeof(ushort)] = DbType.UInt16,
        [typeof(int)] = DbType.Int32,
        [typeof(uint)] = DbType.UInt32,
        [typeof(long)] = DbType.Int64,
        [typeof(ulong)] = DbType.UInt64,
        // (Floating-Point & Currency Types) 
        [typeof(float)] = DbType.Single,
        [typeof(double)] = DbType.Double,
        [typeof(decimal)] = DbType.Decimal,
        // Date & Time
        [typeof(DateTime)] = DbType.DateTime,
        [typeof(DateTimeOffset)] = DbType.DateTimeOffset,
        [typeof(TimeSpan)] = DbType.Time,
        [typeof(DateOnly)] = DbType.Date,
        [typeof(TimeOnly)] = DbType.Time,
        // (Logical & Identifier Types) 
        [typeof(bool)] = DbType.Boolean,
        [typeof(Guid)] = DbType.Guid,
        // (Binary & Special Types) 
        [typeof(byte[])] = DbType.Binary,
        [typeof(object)] = DbType.Object,
        // XML 
        [typeof(System.Xml.Linq.XDocument)] = DbType.Xml,
        [typeof(System.Xml.XmlDocument)] = DbType.Xml,
    };

    public static DbType ToDbType(this Type type)
    {
        var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;
        if (nonNullableType.IsEnum)
        {
            return DbType.Int32;
        }
        return TypeMap.GetValueOrDefault(nonNullableType, DbType.Object);
    }

    public static object? NormalizeMssqlValue(object? value)
    {
        if (value == DBNull.Value) return null;
        return value;
    }

    public static (object? Value, SqlDbType? DbType) NormalizeForMssql(object? raw)
    {
        if (raw == null || raw == DBNull.Value)
            return (DBNull.Value, SqlDbType.Variant);

        var p = new SqlParameter { Value = raw };
        var sqlDbType = p.SqlDbType;

        // DateTime handling
        if (raw is DateTime dt)
        {
            return (dt, SqlDbType.DateTime2);
        }

        return (raw, sqlDbType);
    }
}