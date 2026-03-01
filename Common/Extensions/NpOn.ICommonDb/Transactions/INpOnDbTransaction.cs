using System.Data.Common;

namespace Common.Extensions.NpOn.ICommonDb.Transactions;

public interface INpOnDbTransaction : IAsyncDisposable, IDisposable
{
    DbTransaction DbTransaction { get; }
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}