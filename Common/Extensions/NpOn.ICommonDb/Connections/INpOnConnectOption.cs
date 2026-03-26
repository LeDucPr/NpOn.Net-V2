namespace Common.Extensions.NpOn.ICommonDb.Connections;

public interface INpOnConnectOption
{
    bool IsConnectValid(); // validate when initialize 
    bool IsValid([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null);
    bool IsValidRequireFromBase([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null);
    string Code { get; }

    INpOnConnectOption SetConnectionString(string connectionString);
    string? ConnectionString { get; }

    INpOnConnectOption? SetKeyspace(string keyspace); // where T : INpOnDbDriver;
    string? Keyspace { get; }

    INpOnConnectOption? SetPort<T>(int port) where T : INpOnDbDriver;
    int? Port { get; }

    INpOnConnectOption? SetDatabaseName(string databaseName);
    string? DatabaseName { get; }

    INpOnConnectOption? SetCollectionName<T>(string keyspace) where T : INpOnDbDriver;
    string? CollectionName { get; }

    INpOnConnectOption SetContactAddresses /*<T>*/(string[]? contactAddresses); /* where T : INpOnDbDriver;*/
    string[]? ContactAddresses { get; }

    INpOnConnectOption SetShutdownImmediate(bool isShutdownImmediate);
    bool IsShutdownImmediate { get; }

    INpOnConnectOption SetWaitNextTransaction(bool isWaitNextTransaction);
    bool IsWaitNextTransaction { get; }

    INpOnConnectOption SetSessionTimeout(long secondsTimeout);
    void ResetSessionTimeout();
    long ConnectionTimeoutSessions { get; }
}