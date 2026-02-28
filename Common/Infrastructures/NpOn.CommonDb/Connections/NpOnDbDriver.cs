using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.ICommonDb.DbCommands;
using Common.Infrastructures.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.ICommonDb.Transactions;

namespace Common.Infrastructures.NpOn.CommonDb.Connections;

public abstract class NpOnDbDriver : INpOnDbDriver, IAsyncDisposable
{
    private bool _disposed = false;
    public abstract string Name { get; set; }
    public abstract string Version { get; set; }
    public abstract bool IsValidSession { get; }
    public virtual INpOnConnectOption Option { get; }
    public abstract Task ConnectAsync(CancellationToken cancellationToken);
    public abstract Task DisconnectAsync();

    public virtual Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        throw new NotImplementedException("Need to override this method");
    }

    public virtual Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Need to override this method");
    }

    protected NpOnDbDriver(INpOnConnectOption option)
    {
        if (!option.IsValid() && !option.IsValidRequireFromBase(EConnectLink.SelfValidateConnection.GetDisplayName()))
            return;
        Option = option;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        await DisconnectAsync();
        _disposed = true;
    }

    public virtual Task<bool> IsAliveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(IsValidSession && !_disposed);


    protected virtual Task<INpOnDbTransaction> CreateTransaction(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Need to override this method");
    }

    protected async Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> TransactionWrapper(
        Func<INpOnDbTransaction, Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>>> transactionProcess,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await CreateTransaction(cancellationToken);
        try
        {
            var results = await transactionProcess(transaction);
            if (results.Any(result => !result.Value.Status))
                await transaction.RollbackAsync(cancellationToken);
            else 
                await transaction.CommitAsync(cancellationToken);
            return results;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}