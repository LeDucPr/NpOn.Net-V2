using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.MySqlExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.MySqlFactory.FactoryResults;

public class MySqlDriverFactory : BaseDbDriverFactory
{
    private readonly IObjectPoolStore? _poolStore;

    private static INpOnConnectOption InjectPoolStore(INpOnConnectOption option, IObjectPoolStore? poolStore)
    {
        if (option is MySqlConnectOption pgOption)
        {
            pgOption.PoolStore = poolStore;
        }
        return option;
    }

    public MySqlDriverFactory(INpOnConnectOption option, IObjectPoolStore? poolStore = null,
        int connectionNumber = 1) : base(EDb.MySql, InjectPoolStore(option, poolStore), connectionNumber)
    {
        _poolStore = poolStore;
    }

    protected override NpOnDbConnection InitConnection() => CreateMySqlConnection(Option);

    private NpOnDbConnection CreateMySqlConnection(INpOnConnectOption? option)
    {
        if (Option == null)
        {
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");
        }

        if (option is not MySqlConnectOption mysqlOptions)
        {
            throw new ArgumentException("Invalid options for MySql. Expected MySqlConnectOptions.",
                nameof(option));
        }

        INpOnDbDriver driver = new MySqlDriver(mysqlOptions, mysqlOptions.PoolStore ?? _poolStore);
        return new NpOnDbConnection<MySqlDriver>(driver);
    }
}