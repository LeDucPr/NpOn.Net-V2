using System.Collections;
using System.Collections.ObjectModel;
using System.Data.Common;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using MySqlConnector;

namespace Common.Infrastructures.NpOn.MySqlExtCm.Results;

public class MySqlRowWrapper : INpOnRowWrapper, INpOnWrapperResult<object[], IReadOnlyDictionary<string, INpOnCell>>
{
    private object[] _parent;
    private Func<object[], IReadOnlyDictionary<string, INpOnCell>> _mapper;
    private IReadOnlyDictionary<string, INpOnCell>? _result;

    public MySqlRowWrapper(object[] parent, Func<object[], IReadOnlyDictionary<string, INpOnCell>> mapper)
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

public class MySqlColumnWrapper : NpOnWrapperResult<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>,
    INpOnColumnWrapper
{
    private readonly Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> _mapper;

    public MySqlColumnWrapper(List<object[]> parent,
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

public class MySqlColumnCollection : IReadOnlyDictionary<string, MySqlColumnWrapper>,
    IReadOnlyDictionary<int, MySqlColumnWrapper>, INpOnCollectionWrapper
{
    private readonly List<MySqlColumnWrapper> _columnWrappers;
    private readonly IReadOnlyDictionary<string, int> _nameToIndexMap;

    public MySqlColumnCollection(List<object[]> data, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        _nameToIndexMap = nameToIndexMap;
        _columnWrappers = new List<MySqlColumnWrapper>(schemaMap.Count);

        foreach (var schemaInfo in schemaMap.Values)
        {
            var mapper = MySqlMappingExtensions.CreateColumnMapper(schemaInfo.ColumnName, schemaMap, nameToIndexMap);
            _columnWrappers.Add(new MySqlColumnWrapper(data, mapper));
        }
    }

    public MySqlColumnWrapper this[string columnName] => _columnWrappers[_nameToIndexMap[columnName]];
    public MySqlColumnWrapper this[int columnIndex] => _columnWrappers[columnIndex];

    public IEnumerable<string> Keys => _nameToIndexMap.Keys;
    public IEnumerable<MySqlColumnWrapper> Values => _columnWrappers;
    public int Count => _columnWrappers.Count;
    public bool ContainsKey(string key) => _nameToIndexMap.ContainsKey(key);

    public bool TryGetValue(string key, out MySqlColumnWrapper value)
    {
        if (_nameToIndexMap.TryGetValue(key, out int index))
        {
            value = _columnWrappers[index];
            return true;
        }

        value = null!;
        return false;
    }

    public IEnumerator<KeyValuePair<string, MySqlColumnWrapper>> GetEnumerator()
    {
        foreach (var pair in _nameToIndexMap)
        {
            yield return new KeyValuePair<string, MySqlColumnWrapper>(pair.Key, _columnWrappers[pair.Value]);
        }
    }

    IEnumerable<int> IReadOnlyDictionary<int, MySqlColumnWrapper>.Keys => Enumerable.Range(0, Count);
    bool IReadOnlyDictionary<int, MySqlColumnWrapper>.ContainsKey(int key) => key >= 0 && key < Count;

    bool IReadOnlyDictionary<int, MySqlColumnWrapper>.TryGetValue(int key, out MySqlColumnWrapper value)
    {
        if (key >= 0 && key < Count)
        {
            value = _columnWrappers[key];
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<int, MySqlColumnWrapper>> IEnumerable<KeyValuePair<int, MySqlColumnWrapper>>.
        GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return new KeyValuePair<int, MySqlColumnWrapper>(i, _columnWrappers[i]);
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

public class MySqlRowWrapperDictionaryView : IReadOnlyDictionary<int, INpOnRowWrapper?>
{
    private readonly MySqlResultSetWrapper _owner;

    public MySqlRowWrapperDictionaryView(MySqlResultSetWrapper owner)
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

public class MySqlResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    private readonly List<object[]> _data = new();
    private readonly List<MySqlRowWrapper> _pooledRows = new();
    private int _activeRowCount = 0;
    private IReadOnlyDictionary<string, int> _nameToIndexMap = null!;
    private IReadOnlyList<string>? _primaryKeyColumns;
    private Func<object[], IReadOnlyDictionary<string, INpOnCell>>? _rowMapper;

    private readonly MySqlRowWrapperDictionaryView _rowWrappersView;

    // Fast Tabular interface implementation
    public IReadOnlyDictionary<string, int> NameToIndexMap => _nameToIndexMap;
    public IReadOnlyList<object[]> RawData => _data;
    public IReadOnlyList<string>? PrimaryKeyColumns => _primaryKeyColumns;

    private Func<MySqlColumnCollection>? _columnsFactory;
    private MySqlColumnCollection? _columns;

    public Action<MySqlResultSetWrapper>? ReturnToPool { get; set; }
    public int ActiveRowCount => _activeRowCount;

    public MySqlRowWrapper? GetRowWrapperAt(int index)
    {
        if (index >= 0 && index < _activeRowCount && _rowMapper != null)
        {
            while (_pooledRows.Count <= index)
            {
                _pooledRows.Add(new MySqlRowWrapper(_data[_pooledRows.Count], _rowMapper));
            }
            _pooledRows[index].ReInit(_data[index], _rowMapper);
            return _pooledRows[index];
        }
        return null;
    }

    public MySqlResultSetWrapper()
    {
        _rowWrappersView = new MySqlRowWrapperDictionaryView(this);
    }

    public MySqlResultSetWrapper(MySqlDataReader? reader = null) : this()
    {
        Init(reader);
    }

    public void Init(MySqlDataReader? reader)
    {
        Reset();

        if (reader == null)
        {
            SetFail(EDbError.MySqlDataTableNull);
            return;
        }

        if (!reader.HasRows)
        {
            SetSuccess();
            return;
        }

        ReadOnlyCollection<DbColumn>? columnSchema = null;
        if (reader.CanGetColumnSchema())
        {
            columnSchema = reader.GetColumnSchema();
        }
        
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>(reader.FieldCount);
        var nameToIndexMap = new Dictionary<string, int>(reader.FieldCount);
        var pkCols = new List<string>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var isPrimaryKey = columnSchema?[i].IsKey ?? false;
            if (isPrimaryKey) pkCols.Add(columnName);

            var schemaInfo = new NpOnColumnSchemaInfo(
                columnName,
                reader.GetFieldType(i),
                reader.GetDataTypeName(i),
                isPrimaryKey
            );
            schemaMap.Add(columnName, schemaInfo);
            nameToIndexMap.Add(columnName, i);
        }

        if (pkCols.Count == 0 && reader.FieldCount > 0)
        {
            pkCols.Add(reader.GetName(0));
        }

        _nameToIndexMap = nameToIndexMap;
        _primaryKeyColumns = pkCols;

        var normalizeMethod =
            typeof(MySqlUtils).GetMethod(nameof(MySqlUtils.NormalizeMySqlValue), new[] { typeof(object) });
        var mapper = MySqlMappingExtensions.CreateArrayRowMapper(reader, normalizeMethod);

        while (reader.Read())
        {
            _data.Add(mapper(reader));
        }

        _activeRowCount = _data.Count;
        _rowMapper = MySqlMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);
        _columnsFactory = () => new MySqlColumnCollection(_data, schemaMap, nameToIndexMap);

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
