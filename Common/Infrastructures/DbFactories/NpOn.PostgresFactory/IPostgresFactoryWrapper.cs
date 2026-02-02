using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.Generics;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;
using Common.Infrastructures.NpOn.CommonDb.DbResults;

namespace Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;

public interface IPostgresFactoryWrapper : IDbFactoryWrapper
{
    Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain;
    Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnExecuteCommand);
}