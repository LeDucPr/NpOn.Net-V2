using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.ElasticSearchExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.ElasticSearchFactory.FactoryResults;

public class ElasticSearchDriverFactory : BaseDbDriverFactory
{
    public ElasticSearchDriverFactory(INpOnConnectOption option, int connectionNumber = 1) : base(EDb.ElasticSearch,
        option, connectionNumber)
    {
    }

    protected override NpOnDbConnection InitConnection() => CreateElasticSearchDbConnection(Option);

    private NpOnDbConnection CreateElasticSearchDbConnection(INpOnConnectOption? option)
    {
        if (Option == null)
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");

        if (option is not ElasticSearchConnectOption elasticOptions)
            throw new ArgumentException("Invalid options for ElasticSearch. Expected ElasticSearchConnectOption.",
                nameof(option));

        INpOnDbDriver driver = new ElasticSearchDriver(elasticOptions);
        return new NpOnDbConnection<ElasticSearchDriver>(driver);
    }
}
