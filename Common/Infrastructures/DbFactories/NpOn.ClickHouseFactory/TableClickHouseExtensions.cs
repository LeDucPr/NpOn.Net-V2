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
        // FAST-PATH: Direct Array Indexing
        if (table.NameToIndexMap != null && table.RawData != null)
        {
            var rawData = table.RawData;
            if (rawData.Count == 0) throw new ArgumentException("Table has no rows.");

            var nameToIndex = table.NameToIndexMap;
            var columnNames = nameToIndex.Keys.ToList();
            if (columnNames.Count == 0) throw new ArgumentException("Table has no columns.");

            var colIndices = columnNames.Select(c => nameToIndex[c]).ToArray();
            var quotedColumns = string.Join("\",\"", columnNames);

            var parameters = new List<INpOnDbCommandParam>(rawData.Count * columnNames.Count);
            var sql = new StringBuilder(rawData.Count * columnNames.Count * 16);

            sql.Append($"INSERT INTO {tableName} (\"{quotedColumns}\") VALUES ");

            int paramCounter = 0;
            for (int r = 0; r < rawData.Count; r++)
            {
                if (r > 0) sql.Append(", ");
                sql.Append('(');
                var rowValues = rawData[r];

                for (int c = 0; c < colIndices.Length; c++)
                {
                    if (c > 0) sql.Append(',');
                    var paramName = $"p{paramCounter++}";
                    sql.Append($"@{paramName}");

                    parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                    {
                        ParamName = paramName,
                        ParamValue = rowValues[colIndices[c]] ?? DBNull.Value,
                        ParamType = EClickHouseDbType.Unknown
                    });
                }
                sql.Append(')');
            }
            sql.Append(';');
            return (sql.ToString(), parameters);
        }

        // FALLBACK-PATH: Legacy RowWrapper evaluation
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var fallbackColumns = firstRow.Keys.ToList();
        if (fallbackColumns.Count == 0) throw new ArgumentException("Table has no columns.");

        var fallbackParameters = new List<INpOnDbCommandParam>();
        var fallbackSql = new StringBuilder();
        int fallbackParamCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            var valueParams = new List<string>(fallbackColumns.Count);

            foreach (var colName in fallbackColumns)
            {
                valueParams.Add($"@p{fallbackParamCounter}");
                row.TryGetValue(colName, out var cell);
                
                fallbackParameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = $"p{fallbackParamCounter++}",
                    ParamValue = cell?.ValueAsObject ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            fallbackSql.Append($"INSERT INTO {tableName} (\"{string.Join("\",\"", fallbackColumns)}\") VALUES ({string.Join(",", valueParams)}); ");
        }

        return (fallbackSql.ToString(), fallbackParameters);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsUpdate(
        this INpOnTableWrapper table, string tableName)
    {
        // FAST-PATH: Direct Array Indexing
        if (table.NameToIndexMap != null && table.RawData != null)
        {
            var rawData = table.RawData;
            if (rawData.Count == 0) throw new ArgumentException("Table has no rows.");

            var nameToIndex = table.NameToIndexMap;
            var pkCols = (table.PrimaryKeyColumns != null && table.PrimaryKeyColumns.Count > 0)
                ? table.PrimaryKeyColumns.ToList()
                : new List<string> { nameToIndex.Keys.First() };

            var allCols = nameToIndex.Keys.ToList();
            var updateCols = allCols.Except(pkCols).ToList();
            if (updateCols.Count == 0) throw new ArgumentException("No columns to update.");

            var updateIndices = updateCols.Select(c => nameToIndex[c]).ToArray();
            var pkIndices = pkCols.Select(c => nameToIndex[c]).ToArray();

            var parameters = new List<INpOnDbCommandParam>(rawData.Count * allCols.Count);
            var sql = new StringBuilder(rawData.Count * 64);
            int paramCounter = 0;

            for (int r = 0; r < rawData.Count; r++)
            {
                var rowValues = rawData[r];
                var setClauses = new List<string>(updateCols.Count);

                for (int c = 0; c < updateCols.Count; c++)
                {
                    var paramName = $"v{paramCounter++}";
                    setClauses.Add($"\"{updateCols[c]}\" = @{paramName}");
                    parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                    {
                        ParamName = paramName,
                        ParamValue = rowValues[updateIndices[c]] ?? DBNull.Value,
                        ParamType = EClickHouseDbType.Unknown
                    });
                }

                var whereClauses = new List<string>(pkCols.Count);
                for (int p = 0; p < pkCols.Count; p++)
                {
                    var pkParamName = $"pk{paramCounter++}";
                    whereClauses.Add($"\"{pkCols[p]}\" = @{pkParamName}");
                    parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                    {
                        ParamName = pkParamName,
                        ParamValue = rowValues[pkIndices[p]] ?? DBNull.Value,
                        ParamType = EClickHouseDbType.Unknown
                    });
                }

                sql.Append($"ALTER TABLE {tableName} UPDATE {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}; ");
            }

            return (sql.ToString(), parameters);
        }

        // FALLBACK-PATH: Legacy RowWrapper evaluation
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        var allColumnNames = firstRow.Keys.ToList();
        var updateColumnNames = allColumnNames.Except(pkColumnNames).ToList();

        if (updateColumnNames.Count == 0) throw new ArgumentException("No columns to update.");

        var fallbackParameters = new List<INpOnDbCommandParam>();
        var fallbackSql = new StringBuilder();
        int fallbackParamCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            
            var setClauses = new List<string>();
            foreach (var colName in updateColumnNames)
            {
                setClauses.Add($"\"{colName}\" = @v{fallbackParamCounter}");
                row.TryGetValue(colName, out var cell);
                fallbackParameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = $"v{fallbackParamCounter++}",
                    ParamValue = cell?.ValueAsObject ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            var whereClauses = new List<string>();
            foreach (var pkColName in pkColumnNames)
            {
                whereClauses.Add($"\"{pkColName}\" = @pk{fallbackParamCounter}");
                row.TryGetValue(pkColName, out var cell);
                fallbackParameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = $"pk{fallbackParamCounter++}",
                    ParamValue = cell?.ValueAsObject ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            fallbackSql.Append($"ALTER TABLE {tableName} UPDATE {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}; ");
        }

        return (fallbackSql.ToString(), fallbackParameters);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsMerge(
        this INpOnTableWrapper table, string tableName)
    {
        return ToClickHouseParamsInsert(table, tableName);
    }

    public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToClickHouseParamsDelete(
        this INpOnTableWrapper table, string tableName)
    {
        // FAST-PATH: Direct Array Indexing
        if (table.NameToIndexMap != null && table.RawData != null)
        {
            var rawData = table.RawData;
            if (rawData.Count == 0) throw new ArgumentException("Table has no rows.");

            var nameToIndex = table.NameToIndexMap;
            var pkCols = (table.PrimaryKeyColumns != null && table.PrimaryKeyColumns.Count > 0)
                ? table.PrimaryKeyColumns.ToList()
                : new List<string> { nameToIndex.Keys.First() };

            var pkIndices = pkCols.Select(c => nameToIndex[c]).ToArray();
            var parameters = new List<INpOnDbCommandParam>(rawData.Count * pkCols.Count);
            var sql = new StringBuilder();
            int paramCounter = 0;

            if (pkCols.Count == 1)
            {
                var pkColName = pkCols[0];
                var pkIndex = pkIndices[0];
                var pkParamNames = new List<string>(rawData.Count);

                for (int r = 0; r < rawData.Count; r++)
                {
                    var paramName = $"pk{paramCounter++}";
                    pkParamNames.Add($"@{paramName}");
                    parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                    {
                        ParamName = paramName,
                        ParamValue = rawData[r][pkIndex] ?? DBNull.Value,
                        ParamType = EClickHouseDbType.Unknown
                    });
                }

                sql.Append($"ALTER TABLE {tableName} DELETE WHERE \"{pkColName}\" IN ({string.Join(", ", pkParamNames)});");
                return (sql.ToString(), parameters);
            }

            for (int r = 0; r < rawData.Count; r++)
            {
                var pkConditions = new List<string>(pkCols.Count);
                for (int p = 0; p < pkCols.Count; p++)
                {
                    var paramName = $"pk{paramCounter++}";
                    pkConditions.Add($"\"{pkCols[p]}\" = @{paramName}");
                    parameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                    {
                        ParamName = paramName,
                        ParamValue = rawData[r][pkIndices[p]] ?? DBNull.Value,
                        ParamType = EClickHouseDbType.Unknown
                    });
                }
                sql.Append($"ALTER TABLE {tableName} DELETE WHERE {string.Join(" AND ", pkConditions)}; ");
            }

            return (sql.ToString(), parameters);
        }

        // FALLBACK-PATH: Legacy RowWrapper evaluation
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        var fallbackParameters = new List<INpOnDbCommandParam>();
        var fallbackSql = new StringBuilder();
        int fallbackParamCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();

            var pkConditions = new List<string>();
            foreach (var pkColName in pkColumnNames)
            {
                pkConditions.Add($"\"{pkColName}\" = @pk{fallbackParamCounter}");
                row.TryGetValue(pkColName, out var cell);
                fallbackParameters.Add(new NpOnDbCommandParam<EClickHouseDbType>
                {
                    ParamName = $"pk{fallbackParamCounter++}",
                    ParamValue = cell?.ValueAsObject ?? DBNull.Value,
                    ParamType = EClickHouseDbType.Unknown
                });
            }

            fallbackSql.Append($"ALTER TABLE {tableName} DELETE WHERE {string.Join(" AND ", pkConditions)}; ");
        }

        return (fallbackSql.ToString(), fallbackParameters);
    }
}
