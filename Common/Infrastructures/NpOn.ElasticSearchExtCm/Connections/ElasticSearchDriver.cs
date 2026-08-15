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
                    // var indexRes = await _client.IndexAsync(esCommand.Document,
                    //     idx => idx.Index(esCommand.IndexName).Id(esCommand.Id));
                    var indexRes = await _client.IndexAsync(esCommand.Document,
                        idx =>
                        {
                            if (esCommand.Id != null) idx.Index(esCommand.IndexName).Id(esCommand.Id);
                        });
                    var indexContainer = new ElasticSearchValueContainer(indexRes.IsValidResponse,
                        NetJsonMode.ToJson(indexRes.Id) /*, null*/);
                    MapMetadata(indexContainer, indexRes);
                    return new ElasticSearchValueWrapper(indexContainer);

                case EElasticSearchCommand.Get:
                    if (string.IsNullOrWhiteSpace(esCommand.Id))
                        throw new ArgumentNullException(nameof(esCommand.Id));
                    // use raw output for getting JSON string directly back via dynamic dictionary representation or raw source
                    var getRes = await _client.GetAsync<object>(esCommand.Id, idx => idx.Index(esCommand.IndexName));
                    var getContainer = new ElasticSearchValueContainer(getRes.IsValidResponse,
                        NetJsonMode.ToJson(getRes.Source), getRes.Source);
                    MapMetadata(getContainer, getRes);
                    return new ElasticSearchValueWrapper(getContainer);

                case EElasticSearchCommand.Delete:
                    if (string.IsNullOrWhiteSpace(esCommand.Id))
                        throw new ArgumentNullException(nameof(esCommand.Id));
                    var delRes = await _client.DeleteAsync(esCommand.IndexName, esCommand.Id);
                    var delContainer = new ElasticSearchValueContainer(delRes.IsValidResponse);
                    MapMetadata(delContainer, delRes);
                    return new ElasticSearchValueWrapper(delContainer);

                case EElasticSearchCommand.Search:
                    // Using raw query or SearchRequest. Passing generic object allows flexible queries.
                    // For truly dynamic user search, they either pass SearchRequest or a string query
                    if (esCommand.Query is Action<SearchRequestDescriptor<object>> queryAction)
                    {
                        var searchObjRes = await _client.SearchAsync(esCommand.IndexName, queryAction);
                        var searchObjContainer = new ElasticSearchValueContainer(searchObjRes.IsValidResponse,
                            NetJsonMode.ToJson(searchObjRes.Documents), searchObjRes.Documents);
                        MapMetadata(searchObjContainer, searchObjRes);
                        return new ElasticSearchValueWrapper(searchObjContainer);
                    }

                    if (esCommand.Query is string stringQuery)
                    {
                        var searchStrRes = await _client.SearchAsync<object>(s =>
                            s.Indices(esCommand.IndexName).Query(q => q.QueryString(qs => qs.Query(stringQuery))));
                        var searchStrContainer = new ElasticSearchValueContainer(searchStrRes.IsValidResponse,
                            NetJsonMode.ToJson(searchStrRes.Documents), searchStrRes.Documents);
                        MapMetadata(searchStrContainer, searchStrRes);
                        return new ElasticSearchValueWrapper(searchStrContainer);
                    }

                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(false)).SetFail(
                        "Invalid Search Query type");

                case EElasticSearchCommand.IndexMany:
                    if (esCommand.Documents == null) throw new ArgumentNullException(nameof(esCommand.Documents));
                    var bulkRes =
                        await _client.BulkAsync(b => b.Index(esCommand.IndexName).IndexMany(esCommand.Documents));
                    var bulkContainer = new ElasticSearchValueContainer(bulkRes.IsValidResponse);
                    MapMetadata(bulkContainer, bulkRes);
                    return new ElasticSearchValueWrapper(bulkContainer);

                case EElasticSearchCommand.GetMany:
                    if (esCommand.Ids == null || esCommand.Ids.Length == 0)
                        throw new ArgumentNullException(nameof(esCommand.Ids));
                    var getManyRes = await _client.SearchAsync<object>(s => s
                        .Indices(esCommand.IndexName)
                        .Query(q => q.Ids(i => i.Values(esCommand.Ids))));
                    var getManyContainer = new ElasticSearchValueContainer(getManyRes.IsValidResponse,
                        NetJsonMode.ToJson(getManyRes.Documents), getManyRes.Documents);
                    MapMetadata(getManyContainer, getManyRes);
                    return new ElasticSearchValueWrapper(getManyContainer);

                default:
                    return new ElasticSearchValueWrapper(new ElasticSearchValueContainer(false)).SetFail(
                        EDbError.CommandNotSupported);
            }
        }
        catch (Exception ex)when
            (ex is
                 TransportException or // Base transport error
                 UnexpectedTransportException or // Lỗi bất ngờ trong transport layer
                 PipelineException or // Pipeline gãy (selector không tìm được node)
                 // ── HTTP / TCP / IO tầng thấp ──
                 HttpRequestException or // HTTP fail (TLS, proxy, DNS)
                 System.Net.Sockets.SocketException or // TCP reset, connection refused
                 IOException or // Stream gãy giữa chừng
                 TaskCanceledException or // HttpClient timeout
                 TimeoutException or
                 OperationCanceledException) // CancellationToken triggered
        {
            throw;
            // return new ElasticSearchValueWrapper(
            //     new ElasticSearchValueContainer(false)).SetFail(ex);
        }
        catch (Exception ex)
        {
            throw new ObjectDisposedException("");
            // return new ElasticSearchValueWrapper(
            //     new ElasticSearchValueContainer(false)).SetFail(ex);
        }
        finally
        {
            ResetSessionTimeout();
        }
    }

    private static void MapMetadata(ElasticSearchValueContainer container, object? elasticResponse)
    {
        if (elasticResponse == null) return;

        try
        {
            var type = elasticResponse.GetType();

            var tookProp = type.GetProperty(nameof(ElasticSearchValueContainer.Took));
            if (tookProp != null)
            {
                var tookVal = tookProp.GetValue(elasticResponse);
                if (tookVal is long tookL) container.Took = tookL;
            }

            var shardsProp = type.GetProperty(nameof(ElasticSearchValueContainer.Shards));
            if (shardsProp == null)
                return;
            var sourceShards = shardsProp.GetValue(elasticResponse);
            if (sourceShards == null)
                return;
            var shardType = sourceShards.GetType();
            var total = (int?)shardType.GetProperty(nameof(ElasticSearchValueShardStatistics.Total))
                ?.GetValue(sourceShards) ?? 0;
            var successful = (int?)shardType.GetProperty(nameof(ElasticSearchValueShardStatistics.Successful))
                ?.GetValue(sourceShards) ?? 0;
            var failed = (int?)shardType.GetProperty(nameof(ElasticSearchValueShardStatistics.Failed))
                ?.GetValue(sourceShards) ?? 0;
            var skipped = (int?)shardType.GetProperty(nameof(ElasticSearchValueShardStatistics.Skipped))
                ?.GetValue(sourceShards) ?? 0;

            container.Shards = new ElasticSearchValueShardStatistics
            {
                Total = total,
                Successful = successful,
                Failed = failed,
                Skipped = skipped
            };
        }
        catch
        {
            // ignored
        }
    }
}