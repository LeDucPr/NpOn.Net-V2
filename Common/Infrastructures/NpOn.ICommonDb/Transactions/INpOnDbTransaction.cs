using System.Data.Common;
using Common.Infrastructures.NpOn.ICommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.ICommonDb.Transactions;

public interface INpOnDbTransaction : IAsyncDisposable, IDisposable
{
    DbTransaction DbTransaction { get; }
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}