using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Infrastructures.NpOn.CommonDb.DbResults;

namespace Common.Infrastructures.NpOn.BaseRepository;

public interface INpOnBaseRepository
{
    Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain;
    Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain;
}