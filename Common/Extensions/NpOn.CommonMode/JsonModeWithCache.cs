using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Common.Extensions.NpOn.CommonMode;

public static class JsonModeWithCache
{
    private static readonly WrapperCacheStore<Type, Delegate> SerializerCache = new();
    private static readonly WrapperCacheStore<Type, Delegate> SerializerAsNullCache = new();
    private static readonly WrapperCacheStore<Type, Delegate> DeserializerCache = new();

    public static string ToJson(object? obj)
    {
        if (obj == null)
            return string.Empty;
        var type = obj.GetType();

        // Fast paths for primitives or arrays
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum || type.IsArray || type.IsGenericType)
            return JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        var serializer = (Func<object, string>)SerializerCache.GetOrAdd(type, t => CreateSerializer(t, false));
        return serializer(obj);
    }

    public static string? ToJsonAsNull(object? obj)
    {
        if (obj == null)
            return null;
        var type = obj.GetType();

        // Fast paths for primitives or arrays
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum || type.IsArray || type.IsGenericType)
            return JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });

        var serializer = (Func<object, string>)SerializerAsNullCache.GetOrAdd(type, t => CreateSerializer(t, true));
        return serializer(obj);
    }

    public static T? FromJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        var type = typeof(T);

        if (type.IsPrimitive || type == typeof(string) || type.IsEnum || type.IsArray || type.IsGenericType)
            return JsonConvert.DeserializeObject<T>(json);

        var deserializer = (Func<string, object>)DeserializerCache.GetOrAdd(type, t => CreateDeserializer(t));
        return (T?)deserializer(json);
    }

    public static object? FromJson(string? json, Type type)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        if (type.IsPrimitive || type == typeof(string) || type.IsEnum || type.IsArray || type.IsGenericType)
            return JsonConvert.DeserializeObject(json, type);

        var deserializer = (Func<string, object>)DeserializerCache.GetOrAdd(type, t => CreateDeserializer(t));
        return deserializer(json);
    }

    public static bool TryFromJson<T>(string? json, out T? result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            result = FromJson<T>(json);
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
            result = FromJson(json, type);
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    private static Func<object, string> CreateSerializer(Type type, bool includeNulls)
    {
        var method = new DynamicMethod($"Serialize_{type.Name}_{(includeNulls ? "Null" : "NoNull")}", typeof(string),
            [typeof(object)], typeof(JsonModeWithCache).Module, true);
        var il = method.GetILGenerator();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanRead)
            .Where(p => !p.CustomAttributes.Any(attr =>
                attr.AttributeType.Name == nameof( /*Newtonsoft.Json.*/JsonIgnoreAttribute) ||
                attr.AttributeType.Name == nameof(System.Text.Json.Serialization.JsonIgnoreAttribute)))
            .ToArray();
        var sbConstructor = typeof(StringBuilder).GetConstructor(Type.EmptyTypes);
        var sbAppendString = typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append), [typeof(string)]);
        var sbToString = typeof(StringBuilder).GetMethod(nameof(StringBuilder.ToString), Type.EmptyTypes);
        var serializeObject = typeof(JsonConvert).GetMethod(nameof(JsonConvert.SerializeObject), [typeof(object)]);

        var sbLocal = il.DeclareLocal(typeof(StringBuilder));
        var typedLocal = il.DeclareLocal(type);
        var valueLocal = il.DeclareLocal(typeof(object));
        var isFirstLocal = il.DeclareLocal(typeof(bool));

        if (sbConstructor != null) il.Emit(OpCodes.Newobj, sbConstructor);
        il.Emit(OpCodes.Stloc, sbLocal);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, type);
        il.Emit(OpCodes.Stloc, typedLocal);

        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stloc, isFirstLocal);

        il.Emit(OpCodes.Ldloc, sbLocal);
        il.Emit(OpCodes.Ldstr, "{");
        if (sbAppendString != null) il.Emit(OpCodes.Callvirt, sbAppendString);
        il.Emit(OpCodes.Pop);

        for (int i = 0; i < properties.Length; i++)
        {
            var prop = properties[i];
            var getMethod = prop.GetGetMethod();
            if (getMethod == null) continue;

            var skipPropertyLabel = il.DefineLabel();

            il.Emit(OpCodes.Ldloc, typedLocal);
            il.Emit(OpCodes.Callvirt, getMethod);

            if (prop.PropertyType.IsValueType)
            {
                il.Emit(OpCodes.Box, prop.PropertyType);
            }

            il.Emit(OpCodes.Stloc, valueLocal);

            if (!includeNulls)
            {
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Brfalse, skipPropertyLabel);
            }

            var notFirstLabel = il.DefineLabel();
            var appendNameLabel = il.DefineLabel();

            // if (!isFirstLocal) sb.Append(",");
            il.Emit(OpCodes.Ldloc, isFirstLocal);
            il.Emit(OpCodes.Brfalse, notFirstLabel);

            // isFirstLocal = false;
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc, isFirstLocal);
            il.Emit(OpCodes.Br, appendNameLabel);

            il.MarkLabel(notFirstLabel);
            il.Emit(OpCodes.Ldloc, sbLocal);
            il.Emit(OpCodes.Ldstr, ",");
            if (sbAppendString != null) il.Emit(OpCodes.Callvirt, sbAppendString);
            il.Emit(OpCodes.Pop);

            il.MarkLabel(appendNameLabel);
            il.Emit(OpCodes.Ldloc, sbLocal);
            il.Emit(OpCodes.Ldstr, $"\"{prop.Name}\":");
            if (sbAppendString != null) il.Emit(OpCodes.Callvirt, sbAppendString);
            il.Emit(OpCodes.Pop);

            il.Emit(OpCodes.Ldloc, valueLocal);
            if (serializeObject != null) il.Emit(OpCodes.Call, serializeObject);

            var tempStrLocal = il.DeclareLocal(typeof(string));
            il.Emit(OpCodes.Stloc, tempStrLocal);
            il.Emit(OpCodes.Ldloc, sbLocal);
            il.Emit(OpCodes.Ldloc, tempStrLocal);
            if (sbAppendString != null) il.Emit(OpCodes.Callvirt, sbAppendString);
            il.Emit(OpCodes.Pop);

            il.MarkLabel(skipPropertyLabel);
        }

        il.Emit(OpCodes.Ldloc, sbLocal);
        il.Emit(OpCodes.Ldstr, "}");
        if (sbAppendString != null) il.Emit(OpCodes.Callvirt, sbAppendString);
        il.Emit(OpCodes.Pop);

        il.Emit(OpCodes.Ldloc, sbLocal);
        if (sbToString != null) il.Emit(OpCodes.Callvirt, sbToString);
        il.Emit(OpCodes.Ret);

        return (Func<object, string>)method.CreateDelegate(typeof(Func<object, string>));
    }

    private static Func<string, object> CreateDeserializer(Type type)
    {
        var method = new DynamicMethod($"Deserialize_{type.Name}", typeof(object), [typeof(string)],
            typeof(JsonModeWithCache).Module, true);
        var il = method.GetILGenerator();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanWrite)
            .Where(p => !p.CustomAttributes.Any(attr =>
                attr.AttributeType.Name == nameof( /*Newtonsoft.Json.*/JsonIgnoreAttribute) ||
                attr.AttributeType.Name == nameof(System.Text.Json.Serialization.JsonIgnoreAttribute)))
            .ToArray();

        var jObjectParse = typeof(JObject).GetMethod(nameof(JObject.Parse), [typeof(string)]);
        var jTokenIndexer = typeof(JObject).GetProperty("Item", typeof(JToken), [typeof(string)])?.GetGetMethod();
        var toObjectMethod = typeof(JToken).GetMethod(nameof(JToken.ToObject), Type.EmptyTypes);

        var ctor = type.GetConstructor(Type.EmptyTypes);
        if (ctor == null)
        {
            // Fallback for types without parameterless constructor
            var defaultDeserialize =
                typeof(JsonConvert).GetMethod(nameof(JsonConvert.DeserializeObject), [typeof(string), typeof(Type)]);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldtoken, type);
            var getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));
            if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);
            if (defaultDeserialize != null) il.Emit(OpCodes.Call, defaultDeserialize);
            il.Emit(OpCodes.Ret);
            return (Func<string, object>)method.CreateDelegate(typeof(Func<string, object>));
        }

        var instanceLocal = il.DeclareLocal(type);
        var jObjLocal = il.DeclareLocal(typeof(JObject));
        var jTokenLocal = il.DeclareLocal(typeof(JToken));

        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Stloc, instanceLocal);

        il.Emit(OpCodes.Ldarg_0);
        if (jObjectParse != null) il.Emit(OpCodes.Call, jObjectParse);
        il.Emit(OpCodes.Stloc, jObjLocal);

        foreach (var prop in properties)
        {
            var endOfPropLabel = il.DefineLabel();

            il.Emit(OpCodes.Ldloc, jObjLocal);
            il.Emit(OpCodes.Ldstr, prop.Name);
            if (jTokenIndexer != null) il.Emit(OpCodes.Callvirt, jTokenIndexer);
            il.Emit(OpCodes.Stloc, jTokenLocal);

            il.Emit(OpCodes.Ldloc, jTokenLocal);
            il.Emit(OpCodes.Brfalse, endOfPropLabel); // If JToken is null, skip assignment

            il.Emit(OpCodes.Ldloc, instanceLocal);
            il.Emit(OpCodes.Ldloc, jTokenLocal);

            var genericToObject = toObjectMethod?.MakeGenericMethod(prop.PropertyType);
            if (genericToObject != null) il.Emit(OpCodes.Callvirt, genericToObject);

            var setMethod = prop.GetSetMethod();
            if (setMethod != null) il.Emit(OpCodes.Callvirt, setMethod);

            il.MarkLabel(endOfPropLabel);
        }

        il.Emit(OpCodes.Ldloc, instanceLocal);
        il.Emit(OpCodes.Box, type);
        il.Emit(OpCodes.Ret);

        return (Func<string, object>)method.CreateDelegate(typeof(Func<string, object>));
    }
}