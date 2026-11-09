using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.DbResults.Blocks;
using Npgsql;
using Npgsql.Schema;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

/// <summary>
/// High-performance recyclable RowWrapper without Stopwatch/Lazy overhead.
/// </summary>
public class PostgresRowWrapper : INpOnRowWrapper, INpOnWrapperResult<object[], IReadOnlyDictionary<string, INpOnCell>>
{
    private object[] _parent;
    private Func<object[], IReadOnlyDictionary<string, INpOnCell>> _mapper;
    private IReadOnlyDictionary<string, INpOnCell>? _result;

    public PostgresRowWrapper(object[] parent, Func<object[], IReadOnlyDictionary<string, INpOnCell>> mapper)
    {
        _parent = parent;
        _mapper = mapper;
        _result = null;
    }

    /// <summary>
    /// Re-initialize row state in-place to avoid GC allocation during pooling.
    /// </summary>
    public void ReInit(object[] parent, Func<object[], IReadOnlyDictionary<string, INpOnCell>> mapper)
    {
        _parent = parent;
        _mapper = mapper;
        _result = null;
    }

    public object[] Parent => _parent;

    public IReadOnlyDictionary<string, INpOnCell> Result => GetRowWrapper();

    public IReadOnlyDictionary<string, INpOnCell> GetRowWrapper()
    {
        return _result ??= _mapper(_parent);
    }

    public void SetSuccess() { }
    public INpOnWrapperResult SetFail(EDbError error) => this;
    public INpOnWrapperResult SetFail(string errorString) => this;
    public INpOnWrapperResult SetFail(Exception ex) => this;
    public long QueryTimeMilliseconds => 0;
    public bool Status => true;

    public void Dispose()
    {
        _result = null;
    }
}

/// <summary>
/// ColumnWrapper (truy cập được từ Key-integer hoặc Key-string)
/// </summary>
public class PostgresColumnWrapper : NpOnWrapperResult<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>,
    INpOnColumnWrapper
{
    private readonly Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> _mapper;

    public PostgresColumnWrapper(List<object[]> parent,
        Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> mapper) : base(parent)
    {
        _mapper = mapper;
    }

    protected override IReadOnlyDictionary<int, INpOnCell> CreateResult()
    {
        return _mapper(Parent);
    }

    public IReadOnlyDictionary<int, INpOnCell> GetColumnWrapper() => Result;
}

/// <summary>
/// Collection bọc Column -> truy cập theo cột/hàng 
/// </summary>
public class PostgresColumnCollection : IReadOnlyDictionary<string, PostgresColumnWrapper>,
    IReadOnlyDictionary<int, PostgresColumnWrapper>, INpOnCollectionWrapper
{
    private readonly List<PostgresColumnWrapper> _columnWrappers;
    private readonly IReadOnlyDictionary<string, int> _nameToIndexMap;

    public PostgresColumnCollection(List<object[]> data, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        _nameToIndexMap = nameToIndexMap;
        _columnWrappers = new List<PostgresColumnWrapper>(schemaMap.Count);

        foreach (var schemaInfo in schemaMap.Values)
        {
            var mapper = PostgresMappingExtensions.CreateColumnMapper(schemaInfo.ColumnName, schemaMap, nameToIndexMap);
            _columnWrappers.Add(new PostgresColumnWrapper(data, mapper));
        }
    }

    public PostgresColumnWrapper this[string columnName] => _columnWrappers[_nameToIndexMap[columnName]];
    public PostgresColumnWrapper this[int columnIndex] => _columnWrappers[columnIndex];

    public IEnumerable<string> Keys => _nameToIndexMap.Keys;
    public IEnumerable<PostgresColumnWrapper> Values => _columnWrappers;
    public int Count => _columnWrappers.Count;
    public bool ContainsKey(string key) => _nameToIndexMap.ContainsKey(key);

    public bool TryGetValue(string key, out PostgresColumnWrapper value)
    {
        if (_nameToIndexMap.TryGetValue(key, out int index))
        {
            value = _columnWrappers[index];
            return true;
        }

        value = null!;
        return false;
    }

    public IEnumerator<KeyValuePair<string, PostgresColumnWrapper>> GetEnumerator()
    {
        foreach (var pair in _nameToIndexMap)
        {
            yield return new KeyValuePair<string, PostgresColumnWrapper>(pair.Key, _columnWrappers[pair.Value]);
        }
    }

    IEnumerable<int> IReadOnlyDictionary<int, PostgresColumnWrapper>.Keys => Enumerable.Range(0, Count);
    bool IReadOnlyDictionary<int, PostgresColumnWrapper>.ContainsKey(int key) => key >= 0 && key < Count;

    bool IReadOnlyDictionary<int, PostgresColumnWrapper>.TryGetValue(int key, out PostgresColumnWrapper value)
    {
        if (key >= 0 && key < Count)
        {
            value = _columnWrappers[key];
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<int, PostgresColumnWrapper>> IEnumerable<KeyValuePair<int, PostgresColumnWrapper>>.
        GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return new KeyValuePair<int, PostgresColumnWrapper>(i, _columnWrappers[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IReadOnlyDictionary<int, INpOnColumnWrapper?> GetColumnWrapperByIndexes(int[] indexes)
    {
        indexes = indexes.OrderByDescending(x => x).Where(x => x < Count).Distinct().ToArray();
        Dictionary<int, INpOnColumnWrapper?> result = new();
        foreach (var index in indexes)
            result.Add(index, _columnWrappers[index]);
        return result;
    }

    public IReadOnlyDictionary<string, INpOnColumnWrapper?> GetColumnWrapperByColumnNames(string[]? columnNames = null)
    {
        columnNames ??= Keys.ToArray();
        Dictionary<string, INpOnColumnWrapper?> result = new();
        foreach (var colName in columnNames)
            if (TryGetValue(colName, out var value))
                result.Add(colName, value);
        return result;
    }
}

/// <summary>
/// Zero-allocation view for row wrappers dictionary. Avoids allocating a Dictionary on every RowWrappers call.
/// </summary>
public sealed class PostgresRowWrapperDictionaryView : IReadOnlyDictionary<int, INpOnRowWrapper?>
{
    private readonly PostgresResultSetWrapper _owner;

    public PostgresRowWrapperDictionaryView(PostgresResultSetWrapper owner)
    {
        _owner = owner;
    }

    public INpOnRowWrapper? this[int key] => _owner.GetRowWrapperAt(key);

    public IEnumerable<int> Keys => Enumerable.Range(0, _owner.ActiveRowCount);

    public IEnumerable<INpOnRowWrapper?> Values
    {
        get
        {
            for (int i = 0; i < _owner.ActiveRowCount; i++)
            {
                yield return _owner.GetRowWrapperAt(i);
            }
        }
    }

    public int Count => _owner.ActiveRowCount;

    public bool ContainsKey(int key) => key >= 0 && key < _owner.ActiveRowCount;

    public bool TryGetValue(int key, out INpOnRowWrapper? value)
    {
        if (key >= 0 && key < _owner.ActiveRowCount)
        {
            value = _owner.GetRowWrapperAt(key);
            return true;
        }

        value = null;
        return false;
    }

    public IEnumerator<KeyValuePair<int, INpOnRowWrapper?>> GetEnumerator()
    {
        for (int i = 0; i < _owner.ActiveRowCount; i++)
        {
            yield return new KeyValuePair<int, INpOnRowWrapper?>(i, _owner.GetRowWrapperAt(i));
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class PostgresResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper, IFastTabularResultSet
{
    private sealed class CachedPostgresSchema
    {
        public Dictionary<string, NpOnColumnSchemaInfo> SchemaMap { get; init; } = null!;
        public Dictionary<string, int> NameToIndexMap { get; init; } = null!;
        public List<string> PrimaryKeyColumns { get; init; } = null!;
        public Func<object[], IReadOnlyDictionary<string, INpOnCell>> RowMapper { get; init; } = null!;
        public Action<NpgsqlDataReader, INpOnBlock[], int> RowReader { get; init; } = null!;
        public Type[] ColumnClrTypes { get; init; } = null!;
        public int FieldCount { get; init; }
    }

    private static readonly ConcurrentDictionary<string, CachedPostgresSchema> SchemaCache = new();

    private static Action<NpgsqlDataReader, INpOnBlock[], int> CreateRowReader(Type[] columnClrTypes)
    {
        var dynamicMethod = new DynamicMethod(
            $"PostgresRowReader_{Guid.NewGuid():N}",
            typeof(void),
            [typeof(NpgsqlDataReader), typeof(INpOnBlock[]), typeof(int)], // arg0: reader, arg1: blocks, arg2: rowIdx
            typeof(PostgresResultSetWrapper).Module,
            true);

        var il = dynamicMethod.GetILGenerator();
        var isDbNullMethod = typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.IsDBNull), [typeof(int)])!;

        for (int c = 0; c < columnClrTypes.Length; c++)
        {
            var clrType = columnClrTypes[c];
            var endColLabel = il.DefineLabel();
            var notNullLabel = il.DefineLabel();

            // if (!reader.IsDBNull(c)) goto notNullLabel;
            il.Emit(OpCodes.Ldarg_0); // reader
            il.Emit(OpCodes.Ldc_I4, c);
            il.Emit(OpCodes.Callvirt, isDbNullMethod);
            il.Emit(OpCodes.Brfalse, notNullLabel);

            // DBNull branch
            if (clrType == typeof(long))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(Int64Block));
                il.Emit(OpCodes.Ldfld, typeof(Int64Block).GetField(nameof(Int64Block.NullMask))!);
                il.Emit(OpCodes.Ldarg_2); // rowIdx
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Stelem_I1);
            }
            else if (clrType == typeof(int))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(Int32Block));
                il.Emit(OpCodes.Ldfld, typeof(Int32Block).GetField(nameof(Int32Block.NullMask))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Stelem_I1);
            }
            else if (clrType == typeof(Guid))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(GuidBlock));
                il.Emit(OpCodes.Ldfld, typeof(GuidBlock).GetField(nameof(GuidBlock.NullMask))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Stelem_I1);
            }
            else if (clrType == typeof(DateTime))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(DateTimeBlock));
                il.Emit(OpCodes.Ldfld, typeof(DateTimeBlock).GetField(nameof(DateTimeBlock.NullMask))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Stelem_I1);
            }
            else if (clrType == typeof(bool))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(BooleanBlock));
                il.Emit(OpCodes.Ldfld, typeof(BooleanBlock).GetField(nameof(BooleanBlock.NullMask))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Stelem_I1);
            }
            else if (clrType == typeof(double))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(DoubleBlock));
                il.Emit(OpCodes.Ldfld, typeof(DoubleBlock).GetField(nameof(DoubleBlock.NullMask))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Stelem_I1);
            }
            il.Emit(OpCodes.Br, endColLabel);

            // NotNull branch
            il.MarkLabel(notNullLabel);
            if (clrType == typeof(long))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(Int64Block));
                il.Emit(OpCodes.Ldfld, typeof(Int64Block).GetField(nameof(Int64Block.Buffer))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt64), [typeof(int)])!);
                il.Emit(OpCodes.Stelem_I8);
            }
            else if (clrType == typeof(int))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(Int32Block));
                il.Emit(OpCodes.Ldfld, typeof(Int32Block).GetField(nameof(Int32Block.Buffer))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt32), [typeof(int)])!);
                il.Emit(OpCodes.Stelem_I4);
            }
            else if (clrType == typeof(Guid))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(GuidBlock));
                il.Emit(OpCodes.Ldfld, typeof(GuidBlock).GetField(nameof(GuidBlock.Buffer))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetGuid), [typeof(int)])!);
                il.Emit(OpCodes.Stelem, typeof(Guid));
            }
            else if (clrType == typeof(DateTime))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(DateTimeBlock));
                il.Emit(OpCodes.Ldfld, typeof(DateTimeBlock).GetField(nameof(DateTimeBlock.Buffer))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetDateTime), [typeof(int)])!);
                il.Emit(OpCodes.Stelem, typeof(DateTime));
            }
            else if (clrType == typeof(bool))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(BooleanBlock));
                il.Emit(OpCodes.Ldfld, typeof(BooleanBlock).GetField(nameof(BooleanBlock.Buffer))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetBoolean), [typeof(int)])!);
                il.Emit(OpCodes.Stelem_I1);
            }
            else if (clrType == typeof(double))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(DoubleBlock));
                il.Emit(OpCodes.Ldfld, typeof(DoubleBlock).GetField(nameof(DoubleBlock.Buffer))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetDouble), [typeof(int)])!);
                il.Emit(OpCodes.Stelem_R8);
            }
            else if (clrType == typeof(string))
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(StringBlock));
                il.Emit(OpCodes.Ldfld, typeof(StringBlock).GetField(nameof(StringBlock.Buffer))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetString), [typeof(int)])!);
                il.Emit(OpCodes.Stelem_Ref);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Castclass, typeof(ObjectBlock));
                il.Emit(OpCodes.Ldfld, typeof(ObjectBlock).GetField(nameof(ObjectBlock.Buffer))!);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, c);
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetValue), [typeof(int)])!);
                il.Emit(OpCodes.Stelem_Ref);
            }

            il.MarkLabel(endColLabel);
        }

        il.Emit(OpCodes.Ret);
        return (Action<NpgsqlDataReader, INpOnBlock[], int>)dynamicMethod.CreateDelegate(typeof(Action<NpgsqlDataReader, INpOnBlock[], int>));
    }

    private static INpOnBlock CreateTypedBlock(Type clrType, int capacity)
    {
        if (clrType == typeof(long)) return new Int64Block(capacity, hasNulls: true);
        if (clrType == typeof(int)) return new Int32Block(capacity, hasNulls: true);
        if (clrType == typeof(Guid)) return new GuidBlock(capacity, hasNulls: true);
        if (clrType == typeof(DateTime)) return new DateTimeBlock(capacity, hasNulls: true);
        if (clrType == typeof(bool)) return new BooleanBlock(capacity, hasNulls: true);
        if (clrType == typeof(double)) return new DoubleBlock(capacity, hasNulls: true);
        if (clrType == typeof(string)) return new StringBlock(capacity);
        return new ObjectBlock(capacity);
    }

    // Vectorized Columnar Pages
    private IReadOnlyList<NpOnDataPage>? _pages;
    public IReadOnlyList<NpOnDataPage>? Pages => _pages;

    // Zero-Allocation Contiguous ArrayPool buffer (0 heap arrays allocated per row)
    private object?[]? _rentedFlatBuffer;
    private int _activeRowCount = 0;
    private int _colCount = 0;
    private IReadOnlyDictionary<string, int> _nameToIndexMap = null!;
    private IReadOnlyList<string>? _primaryKeyColumns;
    private Func<object[], IReadOnlyDictionary<string, INpOnCell>>? _rowMapper;
    private IReadOnlyDictionary<string, NpOnColumnSchemaInfo>? _schemaMap;

    private readonly List<PostgresRowWrapper> _pooledRows = new();
    private List<object[]>? _lazyRawData;
    private readonly PostgresRowWrapperDictionaryView _rowWrappersView;

    // Fast Tabular interface implementation
    public object?[]? FlatData
    {
        get
        {
            if (_rentedFlatBuffer != null) return _rentedFlatBuffer;
            if (_pages != null && _pages.Count > 0)
            {
                _rentedFlatBuffer = ArrayPool<object?>.Shared.Rent(_activeRowCount * _colCount);
                int offset = 0;
                for (int p = 0; p < _pages.Count; p++)
                {
                    var page = _pages[p];
                    int pRows = page.RowCount;
                    for (int r = 0; r < pRows; r++)
                    {
                        for (int c = 0; c < _colCount; c++)
                        {
                            _rentedFlatBuffer[offset + c] = page.Blocks[c].GetObject(r);
                        }
                        offset += _colCount;
                    }
                }
            }
            return _rentedFlatBuffer;
        }
    }
    public int RowCount => _activeRowCount;
    public int ColCount => _colCount;
    public IReadOnlyDictionary<string, int> NameToIndexMap => _nameToIndexMap;
    public IReadOnlyList<string>? PrimaryKeyColumns => _primaryKeyColumns;

    public IReadOnlyList<object[]> RawData
    {
        get
        {
            if (_lazyRawData != null) return _lazyRawData;
            var list = new List<object[]>(_activeRowCount);
            int colCount = _colCount;
            var flat = FlatData;
            if (flat != null)
            {
                for (int r = 0; r < _activeRowCount; r++)
                {
                    var row = new object[colCount];
                    Array.Copy(flat, r * colCount, row, 0, colCount);
                    list.Add(row);
                }
            }
            return _lazyRawData = list;
        }
    }

    private Func<PostgresColumnCollection>? _columnsFactory;
    private PostgresColumnCollection? _columns;

    public Action<PostgresResultSetWrapper>? ReturnToPool { get; set; }
    public int ActiveRowCount => _activeRowCount;

    public PostgresRowWrapper? GetRowWrapperAt(int index)
    {
        var flat = FlatData;
        if (index >= 0 && index < _activeRowCount && _rowMapper != null && flat != null)
        {
            var rowValues = new object[_colCount];
            Array.Copy(flat, index * _colCount, rowValues, 0, _colCount);
            while (_pooledRows.Count <= index)
            {
                _pooledRows.Add(new PostgresRowWrapper(rowValues, _rowMapper));
            }
            _pooledRows[index].ReInit(rowValues, _rowMapper);
            return _pooledRows[index];
        }
        return null;
    }

    public PostgresResultSetWrapper()
    {
        _rowWrappersView = new PostgresRowWrapperDictionaryView(this);
    }

    public PostgresResultSetWrapper(NpgsqlDataReader? reader = null) : this()
    {
        Init(reader);
    }

    public void Init(NpgsqlDataReader? reader)
    {
        Reset();

        if (reader == null)
        {
            SetFail(EDbError.PostgresDataTableNull);
            return;
        }

        if (!reader.HasRows)
        {
            SetSuccess();
            return;
        }

        int fieldCount = reader.FieldCount;

        // Permanent Schema Cache lookup by Column Names Fingerprint (0 reflection on hot-path)
        var keyParts = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++) keyParts[i] = reader.GetName(i);
        var schemaKey = $"{fieldCount}:" + string.Join(";", keyParts);

        var cached = SchemaCache.GetOrAdd(schemaKey, _ =>
        {
            ReadOnlyCollection<NpgsqlDbColumn>? columnSchema = null;
            if (reader.CanGetColumnSchema())
            {
                columnSchema = reader.GetColumnSchema();
            }

            var sMap = new Dictionary<string, NpOnColumnSchemaInfo>(fieldCount);
            var nMap = new Dictionary<string, int>(fieldCount);
            var pks = new List<string>();
            var clrTypes = new Type[fieldCount];

            for (int i = 0; i < fieldCount; i++)
            {
                var colName = reader.GetName(i);
                var isPk = columnSchema?[i].IsKey ?? false;
                if (isPk) pks.Add(colName);
                var fieldType = reader.GetFieldType(i);
                clrTypes[i] = fieldType;
                sMap.Add(colName, new NpOnColumnSchemaInfo(colName, fieldType, reader.GetDataTypeName(i), isPk));
                nMap.Add(colName, i);
            }

            if (pks.Count == 0 && fieldCount > 0) pks.Add(reader.GetName(0));
            var rm = PostgresMappingExtensions.CreateRowMapper(sMap, nMap);
            var rowReader = CreateRowReader(clrTypes);

            return new CachedPostgresSchema
            {
                SchemaMap = sMap,
                NameToIndexMap = nMap,
                PrimaryKeyColumns = pks,
                RowMapper = rm,
                RowReader = rowReader,
                ColumnClrTypes = clrTypes,
                FieldCount = fieldCount
            };
        });

        _nameToIndexMap = cached.NameToIndexMap;
        _primaryKeyColumns = cached.PrimaryKeyColumns;
        _rowMapper = cached.RowMapper;
        _schemaMap = cached.SchemaMap;
        _colCount = fieldCount;

        // Vectorized Columnar Streaming Page Reader: Zero Boxing, 0 heap object allocations
        const int VectorPageSize = 2048;
        var pages = new List<NpOnDataPage>();
        var currentBlocks = new INpOnBlock[fieldCount];
        for (int c = 0; c < fieldCount; c++)
            currentBlocks[c] = CreateTypedBlock(cached.ColumnClrTypes[c], VectorPageSize);

        int pageRowIdx = 0;
        int totalRowCount = 0;
        var rowReaderCompiled = cached.RowReader;

        while (reader.Read())
        {
            if (pageRowIdx >= VectorPageSize)
            {
                pages.Add(new NpOnDataPage(pageRowIdx, currentBlocks, _nameToIndexMap));
                currentBlocks = new INpOnBlock[fieldCount];
                for (int c = 0; c < fieldCount; c++)
                    currentBlocks[c] = CreateTypedBlock(cached.ColumnClrTypes[c], VectorPageSize);
                pageRowIdx = 0;
            }

            rowReaderCompiled(reader, currentBlocks, pageRowIdx);
            pageRowIdx++;
            totalRowCount++;
        }

        if (pageRowIdx > 0)
        {
            pages.Add(new NpOnDataPage(pageRowIdx, currentBlocks, _nameToIndexMap));
        }
        else
        {
            for (int c = 0; c < fieldCount; c++) currentBlocks[c].Dispose();
        }

        _pages = pages;
        _activeRowCount = totalRowCount;
        _columnsFactory = () => new PostgresColumnCollection(RawData.ToList(), _schemaMap, _nameToIndexMap);

        SetSuccess();
    }

    public void Reset()
    {
        if (_pages != null)
        {
            for (int i = 0; i < _pages.Count; i++) _pages[i].Dispose();
            _pages = null;
        }
        if (_rentedFlatBuffer != null)
        {
            ArrayPool<object?>.Shared.Return(_rentedFlatBuffer, clearArray: false);
            _rentedFlatBuffer = null;
        }
        _lazyRawData = null;
        _activeRowCount = 0;
        _colCount = 0;
        _nameToIndexMap = null!;
        _primaryKeyColumns = null;
        _rowMapper = null;
        _schemaMap = null;
        _columnsFactory = null;
        _columns = null;
    }

    public IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers => _rowWrappersView;

    public INpOnCollectionWrapper CollectionWrappers => _columns ??= (_columnsFactory?.Invoke() ?? null!);

    public override void Dispose()
    {
        if (_pages != null)
        {
            for (int i = 0; i < _pages.Count; i++) _pages[i].Dispose();
            _pages = null;
        }
        if (_rentedFlatBuffer != null)
        {
            ArrayPool<object?>.Shared.Return(_rentedFlatBuffer, clearArray: false);
            _rentedFlatBuffer = null;
        }
        ReturnToPool?.Invoke(this);
    }
}
