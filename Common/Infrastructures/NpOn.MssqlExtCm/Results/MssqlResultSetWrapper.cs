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
            var mapper = MssqlMappingExtensions.CreateColumnMapper(schemaInfo.ColumnName, schemaMap, nameToIndexMap);
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
    private IReadOnlyDictionary<int, MssqlRowWrapper> Rows { get; set; } = new Dictionary<int, MssqlRowWrapper>();
    private MssqlColumnCollection? Columns { get; set; }

    public Action<MssqlResultSetWrapper>? ReturnToPool { get; set; }

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

        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>(reader.FieldCount);
        var nameToIndexMap = new Dictionary<string, int>(reader.FieldCount);
        
        // MSSQL SqlDataReader doesn't have CanGetColumnSchema but we can use GetColumnSchema() directly
        var columnSchema = reader.GetColumnSchema();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var isPrimaryKey = columnSchema[i].IsKey ?? false;
            var schemaInfo = new NpOnColumnSchemaInfo(
                columnName,
                reader.GetFieldType(i),
                reader.GetDataTypeName(i),
                isPrimaryKey
            );
            schemaMap.Add(columnName, schemaInfo);
            nameToIndexMap.Add(columnName, i);
        }

        var normalizeMethod = typeof(MssqlUtils).GetMethod(nameof(MssqlUtils.NormalizeMssqlValue), [typeof(object)]);
        var mapper = MssqlMappingExtensions.CreateArrayRowMapper(reader, normalizeMethod);

        var data = new List<object[]>();
        while (reader.Read())
        {
            data.Add(mapper(reader));
        }

        var rowMapper = MssqlMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);
        var rows = new Dictionary<int, MssqlRowWrapper>(data.Count);
        for (int i = 0; i < data.Count; i++)
        {
            rows.Add(i, new MssqlRowWrapper(data[i], rowMapper));
        }

        Rows = rows;
        Columns = new MssqlColumnCollection(data, schemaMap, nameToIndexMap);
        SetSuccess();
    }

    public void Reset()
    {
        Rows = new Dictionary<int, MssqlRowWrapper>();
        Columns = null;
        // Status and error are reset by SetSuccess/SetFail
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
