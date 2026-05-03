using System.Collections.ObjectModel;
using System.Data.Common;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using MySqlConnector;

namespace Common.Infrastructures.NpOn.MySqlExtCm.Results;

/// <summary>
/// ColumnWrapper
/// </summary>
public class MySqlRowWrapper : NpOnWrapperResult<object[], IReadOnlyDictionary<string, INpOnCell>>, INpOnRowWrapper
{
    private readonly Func<object[], IReadOnlyDictionary<string, INpOnCell>> _mapper;

    public MySqlRowWrapper(object[] parent, Func<object[], IReadOnlyDictionary<string, INpOnCell>> mapper) :
        base(parent)
    {
        _mapper = mapper;
    }

    protected override IReadOnlyDictionary<string, INpOnCell> CreateResult()
    {
        return _mapper(Parent);
    }

    public IReadOnlyDictionary<string, INpOnCell> GetRowWrapper() => Result;
}

/// <summary>
/// ColumnWrapper (truy cập được từ Key-integer hoặc Key-string)
/// </summary>
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

/// <summary>
/// Collection bọc Column -> truy cập theo cột/hàng 
/// </summary>
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

    // reader
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

    // IReadOnlyDictionary<int, ...>
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

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

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
        columnNames ??= Keys.ToArray(); // get all
        Dictionary<string, INpOnColumnWrapper?> result = new();
        foreach (var colName in columnNames)
            if (TryGetValue(colName, out var value))
                result.Add(colName, value);
        return result;
    }
}

public class MySqlResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    private IReadOnlyDictionary<int, MySqlRowWrapper> Rows { get; set; }
    private MySqlColumnCollection Columns { get; set; }

    // Delegate to return this object to the pool
    public Action<MySqlResultSetWrapper>? ReturnToPool { get; set; }

    public MySqlResultSetWrapper()
    {
        // Default constructor for pooling
        Rows = new Dictionary<int, MySqlRowWrapper>();
        Columns = null!;
    }

    public MySqlResultSetWrapper(MySqlDataReader? reader = null)
    {
        Init(reader);
    }

    public void Init(MySqlDataReader? reader)
    {
        if (reader == null)
        {
            SetFail(EDbError.MySqlDataTableNull);
            Rows = new Dictionary<int, MySqlRowWrapper>();
            Columns = null!; // Or an empty collection
            return;
        }

        if (!reader.HasRows)
        {
            Rows = new Dictionary<int, MySqlRowWrapper>();
            Columns = null!;
            SetSuccess();
            return;
        }

        // 1. Build schema and name-to-index map
        ReadOnlyCollection<DbColumn> columnSchema = null;
        if (reader.CanGetColumnSchema())
        {
            columnSchema = reader.GetColumnSchema();
        }
        
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>(reader.FieldCount);
        var nameToIndexMap = new Dictionary<string, int>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var isPrimaryKey = columnSchema?[i].IsKey ?? false;
            var schemaInfo = new NpOnColumnSchemaInfo(
                columnName,
                reader.GetFieldType(i),
                reader.GetDataTypeName(i),
                isPrimaryKey
            );
            schemaMap.Add(columnName, schemaInfo);
            nameToIndexMap.Add(columnName, i);
        }

        // 2. Create high-performance mapper using Extension Method
        var normalizeMethod =
            typeof(MySqlUtils).GetMethod(nameof(MySqlUtils.NormalizeMySqlValue), new[] { typeof(object) });
        var mapper = MySqlMappingExtensions.CreateArrayRowMapper(reader, normalizeMethod);

        var data = new List<object[]>();

        // 3. Read data
        while (reader.Read())
        {
            data.Add(mapper(reader));
        }

        // 4. Wrap data
        var rowMapper = MySqlMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);
        var rows = new Dictionary<int, MySqlRowWrapper>(data.Count);
        for (int i = 0; i < data.Count; i++)
        {
            rows.Add(i, new MySqlRowWrapper(data[i], rowMapper));
        }

        Rows = rows;

        Columns = new MySqlColumnCollection(data, schemaMap, nameToIndexMap);

        SetSuccess();
    }

    public void Reset() // for objectPooling // Reset state for reuse
    {
        Rows = new Dictionary<int, MySqlRowWrapper>();
        Columns = null!;
        // Reset base class state if needed (e.g. Status, Error)
        // Assuming SetSuccess/SetFail handles this, but we might need to clear errors manually if SetSuccess doesn't.
        // NpOnWrapperResult usually has Status and Error properties.
    }

    public IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers
    {
        get
        {
            Dictionary<int, INpOnRowWrapper?> result = new();
            if (Rows is not { Count: > 0 })
                return result;
            foreach (var row in Rows)
                result.Add(row.Key, row.Value);
            return result;
        }
    }

    public INpOnCollectionWrapper CollectionWrappers => Columns;

    public override void Dispose()
    {
        ReturnToPool?.Invoke(this);
    }
}