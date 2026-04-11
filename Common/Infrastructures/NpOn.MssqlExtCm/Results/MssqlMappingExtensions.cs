using System.Data.Common;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Results;

public static class MssqlMappingExtensions
{
    public static Func<object[], IReadOnlyDictionary<string, INpOnCell>> CreateRowMapper(
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        var dynamicMethod = new DynamicMethod(
            nameof(CreateRowMapper),
            typeof(IReadOnlyDictionary<string, INpOnCell>),
            [typeof(object[])],
            typeof(MssqlMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var dictionary = il.DeclareLocal(typeof(Dictionary<string, INpOnCell>));
        var cell = il.DeclareLocal(typeof(INpOnCell));

        var dictCtor = typeof(Dictionary<string, INpOnCell>).GetConstructor([typeof(int)]);
        var dictAdd = typeof(Dictionary<string, INpOnCell>).GetMethod(nameof(Dictionary<,>.Add));
        var createCell = typeof(MssqlCellDynamicFactory).GetMethod(nameof(MssqlCellDynamicFactory.Create));
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
            $"{nameof(CreateColumnMapper)}_{columnName.Replace(" ", "_")}",
            typeof(IReadOnlyDictionary<int, INpOnCell>),
            [typeof(List<object[]>)],
            typeof(MssqlMappingExtensions).Module,
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
        var createCell = typeof(MssqlCellDynamicFactory).GetMethod(nameof(MssqlCellDynamicFactory.Create));
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

    public static Func<DbDataReader, object[]> CreateArrayRowMapper(this DbDataReader reader,
        MethodInfo? normalizerMethod = null)
    {
        var dynamicMethod = new DynamicMethod(
            nameof(CreateArrayRowMapper),
            typeof(object[]),
            [typeof(DbDataReader)],
            typeof(MssqlMappingExtensions).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var getValueMethod = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetValue), [typeof(int)]);

        il.Emit(OpCodes.Ldc_I4, reader.FieldCount);
        il.Emit(OpCodes.Newarr, typeof(object));

        var values = il.DeclareLocal(typeof(object[]));
        il.Emit(OpCodes.Stloc, values);

        for (int i = 0; i < reader.FieldCount; i++)
        {
            il.Emit(OpCodes.Ldloc, values);
            il.Emit(OpCodes.Ldc_I4, i);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, i);
            if (getValueMethod != null) il.Emit(OpCodes.Callvirt, getValueMethod);

            if (normalizerMethod != null) il.Emit(OpCodes.Call, normalizerMethod);

            il.Emit(OpCodes.Stelem_Ref);
        }

        il.Emit(OpCodes.Ldloc, values);
        il.Emit(OpCodes.Ret);

        return (Func<DbDataReader, object[]>)dynamicMethod.CreateDelegate(typeof(Func<DbDataReader, object[]>));
    }
}
