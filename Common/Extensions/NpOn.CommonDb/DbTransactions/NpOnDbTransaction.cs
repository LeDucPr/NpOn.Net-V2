using System.Data.Common;
using Common.Extensions.NpOn.ICommonDb.Transactions;

namespace Common.Extensions.NpOn.CommonDb.DbTransactions;

public class NpOnDbTransaction : INpOnDbTransaction
{
    private readonly DbTransaction _transaction;
    private bool _isCommittedOrRolledBack;
    
    public NpOnDbTransaction(DbTransaction transaction)
        => _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));

    public DbTransaction DbTransaction => _transaction;

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