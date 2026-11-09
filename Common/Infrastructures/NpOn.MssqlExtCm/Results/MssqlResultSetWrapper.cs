using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data.Common;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Results;

public class MssqlRowWrapper : NpOnWrapperResult<object[], IReadOnlyDictionary<string, INpOnCell>>, INpOnRowWrapper
{
    private readonly Func<object[], IReadOnlyDictionary<string, INpOnCell>> _mapper;

    public MssqlRowWrapper(object[] parent, Func<object[], IReadOnlyDictionary<string, INpOnCell>> mapper) :
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

public class MssqlColumnWrapper : NpOnWrapperResult<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>, INpOnColumnWrapper
{
    private readonly Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> _mapper;

    public MssqlColumnWrapper(List<object[]> parent, Func<List<object[]>, IReadOnlyDictionary<int, INpOnCell>> mapper) : base(parent)
    {
        _mapper = mapper;
    }

    protected override IReadOnlyDictionary<int, INpOnCell> CreateResult()
    {
        return _mapper(Parent);
    }

    public IReadOnlyDictionary<int, INpOnCell> GetColumnWrapper() => Result;
}

public class MssqlColumnCollection : IReadOnlyDictionary<string, MssqlColumnWrapper>,
    IReadOnlyDictionary<int, MssqlColumnWrapper>, INpOnCollectionWrapper
{
    private readonly List<MssqlColumnWrapper> _columnWrappers;
    private readonly IReadOnlyDictionary<string, int> _nameToIndexMap;

    public MssqlColumnCollection(List<object[]> data, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        _nameToIndexMap = nameToIndexMap;
        _columnWrappers = new List<MssqlColumnWrapper>(schemaMap.Count);

        foreach (var schemaInfo in schemaMap.Values)
        {
            var mapper = MssqlMappingExtensions.GetOrCreateColumnMapper(schemaInfo.ColumnName, schemaMap, nameToIndexMap);
            _columnWrappers.Add(new MssqlColumnWrapper(data, mapper));
        }
    }

    public MssqlColumnWrapper this[string columnName] => _columnWrappers[_nameToIndexMap[columnName]];
    public MssqlColumnWrapper this[int columnIndex] => _columnWrappers[columnIndex];

    public IEnumerable<string> Keys => _nameToIndexMap.Keys;
    public IEnumerable<MssqlColumnWrapper> Values => _columnWrappers;
    public int Count => _columnWrappers.Count;
    public bool ContainsKey(string key) => _nameToIndexMap.ContainsKey(key);

    public bool TryGetValue(string key, out MssqlColumnWrapper value)
    {
        if (_nameToIndexMap.TryGetValue(key, out int index))
        {
            value = _columnWrappers[index];
            return true;
        }
        value = null!;
        return false;
    }

    public IEnumerator<KeyValuePair<string, MssqlColumnWrapper>> GetEnumerator()
    {
        foreach (var pair in _nameToIndexMap)
        {
            yield return new KeyValuePair<string, MssqlColumnWrapper>(pair.Key, _columnWrappers[pair.Value]);
        }
    }

    IEnumerable<int> IReadOnlyDictionary<int, MssqlColumnWrapper>.Keys => Enumerable.Range(0, Count);
    bool IReadOnlyDictionary<int, MssqlColumnWrapper>.ContainsKey(int key) => key >= 0 && key < Count;
    bool IReadOnlyDictionary<int, MssqlColumnWrapper>.TryGetValue(int key, out MssqlColumnWrapper value)
    {
        if (key >= 0 && key < Count)
        {
            value = _columnWrappers[key];
            return true;
        }
        value = null!;
        return false;
    }

    IEnumerator<KeyValuePair<int, MssqlColumnWrapper>> IEnumerable<KeyValuePair<int, MssqlColumnWrapper>>.GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return new KeyValuePair<int, MssqlColumnWrapper>(i, _columnWrappers[i]);
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

public class MssqlResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    private static readonly ConcurrentDictionary<string, (Dictionary<string, NpOnColumnSchemaInfo> SchemaMap, Dictionary<string, int> NameToIndexMap, HashSet<string> PrimaryKeys)> SchemaCache = new();

    private IReadOnlyDictionary<int, MssqlRowWrapper>? _rows;
    private MssqlColumnCollection? _columns;

    private object?[]? _rentedFlatBuffer;
    private int _flatCount = 0;
    private int _rowCount = 0;
    private int _colCount = 0;

    private Dictionary<string, int>? _nameToIndexMap;
    private Dictionary<string, NpOnColumnSchemaInfo>? _schemaMap;
    private HashSet<string>? _primaryKeyColumns;
    private List<object[]>? _legacyRawData;

    public Action<MssqlResultSetWrapper>? ReturnToPool { get; set; }

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

    public MssqlResultSetWrapper()
    {
    }

    public MssqlResultSetWrapper(SqlDataReader? reader = null)
    {
        Init(reader);
    }

    public void Init(SqlDataReader? reader)
    {
        if (reader == null)
        {
            SetFail(EDbError.MssqlDataTableNull);
            return;
        }

        if (!reader.HasRows)
        {
            SetSuccess();
            return;
        }

        _colCount = reader.FieldCount;

        // Permanent concurrent schema cache
        var schemaKey = $"{_colCount};{string.Join(";", Enumerable.Range(0, _colCount).Select(i => reader.GetName(i)))}";
        if (!SchemaCache.TryGetValue(schemaKey, out var cached))
        {
            var columnSchema = reader.GetColumnSchema();
            var sm = new Dictionary<string, NpOnColumnSchemaInfo>(_colCount, StringComparer.OrdinalIgnoreCase);
            var ni = new Dictionary<string, int>(_colCount, StringComparer.OrdinalIgnoreCase);
            var pk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _colCount; i++)
            {
                var columnName = reader.GetName(i);
                var isPrimaryKey = columnSchema[i].IsKey ?? false;
                var schemaInfo = new NpOnColumnSchemaInfo(
                    columnName,
                    reader.GetFieldType(i),
                    reader.GetDataTypeName(i),
                    isPrimaryKey
                );
                sm[columnName] = schemaInfo;
                ni[columnName] = i;
                if (isPrimaryKey) pk.Add(columnName);
            }
            cached = (sm, ni, pk);
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

        while (reader.Read())
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
                _rentedFlatBuffer[flatIdx++] = reader.GetValue(c);
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

            var rowMapper = MssqlMappingExtensions.GetOrCreateRowMapper(_schemaMap, _nameToIndexMap);
            var raw = RawData;
            var rows = new Dictionary<int, MssqlRowWrapper>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                rows.Add(i, new MssqlRowWrapper(raw[i], rowMapper));
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
                _columns = new MssqlColumnCollection(dataList, _schemaMap, _nameToIndexMap);
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
