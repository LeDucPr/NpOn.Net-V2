using Common.Infrastructures.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonScope;

public class NpOnTransactionScope : IAsyncDisposable
{
    private readonly List<IBaseNpOnDbCommand> _commands = new();
    public static readonly NpOnTransactionScope Empty = new NpOnTransactionScope();

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
}

public static class NpOnTransactionScopeExtensions
{
    public static void AddRegister(this NpOnTransactionScope scope, IBaseNpOnDbCommand command) 
        => scope.Register(command);
}