using System.Data.Common;
using System.Linq.Expressions;

namespace Common.Extensions.NpOn.CommonDb;

public static class DbDataReaderExtensions
{
    /// <summary>
    /// Asynchronously converts a DbDataReader to a list of dictionaries, with high performance.
    /// This method avoids the overhead of DataTable.
    /// </summary>
    public static async Task<(List<Dictionary<string, object>> Rows, Dictionary<string, NpOnColumnSchemaInfo> Schema)>
        ToInMemoryResultsAsync(this DbDataReader? reader)
    {
        if (reader == null || !reader.HasRows)
        {
            return (new List<Dictionary<string, object>>(), new Dictionary<string, NpOnColumnSchemaInfo>());
        }

        var results = new List<Dictionary<string, object>>();
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>(reader.FieldCount);

        // Build schema map first
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            schemaMap[columnName] = new NpOnColumnSchemaInfo(
                columnName,
                reader.GetFieldType(i),
                reader.GetDataTypeName(i)
            );
        }

        // Create a dynamic mapper function using Expression Trees for high performance
        var mapper = CreateRowMapper(reader);

        while (await reader.ReadAsync())
        {
            results.Add(mapper(reader));
        }

        return (results, schemaMap);
    }

    private static Func<DbDataReader, Dictionary<string, object>> CreateRowMapper(DbDataReader reader)
    {
        var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");

        // Specify Dictionary capacity to avoid resizing
        var dictConstructor = typeof(Dictionary<string, object>).GetConstructor([typeof(int)]);
        var dictInit = Expression.New(dictConstructor!, Expression.Constant(reader.FieldCount));

        var elementInits = new List<ElementInit>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var fieldType = reader.GetFieldType(i);

            // reader.IsDBNull(i)
            var isDbNullCall = Expression.Call(readerParam, "IsDBNull", null, Expression.Constant(i));
            // (object)reader.GetFieldValue<T>(i)
            var getFieldValueMethod =
                typeof(DbDataReader).GetMethod("GetFieldValue", [typeof(int)])!.MakeGenericMethod(fieldType);
            var getFieldValueCall = Expression.Call(readerParam, getFieldValueMethod, Expression.Constant(i));
            var castToObject = Expression.Convert(getFieldValueCall, typeof(object));
            // DBNull.Value
            var dbNullValue = Expression.Constant(DBNull.Value, typeof(object));
            // reader.IsDBNull(i) ? DBNull.Value : (object)reader.GetFieldValue<T>(i)
            var conditionalExpression = Expression.Condition(isDbNullCall, dbNullValue, castToObject);
            // Add to dictionary initializer
            var addMethod = typeof(Dictionary<string, object>).GetMethod("Add", [typeof(string), typeof(object)]);
            if (addMethod != null)
                elementInits.Add(Expression.ElementInit(addMethod, Expression.Constant(columnName),
                    conditionalExpression));
        }

        var listInit = Expression.ListInit(dictInit, elementInits);
        var lambda = Expression.Lambda<Func<DbDataReader, Dictionary<string, object>>>(listInit, readerParam);

        return lambda.Compile();
    }
}