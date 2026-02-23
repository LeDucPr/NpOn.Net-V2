using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

public static class PostgresUtils
{
    public static (DbType DbType, NpgsqlDbType NpgsqlDbType) GetPostgresTypes(object? value, Type type)
    {
        // NpgsqlParameter Driver
        var p = new NpgsqlParameter { Value = value ?? GetDefault(type) };
        return (p.DbType, p.NpgsqlDbType);
    }

    private static object? GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    public static (object? Value, NpgsqlDbType DbType) NormalizeForNpgsql(object? raw)
    {
        if (raw == null || raw == DBNull.Value)
            return (DBNull.Value, NpgsqlDbType.Unknown);

        // Inference NpgsqlParameter 
        // Driver -> FindDataTypeName 
        var p = new NpgsqlParameter { Value = raw };
        var npgsqlDbType = p.NpgsqlDbType;

        // Logic xử lý DateTime đặc thù cho Postgres 6.0+ (yêu cầu UTC cho Timestamptz)
        if (raw is DateTime dt)
        {
            // Npgsql 6.0+ mặc định: Kind.Utc -> TimestampTz, Kind.Unspecified/Local -> Timestamp
            // Nếu muốn ép tất cả về Timestamptz (múi giờ quốc tế)
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                return (DateTime.SpecifyKind(dt, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
            }

            if (dt.Kind == DateTimeKind.Local)
            {
                return (dt.ToUniversalTime(), NpgsqlDbType.TimestampTz);
            }

            // Đã là Utc
            return (dt, NpgsqlDbType.TimestampTz);
        }

        // Đối với Json/Jsonb: Nếu p.NpgsqlDbType vẫn trả về Text/Unknown, 
        // có thể check thêm type name của 'raw' (JObject, JsonDocument...)
        // Nhưng thường Npgsql đã tự handle được qua các Resolver Factory

        return (raw, npgsqlDbType);
    }
}