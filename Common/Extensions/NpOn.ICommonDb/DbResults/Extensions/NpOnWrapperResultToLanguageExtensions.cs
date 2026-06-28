using System.Text;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;

/// <summary>
/// Builder class responsible for "continuous conversion" and "merging" data into query strings (Sql/Cql/...)
/// </summary>
public class NpOnWrapperResultQueryBuilder
{
    private readonly IEnumerable<INpOnRowWrapper> _rows;
    private readonly EDbLanguage _language;
    private string? _tableName;
    private readonly Dictionary<string, string> _columnMappings = new(StringComparer.OrdinalIgnoreCase);

    public NpOnWrapperResultQueryBuilder(IEnumerable<INpOnRowWrapper> rows, EDbLanguage language)
    {
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        _language = language;
    }

    /// <summary>
    /// Assigns the table name to be used in the query
    /// </summary>
    public NpOnWrapperResultQueryBuilder WithTable(string tableName)
    {
        _tableName = tableName;
        return this;
    }

    /// <summary>
    /// Maps / replaces a column from the result to a column in the target DB (e.g., Map From -> To)
    /// </summary>
    public NpOnWrapperResultQueryBuilder WithReplaceColumn(string sourceColumn, string targetColumn)
    {
        _columnMappings[sourceColumn] = targetColumn;
        return this;
    }

    /// <summary>
    /// Generates an INSERT query string
    /// </summary>
    public string BuildInsert()
    {
        var rowList = _rows.ToList();
        if (rowList.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var tableName = _tableName ?? "UnknownTable";

        switch (_language)
        {
            case EDbLanguage.Cql:
                if (rowList.Count > 1) sb.AppendLine("BEGIN BATCH");
                foreach (var rowWrapper in rowList)
                {
                    var cells = rowWrapper.GetRowWrapper();
                    if (cells.Count == 0) continue;

                    var columns = new List<string>();
                    var values = new List<string>();
                    foreach (var kvp in cells)
                    {
                        var targetCol = _columnMappings.GetValueOrDefault(kvp.Key, kvp.Key);
                        columns.Add(targetCol);
                        values.Add(FormatDbValue(kvp.Value.ValueAsObject));
                    }
                    sb.Append($"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});\n");
                }
                if (rowList.Count > 1) sb.Append("APPLY BATCH;");
                break;

            case EDbLanguage.Sql:
                foreach (var rowWrapper in rowList)
                {
                    var cells = rowWrapper.GetRowWrapper();
                    if (cells.Count == 0) continue;

                    var columns = new List<string>();
                    var values = new List<string>();
                    foreach (var kvp in cells)
                    {
                        var targetCol = _columnMappings.GetValueOrDefault(kvp.Key, kvp.Key);
                        columns.Add(targetCol);
                        values.Add(FormatDbValue(kvp.Value.ValueAsObject));
                    }
                    sb.Append($"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});\n");
                }
                break;

            default:
                throw new NotSupportedException($"Language {_language} cannot be merged yet for INSERT.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Centralized build method that routes to BuildInsert, BuildUpdate or BuildMerge based on repository action.
    /// </summary>
    public string Build(ERepositoryAction action)
    {
        return action switch
        {
            ERepositoryAction.Add => BuildInsert(),
            ERepositoryAction.Update => BuildUpdate(),
            ERepositoryAction.Merge => BuildMerge(),
            _ => throw new NotSupportedException($"Action {action} is not supported for CQL/SQL.")
        };
    }


    /// <summary>
    /// Generates an UPDATE query string (using PrimaryKey from Cells as where keys)
    /// </summary>
    public string BuildUpdate()
    {
        var rowList = _rows.ToList();
        if (rowList.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var tableName = _tableName ?? "UnknownTable";

        switch (_language)
        {
            case EDbLanguage.Cql:
                if (rowList.Count > 1) sb.AppendLine("BEGIN BATCH");
                foreach (var rowWrapper in rowList)
                {
                    var cells = rowWrapper.GetRowWrapper();
                    if (cells.Count == 0) continue;

                    var setClauses = new List<string>();
                    var whereClauses = new List<string>();
                    foreach (var kvp in cells)
                    {
                        var targetCol = _columnMappings.GetValueOrDefault(kvp.Key, kvp.Key);
                        var formattedValue = FormatDbValue(kvp.Value.ValueAsObject);

                        if (kvp.Value.IsPrimaryKey)
                            whereClauses.Add($"{targetCol} = {formattedValue}");
                        else
                            setClauses.Add($"{targetCol} = {formattedValue}");
                    }
                    if (whereClauses.Count == 0) whereClauses.Add("1 = 0"); // break
                    sb.Append($"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};\n");
                }
                if (rowList.Count > 1) sb.Append("APPLY BATCH;");
                break;

            case EDbLanguage.Sql:
                foreach (var rowWrapper in rowList)
                {
                    var cells = rowWrapper.GetRowWrapper();
                    if (cells.Count == 0) continue;

                    var setClauses = new List<string>();
                    var whereClauses = new List<string>();
                    foreach (var kvp in cells)
                    {
                        var targetCol = _columnMappings.GetValueOrDefault(kvp.Key, kvp.Key);
                        var formattedValue = FormatDbValue(kvp.Value.ValueAsObject);

                        if (kvp.Value.IsPrimaryKey)
                            whereClauses.Add($"{targetCol} = {formattedValue}");
                        else
                            setClauses.Add($"{targetCol} = {formattedValue}");
                    }
                    if (whereClauses.Count == 0) whereClauses.Add("1 = 0"); // break
                    sb.Append($"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};\n");
                }
                break;

            default:
                throw new NotSupportedException($"Language {_language} cannot be merged yet for UPDATE.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a MERGE query string (Upsert: Add and Update).
    /// </summary>
    public string BuildMerge()
    {
        var rowList = _rows.ToList();
        if (rowList.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var tableName = _tableName ?? "UnknownTable";

        switch (_language)
        {
            case EDbLanguage.Cql:
                // CQL Insert is automatically an Upsert
                if (rowList.Count > 1) sb.AppendLine("BEGIN BATCH");
                foreach (var rowWrapper in rowList)
                {
                    var cells = rowWrapper.GetRowWrapper();
                    if (cells.Count == 0) continue;

                    var columns = new List<string>();
                    var values = new List<string>();
                    foreach (var kvp in cells)
                    {
                        var targetCol = _columnMappings.GetValueOrDefault(kvp.Key, kvp.Key);
                        columns.Add(targetCol);
                        values.Add(FormatDbValue(kvp.Value.ValueAsObject));
                    }
                    sb.Append($"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)});\n");
                }
                if (rowList.Count > 1) sb.Append("APPLY BATCH;");
                break;

            case EDbLanguage.Sql:
                foreach (var rowWrapper in rowList)
                {
                    var cells = rowWrapper.GetRowWrapper();
                    if (cells.Count == 0) continue;

                    var columns = new List<string>();
                    var values = new List<string>();
                    var primaryKeys = new List<string>();
                    var updateClauses = new List<string>();
                    foreach (var kvp in cells)
                    {
                        var targetCol = _columnMappings.GetValueOrDefault(kvp.Key, kvp.Key);
                        var formattedValue = FormatDbValue(kvp.Value.ValueAsObject);
                        columns.Add(targetCol);
                        values.Add(formattedValue);

                        if (kvp.Value.IsPrimaryKey)
                            primaryKeys.Add(targetCol);
                        else
                            updateClauses.Add($"{targetCol} = EXCLUDED.{targetCol}");
                    }

                    sb.Append($"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})");
                    if (primaryKeys.Count > 0 && updateClauses.Count > 0)
                        sb.Append($" ON CONFLICT ({string.Join(", ", primaryKeys)}) DO UPDATE SET {string.Join(", ", updateClauses)}");
                    else if (primaryKeys.Count > 0)
                        sb.Append($" ON CONFLICT ({string.Join(", ", primaryKeys)}) DO NOTHING");
                    sb.Append(";\n");
                }
                break;

            default:
                throw new NotSupportedException($"Language {_language} cannot be merged yet for MERGE.");
        }

        return sb.ToString();
    }

    private string FormatDbValue(object? value)
    {
        if (value == null) return "NULL";

        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'",
            bool b => b ? "true" : "false",
            Guid g => g.ToString(), // UUID literals in CQL are not quoted
            _ => value.ToString() ?? "NULL"
        };
    }
}

public static partial class NpOnWrapperResultExtensions
{
    /// <summary>
    /// Initializes the syntax generation process for a Table Wrapper Result.
    /// Data is automatically retrieved from all rows within the result.
    /// </summary>
    public static NpOnWrapperResultQueryBuilder ToQueryBuilder(this INpOnWrapperResult result, EDbLanguage language)
    {
        IEnumerable<INpOnRowWrapper> rows;

        if (result is INpOnTableWrapper tableWrapper)
        {
            rows = tableWrapper.RowWrappers.Values.Where(r => r != null)!;
        }
        else
        {
            rows = Enumerable.Empty<INpOnRowWrapper>();
        }

        var builder = new NpOnWrapperResultQueryBuilder(rows, language);
        
        var typeName = result.GetType().Name;
        if (typeName.Contains('`')) typeName = typeName[..typeName.IndexOf('`')];
        typeName = typeName.Replace("WrapperResult", "").Replace("Result", "");
        
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            builder.WithTable(typeName);
        }

        return builder;
    }

    /// <summary>
    /// Initializes the syntax generation process for a single data row (RowWrapper).
    /// </summary>
    public static NpOnWrapperResultQueryBuilder ToQueryBuilder(this INpOnRowWrapper rowWrapper, EDbLanguage language)
    {
        return new NpOnWrapperResultQueryBuilder(new[] { rowWrapper }, language);
    }

}
