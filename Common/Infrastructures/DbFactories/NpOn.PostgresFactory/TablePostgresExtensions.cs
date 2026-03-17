using System.Text;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.PostgresExtCm.Results;
using Npgsql;

namespace Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory
{
    public static class TablePostgresExtensions
    {
        private static List<string> GetPrimaryKeyColumnNames(IReadOnlyDictionary<string, INpOnCell> row)
        {
            var pkCols = row.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key).ToList();
            if (pkCols.Count == 0)
            {
                // Fallback: if no PK is marked, assume the first column is the key.
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
            if (table.RowWrappers is not { Count: > 0 } rowWrappers)
                throw new ArgumentException("Table has no rows.");

            var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
            if (firstRow == null)
                throw new ArgumentException("Table contains no valid rows.");

            var columnNames = firstRow.Keys.ToList();
            if (columnNames.Count == 0)
                throw new ArgumentException("Table has no columns.");

            var parameters = new List<NpgsqlParameter>();
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
                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell?.ValueAsObject);

                    var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value)
                    {
                        NpgsqlDbType = npgType
                    };
                    parameters.Add(p);
                }

                sql.Append($"INSERT INTO {tableName} (\"{string.Join("\",\"", columnNames)}\") VALUES ({string.Join(",", valueParams)});");
            }

            return (sql.ToString(), parameters);
        }

        public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsUpdate(
            this INpOnTableWrapper table, string tableName)
        {
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

            var parameters = new List<NpgsqlParameter>();
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
                    setClauses.Add($"\"{colName}\" = {paramName}");

                    row.TryGetValue(colName, out var cell);
                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell?.ValueAsObject);
                    var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value)
                    {
                        NpgsqlDbType = npgType
                    };
                    parameters.Add(p);
                }

                var whereClauses = new List<string>();
                foreach (var pkColName in pkColumnNames)
                {
                    var pkParamName = $"@pk{paramCounter++}";
                    whereClauses.Add($"\"{pkColName}\" = {pkParamName}");

                    if (!row.TryGetValue(pkColName, out var cell) || cell.ValueAsObject == null)
                        throw new InvalidOperationException($"PK value for '{pkColName}' cannot be null.");

                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell.ValueAsObject);
                    var p = new NpgsqlParameter(pkParamName, paramValue)
                    {
                        NpgsqlDbType = npgType
                    };
                    parameters.Add(p);
                }

                sql.Append($"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};");
            }

            return (sql.ToString(), parameters);
        }

        public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsMerge(
            this INpOnTableWrapper table, string tableName)
        {
            if (table.RowWrappers is not { Count: > 0 } rowWrappers)
                throw new ArgumentException("Table has no rows.");

            var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
            if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

            var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
            if (pkColumnNames.Count == 0)
                throw new InvalidOperationException("Could not determine primary key for merge operation.");

            var allColumnNames = firstRow.Keys.ToList();
            var updateColumnNames = allColumnNames.Except(pkColumnNames).ToList();

            var parameters = new List<NpgsqlParameter>();
            var sql = new StringBuilder();
            int paramCounter = 0;

            foreach (var rowWrapper in rowWrappers.Values)
            {
                if (rowWrapper == null) continue;
                var row = rowWrapper.GetRowWrapper();

                var insertCols = new List<string>();
                var insertParams = new List<string>();
                
                foreach (var colName in allColumnNames)
                {
                    var paramName = $"@p{paramCounter++}";
                    insertCols.Add($"\"{colName}\"");
                    insertParams.Add(paramName);

                    row.TryGetValue(colName, out var cell);
                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell?.ValueAsObject);
                    var p = new NpgsqlParameter(paramName, paramValue ?? DBNull.Value)
                    {
                        NpgsqlDbType = npgType
                    };
                    parameters.Add(p);
                }

                sql.Append($"INSERT INTO {tableName} ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertParams)})");
                sql.Append($" ON CONFLICT (\"{string.Join("\",\"", pkColumnNames)}\") DO UPDATE SET ");
                
                var updateClauses = updateColumnNames.Select(c => $"\"{c}\" = EXCLUDED.\"{c}\"");
                sql.Append(string.Join(", ", updateClauses));
                sql.Append(';');
            }

            return (sql.ToString(), parameters);
        }

        public static (string CommandText, List<NpgsqlParameter> Parameters) ToPostgresParamsDelete(
            this INpOnTableWrapper table, string tableName)
        {
            if (table.RowWrappers is not { Count: > 0 } rowWrappers)
                throw new ArgumentException("Table has no rows.");

            var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
            if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

            var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
            if (pkColumnNames.Count == 0)
                throw new InvalidOperationException("Could not determine primary key for delete operation.");

            var parameters = new List<NpgsqlParameter>();
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
                    pkConditions.Add($"\"{pkColName}\" = {pkParamName}");

                    if (!row.TryGetValue(pkColName, out var cell) || cell.ValueAsObject == null)
                        throw new InvalidOperationException($"PK value for '{pkColName}' cannot be null.");

                    var (paramValue, npgType) = PostgresUtils.NormalizeForNpgsql(cell.ValueAsObject);
                    var p = new NpgsqlParameter(pkParamName, paramValue)
                    {
                        NpgsqlDbType = npgType
                    };
                    parameters.Add(p);
                }
                whereClauses.Add($"({string.Join(" AND ", pkConditions)})");
            }

            if (whereClauses.Count > 0)
            {
                sql.Append($"DELETE FROM {tableName} WHERE {string.Join(" OR ", whereClauses)};");
            }

            return (sql.ToString(), parameters);
        }
    }
}