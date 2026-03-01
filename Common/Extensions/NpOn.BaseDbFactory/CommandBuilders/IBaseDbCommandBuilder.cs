using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.BaseDbFactory.CommandBuilders;

public interface IBaseDbCommandBuilder
{
    IBaseNpOnDbCommand CommandBuilder<T>
        (IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain;
}