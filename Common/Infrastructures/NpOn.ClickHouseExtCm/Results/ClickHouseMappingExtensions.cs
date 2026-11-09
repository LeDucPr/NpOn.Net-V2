using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.ClickHouseExtCm.Results;

public static class ClickHouseMappingExtensions
{
    // Thread-safe delegate caches: compiled once per schema signature, reused across all queries
    private static readonly ConcurrentDictionary<(int FieldCount, MethodInfo? Normalizer), Func<DbDataReader, object[]>>
        ArrayRowMapperCache = new();

    private static readonly ConcurrentDictionary<string, Func<object[], IReadOnlyDictionary<string, INpOnCell>>>
        RowMapperCache = new();

    private static readonly ConcurrentDictionary<string, Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>>
        ColumnMapperCache = new();

    public static Func<object[], IReadOnlyDictionary<string, INpOnCell>> CreateRowMapper(
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var schemaKey = BuildSchemaFingerprint(schemaMap, nameToIndexMap);
        return RowMapperCache.GetOrAdd(schemaKey, _ => CompileRowMapper(schemaMap, nameToIndexMap));
    }

    private static string BuildSchemaFingerprint(
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var parts = new string[schemaMap.Count];
        int idx = 0;
        foreach (var s in schemaMap.Values)
        {
            var colIdx = nameToIndexMap.TryGetValue(s.ColumnName, out var ci) ? ci : idx;
            parts[idx++] = $"{s.ColumnName}:{s.DataType.FullName}:{s.ProviderDataTypeName}:{s.IsPrimaryKey}:{colIdx}";
        }
        return string.Join(";", parts);
    }

    private static Func<object[], IReadOnlyDictionary<string, INpOnCell>> CompileRowMapper(
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var dynamicMethod = new DynamicMethod(
            nameof(CreateRowMapper),
            typeof(IReadOnlyDictionary<string, INpOnCell>),
            [typeof(object[])], // arg0: parent object[]
            typeof(ClickHouseMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var dictionary = il.DeclareLocal(typeof(Dictionary<string, INpOnCell>));
        var cell = il.DeclareLocal(typeof(INpOnCell));

        var dictCtor = typeof(Dictionary<string, INpOnCell>).GetConstructor([typeof(int)]);
        var dictAdd = typeof(Dictionary<string, INpOnCell>).GetMethod(nameof(Dictionary<string, INpOnCell>.Add));
        var fallbackCreateCell = typeof(ClickHouseCellDynamicFactory).GetMethod(nameof(ClickHouseCellDynamicFactory.Create));
        var getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)]);

        // var dictionary = new Dictionary<string, INpOnCell>(schemaMap.Count);
        il.Emit(OpCodes.Ldc_I4, schemaMap.Count);
        if (dictCtor != null) il.Emit(OpCodes.Newobj, dictCtor);
        il.Emit(OpCodes.Stloc, dictionary);

        foreach (var schemaInfo in schemaMap.Values)
        {
            var columnIndex = nameToIndexMap[schemaInfo.ColumnName];

            var genericCellType = typeof(ClickHouseCell<>).MakeGenericType(schemaInfo.DataType);
            var directFromClickHouse = genericCellType.GetMethod(
                nameof(ClickHouseCell<object>.FromClickHouse),
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(object), typeof(string), typeof(bool)],
                null
            );

            if (directFromClickHouse != null)
            {
                il.Emit(OpCodes.Ldarg_0); // parent object[]
                il.Emit(OpCodes.Ldc_I4, columnIndex);
                il.Emit(OpCodes.Ldelem_Ref); // cellValue
                il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
                il.Emit(schemaInfo.IsPrimaryKey ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0); // isPrimaryKey
                il.Emit(OpCodes.Call, directFromClickHouse);
                il.Emit(OpCodes.Stloc, cell);
            }
            else
            {
                il.Emit(OpCodes.Ldtoken, schemaInfo.DataType);
                if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, columnIndex);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
                il.Emit(schemaInfo.IsPrimaryKey ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                if (fallbackCreateCell != null) il.Emit(OpCodes.Call, fallbackCreateCell);
                il.Emit(OpCodes.Stloc, cell);
            }

            // dictionary.Add(schemaInfo.ColumnName, cell);
            il.Emit(OpCodes.Ldloc, dictionary);
            il.Emit(OpCodes.Ldstr, schemaInfo.ColumnName);
            il.Emit(OpCodes.Ldloc, cell);
            if (dictAdd != null) il.Emit(OpCodes.Callvirt, dictAdd);
        }

        // return dictionary;
        il.Emit(OpCodes.Ldloc, dictionary);
        il.Emit(OpCodes.Ret);

        return (Func<object[], IReadOnlyDictionary<string, INpOnCell>>)dynamicMethod.CreateDelegate(
            typeof(Func<object[], IReadOnlyDictionary<string, INpOnCell>>));
    }

    public static Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> CreateColumnMapper(
        string columnName,
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var schemaInfo = schemaMap[columnName];
        var columnIndex = nameToIndexMap[columnName];
        var cacheKey = $"{columnName}:{schemaInfo.DataType.FullName}:{schemaInfo.ProviderDataTypeName}:{schemaInfo.IsPrimaryKey}:{columnIndex}";

        return ColumnMapperCache.GetOrAdd(cacheKey, _ => CompileColumnMapper(columnName, schemaMap, nameToIndexMap));
    }

    private static Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> CompileColumnMapper(
        string columnName,
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var dynamicMethod = new DynamicMethod(
            $"{nameof(CreateColumnMapper)}_{columnName}",
            typeof(IReadOnlyDictionary<int, INpOnCell>),
            [typeof(List<object[]>)],
            typeof(ClickHouseMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var schemaInfo = schemaMap[columnName];
        var columnIndex = nameToIndexMap[columnName];

        var dictionary = il.DeclareLocal(typeof(Dictionary<int, INpOnCell>));
        var rowCount = il.DeclareLocal(typeof(int));
        var i = il.DeclareLocal(typeof(int));
        var cell = il.DeclareLocal(typeof(INpOnCell));

        var listCountGetter = typeof(List<object[]>).GetProperty(nameof(List<object[]>.Count))?.GetGetMethod();
        var listIndexerGetter = typeof(List<object[]>).GetMethod("get_Item");
        var dictCtor = typeof(Dictionary<int, INpOnCell>).GetConstructor([typeof(int)]);
        var dictAdd = typeof(Dictionary<int, INpOnCell>).GetMethod(nameof(Dictionary<int, INpOnCell>.Add));
        var fallbackCreateCell = typeof(ClickHouseCellDynamicFactory).GetMethod(nameof(ClickHouseCellDynamicFactory.Create));
        var getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)]);

        var genericCellType = typeof(ClickHouseCell<>).MakeGenericType(schemaInfo.DataType);
        var directFromClickHouse = genericCellType.GetMethod(
            nameof(ClickHouseCell<object>.FromClickHouse),
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(object), typeof(string), typeof(bool)],
            null
        );

        // Load Argument 0
        il.Emit(OpCodes.Ldarg_0);
        if (listCountGetter != null) il.Emit(OpCodes.Callvirt, listCountGetter);
        il.Emit(OpCodes.Stloc, rowCount);

        il.Emit(OpCodes.Ldloc, rowCount);
        if (dictCtor != null) il.Emit(OpCodes.Newobj, dictCtor);
        il.Emit(OpCodes.Stloc, dictionary);

        // int i = 0
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, i);
        var loopStart = il.DefineLabel();
        var loopCheck = il.DefineLabel();
        il.Emit(OpCodes.Br, loopCheck);

        il.MarkLabel(loopStart);

        if (directFromClickHouse != null)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, i);
            if (listIndexerGetter != null) il.Emit(OpCodes.Callvirt, listIndexerGetter);
            il.Emit(OpCodes.Ldc_I4, columnIndex);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
            il.Emit(schemaInfo.IsPrimaryKey ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Call, directFromClickHouse);
            il.Emit(OpCodes.Stloc, cell);
        }
        else
        {
            il.Emit(OpCodes.Ldtoken, schemaInfo.DataType);
            if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, i);
            if (listIndexerGetter != null) il.Emit(OpCodes.Callvirt, listIndexerGetter);
            il.Emit(OpCodes.Ldc_I4, columnIndex);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
            il.Emit(schemaInfo.IsPrimaryKey ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            if (fallbackCreateCell != null) il.Emit(OpCodes.Call, fallbackCreateCell);
            il.Emit(OpCodes.Stloc, cell);
        }

        il.Emit(OpCodes.Ldloc, dictionary);
        il.Emit(OpCodes.Ldloc, i);
        il.Emit(OpCodes.Ldloc, cell);
        if (dictAdd != null) il.Emit(OpCodes.Callvirt, dictAdd);

        // for i in 0..rowCount
        il.Emit(OpCodes.Ldloc, i);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add); // i++
        il.Emit(OpCodes.Stloc, i);

        // if i < rowCount
        il.MarkLabel(loopCheck);
        il.Emit(OpCodes.Ldloc, i);
        il.Emit(OpCodes.Ldloc, rowCount);
        il.Emit(OpCodes.Blt, loopStart);

        il.Emit(OpCodes.Ldloc, dictionary);
        il.Emit(OpCodes.Ret);

        return (Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>)dynamicMethod.CreateDelegate(
            typeof(Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>));
    }

    public static Func<DbDataReader, object[]> CreateArrayRowMapper(this DbDataReader reader,
        MethodInfo? normalizerMethod = null)
    {
        var key = (reader.FieldCount, normalizerMethod);
        return ArrayRowMapperCache.GetOrAdd(key, k => CompileArrayRowMapper(k.FieldCount, k.Normalizer));
    }

    private static Func<DbDataReader, object[]> CompileArrayRowMapper(int fieldCount, MethodInfo? normalizerMethod)
    {
        var dynamicMethod = new DynamicMethod(
            $"{nameof(CreateArrayRowMapper)}_{fieldCount}",
            typeof(object[]),
            [typeof(DbDataReader)],
            typeof(ClickHouseMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var getValueMethod = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetValue), [typeof(int)]);

        // var values = new object[fieldCount];
        il.Emit(OpCodes.Ldc_I4, fieldCount);
        il.Emit(OpCodes.Newarr, typeof(object));

        var values = il.DeclareLocal(typeof(object[]));
        il.Emit(OpCodes.Stloc, values);

        for (int i = 0; i < fieldCount; i++)
        {
            // values[i] = ...
            il.Emit(OpCodes.Ldloc, values);
            il.Emit(OpCodes.Ldc_I4, i);

            // reader.GetValue(i)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, i);
            if (getValueMethod != null) il.Emit(OpCodes.Callvirt, getValueMethod);

            // if (normalizerMethod != null) normalizerMethod(value)
            if (normalizerMethod != null) il.Emit(OpCodes.Call, normalizerMethod);

            il.Emit(OpCodes.Stelem_Ref);
        }

        il.Emit(OpCodes.Ldloc, values);
        il.Emit(OpCodes.Ret);

        return (Func<DbDataReader, object[]>)dynamicMethod.CreateDelegate(typeof(Func<DbDataReader, object[]>));
    }
}
