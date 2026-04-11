using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using System.Text;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Infrastructures.NpOn.ClickHouseExtCm.Results;

namespace Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;

public static class TableClickHouseExtensions
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

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsInsert(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var columnNames = firstRow.Keys.ToList();
        if (columnNames.Count == 0) throw new ArgumentException("Table has no columns.");

        var parameters = new List<INpOnDbCommandParam>();
        var sql = new StringBuilder();
        int paramCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            var valueParams = new List<string>();

            foreach (var colName in columnNames)
            {
                valueParams.Add($"@p{paramCounter}");
                row.TryGetValue(colName, out var cell);
                
                // Note: ClickHouse type info could be added here if available in cell schema
                parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = $"p{paramCounter++}",
                    ParamValue = cell?.ValueAsObject ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            sql.Append($"INSERT INTO {tableName} (\"{string.Join("\",\"", columnNames)}\") VALUES ({string.Join(",", valueParams)}); ");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsUpdate(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        var allColumnNames = firstRow.Keys.ToList();
        var updateColumnNames = allColumnNames.Except(pkColumnNames).ToList();

        if (updateColumnNames.Count == 0) throw new ArgumentException("No columns to update.");

        var parameters = new List<INpOnDbCommandParam>();
        var sql = new StringBuilder();
        int paramCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            
            var setClauses = new List<string>();
            foreach (var colName in updateColumnNames)
            {
                setClauses.Add($"\"{colName}\" = @v{paramCounter}");
                row.TryGetValue(colName, out var cell);
                parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = $"v{paramCounter++}",
                    ParamValue = cell?.ValueAsObject ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            var whereClauses = new List<string>();
            foreach (var pkColName in pkColumnNames)
            {
                whereClauses.Add($"\"{pkColName}\" = @pk{paramCounter}");
                row.TryGetValue(pkColName, out var cell);
                parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = $"pk{paramCounter++}",
                    ParamValue = cell?.ValueAsObject ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            // ClickHouse Mutation Syntax
            sql.Append($"ALTER TABLE {tableName} UPDATE {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}; ");
        }

        return (sql.ToString(), parameters);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsMerge(
        this INpOnTableWrapper table, string tableName)
    {
        // For ClickHouse, Merge is implemented as Insert (Upsert behavior in ReplacingMergeTree etc.)
        return ToClickHouseParamsInsert(table, tableName);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsDelete(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        var parameters = new List<INpOnDbCommandParam>();
        var sql = new StringBuilder();
        int paramCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();

            var pkConditions = new List<string>();
            foreach (var pkColName in pkColumnNames)
            {
                pkConditions.Add($"\"{pkColName}\" = @pk{paramCounter}");
                row.TryGetValue(pkColName, out var cell);
                parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = $"pk{paramCounter++}",
                    ParamValue = cell?.ValueAsObject ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            // ClickHouse Mutation Syntax
            sql.Append($"ALTER TABLE {tableName} DELETE WHERE {string.Join(" AND ", pkConditions)}; ");
        }

        return (sql.ToString(), parameters);
    }
}
