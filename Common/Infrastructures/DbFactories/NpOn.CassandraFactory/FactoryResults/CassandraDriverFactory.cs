using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.CassandraExtCm.Connections;
using Common.Infrastructures.NpOn.CassandraExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.CassandraFactory.FactoryResults;

public class CassandraDriverFactory : BaseDbDriverFactory
{
    private readonly IObjectPoolStore? _poolStore;

    public CassandraDriverFactory(INpOnConnectOption option, IObjectPoolStore? poolStore = null,
        int connectionNumber = 1) : base(EDb.Postgres, option, connectionNumber)
    {
        _poolStore = poolStore;
    }

    protected override NpOnDbConnection InitConnection() => CreateCassandraConnection(Option);

    private NpOnDbConnection CreateCassandraConnection(INpOnConnectOption? option)
    {
        if (Option == null)
        {
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");
        }

        if (option is not CassandraConnectOption cassandraOptions)
        {
            throw new ArgumentException("Invalid options for Cassandra. Expected CassandraConnectOption.",
                nameof(option));
        }

        INpOnDbDriver driver = new CassandraDriver(cassandraOptions, _poolStore);
        return new NpOnDbConnection<CassandraDriver>(driver);
    }
}