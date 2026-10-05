using System.Globalization;
using System.Text;

namespace Common.Extensions.NpOn.CommonMode;

public static class DefaultValueMode
{
    public const int DefaultValueForEnumInt = 0;
    public const int DefaultValueForInt = 0;
    public const long DefaultValueForLong = 0;
}

public static class DefaultValueModeExtensions
{
    public static int EnumAsInt<TEnum>(this TEnum? value) where TEnum : struct, Enum
        => Convert.ToInt32(value);

    public static int EnumAsInt<TEnum>(this TEnum value) where TEnum : struct, Enum
        => Convert.ToInt32(value);

    public static long EnumAsLong<TEnum>(this TEnum value) where TEnum : struct, Enum
        => Convert.ToInt64(value);

    public static long EnumAsLong<TEnum>(this TEnum? value) where TEnum : struct, Enum
        => Convert.ToInt64(value);

    public static byte EnumAsByte<TEnum>(this TEnum? value) where TEnum : struct, Enum
        => Convert.ToByte(value);

    public static byte EnumAsByte<TEnum>(this TEnum value) where TEnum : struct, Enum
        => Convert.ToByte(value);

    
    public static int AsDefaultInt(this object? obj, int defaultValue = DefaultValueMode.DefaultValueForInt)
    {
        if (obj == null)
            return defaultValue;
        if (int.TryParse(obj.ToString(), out int result))
            return result;
        return defaultValue;
    }

    public static long AsDefaultLong(this object? obj, long defaultValue = DefaultValueMode.DefaultValueForLong)
    {
        if (obj == null)
            return defaultValue;
        if (long.TryParse(obj.ToString(), out long result))
            return result;
        return defaultValue;
    }

    public static int AsDefaultEnum<TEnum>(this object? obj, int defaultValue = DefaultValueMode.DefaultValueForEnumInt) where TEnum : struct, Enum
    {
        if (obj == null)
            return defaultValue;
        if (Enum.TryParse(obj.ToString(), out TEnum result))
            return (int)(object)result;
        return defaultValue;
    }

    public static Guid AsDefaultGuid(this object? obj, Guid defaultValue = default)
    {
        if (obj == null)
            return defaultValue;

        if (obj is Guid guid)
            return guid;

        if (Guid.TryParse(obj.ToString()?.Trim(), out var result))
            return result;

        return defaultValue;
    }

    public static string AsDefaultAscii(this object? obj, string defaultValue = "")
    {
        if (obj == null)
            return defaultValue;

        var input = obj.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(input))
            return defaultValue;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC);
        result = result.Replace('Đ', 'D').Replace('đ', 'd');

        return string.IsNullOrEmpty(result) ? defaultValue : result;
    }

    public static string AsDefaultString(this object? obj, string defaultValue = "")
    {
        if (obj == null)
            return defaultValue;

        string str = obj.ToString() ?? string.Empty;
        return string.IsNullOrEmpty(str) ? defaultValue : str;
    }

    public static string AsEmptyString(this object? obj, string defaultValue = "")
    {
        if (obj == null)
            return defaultValue;

        string str = obj.ToString()?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(str) ? defaultValue : str;
    }

    public static DateTime AsDefaultDateTime(this object? obj, DateTime? defaultValue = null)
    {
        DateTime fallback = defaultValue ?? DateTime.MinValue;
        if (obj == null)
            return fallback;
        if (DateTime.TryParse(obj.ToString(), out DateTime result))
            return result;
        return fallback;
    }

    public static bool AsDefaultBool(this object? obj, bool defaultValue = false)
    {
        if (obj == null)
            return defaultValue;
        if (bool.TryParse(obj.ToString(), out bool result))
            return result;
        return defaultValue;
    }

    // Convert to world standard
    public static DateTime AsDefaultStandardDateTime(this object? obj, DateTime? defaultValue = null)
    {
        DateTime fallback = defaultValue ?? DateTime.MinValue;
        if (obj == null)
            return fallback;
        if (DateTime.TryParse(obj.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal,
                out DateTime result)
           )
            return result;
        return fallback;
    }

    public static DateTime AsUtcDateTime(this object? obj, DateTime? defaultValue = null)
    {
        DateTime fallback = defaultValue ?? DateTime.MinValue;
        if (obj == null)
            return fallback;
        if (DateTime.TryParse(obj.ToString(), out DateTime result))
        {
            if (result.Kind == DateTimeKind.Unspecified)
                return DateTime.SpecifyKind(result, DateTimeKind.Utc);
            return result.ToUniversalTime();
        }

        return fallback;
    }
    
    
    
    public static string AsArrayJoin(this IEnumerable<string>? strings)
        => strings != null ? string.Join(",", strings) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<string>? strings, string separator)
        => strings != null ? string.Join(separator, strings) : string.Empty;

    // int
    public static string AsArrayJoin(this IEnumerable<int>? ints)
        => ints != null ? string.Join(",", ints) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<int>? ints, string separator)
        => ints != null ? string.Join(separator, ints) : string.Empty;

    // long
    public static string AsArrayJoin(this IEnumerable<long>? longs)
        => longs != null ? string.Join(",", longs) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<long>? longs, string separator)
        => longs != null ? string.Join(separator, longs) : string.Empty;

    // short
    public static string AsArrayJoin(this IEnumerable<short>? shorts)
        => shorts != null ? string.Join(",", shorts) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<short>? shorts, string separator)
        => shorts != null ? string.Join(separator, shorts) : string.Empty;

    // byte
    public static string AsArrayJoin(this IEnumerable<byte>? bytes)
        => bytes != null ? string.Join(",", bytes) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<byte>? bytes, string separator)
        => bytes != null ? string.Join(separator, bytes) : string.Empty;

    // bool
    public static string AsArrayJoin(this IEnumerable<bool>? bools)
        => bools != null ? string.Join(",", bools) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<bool>? bools, string separator)
        => bools != null ? string.Join(separator, bools) : string.Empty;

    // float
    public static string AsArrayJoin(this IEnumerable<float>? floats)
        => floats != null ? string.Join(",", floats) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<float>? floats, string separator)
        => floats != null ? string.Join(separator, floats) : string.Empty;

    // double
    public static string AsArrayJoin(this IEnumerable<double>? doubles)
        => doubles != null ? string.Join(",", doubles) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<double>? doubles, string separator)
        => doubles != null ? string.Join(separator, doubles) : string.Empty;

    // decimal
    public static string AsArrayJoin(this IEnumerable<decimal>? decimals)
        => decimals != null ? string.Join(",", decimals) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<decimal>? decimals, string separator)
        => decimals != null ? string.Join(separator, decimals) : string.Empty;

    // char
    public static string AsArrayJoin(this IEnumerable<char>? chars)
        => chars != null ? string.Join(",", chars) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<char>? chars, string separator)
        => chars != null ? string.Join(separator, chars) : string.Empty;

    // Guid
    public static string AsArrayJoin(this IEnumerable<Guid>? guids)
        => guids != null ? string.Join(",", guids) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<Guid>? guids, string separator)
        => guids != null ? string.Join(separator, guids) : string.Empty;

    // object
    public static string AsArrayJoin(this IEnumerable<object>? objs)
        => objs != null ? string.Join(",", objs.Select(x => x.ToString())) : string.Empty;

    public static string AsArrayJoin(this IEnumerable<object>? objs, string separator)
        => objs != null ? string.Join(separator, objs.Select(x => x.ToString())) : string.Empty;
}