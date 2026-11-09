using System.Text;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.PostgresExtCm.Results;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;

public static class TablePostgresExtensions
{
    private static List<string> GetPrimaryKeyColumnNames(IReadOnlyDictionary<string, INpOnCell> row)
    {
        var pkCols = row.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key).ToList();
        if (pkCols.Count == 0)
        {
            var firstCol = row.Keys.FirstOrDefault();
            if (firstCol != null)
            {
                pkCols.Add(firstCol);
            }
        }
        return pkCols;
    }

    public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsInsert(
        this INpOnTableWrapper table, string tableName)
    {
        // FAST-PATH: Direct Array Indexing without RowWrapper, Dictionary, or Cell allocations
        if (table.NameToIndexMap != null && table.RawData != null)
        {
            var rawData = table.RawData;
            if (rawData.Count == 0) throw new ArgumentException("Table has no rows.");

            var nameToIndex = table.NameToIndexMap;
            var columnNames = nameToIndex.Keys.ToList();
            if (columnNames.Count == 0) throw new ArgumentException("Table has no columns.");

            var colIndices = columnNames.Select(c => nameToIndex[c]).ToArray();
            var quotedColumns = string.Join("\",\"", columnNames);

            var parameters = new List<NpgsqlParameter>(rawData.Count * columnNames.Count);
            var sql = new StringBuilder(rawData.Count * columnNames.Count * 16);

            int paramCounter = 0;
            bool isFirstRowInBatch = true;

            for (int r = 0; r < rawData.Count; r++)
            {
                // PostgreSQL parameter limit protection (60,000 safety threshold)
                if (isFirstRowInBatch || (paramCounter + columnNames.Count > 60000))
                {
                    if (!isFirstRowInBatch)
                    {
                        sql.Append("; ");
                    }
                    sql.Append($"INSERT INTO {tableName} (\"{quotedColumns}\") VALUES ");
                    isFirstRowInBatch = false;
                }
                else
                {
                    sql.Append(", ");
                }

                sql.Append('(');
                var rowValues = rawData[r];

                for (int c = 0; c < colIndices.Length; c++)
                {
                    if (c > 0) sql.Append(',');
                    var paramName = $"@p{paramCounter++}";
                    sql.Append(paramName);

                    var rawVal = rowValues[colIndices[c]];
                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(rawVal);
                    var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value);
                    if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                    parameters.Add(p);
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
        if (firstRow == null)
            throw new ArgumentException("Table contains no valid rows.");

        var fallbackColumns = firstRow.Keys.ToList();
        if (fallbackColumns.Count == 0)
            throw new ArgumentException("Table has no columns.");

        var fallbackParams = new List<NpgsqlParameter>();
        var fallbackSql = new StringBuilder();
        int fallbackParamCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            var valueParams = new List<string>(fallbackColumns.Count);

            foreach (var colName in fallbackColumns)
            {
                var paramName = $"@p{fallbackParamCounter++}";
                valueParams.Add(paramName);

                row.TryGetValue(colName, out var cell);
                var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell?.ValueAsObject);

                var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                fallbackParams.Add(p);
            }

            fallbackSql.Append($"INSERT INTO {tableName} (\"{string.Join("\",\"", fallbackColumns)}\") VALUES ({string.Join(",", valueParams)});");
        }

        return (fallbackSql.ToString(), fallbackParams);
    }

    public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsUpdate(
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
            if (updateCols.Count == 0)
                throw new ArgumentException("No columns to update (all columns are PKs).");

            var updateIndices = updateCols.Select(c => nameToIndex[c]).ToArray();
            var pkIndices = pkCols.Select(c => nameToIndex[c]).ToArray();

            var parameters = new List<NpgsqlParameter>(rawData.Count * allCols.Count);
            var sql = new StringBuilder(rawData.Count * 64);
            int paramCounter = 0;

            for (int r = 0; r < rawData.Count; r++)
            {
                var rowValues = rawData[r];
                sql.Append($"UPDATE {tableName} SET ");

                for (int c = 0; c < updateCols.Count; c++)
                {
                    if (c > 0) sql.Append(", ");
                    var paramName = $"@v{paramCounter++}";
                    sql.Append($"\"{updateCols[c]}\" = {paramName}");

                    var rawVal = rowValues[updateIndices[c]];
                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(rawVal);
                    var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value);
                    if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                    parameters.Add(p);
                }

                sql.Append(" WHERE ");
                for (int p = 0; p < pkCols.Count; p++)
                {
                    if (p > 0) sql.Append(" AND ");
                    var pkParamName = $"@pk{paramCounter++}";
                    sql.Append($"\"{pkCols[p]}\" = {pkParamName}");

                    var pkVal = rowValues[pkIndices[p]];
                    if (pkVal == null || pkVal is DBNull)
                        throw new InvalidOperationException($"PK value for '{pkCols[p]}' cannot be null.");

                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(pkVal);
                    var param = new NpgsqlParameter(pkParamName, paramValue);
                    if (npgType != NpgsqlDbType.Unknown) param.NpgsqlDbType = npgType;
                    parameters.Add(param);
                }
                sql.Append(';');
            }

            return (sql.ToString(), parameters);
        }

        // FALLBACK-PATH: Legacy RowWrapper evaluation
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        if (pkColumnNames.Count == 0)
            throw new InvalidOperationException("Could not determine primary key for update operation.");

        var allColumnNames = firstRow.Keys.ToList();
        var updateColumnNames = allColumnNames.Except(pkColumnNames).ToList();

        if (updateColumnNames.Count == 0)
            throw new ArgumentException("No columns to update (all columns are PKs).");

        var fallbackParameters = new List<NpgsqlParameter>();
        var fallbackSql = new StringBuilder();
        int fallbackParamCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
                
            var setClauses = new List<string>(updateColumnNames.Count);
            foreach (var colName in updateColumnNames)
            {
                var paramName = $"@v{fallbackParamCounter++}";
                setClauses.Add($"\"{colName}\" = {paramName}");

                row.TryGetValue(colName, out var cell);
                var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell?.ValueAsObject);
                var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                fallbackParameters.Add(p);
            }

            var whereClauses = new List<string>(pkColumnNames.Count);
            foreach (var pkColName in pkColumnNames)
            {
                var pkParamName = $"@pk{fallbackParamCounter++}";
                whereClauses.Add($"\"{pkColName}\" = {pkParamName}");

                if (!row.TryGetValue(pkColName, out var cell) || cell.ValueAsObject == null)
                    throw new InvalidOperationException($"PK value for '{pkColName}' cannot be null.");

                var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell.ValueAsObject);
                var p = new NpgsqlParameter(pkParamName, paramValue);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                fallbackParameters.Add(p);
            }

            fallbackSql.Append($"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};");
        }

        return (fallbackSql.ToString(), fallbackParameters);
    }

    public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsMerge(
        this INpOnTableWrapper table, string tableName)
    {
        // FAST-PATH: High-Speed Multi-Row UPSERT
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

            var allIndices = allCols.Select(c => nameToIndex[c]).ToArray();
            var quotedCols = string.Join("\",\"", allCols);
            var quotedPks = string.Join("\",\"", pkCols);

            var parameters = new List<NpgsqlParameter>(rawData.Count * allCols.Count);
            var sql = new StringBuilder(rawData.Count * allCols.Count * 16);
            int paramCounter = 0;

            sql.Append($"INSERT INTO {tableName} (\"{quotedCols}\") VALUES ");

            for (int r = 0; r < rawData.Count; r++)
            {
                if (r > 0) sql.Append(", ");
                sql.Append('(');
                var rowValues = rawData[r];

                for (int c = 0; c < allIndices.Length; c++)
                {
                    if (c > 0) sql.Append(',');
                    var paramName = $"@p{paramCounter++}";
                    sql.Append(paramName);

                    var rawVal = rowValues[allIndices[c]];
                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(rawVal);
                    var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value);
                    if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                    parameters.Add(p);
                }
                sql.Append(')');
            }

            sql.Append($" ON CONFLICT (\"{quotedPks}\") DO UPDATE SET ");
            var updateClauses = updateCols.Select(c => $"\"{c}\" = EXCLUDED.\"{c}\"");
            sql.Append(string.Join(", ", updateClauses));
            sql.Append(';');

            return (sql.ToString(), parameters);
        }

        // FALLBACK-PATH: Legacy RowWrapper evaluation
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        if (pkColumnNames.Count == 0)
            throw new InvalidOperationException("Could not determine primary key for merge operation.");

        var allColumnNames = firstRow.Keys.ToList();
        var updateColumnNames = allColumnNames.Except(pkColumnNames).ToList();

        var fallbackParameters = new List<NpgsqlParameter>();
        var fallbackSql = new StringBuilder();
        int fallbackParamCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();

            var insertCols = new List<string>();
            var insertParams = new List<string>();
                
            foreach (var colName in allColumnNames)
            {
                var paramName = $"@p{fallbackParamCounter++}";
                insertCols.Add($"\"{colName}\"");
                insertParams.Add(paramName);

                row.TryGetValue(colName, out var cell);
                var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell?.ValueAsObject);
                var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                fallbackParameters.Add(p);
            }

            fallbackSql.Append($"INSERT INTO {tableName} ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertParams)})");
            fallbackSql.Append($" ON CONFLICT (\"{string.Join("\",\"", pkColumnNames)}\") DO UPDATE SET ");
                
            var updateClauses = updateColumnNames.Select(c => $"\"{c}\" = EXCLUDED.\"{c}\"");
            fallbackSql.Append(string.Join(", ", updateClauses));
            fallbackSql.Append(';');
        }

        return (fallbackSql.ToString(), fallbackParameters);
    }

    public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsDelete(
        this INpOnTableWrapper table, string tableName)
    {
        // FAST-PATH: High-Speed Direct Array Indexing
        if (table.NameToIndexMap != null && table.RawData != null)
        {
            var rawData = table.RawData;
            if (rawData.Count == 0) throw new ArgumentException("Table has no rows.");

            var nameToIndex = table.NameToIndexMap;
            var pkCols = (table.PrimaryKeyColumns != null && table.PrimaryKeyColumns.Count > 0)
                ? table.PrimaryKeyColumns.ToList()
                : new List<string> { nameToIndex.Keys.First() };

            var pkIndices = pkCols.Select(c => nameToIndex[c]).ToArray();
            var parameters = new List<NpgsqlParameter>(rawData.Count * pkCols.Count);
            var sql = new StringBuilder();
            int paramCounter = 0;

            if (pkCols.Count == 1)
            {
                // Single-column PK: High-speed IN clause
                var pkColName = pkCols[0];
                var pkIndex = pkIndices[0];
                sql.Append($"DELETE FROM {tableName} WHERE \"{pkColName}\" IN (");

                for (int r = 0; r < rawData.Count; r++)
                {
                    if (r > 0) sql.Append(", ");
                    var pkParamName = $"@pk{paramCounter++}";
                    sql.Append(pkParamName);

                    var pkVal = rawData[r][pkIndex];
                    if (pkVal == null || pkVal is DBNull)
                        throw new InvalidOperationException($"PK value for '{pkColName}' cannot be null.");

                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(pkVal);
                    var p = new NpgsqlParameter(pkParamName, paramValue);
                    if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                    parameters.Add(p);
                }
                sql.Append(");");
            }
            else
            {
                // Composite PK: WHERE ("pk1" = @pk0 AND "pk2" = @pk1) OR ...
                sql.Append($"DELETE FROM {tableName} WHERE ");
                for (int r = 0; r < rawData.Count; r++)
                {
                    if (r > 0) sql.Append(" OR ");
                    sql.Append('(');
                    for (int p = 0; p < pkCols.Count; p++)
                    {
                        if (p > 0) sql.Append(" AND ");
                        var pkParamName = $"@pk{paramCounter++}";
                        sql.Append($"\"{pkCols[p]}\" = {pkParamName}");

                        var pkVal = rawData[r][pkIndices[p]];
                        if (pkVal == null || pkVal is DBNull)
                            throw new InvalidOperationException($"PK value for '{pkCols[p]}' cannot be null.");

                        var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(pkVal);
                        var param = new NpgsqlParameter(pkParamName, paramValue);
                        if (npgType != NpgsqlDbType.Unknown) param.NpgsqlDbType = npgType;
                        parameters.Add(param);
                    }
                    sql.Append(')');
                }
                sql.Append(';');
            }

            return (sql.ToString(), parameters);
        }

        // FALLBACK-PATH: Legacy RowWrapper evaluation
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        if (pkColumnNames.Count == 0)
            throw new InvalidOperationException("Could not determine primary key for delete operation.");

        var fallbackParameters = new List<NpgsqlParameter>();
        var fallbackSql = new StringBuilder();
        var whereClauses = new List<string>();
        int fallbackParamCounter = 0;

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();

            var pkConditions = new List<string>();
            foreach (var pkColName in pkColumnNames)
            {
                var pkParamName = $"@pk{fallbackParamCounter++}";
                pkConditions.Add($"\"{pkColName}\" = {pkParamName}");

                if (!row.TryGetValue(pkColName, out var cell) || cell.ValueAsObject == null)
                    throw new InvalidOperationException($"PK value for '{pkColName}' cannot be null.");

                var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell.ValueAsObject);
                var p = new NpgsqlParameter(pkParamName, paramValue);
                if (npgType != NpgsqlDbType.Unknown) p.NpgsqlDbType = npgType;
                fallbackParameters.Add(p);
            }
            whereClauses.Add($"({string.Join(" AND ", pkConditions)})");
        }

        if (whereClauses.Count > 0)
        {
            fallbackSql.Append($"DELETE FROM {tableName} WHERE {string.Join(" OR ", whereClauses)};");
        }

        return (fallbackSql.ToString(), fallbackParameters);
    }
}
