using System.Reflection;
using System.Reflection.Emit;
using Cassandra;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Results;

public static class CassandraMappingExtensions
{
    public static Func<object[], IReadOnlyDictionary<string, INpOnCell>> CreateRowMapper(
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var dynamicMethod = new DynamicMethod(
            nameof(CreateRowMapper),
            typeof(IReadOnlyDictionary<string, INpOnCell>),
            new[] { typeof(object[]) },
            typeof(CassandraMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var dictionary = il.DeclareLocal(typeof(Dictionary<string, INpOnCell>));
        var cell = il.DeclareLocal(typeof(INpOnCell));

        var dictCtor = typeof(Dictionary<string, INpOnCell>).GetConstructor(new[] { typeof(int) });
        var dictAdd = typeof(Dictionary<string, INpOnCell>).GetMethod(nameof(Dictionary<string, INpOnCell>.Add));
        var createCell = typeof(CassandraCellDynamicFactory).GetMethod(nameof(CassandraCellDynamicFactory.Create));
        var getTypeFromHandle =
            typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) });

        // var dictionary = new Dictionary<string, INpOnCell>(schemaMap.Count);
        il.Emit(OpCodes.Ldc_I4, schemaMap.Count);
        if (dictCtor != null) il.Emit(OpCodes.Newobj, dictCtor);
        il.Emit(OpCodes.Stloc, dictionary);

        foreach (var schemaInfo in schemaMap.Values)
        {
            var columnIndex = nameToIndexMap[schemaInfo.ColumnName];

            // INpOnCell cell = CassandraCellDynamicFactory.Create(...)
            il.Emit(OpCodes.Ldtoken, schemaInfo.DataType);
            if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);
            il.Emit(OpCodes.Ldarg_0); // parent object[]
            il.Emit(OpCodes.Ldc_I4, columnIndex);
            il.Emit(OpCodes.Ldelem_Ref); // cellValue
            il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
            il.Emit(schemaInfo.IsPrimaryKey ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0); // isPrimaryKey
            if (createCell != null) il.Emit(OpCodes.Call, createCell);
            il.Emit(OpCodes.Stloc, cell);

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
        var dynamicMethod = new DynamicMethod(
            $"{nameof(CreateColumnMapper)}_{columnName}",
            typeof(IReadOnlyDictionary<int, INpOnCell>),
            new[] { typeof(List<object[]>) },
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
        var dictCtor = typeof(Dictionary<int, INpOnCell>).GetConstructor(new[] { typeof(int) });
        var dictAdd = typeof(Dictionary<int, INpOnCell>).GetMethod(nameof(Dictionary<int, INpOnCell>.Add));
        var createCell = typeof(CassandraCellDynamicFactory).GetMethod(nameof(CassandraCellDynamicFactory.Create));
        var getTypeFromHandle =
            typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) });

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

        // i++
        il.Emit(OpCodes.Ldloc, i);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
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

    public static Func<Row, object[]> CreateArrayRowMapper(IReadOnlyList<NpOnColumnSchemaInfo> orderedSchemas,
        MethodInfo? normalizerMethod = null)
    {
        var dynamicMethod = new DynamicMethod(
            nameof(CreateArrayRowMapper),
            typeof(object[]),
            new[] { typeof(Row) },
            typeof(CassandraMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var getValueMethod = typeof(Row).GetMethod(nameof(Row.GetValue), new[] { typeof(Type), typeof(string) });
        var getTypeFromHandle =
            typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new[] { typeof(RuntimeTypeHandle) });

        // var values = new object[orderedSchemas.Count];
        il.Emit(OpCodes.Ldc_I4, orderedSchemas.Count);
        il.Emit(OpCodes.Newarr, typeof(object));

        var values = il.DeclareLocal(typeof(object[]));
        il.Emit(OpCodes.Stloc, values);

        for (int i = 0; i < orderedSchemas.Count; i++)
        {
            var schemaInfo = orderedSchemas[i];

            // values[i] = ...
            il.Emit(OpCodes.Ldloc, values);
            il.Emit(OpCodes.Ldc_I4, i);

            // reader.GetValue(type, name)
            il.Emit(OpCodes.Ldarg_0); // Row

            il.Emit(OpCodes.Ldtoken, schemaInfo.DataType);
            if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);

            il.Emit(OpCodes.Ldstr, schemaInfo.ColumnName);

            if (getValueMethod != null) il.Emit(OpCodes.Callvirt, getValueMethod);

            // if (normalizerMethod != null) normalizerMethod(value)
            if (normalizerMethod != null) il.Emit(OpCodes.Call, normalizerMethod);

            il.Emit(OpCodes.Stelem_Ref);
        }

        il.Emit(OpCodes.Ldloc, values);
        il.Emit(OpCodes.Ret);

        return (Func<Row, object[]>)dynamicMethod.CreateDelegate(typeof(Func<Row, object[]>));
    }
}