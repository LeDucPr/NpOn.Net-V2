namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public static class GeneralQuerySyncBroadcastConstant
{
    public const string GeneralQuerySyncChannel = "NpOn.GeneralService.QuerySync";
}

public sealed class GeneralQuerySyncBroadcastMessage
{
    public string[]? Codes { get; set; }
    public string DeployVersion { get; set; } = string.Empty;
    public bool IsFullReload { get; set; }
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GeneralQuerySyncBroadcastTrigger : BaseRedisBaseBroadcastTrigger<GeneralQuerySyncBroadcastMessage>
{
    public override string Channel => GeneralQuerySyncBroadcastConstant.GeneralQuerySyncChannel;
}
