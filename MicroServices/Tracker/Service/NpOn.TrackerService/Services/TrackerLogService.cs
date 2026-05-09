using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;
using MicroServices.Tracker.Service.NpOn.ITrackerService;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.Services;

public class TrackerLogService : ITrackerLogService
{
    private readonly IClickHouseFactoryWrapper _clickHouseFactory;

    public TrackerLogService(IClickHouseFactoryWrapper clickHouseFactory)
    {
        _clickHouseFactory = clickHouseFactory;
    }

    public async Task<CommonResponse> PushLogAsync(TrackerLogCommand command)
    {
        return await PushLogsAsync(new List<TrackerLogCommand> { command });
    }

    public async Task<CommonResponse> PushLogsAsync(List<TrackerLogCommand> commands)
    {
        var response = new CommonResponse();
        if (commands == null || commands.Count == 0)
        {
            response.SetSuccess();
            return response;
        }

        try
        {
            var sql = @"
                INSERT INTO SystemLogs (Timestamp, Level, Source, Message, Attributes) 
                VALUES (@Timestamp, @Level, @Source, @Message, @Attributes)
            ";

            foreach (var log in commands)
            {
                var execCommand = new NpOnDbExecuteCommand
                {
                    CommandText = sql,
                    ExecType = EExecType.Query,
                    Parameters = new INpOnDbCommandParam[]
                    {
                        new NpOnDbCommandParam { ParamName = "@Timestamp", ParamValue = log.Timestamp },
                        new NpOnDbCommandParam { ParamName = "@Level", ParamValue = log.Level },
                        new NpOnDbCommandParam { ParamName = "@Source", ParamValue = log.Source },
                        new NpOnDbCommandParam { ParamName = "@Message", ParamValue = log.Message },
                        new NpOnDbCommandParam { ParamName = "@Attributes", ParamValue = log.Attributes }
                    }
                };
                await _clickHouseFactory.Execute(execCommand);
            }

            response.SetSuccess();
            return response;
        }
        catch (Exception ex)
        {
            response.SetFail(ex);
            return response;
        }
    }
}
