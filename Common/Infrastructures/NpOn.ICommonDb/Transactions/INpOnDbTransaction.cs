using System.Data.Common;
using Common.Infrastructures.NpOn.ICommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.ICommonDb.Transactions;

public interface INpOnDbTransaction : IAsyncDisposable, IDisposable
{
    DbTransaction DbTransaction { get; }
    IEnumerable<IBaseNpOnDbCommand>? Commands { get; }
    void AddCommands(IEnumerable<IBaseNpOnDbCommand> commands);
    void RemoveCommands(IEnumerable<IBaseNpOnDbCommand> commands);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}