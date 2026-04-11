using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Commands;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Connections;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Results;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.DbFactories.NpOn.ElasticSearchFactory;

public class ElasticSearchFactoryWrapper : DbFactoryWrapper<ElasticSearchDriver>, IElasticSearchFactoryWrapper
{
    public ElasticSearchFactoryWrapper(IDbDriverFactory dbDriverFactory, ILogger<ElasticSearchFactoryWrapper> logger) : base(dbDriverFactory, logger)
    {
    }

    private async Task<ElasticSearchValueWrapper?> ExecuteCommandAsync(ElasticSearchDbCommand command)
    {
        if (DbDriverFactory == null || DbDriverFactory.FirstValidConnection == null)
            return null;

        var result = await DbDriverFactory.ExecuteAsync(command);
        return result as ElasticSearchValueWrapper;
    }

    public Task<ElasticSearchValueWrapper?> GetAsync(string indexName, string id)
    {
        return ExecuteCommandAsync(new ElasticSearchDbCommand(EElasticSearchCommand.Get, indexName, id));
    }

    public Task<ElasticSearchValueWrapper?> IndexAsync(string indexName, string? id, object document)
    {
        return ExecuteCommandAsync(new ElasticSearchDbCommand(EElasticSearchCommand.Index, indexName, id, document));
    }

    public Task<ElasticSearchValueWrapper?> DeleteAsync(string indexName, string id)
    {
        return ExecuteCommandAsync(new ElasticSearchDbCommand(EElasticSearchCommand.Delete, indexName, id));
    }

    public Task<ElasticSearchValueWrapper?> SearchAsync(string indexName, object query)
    {
        return ExecuteCommandAsync(new ElasticSearchDbCommand(EElasticSearchCommand.Search, indexName, null, null, query));
    }

    public Task<ElasticSearchValueWrapper?> GetManyAsync(string indexName, params string[] ids)
    {
        return ExecuteCommandAsync(new ElasticSearchDbCommand(EElasticSearchCommand.GetMany, indexName, ids));
    }

    public Task<ElasticSearchValueWrapper?> IndexManyAsync(string indexName, IEnumerable<object> documents)
    {
        return ExecuteCommandAsync(new ElasticSearchDbCommand(EElasticSearchCommand.IndexMany, indexName, documents));
    }
}
