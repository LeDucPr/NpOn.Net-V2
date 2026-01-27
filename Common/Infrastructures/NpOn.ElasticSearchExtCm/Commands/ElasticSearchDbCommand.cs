using Common.Extensions.NpOn.CommonEnums;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Commands;

public enum EElasticSearchCommand
{
    Index,
    Get,
    Search,
    Delete,
    Update
}

public class ElasticSearchDbCommand : NpOnDbCommand
{
    private readonly EDb _dbType = EDb.ElasticSearch;
    public EElasticSearchCommand CommandType { get; }
    public string IndexName { get; }
    public string? Id { get; }
    public object? Document { get; }
    
    // For search queries, we might pass a query object or string
    public object? Query { get; }

    public ElasticSearchDbCommand(EElasticSearchCommand command, string indexName, string? id = null, object? document = null, object? query = null) :
        base(EDb.ElasticSearch, $"{command} {indexName} {id}")
    {
        CommandType = command;
        IndexName = indexName;
        Id = id;
        Document = document;
        Query = query;
    }
}