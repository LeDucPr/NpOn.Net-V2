using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;

namespace MicroServices.Account.Service.NpOn.AccountService.RedisBroadcast;

public sealed class GeneralQuerySyncBroadcastHandler(
    IServiceScopeFactory scopeFactory,
    GeneralQuerySyncBroadcastTrigger trigger,
    ILogger<GeneralQuerySyncBroadcastHandler> logger
) : BaseRedisBroadcastHandler<GeneralQuerySyncBroadcastMessage>(trigger,
    async msg =>
    {
        if (msg is not RedisBroadcastMessage<GeneralQuerySyncBroadcastMessage> syncMsg ||
            syncMsg.Value == null)
        {
            return false;
        }

        using var scope = scopeFactory.CreateScope();
        var generalCommandClient = scope.ServiceProvider.GetRequiredService<IGeneralCommandClient>();
        int updated = await generalCommandClient.RefreshCodesAsync(
            syncMsg.Value.Codes,
            syncMsg.Value.IsFullReload);

        logger.LogInformation(
            "Synchronized L1 query cache for deploy '{DeployVersion}': refreshed {UpdatedCount} queries (fullReload={IsFullReload}).",
            syncMsg.Value.DeployVersion,
            updated,
            syncMsg.Value.IsFullReload);
        return true;
    });
