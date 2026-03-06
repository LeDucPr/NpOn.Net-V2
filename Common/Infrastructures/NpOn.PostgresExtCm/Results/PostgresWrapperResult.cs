using System.Linq.Expressions;
using Common.Extensions.NpOn.CommonDb;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Npgsql;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

/// <summary>
/// ColumnWrapper
/// </summary>
public class PostgresRowWrapper : NpOnWrapperResult<object[], IReadOnlyDictionary<string, INpOnCell>>, INpOnRowWrapper
{
    private readonly IReadOnlyDictionary<string, NpOnColumnSchemaInfo> _schemaMap;
    private readonly IReadOnlyDictionary<string, int> _nameToIndexMap;

    public PostgresRowWrapper(object[] parent, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap) :
        base(parent)
    {
        _schemaMap = schemaMap;
        _nameToIndexMap = nameToIndexMap;
    }

    protected override IReadOnlyDictionary<string, INpOnCell> CreateResult()
    {
        var dictionary = new Dictionary<string, INpOnCell>(_schemaMap.Count);
        foreach (var schemaInfo in _schemaMap.Values)
        {
            var columnIndex = _nameToIndexMap[schemaInfo.ColumnName];
            object cellValue = Parent[columnIndex];
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
public class PostgresColumnWrapper : NpOnWrapperResult<List<object[]>, IReadOnlyDictionary<int, INpOnCell>>,
    INpOnColumnWrapper
{
    private readonly string _columnName;
    private readonly IReadOnlyDictionary<string, NpOnColumnSchemaInfo> _schemaMap;
    private readonly IReadOnlyDictionary<string, int> _nameToIndexMap;

    public PostgresColumnWrapper(List<object[]> parent, string columnName,
        IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap) : base(parent)
    {
        _columnName = columnName;
        _schemaMap = schemaMap;
        _nameToIndexMap = nameToIndexMap;
    }

    protected override IReadOnlyDictionary<int, INpOnCell> CreateResult()
    {
        var schemaInfo = _schemaMap[_columnName];
        var rowCount = Parent.Count;
        var dictionary = new Dictionary<int, INpOnCell>(rowCount);
        Type columnType = schemaInfo.DataType;
        var columnIndex = _nameToIndexMap[_columnName];

        for (int i = 0; i < rowCount; i++)
        {
            object? cellValue = Parent[i][columnIndex];

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

    public PostgresColumnCollection(List<object[]> data, IReadOnlyDictionary<string, NpOnColumnSchemaInfo> schemaMap,
        IReadOnlyDictionary<string, int> nameToIndexMap)
    {
        _nameToIndexMap = nameToIndexMap;
        _columnWrappers = new List<PostgresColumnWrapper>(schemaMap.Count);

        foreach (var schemaInfo in schemaMap.Values)
        {
            _columnWrappers.Add(new PostgresColumnWrapper(data, schemaInfo.ColumnName, schemaMap, nameToIndexMap));
        }
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
            Rows = new Dictionary<int, PostgresRowWrapper>();
            Columns = null!; // Or an empty collection
            return;
        }

        if (!reader.HasRows)
        {
            Rows = new Dictionary<int, PostgresRowWrapper>();
            Columns = null!;
            SetSuccess();
            return;
        }

        // 1. Build schema and name-to-index map
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>(reader.FieldCount);
        var nameToIndexMap = new Dictionary<string, int>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var schemaInfo = new NpOnColumnSchemaInfo(
                columnName,
                reader.GetFieldType(i),
                reader.GetDataTypeName(i)
            );
            schemaMap.Add(columnName, schemaInfo);
            nameToIndexMap.Add(columnName, i);
        }

        // 2. Create high-performance mapper
        var mapper = CreateRowMapper(reader);
        var data = new List<object[]>();

        // 3. Read data
        while (reader.Read())
        {
            data.Add(mapper(reader));
        }

        // 4. Wrap data
        var rows = new Dictionary<int, PostgresRowWrapper>(data.Count);
        for (int i = 0; i < data.Count; i++)
        {
            rows.Add(i, new PostgresRowWrapper(data[i], schemaMap, nameToIndexMap));
        }

        Rows = rows;

        Columns = new PostgresColumnCollection(data, schemaMap, nameToIndexMap);

        SetSuccess();
    }

    private static Func<NpgsqlDataReader, object[]> CreateRowMapper(NpgsqlDataReader reader)
    {
        var readerParam = Expression.Parameter(typeof(NpgsqlDataReader), "reader");
        // var newArray = Expression.NewArrayBounds(typeof(object), Expression.Constant(reader.FieldCount));

        var initializers = new List<Expression>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var fieldType = reader.GetFieldType(i);
            // reader.IsDBNull(i)
            var isDbNullCall = Expression.Call(readerParam, "IsDBNull", null, Expression.Constant(i));
            // (object)reader.GetFieldValue<T>(i)
            var getFieldValueMethod =
                typeof(NpgsqlDataReader).GetMethod("GetFieldValue", [typeof(int)])!.MakeGenericMethod(fieldType);
            var getFieldValueCall = Expression.Call(readerParam, getFieldValueMethod, Expression.Constant(i));
            // PostgresUtils.NormalizePostgresValue
            var normalizeMethod = typeof(PostgresUtils)
                .GetMethod(nameof(PostgresUtils.NormalizePostgresValue), [typeof(object)]);
            var normalizeCall =
                Expression.Call(normalizeMethod!, Expression.Convert(getFieldValueCall, typeof(object)));
            var castToObject = Expression.Convert(normalizeCall, typeof(object));
            var dbNullValue = Expression.Constant(DBNull.Value, typeof(object));
            var conditionalExpression = Expression.Condition(isDbNullCall, dbNullValue, castToObject);
            initializers.Add(conditionalExpression);
        }

        var arrayInit = Expression.NewArrayInit(typeof(object), initializers);
        var lambda = Expression.Lambda<Func<NpgsqlDataReader, object[]>>(arrayInit, readerParam);

        return lambda.Compile();
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