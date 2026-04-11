using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Extensions.NpOn.CommonMode;

/// <summary>
/// Fast native JSON processing using System.Text.Json
/// </summary>
public static class NetJsonMode
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string ToJson(object? obj) 
        => obj == null ? string.Empty : JsonSerializer.Serialize(obj, Options);

    public static T? FromJson<T>(string? json) 
        => string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, Options);

    public static object? FromJson(string? json, Type type) 
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize(json, type, Options);
    
    public static bool TryFromJson<T>(string? json, out T? result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            result = JsonSerializer.Deserialize<T>(json, Options);
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryFromJson(string? json, Type type, out object? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            result = JsonSerializer.Deserialize(json, type, Options);
            return result != null;
        }
        catch
        {
            return false;
        }
    }
}
