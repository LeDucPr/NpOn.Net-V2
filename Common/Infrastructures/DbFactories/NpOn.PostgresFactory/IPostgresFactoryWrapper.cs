using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.Generics;

namespace Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;

public interface IPostgresFactoryWrapper : IDbFactoryWrapper
{
    Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain;
    Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnExecuteCommand);
}