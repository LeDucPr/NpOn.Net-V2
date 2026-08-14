using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Extensions.NpOn.ICommonDb.Connections;

public interface INpOnDbDriver
{
    string Name { get; }
    string Version { get; }
    public bool IsValidSession { get; }
    public INpOnConnectOption Option { get; }
    bool IsBusy { get; set; }
    void ResetSessionTimeout();
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
    Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command);

    Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands,
        CancellationToken cancellationToken = default);

    Task<bool> IsAliveAsync(CancellationToken cancellationToken = default);
}