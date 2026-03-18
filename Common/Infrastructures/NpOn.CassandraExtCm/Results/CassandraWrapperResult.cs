using Cassandra;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Results;

/// <summary>
/// ColumnWrapper
/// </summary>
public class CassandraRowWrapper : NpOnWrapperResult<object[], IReadOnlyDictionary<string, INpOnCell>>, INpOnRowWrapper
{
    private readonly Func<object[], IReadOnlyDictionary<string, INpOnCell>> _mapper;

    public CassandraRowWrapper(object[] parent, Func<object[], IReadOnlyDictionary<string, INpOnCell>> mapper) :
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
public class CassandraColumnWrapper : NpOnWrapperResult<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>,
    INpOnColumnWrapper
{
    private readonly Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> _mapper;

    public CassandraColumnWrapper(List<object[]> parent,
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
public class CassandraColumnCollection : IReadOnlyDictionary<string, CassandraColumnWrapper>,
    IReadOnlyDictionary<int, CassandraColumnWrapper>, INpOnCollectionWrapper
{
    private readonly List<CassandraColumnWrapper> _columnWrappers;
    private readonly IReadOnlyDictionary<string, int> _nameToIndexMap;

    public CassandraColumnCollection(List<object[]> data, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        _nameToIndexMap = nameToIndexMap;
        _columnWrappers = new List<CassandraColumnWrapper>(schemaMap.Count);

        foreach (var schemaInfo in schemaMap.Values)
        {
            var mapper = CassandraMappingExtensions.CreateColumnMapper(schemaInfo.ColumnName, schemaMap, nameToIndexMap);
            _columnWrappers.Add(new CassandraColumnWrapper(data, mapper));
        }
    }

    public CassandraColumnWrapper this[string columnName] => _columnWrappers[_nameToIndexMap[columnName]];
    public CassandraColumnWrapper this[int columnIndex] => _columnWrappers[columnIndex];

    // reader
    public IEnumerable<string> Keys => _nameToIndexMap.Keys;
    public IEnumerable<CassandraColumnWrapper> Values => _columnWrappers;
    public int Count => _columnWrappers.Count;
    public bool ContainsKey(string key) => _nameToIndexMap.ContainsKey(key);

    public bool TryGetValue(string key, out CassandraColumnWrapper value)
    {
        if (_nameToIndexMap.TryGetValue(key, out int index))
        {
            value = _columnWrappers[index];
            return true;
        }

        value = null!;
        return false;
    }

    public IEnumerator<KeyValuePair<string, CassandraColumnWrapper>> GetEnumerator()
    {
        foreach (var pair in _nameToIndexMap)
        {
            yield return new KeyValuePair<string, CassandraColumnWrapper>(pair.Key, _columnWrappers[pair.Value]);
        }
    }

    // IReadOnlyDictionary<int, ...>
    IEnumerable<int> IReadOnlyDictionary<int, CassandraColumnWrapper>.Keys => Enumerable.Range(0, Count);
    bool IReadOnlyDictionary<int, CassandraColumnWrapper>.ContainsKey(int key) => key >= 0 && key < Count;

    bool IReadOnlyDictionary<int, CassandraColumnWrapper>.TryGetValue(int key, out CassandraColumnWrapper value)
    {
        if (key >= 0 && key < Count)
        {
            value = _columnWrappers[key];
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<int, CassandraColumnWrapper>> IEnumerable<KeyValuePair<int, CassandraColumnWrapper>>.
        GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return new KeyValuePair<int, CassandraColumnWrapper>(i, _columnWrappers[i]);
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

public class CassandraResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    private IReadOnlyDictionary<int, CassandraRowWrapper> Rows { get; set; }
    private CassandraColumnCollection Columns { get; set; }

    // Delegate to return this object to the pool
    public Action<CassandraResultSetWrapper>? ReturnToPool { get; set; }

    public CassandraResultSetWrapper()
    {
        // Default constructor for pooling
        Rows = new Dictionary<int, CassandraRowWrapper>();
        Columns = null!;
    }

    public CassandraResultSetWrapper(RowSet? rowSet = null, HashSet<string>? primaryKeys = null)
    {
        Init(rowSet, primaryKeys);
    }

    public void Init(RowSet? rowSet, HashSet<string>? primaryKeys = null)
    {
        if (rowSet == null)
        {
            SetFail(EDbError.CassandraRowSetNull);
            Rows = new Dictionary<int, CassandraRowWrapper>();
            Columns = null!; 
            return;
        }

        // 1. Build schema and name-to-index map
        var cqlColumns = rowSet.Columns;
        var cqlColumnsLength = cqlColumns?.Length ?? 0;
        
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>(cqlColumnsLength);
        var nameToIndexMap = new Dictionary<string, int>(cqlColumnsLength);
        var orderedSchemas = new List<NpOnColumnSchemaInfo>(cqlColumnsLength);

        if (cqlColumns != null)
        {
            for (int i = 0; i < cqlColumns.Length; i++)
            {
                var cqlColumn = cqlColumns[i];
                var isPrimaryKey = primaryKeys?.Contains(cqlColumn.Name) ?? false;
                var schemaInfo = new NpOnColumnSchemaInfo(
                    cqlColumn.Name,
                    cqlColumn.Type,
                    cqlColumn.GetCqlTypeName(),
                    isPrimaryKey
                );
                schemaMap.Add(cqlColumn.Name, schemaInfo);
                nameToIndexMap.Add(cqlColumn.Name, i);
                orderedSchemas.Add(schemaInfo);
            }
        }

        // 2. Create high-performance mapping from Cassandra.Row to object[] array
        var normalizeMethod = typeof(CassandraUtils).GetMethod(nameof(CassandraUtils.NormalizeCassandraValue), new[] { typeof(object) });
        var arrayMapper = CassandraMappingExtensions.CreateArrayRowMapper(orderedSchemas, normalizeMethod);

        var data = new List<object[]>();

        // 3. Read data
        foreach (var row in rowSet)
        {
            data.Add(arrayMapper(row));
        }

        // 4. Wrap data
        var rowMapper = CassandraMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);
        var rows = new Dictionary<int, CassandraRowWrapper>(data.Count);
        for (int i = 0; i < data.Count; i++)
        {
            rows.Add(i, new CassandraRowWrapper(data[i], rowMapper));
        }

        Rows = rows;
        Columns = new CassandraColumnCollection(data, schemaMap, nameToIndexMap);

        SetSuccess();
    }

    public void Reset() // for objectPooling 
    {
        Rows = new Dictionary<int, CassandraRowWrapper>();
        Columns = null!;
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
