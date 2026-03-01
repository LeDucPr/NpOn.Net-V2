using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.Generics;

public interface IDbFactoryWrapper
{
    string? FactoryOptionCode { get; }
    EDb GetDbType();
    Task<INpOnWrapperResult?> ExecuteAsync(INpOnDbCommand dbCommand);
    Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<INpOnDbCommandParam> parameters);

    Task<INpOnWrapperResult?> ExecuteFuncParams<TEnumDbType>(string funcName,
        List<INpOnDbCommandParam<TEnumDbType>>? parameters) where TEnumDbType : Enum;
}