using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Results;

namespace Common.Infrastructures.DbFactories.NpOn.ElasticSearchFactory;

public interface IElasticSearchFactoryWrapper : IDbFactoryWrapper
{
    Task<ElasticSearchValueWrapper?> GetAsync(string indexName, string id);
    Task<ElasticSearchValueWrapper?> IndexAsync(string indexName, string? id, object document);
    Task<ElasticSearchValueWrapper?> DeleteAsync(string indexName, string id);
    Task<ElasticSearchValueWrapper?> SearchAsync(string indexName, object query);
    
    Task<ElasticSearchValueWrapper?> GetManyAsync(string indexName, params string[] ids);
    Task<ElasticSearchValueWrapper?> IndexManyAsync(string indexName, IEnumerable<object> documents);
}
