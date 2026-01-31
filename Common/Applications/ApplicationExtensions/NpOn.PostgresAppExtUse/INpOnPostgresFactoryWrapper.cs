using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Infrastructures.NpOn.BaseExecution;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Common.Infrastructures.NpOn.DbFactory.Generics;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

public interface INpOnPostgresFactoryWrapper : IDbFactoryWrapper
{
    Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain;
    Task<INpOnWrapperResult?> Execute(NpOnExecuteCommand npOnExecuteCommand);
}