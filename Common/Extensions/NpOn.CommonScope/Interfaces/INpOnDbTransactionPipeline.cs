using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonScope.Interfaces;

public interface INpOnDbTransactionPipeline : INpOnBaseTransactionPipeline
{
    NpOnDbTransactionPipeline Register(IDbFactoryWrapper dbFactoryWrapper);
    NpOnDbTransactionPipeline Register(IBaseNpOnDbCommand command);
}