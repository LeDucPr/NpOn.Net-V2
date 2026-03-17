using System;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Extensions.NpOn.BaseDbFactory.Generics;

public static class ActionGeneratorExtensions 
{
    public static void CheckBuildTableActionCommand(this INpOnWrapperResult table, ERepositoryAction action, string tableName)
    {
        if (table is not INpOnTableWrapper tableWrapper)
            throw new ArgumentException("Invalid initialization object");
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty or null");
    }
}