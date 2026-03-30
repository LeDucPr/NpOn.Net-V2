using System.Text;
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
        var sb = new StringBuilder();
        var tableName = _tableName ?? "UnknownTable";

        foreach (var rowWrapper in _rows)
        {
            var cells = rowWrapper.GetRowWrapper();
            if (cells.Count == 0) 
                continue;

            var columns = new List<string>();
            var values = new List<string>();

            foreach (var kvp in cells)
            {
                var sourceCol = kvp.Key;
                var targetCol = _columnMappings.GetValueOrDefault(sourceCol, sourceCol);
                columns.Add(targetCol);

                var cellValue = kvp.Value.ValueAsObject;
                values.Add(FormatDbValue(cellValue));
            }

            switch (_language)
            {
                case EDbLanguage.Sql:
                case EDbLanguage.Cql:
                    sb.Append("INSERT INTO ")
                      .Append(tableName)
                      .Append(" (")
                      .Append(string.Join(", ", columns))
                      .Append(") VALUES (")
                      .Append(string.Join(", ", values))
                      .Append(");\n");
                    break;
                case EDbLanguage.Json:
                case EDbLanguage.Bson:
                case EDbLanguage.Unknown:
                default:
                    throw new NotSupportedException($"Language {_language} cannot be merged yet for INSERT.");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates an UPDATE query string (using PrimaryKey from Cells as where keys)
    /// </summary>
    public string BuildUpdate()
    {
        var sb = new StringBuilder();
        var tableName = _tableName ?? "UnknownTable";

        foreach (var rowWrapper in _rows)
        {
            var cells = rowWrapper.GetRowWrapper();
            if (cells.Count == 0) 
                continue;

            var setClauses = new List<string>();
            var whereClauses = new List<string>();

            foreach (var kvp in cells)
            {
                var sourceCol = kvp.Key;
                var targetCol = _columnMappings.TryGetValue(sourceCol, out var mappedCol) ? mappedCol : sourceCol;
                var cellValue = kvp.Value?.ValueAsObject;
                var formattedValue = FormatDbValue(cellValue);

                if (kvp.Value?.IsPrimaryKey == true)
                {
                    whereClauses.Add($"{targetCol} = {formattedValue}");
                }
                else
                {
                    setClauses.Add($"{targetCol} = {formattedValue}");
                }
            }

            if (whereClauses.Count == 0)
            {
                whereClauses.Add("1 = 0"); 
            }

            switch (_language)
            {
                case EDbLanguage.Sql:
                case EDbLanguage.Cql:
                    sb.Append("UPDATE ")
                      .Append(tableName)
                      .Append(" SET ")
                      .Append(string.Join(", ", setClauses))
                      .Append(" WHERE ")
                      .Append(string.Join(" AND ", whereClauses))
                      .Append(";\n");
                    break;
                case EDbLanguage.Json:
                case EDbLanguage.Bson:
                case EDbLanguage.Unknown:
                default:
                    throw new NotSupportedException($"Language {_language} cannot be merged yet for UPDATE.");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a MERGE query string (Upsert: Add and Update).
    /// </summary>
    public string BuildMerge()
    {
        var sb = new StringBuilder();
        var tableName = _tableName ?? "UnknownTable";

        foreach (var rowWrapper in _rows)
        {
            var cells = rowWrapper.GetRowWrapper();
            if (cells.Count == 0) 
                continue;

            var columns = new List<string>();
            var values = new List<string>();
            var primaryKeys = new List<string>();
            var updateClauses = new List<string>();

            foreach (var kvp in cells)
            {
                var sourceCol = kvp.Key;
                var targetCol = _columnMappings.GetValueOrDefault(sourceCol, sourceCol);
                columns.Add(targetCol);

                var cellValue = kvp.Value.ValueAsObject;
                var formattedValue = FormatDbValue(cellValue);
                values.Add(formattedValue);

                if (kvp.Value.IsPrimaryKey)
                {
                    primaryKeys.Add(targetCol);
                }
                else
                {
                    updateClauses.Add($"{targetCol} = EXCLUDED.{targetCol}");
                }
            }

            switch (_language)
            {
                case EDbLanguage.Sql:
                    // Postgres-style Upsert (MERGE ON CONFLICT)
                    sb.Append("INSERT INTO ")
                      .Append(tableName)
                      .Append(" (")
                      .Append(string.Join(", ", columns))
                      .Append(") VALUES (")
                      .Append(string.Join(", ", values))
                      .Append(")");

                    if (primaryKeys.Count > 0 && updateClauses.Count > 0)
                    {
                        sb.Append(" ON CONFLICT (")
                          .Append(string.Join(", ", primaryKeys))
                          .Append(") DO UPDATE SET ")
                          .Append(string.Join(", ", updateClauses));
                    }
                    else if (primaryKeys.Count > 0 && updateClauses.Count == 0)
                    {
                        sb.Append(" ON CONFLICT (")
                          .Append(string.Join(", ", primaryKeys))
                          .Append(") DO NOTHING");
                    }
                    sb.Append(";\n");
                    break;

                case EDbLanguage.Cql:
                    // CQL Insert is automatically an Upsert
                    sb.Append("INSERT INTO ")
                      .Append(tableName)
                      .Append(" (")
                      .Append(string.Join(", ", columns))
                      .Append(") VALUES (")
                      .Append(string.Join(", ", values))
                      .Append(");\n");
                    break;

                case EDbLanguage.Json:
                case EDbLanguage.Bson:
                case EDbLanguage.Unknown:
                default:
                    throw new NotSupportedException($"Language {_language} cannot be merged yet for MERGE.");
            }
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
