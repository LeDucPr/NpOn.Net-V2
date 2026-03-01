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

    public override async Task<INpOnBaseTransactionPipeline> Begin()
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
        return this;
    }

    public async ValueTask DisposeAsync()
    {
        // clean up ADO.NET resources if necessary
        _commands.Clear();
        await Task.CompletedTask;
    }
}

public static class NpOnTransactionPipelineExtensions
{
    public static void AddRegister(this NpOnDbTransactionPipeline scope, IBaseNpOnDbCommand command)
        => scope.Register(command);
}