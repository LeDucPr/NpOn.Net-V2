using System.Data;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.CommonDb.DbTransactions;

public interface INpOnDbTransaction : IDisposable
{
    void AddCommands(IEnumerable<INpOnDbCommand> commands);
    void RemoveCommands(IEnumerable<INpOnDbCommand> commands);
    void Commit();
    void Rollback();
}

public class NpOnDbTransaction : INpOnDbTransaction
{
    private readonly IDbTransaction _transaction;
    private List<INpOnDbCommand>? _commands;
    // private List<Action<>>
    private bool _isStartedTransaction; // == false

    public NpOnDbTransaction(IDbTransaction transaction)
        => _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));

    public void AddCommands(IEnumerable<INpOnDbCommand> commands)
    {
        if (_isStartedTransaction)
            throw new InvalidOperationException("Cannot add commands to a transaction that has already started.");
        _commands ??= [];
        _commands.AddRange(commands);
    }

    public void RemoveCommands(IEnumerable<INpOnDbCommand> commands)
    {
        if (_isStartedTransaction)
            throw new InvalidOperationException("Cannot remove commands to a transaction that has already started.");
        if (_commands is not { Count: > 0 })
            return;
        commands.ToList().ForEach(c => _commands.Remove(c));
    }

    public void Commit()
    {
        if (_isStartedTransaction)
            throw new InvalidOperationException("The transaction has already been committed or rolled back.");
        _transaction.Commit();
        _isStartedTransaction = true;
    }

    public void Rollback()
    {
        if (_isStartedTransaction)
            throw new InvalidOperationException("The transaction has already been committed or rolled back.");
        _transaction.Rollback();
        _isStartedTransaction = true;
    }

    public void Dispose()
    {
        _isStartedTransaction = false;
        _transaction.Dispose();
    }
}