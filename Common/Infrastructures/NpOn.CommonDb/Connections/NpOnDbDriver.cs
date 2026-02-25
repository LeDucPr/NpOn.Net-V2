using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Common.Infrastructures.NpOn.CommonDb.DbTransactions;

namespace Common.Infrastructures.NpOn.CommonDb.Connections;

public interface INpOnDbDriver
{
    string Name { get; }
    string Version { get; }
    public bool IsValidSession { get; }
    public INpOnConnectOption Option { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
    Task<INpOnWrapperResult> Execute(INpOnDbCommand? command);

    Task<INpOnWrapperResult> ExecuteFunc(INpOnDbExecFuncCommand? execCommand);

    Task<bool> IsAliveAsync(CancellationToken cancellationToken = default);
    
    Task<INpOnDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public abstract class NpOnDbDriver : INpOnDbDriver, IAsyncDisposable
{
    private bool _disposed = false;
    public abstract string Name { get; set; }
    public abstract string Version { get; set; }
    public abstract bool IsValidSession { get; }
    public virtual INpOnConnectOption Option { get; }
    public abstract Task ConnectAsync(CancellationToken cancellationToken);
    public abstract Task DisconnectAsync();

    public virtual Task<INpOnWrapperResult> Execute(INpOnDbCommand? command)
    {
        throw new NotImplementedException("Need to override this method");
    }

    public virtual Task<INpOnWrapperResult> ExecuteFunc(INpOnDbExecFuncCommand? execCommand)
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

    public virtual Task<INpOnDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Need to override this method");
    }

    protected async Task<INpOnWrapperResult> TransactionWrapper(
        Func<INpOnDbTransaction, Task<INpOnWrapperResult>> transactionProcess,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await transactionProcess(transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}