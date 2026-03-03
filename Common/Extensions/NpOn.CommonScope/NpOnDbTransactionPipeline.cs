using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonScope.Interfaces;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonScope;

public class NpOnDbTransactionPipeline : NpOnBaseTransactionPipeline, INpOnDbTransactionPipeline, IAsyncDisposable
{
    // static 
    public static readonly NpOnDbTransactionPipeline Empty = new NpOnDbTransactionPipeline();

    private readonly List<IBaseNpOnDbCommand> _commands = new();
    private IDbFactoryWrapper? _dbFactoryWrapper;
    private bool _isRefreshAfterInvoke = true;

    public IReadOnlyList<IBaseNpOnDbCommand> Commands => _commands;

    public NpOnDbTransactionPipeline Register(IDbFactoryWrapper dbFactoryWrapper)
    {
        _dbFactoryWrapper = dbFactoryWrapper;
        return this;
    }

    public NpOnDbTransactionPipeline Register(IBaseNpOnDbCommand command)
    {
        if (this == Empty) // Do not register to the Empty scope
            return this;
        // else 
        _commands.Add(command);
        return this;
    }

    public NpOnDbTransactionPipeline SetRefreshAfterInvoke(bool isRefreshAfterInvoke = true)
    {
        _isRefreshAfterInvoke = isRefreshAfterInvoke;
        return this;
    }

    public override async Task<INpOnBaseTransactionPipeline> Invoke()
    {
        await TransactionPipelineWrapper(
            _dbFactoryWrapper,
            _commands,
            transactionProcess: async (_, _) =>
            {
                // with single transactions
                // checked null on Wrapper task
                await _dbFactoryWrapper!.ExecuteWithTransaction(_commands);
            });
        if (!_isRefreshAfterInvoke)
            return this;
        Refresh();
        return this;
    }

    private void Refresh()
    {
        base.BaseRefresh();
        _commands.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        // clean up ADO.NET resources if necessary
        _commands.Clear();
        await Task.CompletedTask;
    }
}