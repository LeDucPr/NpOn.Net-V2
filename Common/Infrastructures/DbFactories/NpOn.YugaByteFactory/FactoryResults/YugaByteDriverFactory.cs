using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.YugaByteExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.YugaByteFactory.FactoryResults;

public class YugaByteDriverFactory : BaseDbDriverFactory
{
    private readonly IObjectPoolStore? _poolStore;

    private static INpOnConnectOption InjectPoolStore(INpOnConnectOption option, IObjectPoolStore? poolStore)
    {
        if (option is YugaByteConnectOption ybOption)
        {
            ybOption.PoolStore = poolStore;
        }
        return option;
    }

    public YugaByteDriverFactory(INpOnConnectOption option, IObjectPoolStore? poolStore = null,
        int connectionNumber = 1) : base(EDb.YugaBytePg, InjectPoolStore(option, poolStore), connectionNumber)
    {
        _poolStore = poolStore;
    }

    protected override NpOnDbConnection InitConnection() => CreateYugaByteConnection(Option);

    private NpOnDbConnection CreateYugaByteConnection(INpOnConnectOption? option)
    {
        if (Option == null)
        {
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");
        }

        if (option is not YugaByteConnectOption ybOptions)
        {
            throw new ArgumentException("Invalid options for YugaByte. Expected YugaByteConnectOption.",
                nameof(option));
        }

        INpOnDbDriver driver = new YugaByteDriver(ybOptions, ybOptions.PoolStore ?? _poolStore);
        return new NpOnDbConnection<YugaByteDriver>(driver);
    }
}
