using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.Neo4JExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.Neo4jDbFactory.FactoryResults;

public class Neo4JDriverFactory : BaseDbDriverFactory
{
    public Neo4JDriverFactory(INpOnConnectOption option, int connectionNumber = 1) 
        : base(EDb.Neo4j, option, connectionNumber)
    {
    }

    protected override NpOnDbConnection InitConnection() => CreateNeo4JConnection(Option);

    private NpOnDbConnection CreateNeo4JConnection(INpOnConnectOption? option)
    {
        if (Option == null)
        {
            throw new InvalidOperationException("Connection options have not been set or are invalid.");
        }

        if (option is not Neo4JConnectOption neo4JOptions)
        {
            throw new ArgumentException("Invalid options for Neo4j.", nameof(option));
        }

        INpOnDbDriver driver = new Neo4JDriver(neo4JOptions);
        return new NpOnDbConnection<Neo4JDriver>(driver);
    }
}
