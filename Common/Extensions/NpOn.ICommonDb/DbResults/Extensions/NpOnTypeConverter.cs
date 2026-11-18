using System.Collections;
using System.Text.Json;

namespace Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;

public static class NpOnTypeConverter
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static object? ConvertDbValue(object? dbVal, Type targetType)
    {
        if (dbVal == null || dbVal is DBNull)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var srcType = dbVal.GetType();

        // Exact type match or assignable
        if (underlyingType.IsAssignableFrom(srcType))
        {
            return dbVal;
        }

        // Enum handling
        if (underlyingType.IsEnum)
        {
            if (dbVal is string enumStr)
                return Enum.Parse(underlyingType, enumStr, ignoreCase: true);

            var underlyingEnumType = Enum.GetUnderlyingType(underlyingType);
            var convertedNumber = Convert.ChangeType(dbVal, underlyingEnumType);
            return Enum.ToObject(underlyingType, convertedNumber);
        }

        // Guid
        if (underlyingType == typeof(Guid))
        {
            if (dbVal is string s) return Guid.Parse(s);
            if (dbVal is byte[] b && b.Length == 16) return new Guid(b);
        }

        // Date and Time types
        if (underlyingType == typeof(DateTime))
        {
            if (dbVal is DateTimeOffset dto) return dto.DateTime;
            if (dbVal is DateOnly dOnly) return dOnly.ToDateTime(TimeOnly.MinValue);
            return Convert.ToDateTime(dbVal);
        }

        if (underlyingType == typeof(DateTimeOffset))
        {
            if (dbVal is DateTime dt) return new DateTimeOffset(dt);
            if (dbVal is string s) return DateTimeOffset.Parse(s);
        }

        if (underlyingType == typeof(DateOnly))
        {
            if (dbVal is DateTime dt) return DateOnly.FromDateTime(dt);
            if (dbVal is string s) return DateOnly.Parse(s);
        }

        if (underlyingType == typeof(TimeOnly))
        {
            if (dbVal is TimeSpan ts) return TimeOnly.FromTimeSpan(ts);
            if (dbVal is string s) return TimeOnly.Parse(s);
        }

        if (underlyingType == typeof(TimeSpan))
        {
            if (dbVal is TimeSpan ts) return ts;
            if (dbVal is string s) return TimeSpan.Parse(s);
        }

        // Array mapping (T[])
        if (underlyingType.IsArray)
        {
            var elemType = underlyingType.GetElementType()!;
            if (dbVal is Array srcArray)
            {
                var newArr = Array.CreateInstance(elemType, srcArray.Length);
                for (int i = 0; i < srcArray.Length; i++)
                {
                    newArr.SetValue(ConvertDbValue(srcArray.GetValue(i), elemType), i);
                }
                return newArr;
            }

            if (dbVal is string jsonArrStr)
            {
                return JsonSerializer.Deserialize(jsonArrStr, underlyingType, DefaultJsonOptions);
            }
        }

        // Generic List / Collection mapping (List<T>, IList<T>, IReadOnlyList<T>)
        if (underlyingType.IsGenericType &&
            (underlyingType.GetGenericTypeDefinition() == typeof(List<>) ||
             underlyingType.GetGenericTypeDefinition() == typeof(IList<>) ||
             underlyingType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ||
             underlyingType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
        {
            var elemType = underlyingType.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elemType);
            var list = (IList)Activator.CreateInstance(listType)!;

            if (dbVal is Array srcArray)
            {
                for (int i = 0; i < srcArray.Length; i++)
                {
                    list.Add(ConvertDbValue(srcArray.GetValue(i), elemType));
                }
                return list;
            }

            if (dbVal is string jsonListStr)
            {
                return JsonSerializer.Deserialize(jsonListStr, listType, DefaultJsonOptions);
            }
        }

        // JSON / JSONB -> Complex Object / Record / Class
        if (underlyingType.IsClass && underlyingType != typeof(string))
        {
            if (dbVal is string jsonStr)
            {
                return JsonSerializer.Deserialize(jsonStr, underlyingType, DefaultJsonOptions);
            }

            if (dbVal is JsonDocument jsonDoc)
            {
                return jsonDoc.Deserialize(underlyingType, DefaultJsonOptions);
            }

            if (dbVal is JsonElement jsonElem)
            {
                return jsonElem.Deserialize(underlyingType, DefaultJsonOptions);
            }
        }

        // Primitive conversions
        try
        {
            return Convert.ChangeType(dbVal, underlyingType);
        }
        catch
        {
            return dbVal;
        }
    }
}

