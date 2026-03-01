using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonScope.Interfaces;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonScope;

public abstract class NpOnBaseTransactionPipeline : INpOnBaseTransactionPipeline
{
    protected bool _isCompleted;
    protected string? _errorMessage;

    public abstract Task<INpOnBaseTransactionPipeline> Begin();
    public bool IsCompleted => _isCompleted;
    public string? ErrorMessage => _errorMessage;

    protected async Task TransactionPipelineWrapper(IDbFactoryWrapper? dbFactoryWrapper,
        IEnumerable<IBaseNpOnDbCommand>? commands,
        Func<IDbFactoryWrapper?, IEnumerable<IBaseNpOnDbCommand>?, Task> transactionProcess
    )
    {
        _isCompleted = false;
        try
        {
            var commandList = commands?.ToList();
            if (dbFactoryWrapper == null)
            {
                _isCompleted = false;
                _errorMessage = "IDbFactoryWrapper is null";
                return;
            }

            if (commandList is not { Count: > 0 })
            {
                _isCompleted = false;
                _errorMessage = "Commands is null or empty";
                return;
            }

            await transactionProcess(dbFactoryWrapper, commandList);
            _isCompleted = true;
        }
        catch
        {
            // ignored
        }
    }
}