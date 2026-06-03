using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Neo4j.Driver;

namespace Common.Infrastructures.NpOn.Neo4jExtCm.Results;

public class Neo4jRowWrapper : NpOnWrapperResult<IRecord, IReadOnlyDictionary<string, INpOnCell>>, INpOnRowWrapper
{
    private readonly IReadOnlyList<string> _keys;

    public Neo4jRowWrapper(IRecord parent, IReadOnlyList<string> keys) : base(parent)
    {
        _keys = keys;
    }

    protected override IReadOnlyDictionary<string, INpOnCell> CreateResult()
    {
        var dictionary = new Dictionary<string, INpOnCell>();

        foreach (var key in _keys)
        {
            var hasValue = Parent.TryGet<object>(key, out var rawObject);
            object? normalizedValue = null;
            Type dotNetType = typeof(object);
            
            if (hasValue && rawObject != null)
            {
                normalizedValue = Neo4jUtils.NormalizeNeo4jValue(rawObject);
                dotNetType = Neo4jUtils.InferDotNetType(rawObject);
            }

            var genericCellType = typeof(NpOnCell<>).MakeGenericType(dotNetType);
            // FIx: object?[] signature matching constructor
            var cell = (INpOnCell)Activator.CreateInstance(genericCellType,
                new object?[] { normalizedValue, dotNetType.ToDbType(), dotNetType.Name, false })!;
                
            dictionary.Add(key, cell);
        }

        return dictionary;
    }

    public IReadOnlyDictionary<string, INpOnCell> GetRowWrapper() => Result;
}

public class Neo4jColumnWrapper : NpOnWrapperResult<IReadOnlyList<IRecord>, IReadOnlyDictionary<int, INpOnCell>>,
    INpOnColumnWrapper
{
    private readonly string _columnName;
    private readonly IReadOnlyList<string> _allKeys;

    public Neo4jColumnWrapper(IReadOnlyList<IRecord> parent, string columnName, IReadOnlyList<string> allKeys) : base(parent)
    {
        _columnName = columnName;
        _allKeys = allKeys;
    }

    protected override IReadOnlyDictionary<int, INpOnCell> CreateResult()
    {
        var dictionary = new Dictionary<int, INpOnCell>();

        for (int i = 0; i < Parent.Count; i++)
        {
            var rowRecord = Parent[i];
            var rowWrapper = new Neo4jRowWrapper(rowRecord, _allKeys);
            var cell = rowWrapper.Result[_columnName];
            dictionary.Add(i, cell);
        }

        return dictionary;
    }

    public IReadOnlyDictionary<int, INpOnCell> GetColumnWrapper() => Result;
}

public class Neo4jColumnCollection : IReadOnlyDictionary<string, Neo4jColumnWrapper>,
    IReadOnlyDictionary<int, Neo4jColumnWrapper>, INpOnCollectionWrapper
{
    private readonly List<Neo4jColumnWrapper> _columnWrappers;
    private readonly IReadOnlyDictionary<string, int> _nameToIndexMap;

    public Neo4jColumnCollection(IReadOnlyList<IRecord> allRows, IReadOnlyList<string> allKeys)
    {
        var nameToIndexMap = new Dictionary<string, int>();
        _columnWrappers = new List<Neo4jColumnWrapper>(allKeys.Count);

        for (int i = 0; i < allKeys.Count; i++)
        {
            var key = allKeys[i];
            nameToIndexMap.Add(key, i);
            _columnWrappers.Add(new Neo4jColumnWrapper(allRows, key, allKeys));
        }

        _nameToIndexMap = nameToIndexMap;
    }

    public Neo4jColumnWrapper this[string columnName] => _columnWrappers[_nameToIndexMap[columnName]];
    public Neo4jColumnWrapper this[int columnIndex] => _columnWrappers[columnIndex];

    public IEnumerable<string> Keys => _nameToIndexMap.Keys;
    public IEnumerable<Neo4jColumnWrapper> Values => _columnWrappers;
    public int Count => _columnWrappers.Count;
    public bool ContainsKey(string key) => _nameToIndexMap.ContainsKey(key);

    public bool TryGetValue(string key, out Neo4jColumnWrapper value)
    {
        if (_nameToIndexMap.TryGetValue(key, out int index))
        {
            value = _columnWrappers[index];
            return true;
        }

        value = null!;
        return false;
    }

    public IEnumerator<KeyValuePair<string, Neo4jColumnWrapper>> GetEnumerator()
    {
        foreach (var pair in _nameToIndexMap)
        {
            yield return new KeyValuePair<string, Neo4jColumnWrapper>(pair.Key, _columnWrappers[pair.Value]);
        }
    }

    IEnumerable<int> IReadOnlyDictionary<int, Neo4jColumnWrapper>.Keys => Enumerable.Range(0, Count);

    bool IReadOnlyDictionary<int, Neo4jColumnWrapper>.ContainsKey(int key) => key >= 0 && key < Count;

    bool IReadOnlyDictionary<int, Neo4jColumnWrapper>.TryGetValue(int key, out Neo4jColumnWrapper value)
    {
        if (key >= 0 && key < Count)
        {
            value = _columnWrappers[key];
            return true;
        }

        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<int, Neo4jColumnWrapper>> IEnumerable<KeyValuePair<int, Neo4jColumnWrapper>>.
        GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return new KeyValuePair<int, Neo4jColumnWrapper>(i, _columnWrappers[i]);
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

public class Neo4jResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    private readonly IReadOnlyList<IRecord> _allRecords;
    private readonly IReadOnlyList<string> _keys;

    public IReadOnlyDictionary<int, Neo4jRowWrapper> Rows { get; }
    public Neo4jColumnCollection Columns { get; }

    public Neo4jResultSetWrapper() 
    {
        _allRecords = new List<IRecord>();
        _keys = new List<string>();
        Rows = new Dictionary<int, Neo4jRowWrapper>();
        Columns = new Neo4jColumnCollection(new List<IRecord>(), new List<string>());
    }

    public Neo4jResultSetWrapper(List<IRecord>? records)
    {
        if (records == null)
        {
            SetFail(EDbError.Neo4jRecordNull);
            _allRecords = new List<IRecord>();
            _keys = new List<string>();
            Rows = new Dictionary<int, Neo4jRowWrapper>();
            Columns = new Neo4jColumnCollection(new List<IRecord>(), new List<string>());
            return;
        }

        _allRecords = records;
        _keys = _allRecords.SelectMany(r => r.Keys).Distinct().ToList();

        Rows = _allRecords
            .Select((doc, index) => new { doc, index })
            .ToDictionary(
                item => item.index,
                item => new Neo4jRowWrapper(item.doc, _keys)
            );

        Columns = new Neo4jColumnCollection(_allRecords, _keys);
        SetSuccess();
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

    public INpOnCollectionWrapper CollectionWrappers => Columns;
}
