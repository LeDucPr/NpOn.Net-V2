namespace NpOn.ITrackerStorageAdapter;

public interface ISystemLogStorageAdapter
{
    Task<bool> InitializeSystemLogsTableAsync();
}