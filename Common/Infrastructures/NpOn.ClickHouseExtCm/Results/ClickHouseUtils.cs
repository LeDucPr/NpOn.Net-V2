using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using System.Data;
using ClickHouse.Client.ADO.Parameters;

namespace Common.Infrastructures.NpOn.ClickHouseExtCm.Results;

public static class ClickHouseUtils
{
    public static object? NormalizeClickHouseValue(object? value)
    {
        if (value == DBNull.Value) return null;
        return value;
    }



    public static DbType GetDbType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType == typeof(int)) return DbType.Int32;
        if (underlyingType == typeof(long)) return DbType.Int64;
        if (underlyingType == typeof(short)) return DbType.Int16;
        if (underlyingType == typeof(byte)) return DbType.Byte;
        if (underlyingType == typeof(bool)) return DbType.Boolean;
        if (underlyingType == typeof(DateTime)) return DbType.DateTime;
        if (underlyingType == typeof(decimal)) return DbType.Decimal;
        if (underlyingType == typeof(double)) return DbType.Double;
        if (underlyingType == typeof(float)) return DbType.Single;
        if (underlyingType == typeof(Guid)) return DbType.Guid;
        if (underlyingType == typeof(string)) return DbType.String;
        return DbType.Object;
    }

    public static object? ConvertToClickHouseType(object? value, EClickHouseDbType? clickHouseType)
    {
        if (value == null || value == DBNull.Value) return DBNull.Value;

        if (value.GetType().IsEnum)
        {
            return Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
        }

        if (value is not string stringValue)
            return value;

        if (clickHouseType == null || clickHouseType == EClickHouseDbType.Unknown)
            return value;

        Type targetType = clickHouseType.Value switch
        {
            EClickHouseDbType.Int8 or EClickHouseDbType.Int16 or EClickHouseDbType.Int32 => typeof(int),
            EClickHouseDbType.Int64 => typeof(long),
            EClickHouseDbType.UInt8 or EClickHouseDbType.UInt16 or EClickHouseDbType.UInt32 => typeof(uint),
            EClickHouseDbType.UInt64 => typeof(ulong),
            EClickHouseDbType.Float32 => typeof(float),
            EClickHouseDbType.Float64 => typeof(double),
            EClickHouseDbType.Decimal => typeof(decimal),
            EClickHouseDbType.String or EClickHouseDbType.FixedString => typeof(string),
            EClickHouseDbType.Date or EClickHouseDbType.Date32 or EClickHouseDbType.DateTime or EClickHouseDbType.DateTime64 => typeof(DateTime),
            EClickHouseDbType.UUID => typeof(Guid),
            EClickHouseDbType.IPv4 or EClickHouseDbType.IPv6 => typeof(string),
            EClickHouseDbType.Bool => typeof(bool),
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
