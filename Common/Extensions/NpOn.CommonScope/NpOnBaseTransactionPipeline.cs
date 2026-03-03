using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonScope.Interfaces;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonScope;

public abstract class NpOnBaseTransactionPipeline : INpOnBaseTransactionPipeline
{
    protected bool _invoked; // = false;
    protected bool _isCompleted;
    protected string? _errorMessage;
    protected INpOnBaseTransactionPipeline? _next;
    
    public void Next(INpOnBaseTransactionPipeline nextTransactionPipeline)
    {
        _next = nextTransactionPipeline;
    }

    protected virtual void BaseRefresh()
    {
        _invoked = false;
        _isCompleted = false;
    }

    public abstract Task<INpOnBaseTransactionPipeline> Invoke();

    public async Task<INpOnBaseTransactionPipeline> Next(INpOnBaseTransactionPipeline? nextTransactionPipeline,
        bool isNext = false)
    {
        _next = nextTransactionPipeline;
        if (isNext)
            await Invoke();
        return nextTransactionPipeline ?? this;
    }

    public bool IsCompleted => _isCompleted | !_invoked; // Tránh khi chưa invoke gọi gây lỗi 
    public string? ErrorMessage => _errorMessage;

    protected async Task TransactionPipelineWrapper(IDbFactoryWrapper? dbFactoryWrapper,
        IEnumerable<IBaseNpOnDbCommand>? commands,
        Func<IDbFactoryWrapper?, IEnumerable<IBaseNpOnDbCommand>?, Task> transactionProcess
    )
    {
        _invoked = true;
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

    protected async Task GeneralPipelineWrapper(Func<Task<bool>>? processFunc)
    {
        _invoked = true;
        _isCompleted = false;

        try
        {
            if (processFunc == null)
            {
                _isCompleted = true;
                return;
            }

            var ok = await processFunc();
            _isCompleted = ok;

            if (!ok)
            {
                _errorMessage = "Pipeline step returned false";
                // return;
            }
        }
        catch (Exception ex)
        {
            _isCompleted = false;
            _errorMessage = $"Pipeline step failed: {ex.Message}";
        }
    }

}