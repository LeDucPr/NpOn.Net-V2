namespace Common.Extensions.NpOn.ICommonDb.Connections;

public interface INpOnConnectOption
{
    bool IsConnectValid(); // validate when initialize 
    bool IsValid([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null);
    bool IsValidRequireFromBase([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null);
    string Code { get; } // as id with connection

    INpOnConnectOption SetConnectionString(string connectionString);
    string? ConnectionString { get; } // Almost database connection use

    INpOnConnectOption? SetKeyspace(string keyspace); // where T : INpOnDbDriver;
    string? Keyspace { get; } // cassandra, scyllaDb

    INpOnConnectOption? SetPort<T>(int port) where T : INpOnDbDriver;
    int? Port { get; } // Almost database connection use

    INpOnConnectOption? SetDatabaseName(string databaseName);
    string? DatabaseName { get; }

    INpOnConnectOption? SetCollectionName /*<T>*/(string keyspace); /*where T : INpOnDbDriver;*/
    string? CollectionName { get; } // MongoDb use as Table

    INpOnConnectOption SetContactAddresses /*<T>*/(string[]? contactAddresses); /* where T : INpOnDbDriver;*/
    string[]? ContactAddresses { get; } // cassandra, scyllaDb 

    INpOnConnectOption SetDatabaseIndex(int databaseIndex);
    int? DatabaseIndex { get; } // redis, dragonFly

    #region generic

    INpOnConnectOption SetShutdownImmediate(bool isShutdownImmediate);
    bool IsShutdownImmediate { get; }

    INpOnConnectOption SetWaitNextTransaction(bool isWaitNextTransaction);
    bool IsWaitNextTransaction { get; }

    INpOnConnectOption SetSessionTimeout(long secondsTimeout);
    void ResetSessionTimeout();
    bool IsExpired { get; }
    long ConnectionTimeoutSessions { get; }

    #endregion generic
}