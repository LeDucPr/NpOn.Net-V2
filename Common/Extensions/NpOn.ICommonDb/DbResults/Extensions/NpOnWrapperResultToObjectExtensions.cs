using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.ICommonDb.DbResults.Grpc;

namespace Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;

public static partial class NpOnWrapperResultExtensions
{
    // Cache for compiled mapper delegates. Key is the Type of the target object.
    private static readonly WrapperCacheStore<Type, Delegate> MapperCache = new();

    #region Private IL Generation and Caching

    /// <summary>
    /// Gets a cached mapper function or creates a new one if it doesn't exist.
    /// </summary>
    private static Func<INpOnRowWrapper, T> GetOrCreateMapper<T>() where T : NpOnBaseGrpcObject
    {
        return (Func<INpOnRowWrapper, T>)MapperCache.GetOrAdd(typeof(T), _ => CreateObjectMapper<T>());
    }

    /// <summary>
    /// Creates a high-performance object mapper function using IL-Emit.
    /// </summary>
    private static Func<INpOnRowWrapper, T> CreateObjectMapper<T>() where T : NpOnBaseGrpcObject
    {
        var objectType = typeof(T);
        var dynamicMethod = new DynamicMethod(
            $"DynamicObjectMapper_{objectType.Name}",
            objectType,
            [typeof(INpOnRowWrapper)],
            objectType.Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        // --- Get necessary metadata ---
        var constructor = objectType.GetConstructor(Type.EmptyTypes) ??
                          throw new InvalidOperationException(
                              $"Type {objectType.Name} must have a parameterless constructor.");

        // Create a temporary instance to get the field map
        var tempInstance = Activator.CreateInstance<T>();
        tempInstance.CreateDefaultFieldMapper();
        var fieldMap = tempInstance.FieldMap ?? objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.Name);

        var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        var getRowWrapperMethod = typeof(INpOnRowWrapper).GetMethod(nameof(INpOnRowWrapper.GetRowWrapper))!;
        var cellsTryGetValueMethod = typeof(IReadOnlyDictionary<string, INpOnCell>).GetMethod("TryGetValue")!;
        var cellValueGetter = typeof(INpOnCell).GetProperty(nameof(INpOnCell.ValueAsObject))!.GetGetMethod()!;
        var changeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!;
        var enumToObjectMethod = typeof(Enum).GetMethod(nameof(Enum.ToObject), [typeof(Type), typeof(object)])!;
        var getTypeFromHandleMethod =
            typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)])!;

        // --- Declare local variables ---
        var instanceLocal = il.DeclareLocal(objectType); // T instance;
        var cellsLocal =
            il.DeclareLocal(
                typeof(IReadOnlyDictionary<string, INpOnCell>)); // IReadOnlyDictionary<string, INpOnCell> cells;
        var cellLocal = il.DeclareLocal(typeof(INpOnCell)); // INpOnCell cell;
        var valueLocal = il.DeclareLocal(typeof(object)); // object value;

        // --- IL Body ---
        // T instance = new T();
        il.Emit(OpCodes.Newobj, constructor);
        il.Emit(OpCodes.Stloc, instanceLocal);

        // var cells = rowWrapper.GetRowWrapper();
        il.Emit(OpCodes.Ldarg_0); // Load INpOnRowWrapper argument
        il.Emit(OpCodes.Callvirt, getRowWrapperMethod);
        il.Emit(OpCodes.Stloc, cellsLocal);

        foreach (var (propertyName, columnName) in fieldMap)
        {
            if (!properties.TryGetValue(propertyName, out var propInfo)) continue;

            var endOfPropertyLabel = il.DefineLabel();

            // if (cells.TryGetValue(columnName, out cell))
            il.Emit(OpCodes.Ldloc, cellsLocal);
            il.Emit(OpCodes.Ldstr, columnName);
            il.Emit(OpCodes.Ldloca_S, cellLocal); // out cell
            il.Emit(OpCodes.Callvirt, cellsTryGetValueMethod);
            il.Emit(OpCodes.Brfalse, endOfPropertyLabel); // if false, goto end

            // object value = cell.ValueAsObject;
            il.Emit(OpCodes.Ldloc, cellLocal);
            il.Emit(OpCodes.Callvirt, cellValueGetter);
            il.Emit(OpCodes.Stloc, valueLocal);

            // if (value != null)
            il.Emit(OpCodes.Ldloc, valueLocal);
            il.Emit(OpCodes.Brfalse, endOfPropertyLabel); // if null, goto end

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

            // instance.Property = value;
            il.Emit(OpCodes.Callvirt, propInfo.GetSetMethod()!);

            il.MarkLabel(endOfPropertyLabel);
        }

        // return instance;
        il.Emit(OpCodes.Ldloc, instanceLocal);
        il.Emit(OpCodes.Ret);

        return (Func<INpOnRowWrapper, T>)dynamicMethod.CreateDelegate(typeof(Func<INpOnRowWrapper, T>));
    }

    #endregion


    #region Public Extensions

    // ReSharper disable once ConvertToExtensionBlock
    public static T? ToFirstOrDefault<T>(this INpOnWrapperResult? result) where T : NpOnBaseGrpcObject
    {
        if (result is not INpOnTableWrapper tableWrapper || tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return null;
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null);
        if (firstRow == null)
            return null;
        var mapper = GetOrCreateMapper<T>();
        return mapper(firstRow);
    }

    public static T? ToLastOrDefault<T>(this INpOnWrapperResult? result) where T : NpOnBaseGrpcObject
    {
        if (result is not INpOnTableWrapper tableWrapper || tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return null;
        var lastRow = rowWrappers.Values.LastOrDefault(r => r != null);
        if (lastRow == null)
            return null;

        var mapper = GetOrCreateMapper<T>();
        return mapper(lastRow);
    }

    public static List<T>? ToList<T>(this INpOnWrapperResult? result) where T : NpOnBaseGrpcObject
    {
        if (result is not INpOnTableWrapper tableWrapper || tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return null;
        var mapper = GetOrCreateMapper<T>();
        var list = new List<T>(rowWrappers.Count);

        // list.AddRange(rowWrappers.Values.OfType<INpOnRowWrapper>().Select(row => mapper(row)));
        foreach (var row in rowWrappers.Values)
            if (row != null)
                list.Add(mapper(row));
        return list.Count > 0 ? list : null;
    }

    public static T[]? ToArray<T>(this INpOnWrapperResult? result) where T : NpOnBaseGrpcObject
    {
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

    public static List<T>? ToRange<T>(this INpOnWrapperResult? result, int skip, int take) where T : NpOnBaseGrpcObject
    {
        if (skip < 0 || take <= 0)
            return null;
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