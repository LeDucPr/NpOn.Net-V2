using System.Text;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.Neo4jExtCm.Results;

namespace Common.Infrastructures.DbFactories.NpOn.Neo4jDbFactory;

public static class TableNeo4JExtensions
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

    public static (string CommandText, Dictionary<string, object?> Parameters) ToNeo4JParamsCreate(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");

        var parameters = new Dictionary<string, object?>();
        var cypher = new StringBuilder();
        var propsList = new List<Dictionary<string, object?>>();

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();
            
            var props = new Dictionary<string, object?>();
            foreach (var kvp in row)
            {
                props[kvp.Key] = Neo4JUtils.NormalizeToCypherValue(kvp.Value.ValueAsObject);
            }
            propsList.Add(props);
        }

        parameters.Add("props", propsList);
        cypher.Append($"UNWIND $props AS prop CREATE (n:`{tableName}`) SET n = prop;");

        return (cypher.ToString(), parameters);
    }

    public static (string CommandText, Dictionary<string, object?> Parameters) ToNeo4JParamsUpdate(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        
        var updatesList = new List<Dictionary<string, object?>>();
        var parameters = new Dictionary<string, object?>();
        var cypher = new StringBuilder();

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();

            var updateItem = new Dictionary<string, object?>();
            var pks = new Dictionary<string, object?>();
            var props = new Dictionary<string, object?>();

            foreach (var kvp in row)
            {
                if (pkColumnNames.Contains(kvp.Key))
                {
                    pks[kvp.Key] = Neo4JUtils.NormalizeToCypherValue(kvp.Value.ValueAsObject);
                }
                else
                {
                    props[kvp.Key] = Neo4JUtils.NormalizeToCypherValue(kvp.Value.ValueAsObject);
                }
            }

            updateItem["pks"] = pks;
            updateItem["props"] = props;
            updatesList.Add(updateItem);
        }

        parameters.Add("updates", updatesList);
        var matchConditions = string.Join(" AND ", pkColumnNames.Select(pk => $"n.`{pk}` = update.pks.`{pk}`"));
        cypher.Append($"UNWIND $updates AS update MATCH (n:`{tableName}`) WHERE {matchConditions} SET n += update.props;");

        return (cypher.ToString(), parameters);
    }

    public static (string CommandText, Dictionary<string, object?> Parameters) ToNeo4JParamsMerge(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        
        var mergesList = new List<Dictionary<string, object?>>();
        var parameters = new Dictionary<string, object?>();
        var cypher = new StringBuilder();

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();

            var pks = new Dictionary<string, object?>();
            var props = new Dictionary<string, object?>();

            foreach (var kvp in row)
            {
                if (pkColumnNames.Contains(kvp.Key))
                    pks[kvp.Key] = Neo4JUtils.NormalizeToCypherValue(kvp.Value.ValueAsObject);
                else
                    props[kvp.Key] = Neo4JUtils.NormalizeToCypherValue(kvp.Value.ValueAsObject);
            }
            mergesList.Add(new Dictionary<string, object?> { ["pks"] = pks, ["props"] = props });
        }

        parameters.Add("merges", mergesList);
        var mergeProperties = string.Join(", ", pkColumnNames.Select(pk => $"`{pk}`: merge.pks.`{pk}`"));
        cypher.Append($"UNWIND $merges AS merge MERGE (n:`{tableName}` {{ {mergeProperties} }}) SET n += merge.props;");

        return (cypher.ToString(), parameters);
    }

    public static (string CommandText, Dictionary<string, object?> Parameters) ToNeo4JParamsDelete(
        this INpOnTableWrapper table, string tableName)
    {
        if (table.RowWrappers is not { Count: > 0 } rowWrappers)
            throw new ArgumentException("Table has no rows.");
        var firstRow = rowWrappers.Values.FirstOrDefault(r => r != null)?.GetRowWrapper();
        if (firstRow == null) throw new ArgumentException("Table contains no valid rows.");

        var pkColumnNames = GetPrimaryKeyColumnNames(firstRow);
        
        var deletesList = new List<Dictionary<string, object?>>();
        var parameters = new Dictionary<string, object?>();
        var cypher = new StringBuilder();

        foreach (var rowWrapper in rowWrappers.Values)
        {
            if (rowWrapper == null) continue;
            var row = rowWrapper.GetRowWrapper();

            var pks = new Dictionary<string, object?>();
            foreach (var pk in pkColumnNames)
            {
                pks[pk] = Neo4JUtils.NormalizeToCypherValue(row[pk].ValueAsObject);
            }
            deletesList.Add(pks);
        }

        parameters.Add("deletes", deletesList);
        var matchConditions = string.Join(" AND ", pkColumnNames.Select(pk => $"n.`{pk}` = item.`{pk}`"));
        cypher.Append($"UNWIND $deletes AS item MATCH (n:`{tableName}`) WHERE {matchConditions} DETACH DELETE n;");

        return (cypher.ToString(), parameters);
    }
}
