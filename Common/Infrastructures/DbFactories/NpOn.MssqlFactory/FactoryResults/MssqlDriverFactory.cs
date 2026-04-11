using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.MssqlExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.MssqlFactory.FactoryResults;

public class MssqlDriverFactory : BaseDbDriverFactory
{
    private readonly IObjectPoolStore? _poolStore;

    private static INpOnConnectOption InjectPoolStore(INpOnConnectOption option, IObjectPoolStore? poolStore)
    {
        if (option is MssqlConnectOption mssqlOption)
        {
            mssqlOption.PoolStore = poolStore;
        }
        return option;
    }

    public MssqlDriverFactory(INpOnConnectOption option, IObjectPoolStore? poolStore = null,
        int connectionNumber = 1) : base(EDb.Mssql, InjectPoolStore(option, poolStore), connectionNumber)
    {
        _poolStore = poolStore;
    }

    protected override NpOnDbConnection InitConnection() => CreateMssqlConnection(Option);

    private NpOnDbConnection CreateMssqlConnection(INpOnConnectOption? option)
    {
        if (Option == null)
        {
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");
        }

        if (option is not MssqlConnectOption mssqlOptions)
        {
            throw new ArgumentException("Invalid options for Mssql. Expected MssqlConnectOption.",
                nameof(option));
        }

        INpOnDbDriver driver = new MssqlDriver(mssqlOptions, mssqlOptions.PoolStore ?? _poolStore);
        return new NpOnDbConnection<MssqlDriver>(driver);
    }
}
