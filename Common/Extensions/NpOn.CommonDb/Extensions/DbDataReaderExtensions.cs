using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace Common.Extensions.NpOn.CommonDb.Extensions;

public static class DbDataReaderExtensions
{
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

    /// <summary>
    /// Creates a compiled function that maps a DbDataReader row to an object array.
    /// Allows injecting a custom normalization method (e.g., for Postgres DateTime handling).
    /// </summary>
    /// <param name="reader">The data reader.</param>
    /// <param name="normalizerMethod">Optional static method info to normalize values (signature: object -> object).</param>
    public static Func<DbDataReader, object[]> CreateArrayRowMapper(this DbDataReader reader, MethodInfo? normalizerMethod = null)
    {
        var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");
        
        var initializers = new List<Expression>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var fieldType = reader.GetFieldType(i);

            // reader.IsDBNull(i)
            var isDbNullCall = Expression.Call(readerParam, "IsDBNull", null, Expression.Constant(i));

            // (object)reader.GetFieldValue<T>(i)
            var getFieldValueMethod = typeof(DbDataReader).GetMethod("GetFieldValue", new[] { typeof(int) })!.MakeGenericMethod(fieldType);
            var getFieldValueCall = Expression.Call(readerParam, getFieldValueMethod, Expression.Constant(i));
            
            Expression valueExpression = Expression.Convert(getFieldValueCall, typeof(object));

            // Apply Normalizer if provided
            if (normalizerMethod != null)
            {
                // normalizerMethod((object)value)
                valueExpression = Expression.Call(normalizerMethod, valueExpression);
            }

            var castToObject = Expression.Convert(valueExpression, typeof(object));

            // Expression for: DBNull.Value
            var dbNullValue = Expression.Constant(DBNull.Value, typeof(object));

            // reader.IsDBNull(i) ? DBNull.Value : (object)NormalizedValue
            var conditionalExpression = Expression.Condition(isDbNullCall, dbNullValue, castToObject);

            initializers.Add(conditionalExpression);
        }

        var arrayInit = Expression.NewArrayInit(typeof(object), initializers);
        var lambda = Expression.Lambda<Func<DbDataReader, object[]>>(arrayInit, readerParam);

        return lambda.Compile();
    }
}