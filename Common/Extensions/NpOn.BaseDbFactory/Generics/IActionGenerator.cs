using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Extensions.NpOn.BaseDbFactory.Generics;

public interface IActionGenerator
{
    public IBaseNpOnDbCommand? TableActionCommand(INpOnWrapperResult table, ERepositoryAction action, string tableName);
}