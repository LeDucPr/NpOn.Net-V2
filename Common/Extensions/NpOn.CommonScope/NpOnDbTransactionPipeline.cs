using Common.Extensions.NpOn.CommonScope.Interfaces;
using Common.Infrastructures.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonScope;

public class NpOnDbTransactionPipeline : INpOnDbTransactionPipeline, IAsyncDisposable
{
    private readonly List<IBaseNpOnDbCommand> _commands = new();
    public static readonly NpOnDbTransactionPipeline Empty = new NpOnDbTransactionPipeline();

    public IReadOnlyList<IBaseNpOnDbCommand> Commands => _commands;

    public void Register(IBaseNpOnDbCommand command)
    {
        if (this == Empty) // Do not register to the Empty scope
            return;
        _commands.Add(command);
    }

    public async ValueTask DisposeAsync()
    {
        // clean up ADO.NET resources if necessary
        _commands.Clear();
        await Task.CompletedTask;
    }

    public void Begin()
    {
        throw new NotImplementedException();
    }

    public void Break()
    {
        throw new NotImplementedException();
    }

    public void SetFail()
    {
        throw new NotImplementedException();
    }

    public void SetFail(string exMess)
    {
        throw new NotImplementedException();
    }

    public void SetFail(Exception ex)
    {
        throw new NotImplementedException();
    }

    public void SetSuccess()
    {
        throw new NotImplementedException();
    }

    public bool IsCompleted { get; set; }
}


public static class NpOnTransactionScopeExtensions
{
    public static void AddRegister(this NpOnDbTransactionPipeline scope, IBaseNpOnDbCommand command) 
        => scope.Register(command);
}