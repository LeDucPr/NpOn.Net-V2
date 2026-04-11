using System.Data;
using System.Text;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.MssqlExtCm.Results;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.DbFactories.NpOn.MssqlFactory;

public static class TableMssqlExtensions
{
    private static List<string> GetPrimaryKeyColumnNames(IReadOnlyDictionary<string, INpOnCell> row)
    {
        var pkCols = row.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key).ToList();
        if (pkCols.Count == 0)
        {
            var firstCol = row.Keys.FirstOrDefault();
            if (firstCol != null) pkCols.Add(firstCol);
        }
        return pkCols;
    }

    public static (string CommandText, List<SqlParameter> Parameters) ToMssqlParamsInsert(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers) throw new ArgumentException("Table has no rows.");
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");
        var columnNames = firstRow.Keys.ToList();
        
        var parameters = new List<SqlParameter>();
        var sql = new StringBuilder();
        int paramCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            var valueParams = new List<string>();

            foreach (var colName in columnNames)
            {
                var paramName = $"@p{paramCounter++}";
                valueParams.Add(paramName);
                row.TryGetValue(colName, out var cell);
                var (paramValue, sqlType) = MssqlUtils.NormalizeForMssql(cell?.ValueAsObject);
                var p = new SqlParameter(paramName, paramValue ?? DBNull.Value);
                if (sqlType != SqlDbType.Variant) p.SqlDbType = sqlType;
                parameters.Add(p);
            }
            sql.Append($"INSERT INTO [{tableName}] ({string.Join(",", columnNames.Select(c => $"[{c}]"))}) VALUES ({string.Join(",", valueParams)});");
        }
        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<SqlParameter> Parameters) ToMssqlParamsUpdate(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers) throw new ArgumentException("Table has no rows.");
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        var allColumnNames = firstRow.Keys.ToList();
        var updateColumnNames = allColumnNames.Except(pkColumnNames).ToList();

        var parameters = new List<SqlParameter>();
        var sql = new StringBuilder();
        int paramCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            var setClauses = new List<string>();
            foreach (var colName in updateColumnNames)
            {
                var paramName = $"@v{paramCounter++}";
                setClauses.Add($"[{colName}] = {paramName}");
                row.TryGetValue(colName, out var cell);
                var (paramValue, sqlType) = MssqlUtils.NormalizeForMssql(cell?.ValueAsObject);
                var p = new SqlParameter(paramName, paramValue ?? DBNull.Value);
                if (sqlType != SqlDbType.Variant) p.SqlDbType = sqlType;
                parameters.Add(p);
            }

            var whereClauses = new List<string>();
            foreach (var pkColName in pkColumnNames)
            {
                var pkParamName = $"@pk{paramCounter++}";
                whereClauses.Add($"[{pkColName}] = {pkParamName}");
                if (!row.TryGetValue(pkColName, out var cell) || cell.ValueAsObject == null)
                    throw new InvalidOperationException($"PK for '{pkColName}' cannot be null.");
                var (paramValue, sqlType) = MssqlUtils.NormalizeForMssql(cell.ValueAsObject);
                var p = new SqlParameter(pkParamName, paramValue);
                // if (sqlType.HasValue) p.SqlDbType = sqlType.Value;
                parameters.Add(p);
            }
            sql.Append($"UPDATE [{tableName}] SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};");
        }
        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<SqlParameter> Parameters) ToMssqlParamsMerge(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers) throw new ArgumentException("Table has no rows.");
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        var allColumnNames = firstRow.Keys.ToList();
        // var updateColumnNames = allColumnNames.Except(pkColumnNames).ToList();

        var parameters = new List<SqlParameter>();
        var sql = new StringBuilder();
        int paramCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            var allCols = new List<string>();
            var allParams = new List<string>();
            var updateSets = new List<string>();
            var matchConditions = new List<string>();

            foreach (var colName in allColumnNames)
            {
                var paramName = $"@m{paramCounter++}";
                allCols.Add($"[{colName}]");
                allParams.Add(paramName);
                
                row.TryGetValue(colName, out var cell);
                var (paramValue, sqlType) = MssqlUtils.NormalizeForMssql(cell?.ValueAsObject);
                var p = new SqlParameter(paramName, paramValue ?? DBNull.Value);
                if (sqlType != SqlDbType.Variant) p.SqlDbType = sqlType;
                parameters.Add(p);

                if (pkColumnNames.Contains(colName))
                    matchConditions.Add($"target.[{colName}] = {paramName}");
                else
                    updateSets.Add($"target.[{colName}] = {paramName}");
            }

            sql.AppendLine($"MERGE INTO [{tableName}] AS target");
            sql.AppendLine($"USING (SELECT 1 as dual) AS source ON ({string.Join(" AND ", matchConditions)})");
            if (updateSets.Count > 0) sql.AppendLine($"WHEN MATCHED THEN UPDATE SET {string.Join(", ", updateSets)}");
            sql.AppendLine($"WHEN NOT MATCHED THEN INSERT ({string.Join(",", allCols)}) VALUES ({string.Join(",", allParams)});");
        }
        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<SqlParameter> Parameters) ToMssqlParamsDelete(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers) throw new ArgumentException("Table has no rows.");
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        var parameters = new List<SqlParameter>();
        var sql = new StringBuilder();
        var whereClauses = new List<string>();
        int paramCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            var pkConditions = new List<string>();
            foreach (var pkColName in pkColumnNames)
            {
                var pkParamName = $"@pk{paramCounter++}";
                pkConditions.Add($"[{pkColName}] = {pkParamName}");
                if (!row.TryGetValue(pkColName, out var cell) || cell.ValueAsObject == null)
                    throw new InvalidOperationException($"PK for '{pkColName}' cannot be null.");
                var (paramValue, sqlType) = MssqlUtils.NormalizeForMssql(cell.ValueAsObject);
                var p = new SqlParameter(pkParamName, paramValue);
                // if (sqlType.HasValue) p.SqlDbType = sqlType.Value;
                parameters.Add(p);
            }
            whereClauses.Add($"({string.Join(" AND ", pkConditions)})");
        }
        if (whereClauses.Count > 0) sql.Append($"DELETE FROM [{tableName}] WHERE {string.Join(" OR ", whereClauses)};");
        return (sql.ToString(), parameters);
    }
}
