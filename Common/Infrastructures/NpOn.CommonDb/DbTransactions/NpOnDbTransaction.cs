using System.Data.Common;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.CommonDb.DbTransactions;

public interface INpOnDbTransaction : IAsyncDisposable, IDisposable
{
    DbTransaction DbTransaction { get; }
    IEnumerable<IBaseNpOnDbCommand>? Commands { get; }
    void AddCommands(IEnumerable<IBaseNpOnDbCommand> commands);
    void RemoveCommands(IEnumerable<IBaseNpOnDbCommand> commands);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public class NpOnDbTransaction : INpOnDbTransaction
{
    private readonly DbTransaction _transaction;
    private bool _isCommittedOrRolledBack;
    private List<IBaseNpOnDbCommand>? _commands;
    
    public NpOnDbTransaction(DbTransaction transaction)
        => _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));

    public DbTransaction DbTransaction => _transaction;
    public IEnumerable<IBaseNpOnDbCommand>? Commands => _commands;
    
    public void AddCommands(IEnumerable<IBaseNpOnDbCommand> commands)
    {
        _commands ??= [];
        _commands.AddRange(commands);
    }

    public void RemoveCommands(IEnumerable<IBaseNpOnDbCommand>? commands)
    {
        if (commands == null)
            return;
        _commands?.RemoveAll(commands.Contains);
    }


    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_isCommittedOrRolledBack)
            throw new InvalidOperationException("The transaction has already been committed or rolled back.");
        await _transaction.CommitAsync(cancellationToken);
        _isCommittedOrRolledBack = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isCommittedOrRolledBack)
            throw new InvalidOperationException("The transaction has already been committed or rolled back.");
        await _transaction.RollbackAsync(cancellationToken);
        _isCommittedOrRolledBack = true;
    }

    public void Dispose()
    {
        _transaction.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}