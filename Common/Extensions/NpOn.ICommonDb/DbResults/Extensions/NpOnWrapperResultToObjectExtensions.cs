using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.DbResults.Blocks;
using Common.Extensions.NpOn.ICommonDb.DbResults.Grpc;

namespace Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;

public static partial class NpOnWrapperResultExtensions
{
    // Cache for compiled mapper delegates. Key is the Type of the target object.
    private static readonly WrapperCacheStore<Type, Delegate> MapperCache = new();

    // Cache for ultra-fast direct array mappers. Key is (Type.FullName + SchemaFingerprint).
    private static readonly ConcurrentDictionary<string, Delegate> DirectMapperCache = new();

    // Cache for contiguous 1D flat array mappers. Key is (Type.FullName + SchemaFingerprint).
    private static readonly ConcurrentDictionary<string, Delegate> FlatMapperCache = new();

    // Cache for vectorized columnar page mappers. Key is (Type.FullName + SchemaFingerprint).
    private static readonly ConcurrentDictionary<string, Delegate> PageMapperCache = new();

    #region Private IL Generation and Caching

    private static Dictionary<string, string> ResolveFieldMap<T>() where T : class
    {
        var objectType = typeof(T);
        try
        {
            var temp = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(objectType);
            if (temp is NpOnBaseGrpcObject grpcObj)
            {
                grpcObj.CreateDefaultFieldMapper();
                if (grpcObj.FieldMap is { Count: > 0 } map)
                    return map;
            }

            var fieldMapProp = objectType.GetProperty("FieldMap", BindingFlags.Public | BindingFlags.Instance);
            if (fieldMapProp?.GetValue(temp) is Dictionary<string, string> customMap && customMap.Count > 0)
                return customMap;
        }
        catch { }

        return objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p.Name);
    }

    private static bool TryFindColumnIndex(
        string propertyName,
        string mappedColumnName,
        IReadOnlyDictionary<string, int> nameToIndexMap,
        out int colIndex)
    {
        if (nameToIndexMap.TryGetValue(mappedColumnName, out colIndex))
            return true;

        if (nameToIndexMap.TryGetValue(propertyName, out colIndex))
            return true;

        var snake = ToSnakeCase(propertyName);
        if (nameToIndexMap.TryGetValue(snake, out colIndex))
            return true;

        foreach (var kv in nameToIndexMap)
        {
            if (string.Equals(kv.Key, mappedColumnName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kv.Key, propertyName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kv.Key, snake, StringComparison.OrdinalIgnoreCase))
            {
                colIndex = kv.Value;
                return true;
            }
        }

        colIndex = -1;
        return false;
    }

    private static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        var sb = new System.Text.StringBuilder(str.Length + 5);
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && str[i - 1] != '_') sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static Func<NpOnDataPage, int, T> GetOrCreatePageMapper<T>(IReadOnlyDictionary<string, int> nameToIndexMap, IReadOnlyList<INpOnBlock> blocks) where T : class
    {
        var schemaKey = $"{typeof(T).FullName}#Page#" + string.Join(";", nameToIndexMap.OrderBy(k => k.Value).Select(k => $"{k.Key}:{k.Value}:{blocks[k.Value].ClrType.Name}"));
        return (Func<NpOnDataPage, int, T>)PageMapperCache.GetOrAdd(schemaKey, _ => CreatePageObjectMapper<T>(nameToIndexMap, blocks));
    }

    private static Func<NpOnDataPage, int, T> CreatePageObjectMapper<T>(IReadOnlyDictionary<string, int> nameToIndexMap, IReadOnlyList<INpOnBlock> blocks) where T : class
    {
        var objectType = typeof(T);
        var dynamicMethod = new DynamicMethod(
            $"PageObjectMapper_{objectType.Name}_{Guid.NewGuid():N}",
            objectType,
            [typeof(NpOnDataPage), typeof(int)], // arg0: page, arg1: rowIndex
            objectType.Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var constructor = objectType.GetConstructor(Type.EmptyTypes) ??
                          throw new InvalidOperationException($"Type {objectType.Name} must have a parameterless constructor.");

        var fieldMap = ResolveFieldMap<T>();

        var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        var instanceLocal = il.DeclareLocal(objectType);
        var blockLocal = il.DeclareLocal(typeof(INpOnBlock));

        // T instance = new T();
        il.Emit(OpCodes.Newobj, constructor);
        il.Emit(OpCodes.Stloc, instanceLocal);

        var getBlocksMethod = typeof(NpOnDataPage).GetProperty(nameof(NpOnDataPage.Blocks))!.GetMethod!;
        var getBlockItemMethod = typeof(IReadOnlyList<INpOnBlock>).GetMethod("get_Item")!;

        foreach (var (propertyName, columnName) in fieldMap)
        {
            if (!properties.TryGetValue(propertyName, out var propInfo)) continue;
            if (!TryFindColumnIndex(propertyName, columnName, nameToIndexMap, out var colIndex)) continue;
            if (colIndex < 0 || colIndex >= blocks.Count) continue;

            var block = blocks[colIndex];
            var blockType = block.GetType();
            var skipLabel = il.DefineLabel();

            // INpOnBlock block = page.Blocks[colIndex];
            il.Emit(OpCodes.Ldarg_0); // page
            il.Emit(OpCodes.Callvirt, getBlocksMethod);
            il.Emit(OpCodes.Ldc_I4, colIndex);
            il.Emit(OpCodes.Callvirt, getBlockItemMethod);
            il.Emit(OpCodes.Castclass, blockType);
            il.Emit(OpCodes.Stloc, blockLocal);

            // if (block.IsNull(rowIndex)) goto skipLabel;
            il.Emit(OpCodes.Ldloc, blockLocal);
            il.Emit(OpCodes.Ldarg_1); // rowIndex
            il.Emit(OpCodes.Callvirt, blockType.GetMethod(nameof(INpOnBlock.IsNull))!);
            il.Emit(OpCodes.Brtrue, skipLabel);

            // instance.Property = ...
            il.Emit(OpCodes.Ldloc, instanceLocal);
            il.Emit(OpCodes.Ldloc, blockLocal);

            var propType = propInfo.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;

            if (blockType == typeof(Int64Block))
            {
                var bufferField = typeof(Int64Block).GetField(nameof(Int64Block.Buffer))!;
                il.Emit(OpCodes.Ldfld, bufferField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldelem_I8);
                if (Nullable.GetUnderlyingType(propType) != null)
                {
                    var nullableCtor = propType.GetConstructor([typeof(long)])!;
                    il.Emit(OpCodes.Newobj, nullableCtor);
                }
            }
            else if (blockType == typeof(Int32Block))
            {
                var bufferField = typeof(Int32Block).GetField(nameof(Int32Block.Buffer))!;
                il.Emit(OpCodes.Ldfld, bufferField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldelem_I4);
                if (Nullable.GetUnderlyingType(propType) != null)
                {
                    var nullableCtor = propType.GetConstructor([underlyingType])!;
                    il.Emit(OpCodes.Newobj, nullableCtor);
                }
            }
            else if (blockType == typeof(DoubleBlock))
            {
                var bufferField = typeof(DoubleBlock).GetField(nameof(DoubleBlock.Buffer))!;
                il.Emit(OpCodes.Ldfld, bufferField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldelem_R8);
                if (Nullable.GetUnderlyingType(propType) != null)
                {
                    var nullableCtor = propType.GetConstructor([typeof(double)])!;
                    il.Emit(OpCodes.Newobj, nullableCtor);
                }
            }
            else if (blockType == typeof(GuidBlock))
            {
                var bufferField = typeof(GuidBlock).GetField(nameof(GuidBlock.Buffer))!;
                il.Emit(OpCodes.Ldfld, bufferField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldelem, typeof(Guid));
                if (Nullable.GetUnderlyingType(propType) != null)
                {
                    var nullableCtor = propType.GetConstructor([typeof(Guid)])!;
                    il.Emit(OpCodes.Newobj, nullableCtor);
                }
            }
            else if (blockType == typeof(DateTimeBlock))
            {
                var bufferField = typeof(DateTimeBlock).GetField(nameof(DateTimeBlock.Buffer))!;
                il.Emit(OpCodes.Ldfld, bufferField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldelem, typeof(DateTime));
                if (Nullable.GetUnderlyingType(propType) != null)
                {
                    var nullableCtor = propType.GetConstructor([typeof(DateTime)])!;
                    il.Emit(OpCodes.Newobj, nullableCtor);
                }
            }
            else if (blockType == typeof(BooleanBlock))
            {
                var bufferField = typeof(BooleanBlock).GetField(nameof(BooleanBlock.Buffer))!;
                il.Emit(OpCodes.Ldfld, bufferField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldelem_U1);
                if (Nullable.GetUnderlyingType(propType) != null)
                {
                    var nullableCtor = propType.GetConstructor([typeof(bool)])!;
                    il.Emit(OpCodes.Newobj, nullableCtor);
                }
            }
            else if (blockType == typeof(StringBlock))
            {
                var bufferField = typeof(StringBlock).GetField(nameof(StringBlock.Buffer))!;
                il.Emit(OpCodes.Ldfld, bufferField);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldelem_Ref);
            }
            else // ObjectBlock or fallback
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Callvirt, typeof(INpOnBlock).GetMethod(nameof(INpOnBlock.GetObject))!);
                if (propType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, propType);
                else
                    il.Emit(OpCodes.Castclass, propType);
            }

            il.Emit(OpCodes.Callvirt, propInfo.SetMethod!);
            il.MarkLabel(skipLabel);
        }

        il.Emit(OpCodes.Ldloc, instanceLocal);
        il.Emit(OpCodes.Ret);

        return (Func<NpOnDataPage, int, T>)dynamicMethod.CreateDelegate(typeof(Func<NpOnDataPage, int, T>));
    }

    private static Func<object?[], int, T> GetOrCreateFlatMapper<T>(IReadOnlyDictionary<string, int> nameToIndexMap) where T : class
    {
        var schemaKey = $"{typeof(T).FullName}#Flat#" + string.Join(";", nameToIndexMap.OrderBy(k => k.Value).Select(k => $"{k.Key}:{k.Value}"));
        return (Func<object?[], int, T>)FlatMapperCache.GetOrAdd(schemaKey, _ => CreateFlatObjectMapper<T>(nameToIndexMap));
    }

    private static Func<object?[], int, T> CreateFlatObjectMapper<T>(IReadOnlyDictionary<string, int> nameToIndexMap) where T : class
    {
        var objectType = typeof(T);
        var dynamicMethod = new DynamicMethod(
            $"FlatObjectMapper_{objectType.Name}_{Guid.NewGuid():N}",
            objectType,
            [typeof(object?[]), typeof(int)], // arg0: flatData, arg1: baseOffset
            objectType.Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var constructor = objectType.GetConstructor(Type.EmptyTypes) ??
                          throw new InvalidOperationException($"Type {objectType.Name} must have a parameterless constructor.");

        var fieldMap = ResolveFieldMap<T>();

        var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        var changeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;
        var enumToObjectMethod = typeof(Enum).GetMethod(nameof(Enum.ToObject), [typeof(Type), typeof(object)])!;
        var getTypeFromHandleMethod = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)])!;

        var instanceLocal = il.DeclareLocal(objectType);
        var valueLocal = il.DeclareLocal(typeof(object));

        // T instance = new T();
        il.Emit(OpCodes.Newobj, constructor);
        il.Emit(OpCodes.Stloc, instanceLocal);

        foreach (var (propertyName, columnName) in fieldMap)
        {
            if (!properties.TryGetValue(propertyName, out var propInfo)) continue;
            if (!TryFindColumnIndex(propertyName, columnName, nameToIndexMap, out var colIndex)) continue;

            var endOfPropertyLabel = il.DefineLabel();

            // object value = arg0[baseOffset + colIndex];
            il.Emit(OpCodes.Ldarg_0); // flatData
            il.Emit(OpCodes.Ldarg_1); // baseOffset
            il.Emit(OpCodes.Ldc_I4, colIndex);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Stloc, valueLocal);

            // if (value == null) goto end;
            il.Emit(OpCodes.Ldloc, valueLocal);
            il.Emit(OpCodes.Brfalse, endOfPropertyLabel);

            // if (value is DBNull) goto end;
            il.Emit(OpCodes.Ldloc, valueLocal);
            il.Emit(OpCodes.Isinst, typeof(DBNull));
            il.Emit(OpCodes.Brtrue, endOfPropertyLabel);

            // Load instance for setter
            il.Emit(OpCodes.Ldloc, instanceLocal);

            var targetType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

            if (targetType.IsEnum)
            {
                il.Emit(OpCodes.Ldtoken, targetType);
                il.Emit(OpCodes.Call, getTypeFromHandleMethod);
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Call, enumToObjectMethod);
                il.Emit(OpCodes.Unbox_Any, propInfo.PropertyType);
            }
            else if (targetType == typeof(Guid) || targetType == typeof(int) || targetType == typeof(long) ||
                     targetType == typeof(bool) || targetType == typeof(DateTime) || targetType == typeof(short) ||
                     targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal))
            {
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Unbox_Any, propInfo.PropertyType);
            }
            else if (targetType == typeof(string))
            {
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Castclass, typeof(string));
            }
            else
            {
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Ldtoken, targetType);
                il.Emit(OpCodes.Call, getTypeFromHandleMethod);
                il.Emit(OpCodes.Call, changeTypeMethod);
                il.Emit(OpCodes.Unbox_Any, propInfo.PropertyType);
            }

            il.Emit(OpCodes.Callvirt, propInfo.GetSetMethod()!);

            il.MarkLabel(endOfPropertyLabel);
        }

        il.Emit(OpCodes.Ldloc, instanceLocal);
        il.Emit(OpCodes.Ret);

        return (Func<object?[], int, T>)dynamicMethod.CreateDelegate(typeof(Func<object?[], int, T>));
    }

    private static Func<object[], T> GetOrCreateDirectMapper<T>(IReadOnlyDictionary<string, int> nameToIndexMap) where T : class
    {
        var schemaKey = $"{typeof(T).FullName}#" + string.Join(";", nameToIndexMap.OrderBy(k => k.Value).Select(k => $"{k.Key}:{k.Value}"));
        return (Func<object[], T>)DirectMapperCache.GetOrAdd(schemaKey, _ => CreateDirectObjectMapper<T>(nameToIndexMap));
    }

    private static Func<object[], T> CreateDirectObjectMapper<T>(IReadOnlyDictionary<string, int> nameToIndexMap) where T : class
    {
        var objectType = typeof(T);
        var dynamicMethod = new DynamicMethod(
            $"DirectObjectMapper_{objectType.Name}_{Guid.NewGuid():N}",
            objectType,
            [typeof(object[])],
            objectType.Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var constructor = objectType.GetConstructor(Type.EmptyTypes) ??
                          throw new InvalidOperationException($"Type {objectType.Name} must have a parameterless constructor.");

        var fieldMap = ResolveFieldMap<T>();

        var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        var changeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;
        var enumToObjectMethod = typeof(Enum).GetMethod(nameof(Enum.ToObject), [typeof(Type), typeof(object)])!;
        var getTypeFromHandleMethod = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)])!;

        var instanceLocal = il.DeclareLocal(objectType);
        var valueLocal = il.DeclareLocal(typeof(object));

        // T instance = new T();
        il.Emit(OpCodes.Newobj, constructor);
        il.Emit(OpCodes.Stloc, instanceLocal);

        foreach (var (propertyName, columnName) in fieldMap)
        {
            if (!properties.TryGetValue(propertyName, out var propInfo)) continue;
            if (!TryFindColumnIndex(propertyName, columnName, nameToIndexMap, out var colIndex)) continue;

            var endOfPropertyLabel = il.DefineLabel();

            // object value = arg0[colIndex];
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, colIndex);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Stloc, valueLocal);

            // if (value == null) goto end;
            il.Emit(OpCodes.Ldloc, valueLocal);
            il.Emit(OpCodes.Brfalse, endOfPropertyLabel);

            // if (value is DBNull) goto end;
            il.Emit(OpCodes.Ldloc, valueLocal);
            il.Emit(OpCodes.Isinst, typeof(DBNull));
            il.Emit(OpCodes.Brtrue, endOfPropertyLabel);

            // Load instance for setter
            il.Emit(OpCodes.Ldloc, instanceLocal);

            var targetType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

            if (targetType.IsEnum)
            {
                il.Emit(OpCodes.Ldtoken, targetType);
                il.Emit(OpCodes.Call, getTypeFromHandleMethod);
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Call, enumToObjectMethod);
                il.Emit(OpCodes.Unbox_Any, propInfo.PropertyType);
            }
            else if (targetType == typeof(Guid) || targetType == typeof(int) || targetType == typeof(long) ||
                     targetType == typeof(bool) || targetType == typeof(DateTime) || targetType == typeof(short) ||
                     targetType == typeof(double) || targetType == typeof(float) || targetType == typeof(decimal))
            {
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Unbox_Any, propInfo.PropertyType);
            }
            else if (targetType == typeof(string))
            {
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Castclass, typeof(string));
            }
            else
            {
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Ldtoken, targetType);
                il.Emit(OpCodes.Call, getTypeFromHandleMethod);
                il.Emit(OpCodes.Call, changeTypeMethod);
                il.Emit(OpCodes.Unbox_Any, propInfo.PropertyType);
            }

            il.Emit(OpCodes.Callvirt, propInfo.GetSetMethod()!);

            il.MarkLabel(endOfPropertyLabel);
        }

        il.Emit(OpCodes.Ldloc, instanceLocal);
        il.Emit(OpCodes.Ret);

        return (Func<object[], T>)dynamicMethod.CreateDelegate(typeof(Func<object[], T>));
    }

    /// <summary>
    /// Gets a cached mapper function or creates a new one if it doesn't exist.
    /// </summary>
    private static Func<INpOnRowWrapper, T> GetOrCreateMapper<T>() where T : class
    {
        return (Func<INpOnRowWrapper, T>)MapperCache.GetOrAdd(typeof(T), _ => CreateObjectMapper<T>());
    }

    /// <summary>
    /// Creates a high-performance object mapper function using IL-Emit.
    /// </summary>
    private static Func<INpOnRowWrapper, T> CreateObjectMapper<T>() where T : class
    {
        var objectType = typeof(T);
        var dynamicMethod = new DynamicMethod(
            $"DynamicObjectMapper_{objectType.Name}",
            objectType,
            [typeof(INpOnRowWrapper)],
            objectType.Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var constructor = objectType.GetConstructor(Type.EmptyTypes) ??
                          throw new InvalidOperationException($"Type {objectType.Name} must have a parameterless constructor.");

        var fieldMap = ResolveFieldMap<T>();

        var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        var getRowWrapperMethod = typeof(INpOnRowWrapper).GetMethod(nameof(INpOnRowWrapper.GetRowWrapper))!;
        var cellsTryGetValueMethod = typeof(IReadOnlyDictionary<string, INpOnCell>).GetMethod("TryGetValue")!;
        var cellValueGetter = typeof(INpOnCell).GetProperty(nameof(INpOnCell.ValueAsObject))!.GetGetMethod()!;
        var changeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;
        var enumToObjectMethod = typeof(Enum).GetMethod(nameof(Enum.ToObject), [typeof(Type), typeof(object)])!;
        var getTypeFromHandleMethod = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)])!;

        var instanceLocal = il.DeclareLocal(objectType);
        var cellsLocal = il.DeclareLocal(typeof(IReadOnlyDictionary<string, INpOnCell>));
        var cellLocal = il.DeclareLocal(typeof(INpOnCell));
        var valueLocal = il.DeclareLocal(typeof(object));

        // T instance = new T();
        il.Emit(OpCodes.Newobj, constructor);
        il.Emit(OpCodes.Stloc, instanceLocal);

        // var cells = rowWrapper.GetRowWrapper();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, getRowWrapperMethod);
        il.Emit(OpCodes.Stloc, cellsLocal);

        foreach (var (propertyName, columnName) in fieldMap)
        {
            if (!properties.TryGetValue(propertyName, out var propInfo)) continue;

            var endOfPropertyLabel = il.DefineLabel();

            // if (cells.TryGetValue(columnName, out cell))
            il.Emit(OpCodes.Ldloc, cellsLocal);
            il.Emit(OpCodes.Ldstr, columnName);
            il.Emit(OpCodes.Ldloca_S, cellLocal);
            il.Emit(OpCodes.Callvirt, cellsTryGetValueMethod);
            il.Emit(OpCodes.Brfalse, endOfPropertyLabel);

            // object value = cell.ValueAsObject;
            il.Emit(OpCodes.Ldloc, cellLocal);
            il.Emit(OpCodes.Callvirt, cellValueGetter);
            il.Emit(OpCodes.Stloc, valueLocal);

            // if (value != null)
            il.Emit(OpCodes.Ldloc, valueLocal);
            il.Emit(OpCodes.Brfalse, endOfPropertyLabel);

            // Load instance to set property
            il.Emit(OpCodes.Ldloc, instanceLocal);

            var targetType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

            if (targetType.IsEnum)
            {
                il.Emit(OpCodes.Ldtoken, targetType);
                il.Emit(OpCodes.Call, getTypeFromHandleMethod);
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Call, enumToObjectMethod);
                il.Emit(OpCodes.Unbox_Any, propInfo.PropertyType);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Ldtoken, targetType);
                il.Emit(OpCodes.Call, getTypeFromHandleMethod);
                il.Emit(OpCodes.Call, changeTypeMethod);
                il.Emit(OpCodes.Unbox_Any, propInfo.PropertyType);
            }

            il.Emit(OpCodes.Callvirt, propInfo.GetSetMethod()!);

            il.MarkLabel(endOfPropertyLabel);
        }

        il.Emit(OpCodes.Ldloc, instanceLocal);
        il.Emit(OpCodes.Ret);

        return (Func<INpOnRowWrapper, T>)dynamicMethod.CreateDelegate(typeof(Func<INpOnRowWrapper, T>));
    }

    #endregion

    #region Public Extensions

    public static T? ToFirstOrDefault<T>(this INpOnWrapperResult? result) where T : class
    {
        if (result == null) return null;

        // Vectorized Columnar Page Path: Zero-Boxing Direct Primitive Access
        if (result is INpOnTableWrapper { Pages: { Count: > 0 } pages, NameToIndexMap: { } pageNameMap })
        {
            if (pages[0].RowCount == 0) return null;
            var pageMapper = GetOrCreatePageMapper<T>(pageNameMap, pages[0].Blocks);
            return pageMapper(pages[0], 0);
        }

        // Ultra Zero-Copy Flat Path
        if (result is INpOnTableWrapper { FlatData: { } flatData, NameToIndexMap: { } nameToIndexMap } flatTable)
        {
            if (flatTable.RowCount == 0) return null;
            var flatMapper = GetOrCreateFlatMapper<T>(nameToIndexMap);
            return flatMapper(flatData, 0);
        }

        if (result is INpOnTableWrapper { NameToIndexMap: { } nameToIndexMap2, RawData: { } rawData })
        {
            if (rawData.Count == 0) return null;
            var directMapper = GetOrCreateDirectMapper<T>(nameToIndexMap2);
            return directMapper(rawData[0]);
        }

        if (result is not INpOnTableWrapper tableWrapper || tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return null;
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null);
        if (firstRow == null)
            return null;
        var mapper = GetOrCreateMapper<T>();
        return mapper(firstRow);
    }

    public static T? ToLastOrDefault<T>(this INpOnWrapperResult? result) where T : class
    {
        if (result == null) return null;

        // Vectorized Columnar Page Path
        if (result is INpOnTableWrapper { Pages: { Count: > 0 } pages, NameToIndexMap: { } pageNameMap })
        {
            var lastPage = pages[^1];
            if (lastPage.RowCount == 0) return null;
            var pageMapper = GetOrCreatePageMapper<T>(pageNameMap, lastPage.Blocks);
            return pageMapper(lastPage, lastPage.RowCount - 1);
        }

        // Ultra Zero-Copy Flat Path
        if (result is INpOnTableWrapper { FlatData: { } flatData, NameToIndexMap: { } nameToIndexMap } flatTable)
        {
            int rowCount = flatTable.RowCount;
            if (rowCount == 0) return null;
            int colCount = flatTable.ColCount;
            var flatMapper = GetOrCreateFlatMapper<T>(nameToIndexMap);
            return flatMapper(flatData, (rowCount - 1) * colCount);
        }

        if (result is INpOnTableWrapper { NameToIndexMap: { } nameToIndexMap2, RawData: { } rawData })
        {
            if (rawData.Count == 0) return null;
            var directMapper = GetOrCreateDirectMapper<T>(nameToIndexMap2);
            return directMapper(rawData[^1]);
        }

        if (result is not INpOnTableWrapper tableWrapper || tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return null;
        var lastRow = rowWrappers.Values.LastOrDefault(r => r != null);
        if (lastRow == null)
            return null;

        var mapper = GetOrCreateMapper<T>();
        return mapper(lastRow);
    }

    public static List<T>? ToList<T>(this INpOnWrapperResult? result) where T : class
    {
        if (result == null) return null;

        // Vectorized Columnar Page Path: 0 intermediate object allocations, 0 boxing
        if (result is INpOnTableWrapper { Pages: { Count: > 0 } pages, NameToIndexMap: { } pageNameMap })
        {
            int totalRows = 0;
            for (int p = 0; p < pages.Count; p++) totalRows += pages[p].RowCount;
            if (totalRows == 0) return [];

            var pageMapper = GetOrCreatePageMapper<T>(pageNameMap, pages[0].Blocks);
            var fastList = new List<T>(totalRows);

            for (int p = 0; p < pages.Count; p++)
            {
                var page = pages[p];
                int pageRowCount = page.RowCount;
                for (int r = 0; r < pageRowCount; r++)
                {
                    fastList.Add(pageMapper(page, r));
                }
            }
            return fastList;
        }

        // Ultra Zero-Copy Flat Path: 0 heap arrays per row, 1 contiguous flat buffer in CPU L1/L2 cache
        if (result is INpOnTableWrapper { FlatData: { } flatData, NameToIndexMap: { } nameToIndexMap } flatTable)
        {
            int rowCount = flatTable.RowCount;
            if (rowCount == 0) return [];
            int colCount = flatTable.ColCount;
            var flatMapper = GetOrCreateFlatMapper<T>(nameToIndexMap);
            var fastList = new List<T>(rowCount);
            for (int r = 0; r < rowCount; r++)
            {
                fastList.Add(flatMapper(flatData, r * colCount));
            }
            return fastList;
        }

        // Fast-path: Direct array indexing without RowWrapper, Dictionary, or Cell allocations
        if (result is INpOnTableWrapper { NameToIndexMap: { } nameToIndexMap2, RawData: { } rawData })
        {
            if (rawData.Count == 0) return [];
            var directMapper = GetOrCreateDirectMapper<T>(nameToIndexMap2);
            var fastList = new List<T>(rawData.Count);
            for (int i = 0; i < rawData.Count; i++)
            {
                fastList.Add(directMapper(rawData[i]));
            }
            return fastList;
        }

        // Fallback compatibility path
        if (result is not INpOnTableWrapper tableWrapper || tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return null;
        var mapper = GetOrCreateMapper<T>();
        var list = new List<T>(rowWrappers.Count);

        foreach (var row in rowWrappers.Values)
            if (row != null)
                list.Add(mapper(row));
        return list.Count > 0 ? list : null;
    }

    public static T[]? ToArray<T>(this INpOnWrapperResult? result) where T : class
    {
        if (result == null) return null;

        // Vectorized Columnar Page Path
        if (result is INpOnTableWrapper { Pages: { Count: > 0 } pages, NameToIndexMap: { } pageNameMap })
        {
            int totalRows = 0;
            for (int p = 0; p < pages.Count; p++) totalRows += pages[p].RowCount;
            if (totalRows == 0) return null;

            var pageMapper = GetOrCreatePageMapper<T>(pageNameMap, pages[0].Blocks);
            var fastArray = new T[totalRows];
            int idx = 0;

            for (int p = 0; p < pages.Count; p++)
            {
                var page = pages[p];
                int pageRowCount = page.RowCount;
                for (int r = 0; r < pageRowCount; r++)
                {
                    fastArray[idx++] = pageMapper(page, r);
                }
            }
            return fastArray;
        }

        // Ultra Zero-Copy Flat Path
        if (result is INpOnTableWrapper { FlatData: { } flatData, NameToIndexMap: { } nameToIndexMap } flatTable)
        {
            int rowCount = flatTable.RowCount;
            if (rowCount == 0) return null;
            int colCount = flatTable.ColCount;
            var flatMapper = GetOrCreateFlatMapper<T>(nameToIndexMap);
            var fastArray = new T[rowCount];
            for (int r = 0; r < rowCount; r++)
            {
                fastArray[r] = flatMapper(flatData, r * colCount);
            }
            return fastArray;
        }

        if (result is INpOnTableWrapper { NameToIndexMap: { } nameToIndexMap2, RawData: { } rawData })
        {
            if (rawData.Count == 0) return null;
            var directMapper = GetOrCreateDirectMapper<T>(nameToIndexMap2);
            var fastArray = new T[rawData.Count];
            for (int i = 0; i < rawData.Count; i++)
            {
                fastArray[i] = directMapper(rawData[i]);
            }
            return fastArray;
        }

        if (result is not INpOnTableWrapper tableWrapper || tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return null;
        var mapper = GetOrCreateMapper<T>();
        var validRows = rowWrappers.Values.Where(r => r != null).ToArray();
        if (validRows.Length == 0) return null;
        var array = new T[validRows.Length];
        for (int i = 0; i < validRows.Length; i++)
            array[i] = mapper(validRows[i]!);
        return array;
    }

    public static List<T>? ToRange<T>(this INpOnWrapperResult? result, int skip, int take) where T : class
    {
        if (result == null || skip < 0 || take <= 0) return null;

        // Vectorized Columnar Page Path
        if (result is INpOnTableWrapper { Pages: { Count: > 0 } pages, NameToIndexMap: { } pageNameMap })
        {
            int totalRows = 0;
            for (int p = 0; p < pages.Count; p++) totalRows += pages[p].RowCount;
            if (totalRows == 0 || skip >= totalRows) return null;

            int count = Math.Min(take, totalRows - skip);
            var pageMapper = GetOrCreatePageMapper<T>(pageNameMap, pages[0].Blocks);
            var fastList = new List<T>(count);

            int currentGlobalRow = 0;
            for (int p = 0; p < pages.Count && fastList.Count < count; p++)
            {
                var page = pages[p];
                int pageRowCount = page.RowCount;
                for (int r = 0; r < pageRowCount && fastList.Count < count; r++)
                {
                    if (currentGlobalRow >= skip)
                    {
                        fastList.Add(pageMapper(page, r));
                    }
                    currentGlobalRow++;
                }
            }
            return fastList;
        }

        // Ultra Zero-Copy Flat Path
        if (result is INpOnTableWrapper { FlatData: { } flatData, NameToIndexMap: { } nameToIndexMap } flatTable)
        {
            int rowCount = flatTable.RowCount;
            if (rowCount == 0 || skip >= rowCount) return null;
            int colCount = flatTable.ColCount;
            var flatMapper = GetOrCreateFlatMapper<T>(nameToIndexMap);
            int count = Math.Min(take, rowCount - skip);
            var fastList = new List<T>(count);
            for (int i = skip; i < skip + count; i++)
            {
                fastList.Add(flatMapper(flatData, i * colCount));
            }
            return fastList;
        }

        if (result is INpOnTableWrapper { NameToIndexMap: { } nameToIndexMap2, RawData: { } rawData })
        {
            if (rawData.Count == 0 || skip >= rawData.Count) return null;
            var directMapper = GetOrCreateDirectMapper<T>(nameToIndexMap2);
            int count = Math.Min(take, rawData.Count - skip);
            var fastList = new List<T>(count);
            for (int i = skip; i < skip + count; i++)
            {
                fastList.Add(directMapper(rawData[i]));
            }
            return fastList;
        }

        if (result is not INpOnTableWrapper tableWrapper || tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return null;
        var mapper = GetOrCreateMapper<T>();
        var list = new List<T>(take);
        foreach (var row in rowWrappers.Values.Where(r => r != null).Skip(skip).Take(take))
            if (row != null)
                list.Add(mapper(row));
        return list.Count > 0 ? list : null;
    }

    #endregion
}
