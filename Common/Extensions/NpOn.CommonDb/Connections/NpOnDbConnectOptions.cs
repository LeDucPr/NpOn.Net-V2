using System.Text;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Common.Extensions.NpOn.CommonDb.Connections;

public abstract class NpOnDbConnectOption<T> : INpOnConnectOption
{
    // private bool _isUseMultiSessions = false;
    private bool _isShutdownImmediate /*= false*/;
    private bool _isWaitNextTransaction = true;
    private long _secondsTimeout = 30; //2592000; // 30 days 
    private DateTime _currentConnectionTime;
    private DateTime _expiredConnectionTime;
    private string? _connectionString;

    protected readonly ILogger<NpOnDbConnectOption<T>> Logger =
        new Logger<NpOnDbConnectOption<T>>(new NullLoggerFactory());

    #region Validate

    public abstract bool IsConnectValid();

    public virtual bool IsValid(string? propertyName = null)
    {
        try
        {
            if (GetType() == typeof(INpOnConnectOption))
                throw new NotImplementedException("request Validator configuration from inherited class.");
            return true;
        }
        catch (NotImplementedException)
        {
            return false;
        }
    }

    // decoy validate
    public bool IsValidRequireFromBase(string? propertyName)
    {
        var validPropertyNames = new HashSet<string>
        {
            EConnectLink.SelfValidateConnection.GetDisplayName(),
        };
        if (propertyName == null)
            return false;
        return validPropertyNames.Contains(propertyName);
    }

    #endregion Validate


    #region ConnectionString

    public INpOnConnectOption SetConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        return this;
    }

    public string? ConnectionString => _connectionString; // Almost database connection use

    #endregion ConnectionString


    // for databases

    #region Port

    private int? _port; // Almost database connection use

    public INpOnConnectOption SetPort<T1>(int port) where T1 : INpOnDbDriver
    {
        try
        {
            if (!IsValid())
                throw new InvalidOperationException($"Port is not valid for {typeof(INpOnDbDriver)}");
            _port = port;
        }
        catch (InvalidOperationException)
        {
            _port = null;
        }

        return this;
    }

    public int? Port => _port;

    #endregion Port


    #region Keyspace

    private string? _keyspace = string.Empty; // cassandra, scyllaDb

    public virtual INpOnConnectOption SetKeyspace /*<T>*/(string keyspace) /* where T : INpOnDbDriver*/
    {
        try
        {
            if (!IsValid())
                throw new InvalidOperationException($"ConnectOptions is not valid for {typeof(INpOnDbDriver)}");
            _keyspace = keyspace;
        }
        catch (InvalidOperationException)
        {
            _keyspace = null;
        }

        return this;
    }

    public string? Keyspace => _keyspace;

    #endregion Keyspace


    #region Collection

    private string? _collection = string.Empty; // mongoDb

    public virtual INpOnConnectOption SetCollectionName /*<T>*/(string collection) /* where T : INpOnDbDriver*/
    {
        try
        {
            if (!IsValid())
                throw new InvalidOperationException($"ConnectOptions is not valid for {typeof(INpOnDbDriver)}");
            _collection = collection;
        }
        catch (InvalidOperationException)
        {
            _collection = null;
        }

        return this;
    }

    public string? CollectionName => _collection;

    #endregion Keyspace


    #region ContactAddresses

    private string[]? _contactAddresses;

    public virtual INpOnConnectOption
        SetContactAddresses /*<T>*/(string[]? contactAddresses) /* where T : INpOnDbDriver*/
    {
        try
        {
            if (!IsValid())
                throw new InvalidOperationException($"Keyspace is not valid for {typeof(INpOnDbDriver)}");
            if (contactAddresses is not { Length: > 0 })
            {
                _contactAddresses = contactAddresses;
                return this;
            }

            HashSet<string> contactAddressesHashSet = new HashSet<string>(_contactAddresses ?? []);
            foreach (string contactAddress in contactAddresses)
                contactAddressesHashSet.Add(contactAddress);
            _contactAddresses = contactAddressesHashSet.ToArray();
        }
        catch (InvalidOperationException)
        {
            _contactAddresses = null;
        }

        return this;
    }

    public string[]? ContactAddresses => _contactAddresses;

    #endregion ContactAddresses


    #region Database name

    private string? _databaseName = string.Empty; // postgres

    public virtual INpOnConnectOption SetDatabaseName(string databaseName)
    {
        try
        {
            if (!IsValid())
                throw new InvalidOperationException($"ConnectOptions is not valid for {typeof(INpOnDbDriver)}");
            _databaseName = databaseName;
        }
        catch (InvalidOperationException)
        {
            _databaseName = null;
        }

        return this;
    }

    public string? DatabaseName => _databaseName;

    #endregion Database name


    #region SetShutdownImmediate

    private int? _databaseIndex;

    public INpOnConnectOption SetDatabaseIndex(int databaseIndex)
    {
        try
        {
            if (!IsValid())
                throw new InvalidOperationException($"DatabaseIndex is not valid for {typeof(INpOnDbDriver)}");
            _databaseIndex = databaseIndex;
        }
        catch (InvalidOperationException)
        {
            _databaseIndex = null;
        }

        return this;
    }

    public int? DatabaseIndex => _databaseIndex;

    #endregion SetShutdownImmediate


    // generic 

    #region SetShutdownImmediate

    public INpOnConnectOption SetShutdownImmediate(bool isShutdownImmediate = false)
    {
        _isShutdownImmediate = isShutdownImmediate;
        return this;
    }

    public bool IsShutdownImmediate => _isShutdownImmediate;

    #endregion SetShutdownImmediate


    #region WaitNextTransaction

    public INpOnConnectOption SetWaitNextTransaction(bool isWaitNextTransaction = true)
    {
        _isWaitNextTransaction = isWaitNextTransaction;
        return this;
    }

    public bool IsWaitNextTransaction => _isWaitNextTransaction;

    #endregion WaitNextTransaction


    #region UseMultiSessions

    public INpOnConnectOption SetSessionTimeout(long secondsTimeout = 30)
    {
        _secondsTimeout = secondsTimeout;
        _currentConnectionTime = DateTime.Now;
        _expiredConnectionTime = DateTime.Now + TimeSpan.FromSeconds(_secondsTimeout);
        return this;
    }

    public void ResetSessionTimeout()
    {
        _currentConnectionTime = DateTime.Now;
        _expiredConnectionTime = DateTime.Now + TimeSpan.FromSeconds(_secondsTimeout);
    }

    public bool IsExpired => DateTime.Now >= _expiredConnectionTime;

    public long ConnectionTimeoutSessions => _secondsTimeout;

    #endregion UseMultiSessions


    #region KeyCode

    public string Code
    {
        get
        {
            if (field != null)
                return field;

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(ConnectionString))
                sb.Append($"CS={ConnectionString};");

            if (!string.IsNullOrEmpty(Keyspace))
                sb.Append($"KS={Keyspace};");

            if (!string.IsNullOrEmpty(DatabaseName))
                sb.Append($"DB={DatabaseName};");

            if (!string.IsNullOrEmpty(CollectionName))
                sb.Append($"COLL={CollectionName};");

            if (ContactAddresses is { Length: > 0 })
                sb.Append($"CA={string.Join(",", ContactAddresses)};");

            if (IsShutdownImmediate) // Default is false
                sb.Append($"SI={IsShutdownImmediate};");

            if (!IsWaitNextTransaction) // Default is true
                sb.Append($"WNT={IsWaitNextTransaction};");

            field = sb.ToString(); // lưu lại để lần sau dùng luôn
            return field;
        }
    }

    #endregion KeyCode
}