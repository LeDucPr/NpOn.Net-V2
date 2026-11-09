using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using Cassandra;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Results;

public static class CassandraMappingExtensions
{
    private static readonly ConcurrentDictionary<string, Func<object[], IReadOnlyDictionary<string, INpOnCell>>> RowMapperCache = new();
    private static readonly ConcurrentDictionary<string, Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>> ColumnMapperCache = new();
    private static readonly ConcurrentDictionary<string, Func<Row, object[]>> ArrayRowMapperCache = new();

    public static Func<object[], IReadOnlyDictionary<string, INpOnCell>> GetOrCreateRowMapper(
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var cacheKey = string.Join(";", schemaMap.Values.Select(s => $"{s.ColumnName}:{s.DataType.FullName}:{s.IsPrimaryKey}"));
        return RowMapperCache.GetOrAdd(cacheKey, _ => CreateRowMapper(schemaMap, nameToIndexMap));
    }

    public static Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> GetOrCreateColumnMapper(
        string columnName,
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var cacheKey = $"{columnName};{string.Join(";", schemaMap.Values.Select(s => $"{s.ColumnName}:{s.DataType.FullName}"))}";
        return ColumnMapperCache.GetOrAdd(cacheKey, _ => CreateColumnMapper(columnName, schemaMap, nameToIndexMap));
    }

    public static Func<Row, object[]> GetOrCreateArrayRowMapper(IReadOnlyList<NpOnColumnSchemaInfo> orderedSchemas, MethodInfo? normalizerMethod = null)
    {
        var cacheKey = string.Join(";", orderedSchemas.Select(s => $"{s.ColumnName}:{s.DataType.FullName}"));
        return ArrayRowMapperCache.GetOrAdd(cacheKey, _ => CreateArrayRowMapper(orderedSchemas, normalizerMethod));
    }

    public static Func<object[], IReadOnlyDictionary<string, INpOnCell>> CreateRowMapper(
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var dynamicMethod = new DynamicMethod(
            nameof(CreateRowMapper),
            typeof(IReadOnlyDictionary<string, INpOnCell>),
            [typeof(object[])],
            typeof(CassandraMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var dictionary = il.DeclareLocal(typeof(Dictionary<string, INpOnCell>));
        var cell = il.DeclareLocal(typeof(INpOnCell));

        var dictCtor = typeof(Dictionary<string, INpOnCell>).GetConstructor([typeof(int)]);
        var dictAdd = typeof(Dictionary<string, INpOnCell>).GetMethod(nameof(Dictionary<string, INpOnCell>.Add));
        var createCell = typeof(CassandraCellDynamicFactory).GetMethod(nameof(CassandraCellDynamicFactory.Create));
        var getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)]);

        il.Emit(OpCodes.Ldc_I4, schemaMap.Count);
        if (dictCtor != null) il.Emit(OpCodes.Newobj, dictCtor);
        il.Emit(OpCodes.Stloc, dictionary);

        foreach (var schemaInfo in schemaMap.Values)
        {
            var columnIndex = nameToIndexMap[schemaInfo.ColumnName];

            il.Emit(OpCodes.Ldtoken, schemaInfo.DataType);
            if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, columnIndex);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
            il.Emit(schemaInfo.IsPrimaryKey ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            if (createCell != null) il.Emit(OpCodes.Call, createCell);
            il.Emit(OpCodes.Stloc, cell);

            il.Emit(OpCodes.Ldloc, dictionary);
            il.Emit(OpCodes.Ldstr, schemaInfo.ColumnName);
            il.Emit(OpCodes.Ldloc, cell);
            if (dictAdd != null) il.Emit(OpCodes.Callvirt, dictAdd);
        }

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
        var dynamicMethod = new DynamicMethod(
            $"{nameof(CreateColumnMapper)}_{columnName}",
            typeof(IReadOnlyDictionary<int, INpOnCell>),
            [typeof(List<object[]>)],
            typeof(CassandraMappingExtensions).Module,
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
        var createCell = typeof(CassandraCellDynamicFactory).GetMethod(nameof(CassandraCellDynamicFactory.Create));
        var getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)]);

        il.Emit(OpCodes.Ldarg_0);
        if (listCountGetter != null) il.Emit(OpCodes.Callvirt, listCountGetter);
        il.Emit(OpCodes.Stloc, rowCount);

        il.Emit(OpCodes.Ldloc, rowCount);
        if (dictCtor != null) il.Emit(OpCodes.Newobj, dictCtor);
        il.Emit(OpCodes.Stloc, dictionary);

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, i);
        var loopStart = il.DefineLabel();
        var loopCheck = il.DefineLabel();
        il.Emit(OpCodes.Br, loopCheck);

        il.MarkLabel(loopStart);

        il.Emit(OpCodes.Ldtoken, schemaInfo.DataType);
        if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, i);
        if (listIndexerGetter != null) il.Emit(OpCodes.Callvirt, listIndexerGetter);
        il.Emit(OpCodes.Ldc_I4, columnIndex);
        il.Emit(OpCodes.Ldelem_Ref);
        il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
        il.Emit(schemaInfo.IsPrimaryKey ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
        if (createCell != null) il.Emit(OpCodes.Call, createCell);
        il.Emit(OpCodes.Stloc, cell);

        il.Emit(OpCodes.Ldloc, dictionary);
        il.Emit(OpCodes.Ldloc, i);
        il.Emit(OpCodes.Ldloc, cell);
        if (dictAdd != null) il.Emit(OpCodes.Callvirt, dictAdd);

        il.Emit(OpCodes.Ldloc, i);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, i);

        il.MarkLabel(loopCheck);
        il.Emit(OpCodes.Ldloc, i);
        il.Emit(OpCodes.Ldloc, rowCount);
        il.Emit(OpCodes.Blt, loopStart);

        il.Emit(OpCodes.Ldloc, dictionary);
        il.Emit(OpCodes.Ret);

        return (Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>)dynamicMethod.CreateDelegate(
            typeof(Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>));
    }

    public static Func<Row, object[]> CreateArrayRowMapper(IReadOnlyList<NpOnColumnSchemaInfo> orderedSchemas,
        MethodInfo? normalizerMethod = null)
    {
        var dynamicMethod = new DynamicMethod(
            nameof(CreateArrayRowMapper),
            typeof(object[]),
            [typeof(Row)],
            typeof(CassandraMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var getValueMethod = typeof(Row).GetMethod(nameof(Row.GetValue), [typeof(Type), typeof(string)]);
        var getTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), [typeof(RuntimeTypeHandle)]);

        il.Emit(OpCodes.Ldc_I4, orderedSchemas.Count);
        il.Emit(OpCodes.Newarr, typeof(object));

        var values = il.DeclareLocal(typeof(object[]));
        il.Emit(OpCodes.Stloc, values);

        for (int i = 0; i < orderedSchemas.Count; i++)
        {
            var schemaInfo = orderedSchemas[i];

            il.Emit(OpCodes.Ldloc, values);
            il.Emit(OpCodes.Ldc_I4, i);

            il.Emit(OpCodes.Ldarg_0);

            il.Emit(OpCodes.Ldtoken, schemaInfo.DataType);
            if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);

            il.Emit(OpCodes.Ldstr, schemaInfo.ColumnName);

            if (getValueMethod != null) il.Emit(OpCodes.Callvirt, getValueMethod);

            if (normalizerMethod != null) il.Emit(OpCodes.Call, normalizerMethod);

            il.Emit(OpCodes.Stelem_Ref);
        }

        il.Emit(OpCodes.Ldloc, values);
        il.Emit(OpCodes.Ret);

        return (Func<Row, object[]>)dynamicMethod.CreateDelegate(typeof(Func<Row, object[]>));
    }
}
