using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using ClickHouse.Client.ADO.Readers;

namespace Common.Infrastructures.NpOn.ClickHouseExtCm.Results;

public class ClickHouseRowWrapper : NpOnWrapperResult<object[], IReadOnlyDictionary<string, INpOnCell>>, INpOnRowWrapper
{
    private readonly Func<object[], IReadOnlyDictionary<string, INpOnCell>> _mapper;

    public ClickHouseRowWrapper(object[] parent, Func<object[], IReadOnlyDictionary<string, INpOnCell>> mapper) :
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
        columnNames ??= Keys.ToArray();
        Dictionary<string, INpOnColumnWrapper?> result = new();
        foreach (var colName in columnNames)
            if (TryGetValue(colName, out var value))
                result.Add(colName, value);
        return result;
    }
}

public class ClickHouseResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    private IReadOnlyDictionary<int, ClickHouseRowWrapper> Rows { get; set; } = new Dictionary<int, ClickHouseRowWrapper>();
    private ClickHouseColumnCollection? Columns { get; set; }

    public Action<ClickHouseResultSetWrapper>? ReturnToPool { get; set; }

    public ClickHouseResultSetWrapper()
    {
    }

    public ClickHouseResultSetWrapper(ClickHouseDataReader? reader = null)
    {
        Init(reader);
    }

    public void Init(ClickHouseDataReader? reader)
    {
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

        // ClickHouse.Client DataReader schema
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var clickHouseType = reader.GetDataTypeName(i);
            
            var schemaInfo = new NpOnColumnSchemaInfo(
                columnName,
                reader.GetFieldType(i) ?? typeof(object),
                clickHouseType,
                false // ClickHouse drivers don't always expose PK info in schema
            );
            schemaMap.Add(columnName, schemaInfo);
            nameToIndexMap.Add(columnName, i);
        }

        var normalizeMethod = typeof(ClickHouseUtils).GetMethod(nameof(ClickHouseUtils.NormalizeClickHouseValue), new[] { typeof(object) });
        var mapper = ClickHouseMappingExtensions.CreateArrayRowMapper(reader, normalizeMethod!);

        var data = new List<object[]>();
        while (reader.Read())
        {
            data.Add(mapper(reader));
        }

        var rowMapper = ClickHouseMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);
        var rows = new Dictionary<int, ClickHouseRowWrapper>(data.Count);
        for (int i = 0; i < data.Count; i++)
        {
            rows.Add(i, new ClickHouseRowWrapper(data[i], rowMapper));
        }

        Rows = rows;
        Columns = new ClickHouseColumnCollection(data, schemaMap, nameToIndexMap);
        SetSuccess();
    }

    public void Reset()
    {
        Rows = new Dictionary<int, ClickHouseRowWrapper>();
        Columns = null;
    }

    public IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers
    {
        get
        {
            Dictionary<int, INpOnRowWrapper?> result = new();
            foreach (var row in Rows)
                result.Add(row.Key, row.Value);
            return result;
        }
    }

    public INpOnCollectionWrapper CollectionWrappers => Columns!;

    public override void Dispose()
    {
        ReturnToPool?.Invoke(this);
    }
}
