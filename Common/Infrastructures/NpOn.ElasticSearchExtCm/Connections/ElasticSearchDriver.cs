using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Commands;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Results;
using Elastic.Clients.Elasticsearch;

namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Connections;

public class ElasticSearchDriver : NpOnDbDriver
{
    private ElasticsearchClient? _client;
    public override string Name { get; set; } = "ElasticSearch";
    public override string Version { get; set; } = "Unknown";

    public override bool IsValidSession => _client != null; // Basic check, can be improved with ping

    public ElasticSearchDriver(INpOnConnectOption option) : base(option)
    {
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession)
        {
            return;
        }

        await DisconnectAsync();
        if (Option.ConnectionString != null)
        {
            // Assuming connection string is a URL for now. 
            // More complex config (auth, multiple nodes) might need parsing or a different option structure.
            var settings = new ElasticsearchClientSettings(new Uri(Option.ConnectionString));
            
            // Disable certificate validation for development if needed, or configure properly
            // settings.ServerCertificateValidationCallback(CertificateValidations.AllowAll);

            _client = new ElasticsearchClient(settings);
        }

        if (_client != null)
        {
            try 
            {
                var info = await _client.InfoAsync(cancellationToken);
                if (info.IsValidResponse)
                {
                    Version = info.Version.Number;
                    Name = $"ElasticSearch on {Option.ConnectionString}";
                }
            }
            catch
            {
                // Connection failed
                _client = null;
            }
        }
    }

    public override async Task DisconnectAsync()
    {
        // ElasticsearchClient doesn't have a persistent connection to close in the same way as SQL drivers,
        // but we can nullify the client.
        _client = null;
        await Task.CompletedTask;
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _client == null)
        {
            return new ElasticSearchWrapperResult(new ElasticSearchContainer(null)).SetFail(EDbError.Connection);
        }

        if (command is not ElasticSearchDbCommand esCommand)
        {
            return new ElasticSearchWrapperResult(new ElasticSearchContainer(null))
                .SetFail(EDbError.CommandNotSupported);
        }

        try
        {
            switch (esCommand.CommandType)
            {
                case EElasticSearchCommand.Index:
                    if (esCommand.Document == null) return new ElasticSearchWrapperResult(new ElasticSearchContainer(null)).SetFail(EDbError.CommandParam);
                    var indexResponse = await _client.IndexAsync(esCommand.Document, esCommand.IndexName);
                    return new ElasticSearchWrapperResult(new ElasticSearchContainer(indexResponse.Id));

                case EElasticSearchCommand.Get:
                    if (esCommand.Id == null) return new ElasticSearchWrapperResult(new ElasticSearchContainer(null)).SetFail(EDbError.CommandParam);
                    // We need a type for GetAsync usually. For generic wrapper, we might need to fetch as generic object or JsonNode
                    // This part is tricky without a specific T. 
                    // For now, let's assume we are fetching a dynamic/object if possible or return the raw response wrapper
                    // Elastic.Clients.Elasticsearch is strongly typed.
                    // We might need to use a specific method or helper to get raw JSON or a generic dictionary.
                    // For simplicity in this structure, let's assume we return the ID or success status if we can't deserialize to T here.
                    // Ideally, the command should carry the Type info or we use a "dynamic" equivalent.
                    
                    // Workaround: Use object or a Dictionary<string, object>
                    var getResponse = await _client.GetAsync<object>(esCommand.IndexName, esCommand.Id);
                    return new ElasticSearchWrapperResult(new ElasticSearchContainer(getResponse.Source));

                case EElasticSearchCommand.Delete:
                    if (esCommand.Id == null) return new ElasticSearchWrapperResult(new ElasticSearchContainer(null)).SetFail(EDbError.CommandParam);
                    var deleteResponse = await _client.DeleteAsync(esCommand.IndexName, esCommand.Id);
                    return new ElasticSearchWrapperResult(new ElasticSearchContainer(deleteResponse.Result.ToString()));

                case EElasticSearchCommand.Search:
                    // Search is also strongly typed. 
                    // If Query is provided, we need to construct the search request.
                    // This is a placeholder for search implementation.
                    // Real implementation would depend on how 'Query' object is passed (e.g., raw JSON, or a SearchRequest object)
                    return new ElasticSearchWrapperResult(new ElasticSearchContainer(null)).SetFail(EDbError.CommandNotSupported); // Not fully implemented

                default:
                    return new ElasticSearchWrapperResult(new ElasticSearchContainer(null))
                        .SetFail(EDbError.CommandNotSupported);
            }
        }
        catch (Exception ex)
        {
            return new ElasticSearchWrapperResult(new ElasticSearchContainer(null)).SetFail(EDbError.ElasticSearchExecute);
        }
    }
}