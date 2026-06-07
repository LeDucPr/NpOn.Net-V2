using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;

public class ZeroMqDriverFactory : BaseDbDriverFactory
{
    public ZeroMqDriverFactory(INpOnConnectOption option, int connectionNumber = 1) : base(EDb.ZeroMqRunAsDbFlow, option, connectionNumber)
    {
    }

    protected override NpOnDbConnection InitConnection() => CreateZeroMqDbConnection(Option);

    private NpOnDbConnection CreateZeroMqDbConnection(INpOnConnectOption? option)
    {
        if (Option == null)
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");

        if (option is not ZeroMqConnectOption zeroMqOptions)
            throw new ArgumentException("Invalid options for ZeroMQ. Expected ZeroMqConnectOption.",
                nameof(option));

        INpOnDbDriver driver = new ZeroMqDriver(zeroMqOptions);
        return new NpOnDbConnection<ZeroMqDriver>(driver);
    }
}
