using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.PostgresExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory.FactoryResults;

public class PostgresDriverFactory : BaseDbDriverFactory
{
    public PostgresDriverFactory(INpOnConnectOption option, int connectionNumber = 1) : base(EDb.Postgres,
        option, connectionNumber)
    {
    }

    protected override NpOnDbConnection InitConnection() => CreatePostgresConnection(Option);

    private NpOnDbConnection CreatePostgresConnection(INpOnConnectOption? option)
    {
        if (Option == null)
        {
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");
        }

        if (option is not PostgresConnectOption postgresOptions)
        {
            throw new ArgumentException("Invalid options for Postgres. Expected PostgresConnectOptions.",
                nameof(option));
        }

        INpOnDbDriver driver = new PostgresDriver(postgresOptions);
        return new NpOnDbConnection<PostgresDriver>(driver);
    }
}