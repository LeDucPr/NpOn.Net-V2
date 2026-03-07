using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonDb;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results
{
    public static class PostgresMappingExtensions
    {
        public static Func<object[], IReadOnlyDictionary<string, INpOnCell>> CreateRowMapper(
            IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
            IReadOnlyDictionary<string, int> nameToIndexMap)
        {
            var dynamicMethod = new DynamicMethod(
                "DynamicRowMapper",
                typeof(IReadOnlyDictionary<string, INpOnCell>),
                [typeof(object[])], // arg0: parent object[]
                typeof(PostgresMappingExtensions).Module,
                true);

            var il = dynamicMethod.GetILGenerator();

            var dictionary = il.DeclareLocal(typeof(Dictionary<string, INpOnCell>));
            var cell = il.DeclareLocal(typeof(INpOnCell));

            var dictCtor = typeof(Dictionary<string, INpOnCell>).GetConstructor([typeof(int)]);
            var dictAdd = typeof(Dictionary<string, INpOnCell>).GetMethod("Add");
            var createCell = typeof(PostgresCellDynamicFactory).GetMethod(nameof(PostgresCellDynamicFactory.Create));
            var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle", [typeof(RuntimeTypeHandle)]);

            // var dictionary = new Dictionary<string, INpOnCell>(schemaMap.Count);
            il.Emit(OpCodes.Ldc_I4, schemaMap.Count);
            if (dictCtor != null) il.Emit(OpCodes.Newobj, dictCtor);
            il.Emit(OpCodes.Stloc, dictionary);

            foreach (var schemaInfo in schemaMap.Values)
            {
                var columnIndex = nameToIndexMap[schemaInfo.ColumnName];

                // INpOnCell cell = PostgresCellDynamicFactory.Create(...)
                il.Emit(OpCodes.Ldtoken, schemaInfo.DataType);
                if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);
                il.Emit(OpCodes.Ldarg_0); // parent object[]
                il.Emit(OpCodes.Ldc_I4, columnIndex);
                il.Emit(OpCodes.Ldelem_Ref); // cellValue
                il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
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
                "DynamicColumnMapper_" + columnName,
                typeof(IReadOnlyDictionary<int, INpOnCell>),
                [typeof(List<object[]>)],
                typeof(PostgresMappingExtensions).Module,
                true);

            var il = dynamicMethod.GetILGenerator();

            var schemaInfo = schemaMap[columnName];
            var columnIndex = nameToIndexMap[columnName];

            var dictionary = il.DeclareLocal(typeof(Dictionary<int, INpOnCell>));
            var rowCount = il.DeclareLocal(typeof(int));
            var i = il.DeclareLocal(typeof(int));
            var cell = il.DeclareLocal(typeof(INpOnCell));

            var listCountGetter = typeof(List<object[]>).GetProperty("Count")?.GetGetMethod();
            var listIndexerGetter = typeof(List<object[]>).GetMethod("get_Item");
            var dictCtor = typeof(Dictionary<int, INpOnCell>).GetConstructor([typeof(int)]);
            var dictAdd = typeof(Dictionary<int, INpOnCell>).GetMethod("Add");
            var createCell = typeof(PostgresCellDynamicFactory).GetMethod(nameof(PostgresCellDynamicFactory.Create));
            var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle", [typeof(RuntimeTypeHandle)]);
            // Load Argument 0
            il.Emit(OpCodes.Ldarg_0);
            if (listCountGetter != null) il.Emit(OpCodes.Callvirt, listCountGetter); // interface ??
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

            il.Emit(OpCodes.Ldtoken, schemaInfo.DataType); // Type(get type from handler for object)
            if (getTypeFromHandle != null) il.Emit(OpCodes.Call, getTypeFromHandle);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, i);
            if (listIndexerGetter != null) il.Emit(OpCodes.Callvirt, listIndexerGetter);
            il.Emit(OpCodes.Ldc_I4, columnIndex);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Ldstr, schemaInfo.ProviderDataTypeName);
            if (createCell != null) il.Emit(OpCodes.Call, createCell);
            il.Emit(OpCodes.Stloc, cell);

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
            il.Emit(OpCodes.Blt, loopStart); // branch if less than // break

            il.Emit(OpCodes.Ldloc, dictionary);
            il.Emit(OpCodes.Ret);

            return (Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>)dynamicMethod.CreateDelegate(
                typeof(Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>));
        }
    }
}