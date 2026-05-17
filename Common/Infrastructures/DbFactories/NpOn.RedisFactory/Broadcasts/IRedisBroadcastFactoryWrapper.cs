using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public  interface IRedisBroadcastFactoryWrapper : IBaseBroadcastFactoryWrapper
{
    IDbDriverFactory? Factory { get; set; }
    EDb DbType { get; set; }
}