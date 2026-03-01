using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.FactoryResults;
using Common.Infrastructures.NpOn.RedisExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.FactoryResults;

public class RedisDriverFactory : BaseDbDriverFactory
{
    public RedisDriverFactory(INpOnConnectOption option, int connectionNumber = 1) : base(EDb.Redis,
        option, connectionNumber)
    {
    }

    protected override NpOnDbConnection InitConnection() => CreateRedisDbConnection(Option);


    private NpOnDbConnection CreateRedisDbConnection(INpOnConnectOption? option)
    {
        if (Option == null)
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");

        if (option is not RedisConnectOption redisOptions)
            throw new ArgumentException("Invalid options for Redis. Expected RedisConnectOptions.",
                nameof(option));

        INpOnDbDriver driver = new RedisDriver(redisOptions);
        return new NpOnDbConnection<RedisDriver>(driver);
    }
}