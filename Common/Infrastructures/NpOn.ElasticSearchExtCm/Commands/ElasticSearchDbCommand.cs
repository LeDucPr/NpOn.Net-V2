using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Commands;

public class ElasticSearchDbCommand : NpOnDbCommand
{
    public EElasticSearchCommand CommandType { get; }
    public string IndexName { get; }
    
    // Single operations
    public string? Id { get; }
    public object? Document { get; }
    
    // Batch operations
    public string[]? Ids { get; }
    public IEnumerable<object>? Documents { get; }
    
    // Search
    public object? Query { get; } // A query object or string

    public ElasticSearchDbCommand(EElasticSearchCommand command, string indexName, string? id = null, object? document = null, object? query = null) 
        : base(EDb.ElasticSearch, $"{command} {indexName}")
    {
        CommandType = command;
        IndexName = indexName;
        Id = id;
        Document = document;
        Query = query;
    }

    public ElasticSearchDbCommand(EElasticSearchCommand command, string indexName, string[]? ids) 
        : base(EDb.ElasticSearch, $"{command} {indexName} {(ids != null ? string.Join(",", ids) : string.Empty)}")
    {
        CommandType = command;
        IndexName = indexName;
        Ids = ids;
    }

    public ElasticSearchDbCommand(EElasticSearchCommand command, string indexName, IEnumerable<object> documents) 
        : base(EDb.ElasticSearch, $"{command} {indexName} batch")
    {
        CommandType = command;
        IndexName = indexName;
        Documents = documents;
    }
}