using System.Data;
using Common.Extensions.NpOn.CommonDb;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Npgsql;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

/// <summary>
/// ColumnWrapper
/// </summary>
public class PostgresRowWrapper : NpOnWrapperResult<DataRow, IReadOnlyDictionary<string, INpOnCell>>, INpOnRowWrapper
{
    private readonly IReadOnlyDictionary<string, NpOnColumnSchemaInfo> _schemaMap;

    public PostgresRowWrapper(DataRow parent, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap) :
        base(parent)
    {
        _schemaMap = schemaMap;
    }

    protected override IReadOnlyDictionary<string, INpOnCell> CreateResult()
    {
        var dictionary = new Dictionary<string, INpOnCell>(_schemaMap.Count);
        foreach (var schemaInfo in _schemaMap.Values)
        {
            object cellValue = Parent[schemaInfo.ColumnName];
            INpOnCell cell = PostgresCellDynamicFactory.Create(
                schemaInfo.DataType,
                cellValue,
                schemaInfo.ProviderDataTypeName
            );
            dictionary.Add(schemaInfo.ColumnName, cell);
        }

        return dictionary;
    }

    public IReadOnlyDictionary<string, INpOnCell> GetRowWrapper() => Result;
}

/// <summary>
/// ColumnWrapper (truy cập được từ Key-integer hoặc Key-string)
/// </summary>
public class PostgresColumnWrapper : NpOnWrapperResult<DataTable, IReadOnlyDictionary<int, INpOnCell>>,
    INpOnColumnWrapper
{
    private readonly string _columnName;
    private readonly IReadOnlyDictionary<string, NpOnColumnSchemaInfo> _schemaMap;

    public PostgresColumnWrapper(DataTable parent, string columnName,
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap) : base(parent)
    {
        _columnName = columnName;
        _schemaMap = schemaMap;
    }

    protected override IReadOnlyDictionary<int, INpOnCell> CreateResult()
    {
        var schemaInfo = _schemaMap[_columnName];
        var rowCount = Parent.Rows.Count;
        var dictionary = new Dictionary<int, INpOnCell>(rowCount);
        Type columnType = schemaInfo.DataType;
        for (int i = 0; i < rowCount; i++)
        {
            object? cellValue = Parent.Rows[i][_columnName];

            if (cellValue == DBNull.Value) cellValue = null;
            INpOnCell cell = PostgresCellDynamicFactory.Create(
                columnType,
                cellValue,
                schemaInfo.ProviderDataTypeName
            );

            dictionary.Add(i, cell);
        }

        return dictionary;
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

    public PostgresColumnCollection(DataTable dataTable, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap)
    {
        var nameToIndexMap = new Dictionary<string, int>();
        _columnWrappers = new List<PostgresColumnWrapper>(dataTable.Columns.Count);

        int i = 0;
        foreach (var schemaInfo in schemaMap.Values)
        {
            nameToIndexMap.Add(schemaInfo.ColumnName, i++);
            _columnWrappers.Add(new PostgresColumnWrapper(dataTable, schemaInfo.ColumnName, schemaMap));
        }

        _nameToIndexMap = nameToIndexMap;
    }

    public PostgresColumnWrapper this[string columnName] => _columnWrappers[_nameToIndexMap[columnName]];
    public PostgresColumnWrapper this[int columnIndex] => _columnWrappers[columnIndex];

    // reader
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

    // IReadOnlyDictionary<int, ...>
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

public class PostgresResultSetWrapper : NpOnWrapperResult, INpOnTableWrapper
{
    public IReadOnlyDictionary<int, PostgresRowWrapper> Rows { get; }
    public PostgresColumnCollection Columns { get; }

    public PostgresResultSetWrapper(NpgsqlDataReader? reader = null)
    {
        if (reader == null)
        {
            SetFail(EDbError.PostgresDataTableNull);
            return;
        }

        // Build schema
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>();
        var dataTable = new DataTable();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var fieldType = reader.GetFieldType(i);

            var schemaInfo = new NpOnColumnSchemaInfo(
                columnName,
                fieldType,
                reader.GetDataTypeName(i)
            );

            schemaMap.Add(columnName, schemaInfo);

            // Tạo column cho DataTable
            dataTable.Columns.Add(columnName, fieldType);
        }

        // Read data
        while (reader.Read())
        {
            var row = dataTable.NewRow();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i))
                {
                    row[i] = DBNull.Value;
                    continue;
                }
                row[i] = reader.GetValue(i).NormalizePostgresValue();
            }

            dataTable.Rows.Add(row);
        }

        // Wrap
        Rows = dataTable.Rows
            .Cast<DataRow>()
            .Select((row, index) => new { row, index })
            .ToDictionary(
                item => item.index,
                item => new PostgresRowWrapper(item.row, schemaMap)
            );

        Columns = new PostgresColumnCollection(dataTable, schemaMap);

        SetSuccess();
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
}