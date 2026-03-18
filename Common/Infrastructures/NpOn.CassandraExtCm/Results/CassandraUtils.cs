using System.Data;
using System.Net;
using System.Numerics;
using Cassandra;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Results;

public static class CassandraUtils
{
    public const string CqlTypeNameBlob = "blob";

    /// <summary>
    /// Normalize value from Cassandra driver to .NET standard
    /// </summary>
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

    /// <summary>
    /// Get CQL Type Name from Column metadata
    /// </summary>
    public static string GetCqlTypeName(this CqlColumn column)
    {
        if (column == null) return CqlTypeNameBlob;
        
        return column.TypeCode.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Map System.Type to DbType for Cassandra
    /// </summary>
    public static DbType GetDbType(Type type)
    {
        if (type == typeof(string) || type == typeof(char)) return DbType.String;
        if (type == typeof(int)) return DbType.Int32;
        if (type == typeof(long)) return DbType.Int64;
        if (type == typeof(short)) return DbType.Int16;
        if (type == typeof(byte)) return DbType.Byte;
        if (type == typeof(decimal)) return DbType.Decimal;
        if (type == typeof(double)) return DbType.Double;
        if (type == typeof(float)) return DbType.Single;
        if (type == typeof(DateTime)) return DbType.DateTime;
        if (type == typeof(DateTimeOffset)) return DbType.DateTimeOffset;
        if (type == typeof(TimeSpan)) return DbType.Time;
        if (type == typeof(Guid)) return DbType.Guid;
        if (type == typeof(bool)) return DbType.Boolean;
        if (type == typeof(byte[])) return DbType.Binary;
        if (type == typeof(BigInteger)) return DbType.VarNumeric;
        if (type == typeof(IPAddress)) return DbType.String;

        return DbType.Object;
    }
}
