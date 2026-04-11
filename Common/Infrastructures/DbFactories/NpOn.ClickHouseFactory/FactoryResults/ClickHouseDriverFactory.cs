using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.ClickHouseExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory.FactoryResults;

public class ClickHouseDriverFactory : BaseDbDriverFactory
{
    private readonly IObjectPoolStore? _poolStore;

    public ClickHouseDriverFactory(INpOnConnectOption option, IObjectPoolStore? poolStore = null,
        int connectionNumber = 1) : base(EDb.ClickHouse, option, connectionNumber)
    {
        _poolStore = poolStore;
    }

    protected override NpOnDbConnection InitConnection() => CreateClickHouseConnection(Option);

    private NpOnDbConnection CreateClickHouseConnection(INpOnConnectOption? option)
    {
        if (Option == null)
        {
            throw new InvalidOperationException("Connection options have not been set.");
        }

        if (option is not ClickHouseConnectOption clickHouseOptions)
        {
            throw new ArgumentException("Invalid options for ClickHouse. Expected ClickHouseConnectOption.", nameof(option));
        }

        INpOnDbDriver driver = new ClickHouseDriver(clickHouseOptions, _poolStore);
        return new NpOnDbConnection<ClickHouseDriver>(driver);
    }
}
