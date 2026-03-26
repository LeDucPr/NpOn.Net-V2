using System.Text;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Infrastructures.DbFactories.NpOn.CassandraFactory
{
    public static class TableCassandraExtensions
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

        public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToCassandraParamsInsert(
            this INpOnTableWrapper table, string tableName)
        {
            if (table.RowWrappers is not { Count: > 0 } rowWrappers)
                throw new ArgumentException("Table has no rows.");

            var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
            if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

            var columnNames = firstRow.Keys.ToList();
            if (columnNames.Count == 0)
                throw new ArgumentException("Table has no columns.");

            var parameters = new List<INpOnDbCommandParam>();
            var sql = new StringBuilder();
            int paramCounter = 0;

            if (rowWrappers.Count > 1) sql.Append("BEGIN BATCH ");

            foreach (var rowWrapper in rowWrappers.Values)
            {
                if (rowWrapper == null) continue;
                var row = rowWrapper.GetRowWrapper();
                var valueParams = new List<string>();

                foreach (var colName in columnNames)
                {
                    valueParams.Add("?");

                    row.TryGetValue(colName, out var cell);
                    var (paramValue, cassType) = Common.Infrastructures.NpOn.CassandraExtCm.Results.CassandraUtils.NormalizeForCassandra(cell?.ValueAsObject);

                    parameters.Add(new NpOnDbCommandParam<Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType>
                    {
                        ParamName = $"p{paramCounter++}",
                        ParamValue = paramValue ?? DBNull.Value,
                        ParamType = cassType ?? Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown
                    });
                }

                sql.Append($"INSERT INTO {tableName} (\"{string.Join("\",\"", columnNames)}\") VALUES ({string.Join(",", valueParams)}); ");
            }

            if (rowWrappers.Count > 1) sql.Append("APPLY BATCH;");

            return (sql.ToString(), parameters);
        }

        public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToCassandraParamsUpdate(
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

            var parameters = new List<INpOnDbCommandParam>();
            var sql = new StringBuilder();
            int paramCounter = 0;

            if (rowWrappers.Count > 1) sql.Append("BEGIN BATCH ");

            foreach (var rowWrapper in rowWrappers.Values)
            {
                if (rowWrapper == null) continue;
                var row = rowWrapper.GetRowWrapper();
                
                var setClauses = new List<string>();
                foreach (var colName in updateColumnNames)
                {
                    setClauses.Add($"\"{colName}\" = ?");

                    row.TryGetValue(colName, out var cell);
                    var (paramValue, cassType) = Common.Infrastructures.NpOn.CassandraExtCm.Results.CassandraUtils.NormalizeForCassandra(cell?.ValueAsObject);
                    
                    parameters.Add(new NpOnDbCommandParam<Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType>
                    {
                        ParamName = $"v{paramCounter++}",
                        ParamValue = paramValue ?? DBNull.Value,
                        ParamType = cassType ?? Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown
                    });
                }

                var whereClauses = new List<string>();
                foreach (var pkColName in pkColumnNames)
                {
                    whereClauses.Add($"\"{pkColName}\" = ?");

                    if (!row.TryGetValue(pkColName, out var cell) || cell.ValueAsObject == null)
                        throw new InvalidOperationException($"PK value for '{pkColName}' cannot be null.");

                    var (pkValue, pkType) = Common.Infrastructures.NpOn.CassandraExtCm.Results.CassandraUtils.NormalizeForCassandra(cell.ValueAsObject);
                    parameters.Add(new NpOnDbCommandParam<Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType>
                    {
                        ParamName = $"pk{paramCounter++}",
                        ParamValue = pkValue ?? DBNull.Value,
                        ParamType = pkType ?? Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown
                    });
                }

                sql.Append($"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}; ");
            }

            if (rowWrappers.Count > 1) sql.Append("APPLY BATCH;");

            return (sql.ToString(), parameters);
        }

        public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToCassandraParamsMerge(
            this INpOnTableWrapper table, string tableName)
        {
            return ToCassandraParamsInsert(table, tableName);
        }

        public static (string CommandText, List<INpOnDbCommandParam> Parameters) ToCassandraParamsDelete(
            this INpOnTableWrapper table, string tableName)
        {
            if (table.RowWrappers is not { Count: > 0 } rowWrappers)
                throw new ArgumentException("Table has no rows.");

            var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
            if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

            var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
            if (pkColumnNames.Count == 0)
                throw new InvalidOperationException("Could not determine primary key for delete operation.");

            var parameters = new List<INpOnDbCommandParam>();
            var sql = new StringBuilder();
            int paramCounter = 0;

            if (rowWrappers.Count > 1) sql.Append("BEGIN BATCH ");

            foreach (var rowWrapper in rowWrappers.Values)
            {
                if (rowWrapper == null) continue;
                var row = rowWrapper.GetRowWrapper();

                var pkConditions = new List<string>();
                foreach (var pkColName in pkColumnNames)
                {
                    pkConditions.Add($"\"{pkColName}\" = ?");

                    if (!row.TryGetValue(pkColName, out var cell) || cell.ValueAsObject == null)
                        throw new InvalidOperationException($"PK value for '{pkColName}' cannot be null.");

                    var (pkValue, pkType) = Common.Infrastructures.NpOn.CassandraExtCm.Results.CassandraUtils.NormalizeForCassandra(cell.ValueAsObject);
                    parameters.Add(new NpOnDbCommandParam<Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType>
                    {
                        ParamName = $"pk{paramCounter++}",
                        ParamValue = pkValue ?? DBNull.Value,
                        ParamType = pkType ?? Common.Extensions.NpOn.CommonEnums.DatabaseEnums.ECassandraDbType.Unknown
                    });
                }

                if (pkConditions.Count > 0)
                {
                     sql.Append($"DELETE FROM {tableName} WHERE {string.Join(" AND ", pkConditions)}; ");
                }
            }

            if (rowWrappers.Count > 1) sql.Append("APPLY BATCH;");

            return (sql.ToString(), parameters);
        }
    }
}
