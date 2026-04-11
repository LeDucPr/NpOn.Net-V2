using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Commands;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Results;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Connections;

public class ElasticSearchDriver : NpOnDbDriver
{
    private ElasticsearchClient? _client;
    public override string Name { get; set; } = "ElasticSearch";
    public override string Version { get; set; } = "Unknown";

    // Valid if we instantiated client since Elastic pooling is internal
    public override bool IsValidSession => _client != null;

    public ElasticSearchDriver(INpOnConnectOption option) : base(option)
    {
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession) return;

        var connectionString = Option.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        // Split multiple endpoints if comma/semicolon separated
        var uris = connectionString
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => new Uri(u))
            .ToArray();

        ElasticsearchClientSettings settings;
        if (uris.Length > 1)
        {
            var pool = new StaticNodePool(uris);
            settings = new ElasticsearchClientSettings(pool);
        }
        else
            settings = new ElasticsearchClientSettings(uris[0]);

        // Apply credentials if any from Option or connection string logic here.
        // We assume standard setup for now.
        _client = new ElasticsearchClient(settings);

        try
        {
            var ping = await _client.PingAsync(cancellationToken);
            if (ping.IsValidResponse)
            {
                Name = "ElasticSearch Pool";
            }
        }
        catch
        {
            _client = null;
        }
    }

    public override Task DisconnectAsync()
    {
        _client = null; // ElasticsearchClient internally manages connection pool. Dropping reference.
        return Task.CompletedTask;
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _client == null)
        {
            return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(false)).SetFail(EDbError.Connection);
        }

        if (command is not ElasticSearchDbCommand esCommand)
        {
            return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(false)).SetFail(
                EDbError.CommandNotSupported);
        }

        try
        {
            switch (esCommand.CommandType)
            {
                case EElasticSearchCommand.Index:
                    if (esCommand.Document == null)
                        throw new ArgumentNullException(nameof(esCommand.Document));
                    var indexRes = await _client.IndexAsync(esCommand.Document,
                        idx => idx.Index(esCommand.IndexName).Id(esCommand.Id));
                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(indexRes.IsValidResponse,
                        NetJsonMode.ToJson(indexRes.Id), null));

                case EElasticSearchCommand.Get:
                    if (string.IsNullOrWhiteSpace(esCommand.Id))
                        throw new ArgumentNullException(nameof(esCommand.Id));
                    // use raw output for getting json string directly back via dynamic dictionary representation or raw source
                    var getRes = await _client.GetAsync<object>(esCommand.Id, idx => idx.Index(esCommand.IndexName));
                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(getRes.IsValidResponse,
                        NetJsonMode.ToJson(getRes.Source), getRes.Source));

                case EElasticSearchCommand.Delete:
                    if (string.IsNullOrWhiteSpace(esCommand.Id))
                        throw new ArgumentNullException(nameof(esCommand.Id));
                    var delRes = await _client.DeleteAsync(esCommand.IndexName, esCommand.Id);
                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(delRes.IsValidResponse));

                case EElasticSearchCommand.Search:
                    // Using raw query or SearchRequest. Passing generic object allows flexible queries.
                    // For truly dynamic user search, they either pass SearchRequest or a string query
                    if (esCommand.Query is Action<SearchRequestDescriptor<object>> queryAction)
                    {
                        var searchObjRes = await _client.SearchAsync(esCommand.IndexName, queryAction);
                        return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(
                            searchObjRes.IsValidResponse, NetJsonMode.ToJson(searchObjRes.Documents),
                            searchObjRes.Documents));
                    }

                    if (esCommand.Query is string stringQuery)
                    {
                        var searchStrRes = await _client.SearchAsync<object>(s =>
                            s.Index(esCommand.IndexName).Query(q => q.QueryString(qs => qs.Query(stringQuery))));
                        return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(
                            searchStrRes.IsValidResponse, NetJsonMode.ToJson(searchStrRes.Documents),
                            searchStrRes.Documents));
                    }

                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(false)).SetFail(
                        "Invalid Search Query type");

                case EElasticSearchCommand.IndexMany:
                    if (esCommand.Documents == null) throw new ArgumentNullException(nameof(esCommand.Documents));
                    var bulkRes =
                        await _client.BulkAsync(b => b.Index(esCommand.IndexName).IndexMany(esCommand.Documents));
                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(bulkRes.IsValidResponse));

                case EElasticSearchCommand.GetMany:
                    if (esCommand.Ids == null || esCommand.Ids.Length == 0)
                        throw new ArgumentNullException(nameof(esCommand.Ids));
                    var getManyRes = await _client.SearchAsync<object>(s => s
                        .Index(esCommand.IndexName)
                        .Query(q => q.Ids(i => i.Values(esCommand.Ids))));
                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(getManyRes.IsValidResponse,
                        NetJsonMode.ToJson(getManyRes.Documents), getManyRes.Documents));

                default:
                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(false)).SetFail(
                        EDbError.CommandNotSupported);
            }
        }
        catch (Exception ex)
        {
            return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(false)).SetFail(ex);
        }
    }
}