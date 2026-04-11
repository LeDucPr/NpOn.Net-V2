using System.Data;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Results;

public static class MssqlUtils
{
    public static object? NormalizeMssqlValue(object? value)
    {
        if (value == DBNull.Value) return null;
        return value;
    }

    public static (object? Value, SqlDbType SqlType) NormalizeForMssql(object? raw)
    {
        if (raw == null || raw == DBNull.Value)
            return (DBNull.Value, SqlDbType.Variant);

        // Standard vendor-provided type inference
        var p = new SqlParameter { Value = raw };
        var sqlDbType = p.SqlDbType;

        if (raw is DateTime dt)
        {
            // Force high-precision DateTime2 for modern MSSQL standards
            return (dt, SqlDbType.DateTime2);
        }

        return (raw, sqlDbType);
    }

    public static object? ConvertStringToMssqlType(object? value, SqlDbType sqlDbType)
    {
        if (value == null || value == DBNull.Value) return DBNull.Value;

        // Handle Enum mapping immediately
        if (value.GetType().IsEnum)
        {
            return Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
        }

        if (value is not string stringValue)
            return value;
            
        if (sqlDbType == SqlDbType.Variant)
            return value;

        var p = new SqlParameter { SqlDbType = sqlDbType };
        var dbType = p.DbType;

        Type targetType = dbType switch
        {
            DbType.Guid => typeof(Guid),
            DbType.Int32 => typeof(int),
            DbType.Int64 => typeof(long),
            DbType.Boolean => typeof(bool),
            DbType.DateTime => typeof(DateTime),
            DbType.DateTime2 => typeof(DateTime), 
            DbType.DateTimeOffset => typeof(DateTimeOffset),
            DbType.Decimal => typeof(decimal),
            DbType.Currency => typeof(decimal),
            DbType.Double => typeof(double),
            DbType.Single => typeof(float),
            DbType.Byte => typeof(byte),
            _ => typeof(string)
        };

        try
        {
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(targetType);
            return converter.CanConvertFrom(typeof(string))
                ? converter.ConvertFromString(stringValue)
                : Convert.ChangeType(stringValue, targetType);
        }
        catch
        {
            return value;
        }
    }

}