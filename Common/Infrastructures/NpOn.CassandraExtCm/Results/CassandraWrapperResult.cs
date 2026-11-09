using System.Buffers;
using System.Collections.Concurrent;
using Cassandra;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Results;

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
            var mapper = CassandraMappingExtensions.GetOrCreateColumnMapper(schemaInfo.ColumnName, schemaMap, nameToIndexMap);
            _columnWrappers.Add(new CassandraColumnWrapper(data, mapper));
        }
    }

    public CassandraColumnWrapper this[string columnName] => _columnWrappers[_nameToIndexMap[columnName]];
    public CassandraColumnWrapper this[int columnIndex] => _columnWrappers[columnIndex];

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
        columnNames ??= Keys.ToArray();
        Dictionary<string, INpOnColumnWrapper?> result = new();
        foreach (var colName in columnNames)
            if (TryGetValue(colName, out var value))
                result.Add(colName, value);
        return result;
    }
}

public class CassandraResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    private static readonly ConcurrentDictionary<string, (Dictionary<string, NpOnColumnSchemaInfo> SchemaMap, Dictionary<string, int> NameToIndexMap, HashSet<string> PrimaryKeys, List<NpOnColumnSchemaInfo> OrderedSchemas)> SchemaCache = new();

    private IReadOnlyDictionary<int, CassandraRowWrapper>? _rows;
    private CassandraColumnCollection? _columns;

    private object?[]? _rentedFlatBuffer;
    private int _flatCount = 0;
    private int _rowCount = 0;
    private int _colCount = 0;

    private Dictionary<string, int>? _nameToIndexMap;
    private Dictionary<string, NpOnColumnSchemaInfo>? _schemaMap;
    private HashSet<string>? _primaryKeyColumns;
    private List<object[]>? _legacyRawData;

    public Action<CassandraResultSetWrapper>? ReturnToPool { get; set; }

    public IReadOnlyDictionary<string, int>? NameToIndexMap => _nameToIndexMap;
    public object?[]? FlatData => _rentedFlatBuffer;
    public int RowCount => _rowCount;
    public int ColCount => _colCount;
    public IReadOnlySet<string>? PrimaryKeyColumns => _primaryKeyColumns;

    public IReadOnlyList<object[]>? RawData
    {
        get
        {
            if (_legacyRawData != null) return _legacyRawData;
            if (_rentedFlatBuffer == null || _colCount == 0 || _rowCount == 0) return null;

            var list = new List<object[]>(_rowCount);
            for (int r = 0; r < _rowCount; r++)
            {
                var rowArr = new object[_colCount];
                Array.Copy(_rentedFlatBuffer, r * _colCount, rowArr, 0, _colCount);
                list.Add(rowArr);
            }
            _legacyRawData = list;
            return _legacyRawData;
        }
    }

    public CassandraResultSetWrapper()
    {
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
            return;
        }

        var cqlColumns = rowSet.Columns;
        _colCount = cqlColumns?.Length ?? 0;

        if (_colCount == 0)
        {
            SetSuccess();
            return;
        }

        // Permanent concurrent schema cache
        var schemaKey = $"{_colCount};{string.Join(";", cqlColumns!.Select(c => c.Name))}";
        if (!SchemaCache.TryGetValue(schemaKey, out var cached))
        {
            var sm = new Dictionary<string, NpOnColumnSchemaInfo>(_colCount, StringComparer.OrdinalIgnoreCase);
            var ni = new Dictionary<string, int>(_colCount, StringComparer.OrdinalIgnoreCase);
            var pk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var os = new List<NpOnColumnSchemaInfo>(_colCount);

            for (int i = 0; i < cqlColumns!.Length; i++)
            {
                var cqlColumn = cqlColumns[i];
                var isPrimaryKey = primaryKeys?.Contains(cqlColumn.Name) ?? false;
                var schemaInfo = new NpOnColumnSchemaInfo(
                    cqlColumn.Name,
                    cqlColumn.Type,
                    cqlColumn.GetCqlTypeName(),
                    isPrimaryKey
                );
                sm[cqlColumn.Name] = schemaInfo;
                ni[cqlColumn.Name] = i;
                os.Add(schemaInfo);
                if (isPrimaryKey) pk.Add(cqlColumn.Name);
            }
            cached = (sm, ni, pk, os);
            SchemaCache.TryAdd(schemaKey, cached);
        }

        _schemaMap = cached.SchemaMap;
        _nameToIndexMap = cached.NameToIndexMap;
        _primaryKeyColumns = cached.PrimaryKeys;

        // Rent contiguous 1D flat buffer
        int initialCapacity = Math.Max(256 * _colCount, 1024);
        _rentedFlatBuffer = ArrayPool<object?>.Shared.Rent(initialCapacity);
        int flatIdx = 0;
        _rowCount = 0;

        foreach (var row in rowSet)
        {
            if (flatIdx + _colCount > _rentedFlatBuffer.Length)
            {
                int newCapacity = _rentedFlatBuffer.Length * 2;
                var newBuffer = ArrayPool<object?>.Shared.Rent(newCapacity);
                Array.Copy(_rentedFlatBuffer, 0, newBuffer, 0, flatIdx);
                ArrayPool<object?>.Shared.Return(_rentedFlatBuffer);
                _rentedFlatBuffer = newBuffer;
            }

            for (int c = 0; c < _colCount; c++)
            {
                _rentedFlatBuffer[flatIdx++] = row.GetValue(cached.OrderedSchemas[c].DataType, c);
            }
            _rowCount++;
        }

        _flatCount = flatIdx;
        SetSuccess();
    }

    public void Reset()
    {
        if (_rentedFlatBuffer != null)
        {
            ArrayPool<object?>.Shared.Return(_rentedFlatBuffer);
            _rentedFlatBuffer = null;
        }
        _flatCount = 0;
        _rowCount = 0;
        _colCount = 0;
        _rows = null;
        _columns = null;
        _nameToIndexMap = null;
        _schemaMap = null;
        _primaryKeyColumns = null;
        _legacyRawData = null;
    }

    public IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers
    {
        get
        {
            if (_rows != null)
            {
                Dictionary<int, INpOnRowWrapper?> dict = new(_rows.Count);
                foreach (var kvp in _rows) dict.Add(kvp.Key, kvp.Value);
                return dict;
            }

            if (_schemaMap == null || _nameToIndexMap == null || RawData == null)
                return new Dictionary<int, INpOnRowWrapper?>();

            var rowMapper = CassandraMappingExtensions.GetOrCreateRowMapper(_schemaMap, _nameToIndexMap);
            var raw = RawData;
            var rows = new Dictionary<int, CassandraRowWrapper>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                rows.Add(i, new CassandraRowWrapper(raw[i], rowMapper));
            }
            _rows = rows;

            Dictionary<int, INpOnRowWrapper?> res = new(_rows.Count);
            foreach (var kvp in _rows) res.Add(kvp.Key, kvp.Value);
            return res;
        }
    }

    public INpOnCollectionWrapper CollectionWrappers
    {
        get
        {
            if (_columns == null && _schemaMap != null && _nameToIndexMap != null && RawData is List<object[]> dataList)
            {
                _columns = new CassandraColumnCollection(dataList, _schemaMap, _nameToIndexMap);
            }
            return _columns!;
        }
    }

    public override void Dispose()
    {
        Reset();
        ReturnToPool?.Invoke(this);
    }
}
