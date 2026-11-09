using System.Collections;
using System.Collections.ObjectModel;
using System.Data.Common;
using ClickHouse.Client.ADO.Readers;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.ClickHouseExtCm.Results;

public class ClickHouseRowWrapper : INpOnRowWrapper, INpOnWrapperResult<object[], IReadOnlyDictionary<string, INpOnCell>>
{
    private object[] _parent;
    private Func<object[], IReadOnlyDictionary<string, INpOnCell>> _mapper;
    private IReadOnlyDictionary<string, INpOnCell>? _result;

    public ClickHouseRowWrapper(object[] parent, Func<object[], IReadOnlyDictionary<string, INpOnCell>> mapper)
    {
        _parent = parent;
        _mapper = mapper;
        _result = null;
    }

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

public class ClickHouseColumnWrapper : NpOnWrapperResult<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>,
    INpOnColumnWrapper
{
    private readonly Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> _mapper;

    public ClickHouseColumnWrapper(List<object[]> parent,
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

public class ClickHouseColumnCollection : IReadOnlyDictionary<string, ClickHouseColumnWrapper>,
    IReadOnlyDictionary<int, ClickHouseColumnWrapper>, INpOnCollectionWrapper
{
    private readonly List<ClickHouseColumnWrapper> _columnWrappers;
    private readonly IReadOnlyDictionary<string, int> _nameToIndexMap;

    public ClickHouseColumnCollection(List<object[]> data, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        _nameToIndexMap = nameToIndexMap;
        _columnWrappers = new List<ClickHouseColumnWrapper>(schemaMap.Count);

        foreach (var schemaInfo in schemaMap.Values)
        {
            var mapper = ClickHouseMappingExtensions.CreateColumnMapper(schemaInfo.ColumnName, schemaMap, nameToIndexMap);
            _columnWrappers.Add(new ClickHouseColumnWrapper(data, mapper));
        }
    }

    public ClickHouseColumnWrapper this[string columnName] => _columnWrappers[_nameToIndexMap[columnName]];
    public ClickHouseColumnWrapper this[int columnIndex] => _columnWrappers[columnIndex];

    public IEnumerable<string> Keys => _nameToIndexMap.Keys;
    public IEnumerable<ClickHouseColumnWrapper> Values => _columnWrappers;
    public int Count => _columnWrappers.Count;
    public bool ContainsKey(string key) => _nameToIndexMap.ContainsKey(key);

    public bool TryGetValue(string key, out ClickHouseColumnWrapper value)
    {
        if (_nameToIndexMap.TryGetValue(key, out int index))
        {
            value = _columnWrappers[index];
            return true;
        }

        value = null!;
        return false;
    }

    public IEnumerator<KeyValuePair<string, ClickHouseColumnWrapper>> GetEnumerator()
    {
        foreach (var pair in _nameToIndexMap)
        {
            yield return new KeyValuePair<string, ClickHouseColumnWrapper>(pair.Key, _columnWrappers[pair.Value]);
        }
    }

    IEnumerable<int> IReadOnlyDictionary<int, ClickHouseColumnWrapper>.Keys => Enumerable.Range(0, Count);
    bool IReadOnlyDictionary<int, ClickHouseColumnWrapper>.ContainsKey(int key) => key >= 0 && key < Count;

    bool IReadOnlyDictionary<int, ClickHouseColumnWrapper>.TryGetValue(int key, out ClickHouseColumnWrapper value)
    {
        if (key >= 0 && key < Count)
        {
            value = _columnWrappers[key];
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<int, ClickHouseColumnWrapper>> IEnumerable<KeyValuePair<int, ClickHouseColumnWrapper>>.
        GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return new KeyValuePair<int, ClickHouseColumnWrapper>(i, _columnWrappers[i]);
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

public class ClickHouseRowWrapperDictionaryView : IReadOnlyDictionary<int, INpOnRowWrapper?>
{
    private readonly ClickHouseResultSetWrapper _owner;

    public ClickHouseRowWrapperDictionaryView(ClickHouseResultSetWrapper owner)
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
                yield return _owner.GetRowWrapperAt(i);
        }
    }

    public int Count => _owner.ActiveRowCount;
    public bool ContainsKey(int key) => key >= 0 && key < _owner.ActiveRowCount;

    public bool TryGetValue(int key, out INpOnRowWrapper? value)
    {
        if (ContainsKey(key))
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
            yield return new KeyValuePair<int, INpOnRowWrapper?>(i, _owner.GetRowWrapperAt(i));
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class ClickHouseResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    private readonly List<object[]> _data = new();
    private readonly List<ClickHouseRowWrapper> _pooledRows = new();
    private int _activeRowCount = 0;
    private IReadOnlyDictionary<string, int> _nameToIndexMap = null!;
    private IReadOnlyList<string>? _primaryKeyColumns;
    private Func<object[], IReadOnlyDictionary<string, INpOnCell>>? _rowMapper;

    private readonly ClickHouseRowWrapperDictionaryView _rowWrappersView;

    // Fast Tabular interface implementation
    public IReadOnlyDictionary<string, int> NameToIndexMap => _nameToIndexMap;
    public IReadOnlyList<object[]> RawData => _data;
    public IReadOnlyList<string>? PrimaryKeyColumns => _primaryKeyColumns;

    private Func<ClickHouseColumnCollection>? _columnsFactory;
    private ClickHouseColumnCollection? _columns;

    public Action<ClickHouseResultSetWrapper>? ReturnToPool { get; set; }
    public int ActiveRowCount => _activeRowCount;

    public ClickHouseRowWrapper? GetRowWrapperAt(int index)
    {
        if (index >= 0 && index < _activeRowCount && _rowMapper != null)
        {
            while (_pooledRows.Count <= index)
            {
                _pooledRows.Add(new ClickHouseRowWrapper(_data[_pooledRows.Count], _rowMapper));
            }
            _pooledRows[index].ReInit(_data[index], _rowMapper);
            return _pooledRows[index];
        }
        return null;
    }

    public ClickHouseResultSetWrapper()
    {
        _rowWrappersView = new ClickHouseRowWrapperDictionaryView(this);
    }

    public ClickHouseResultSetWrapper(ClickHouseDataReader? reader = null) : this()
    {
        Init(reader);
    }

    public void Init(ClickHouseDataReader? reader)
    {
        Reset();

        if (reader == null)
        {
            SetFail(EDbError.ClickHouseDataTableNull);
            return;
        }

        if (!reader.HasRows)
        {
            SetSuccess();
            return;
        }

        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>(reader.FieldCount);
        var nameToIndexMap = new Dictionary<string, int>(reader.FieldCount);
        var pkCols = new List<string>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var clickHouseType = reader.GetDataTypeName(i);
            
            var schemaInfo = new NpOnColumnSchemaInfo(
                columnName,
                reader.GetFieldType(i) ?? typeof(object),
                clickHouseType,
                false
            );
            schemaMap.Add(columnName, schemaInfo);
            nameToIndexMap.Add(columnName, i);
        }

        if (reader.FieldCount > 0)
        {
            pkCols.Add(reader.GetName(0));
        }

        _nameToIndexMap = nameToIndexMap;
        _primaryKeyColumns = pkCols;

        var normalizeMethod = typeof(ClickHouseUtils).GetMethod(nameof(ClickHouseUtils.NormalizeClickHouseValue), new[] { typeof(object) });
        var mapper = ClickHouseMappingExtensions.CreateArrayRowMapper(reader, normalizeMethod!);

        while (reader.Read())
        {
            _data.Add(mapper(reader));
        }

        _activeRowCount = _data.Count;
        _rowMapper = ClickHouseMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);
        _columnsFactory = () => new ClickHouseColumnCollection(_data, schemaMap, nameToIndexMap);

        SetSuccess();
    }

    public void Reset()
    {
        _activeRowCount = 0;
        _data.Clear();
        _nameToIndexMap = null!;
        _primaryKeyColumns = null;
        _rowMapper = null;
        _columnsFactory = null;
        _columns = null;
    }

    public IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers => _rowWrappersView;
    public INpOnCollectionWrapper CollectionWrappers => _columns ??= (_columnsFactory?.Invoke() ?? null!);

    public override void Dispose()
    {
        ReturnToPool?.Invoke(this);
    }
}
