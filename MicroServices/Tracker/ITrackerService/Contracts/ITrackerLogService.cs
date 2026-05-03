using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.Tracker.Service.NpOn.ITrackerService.Models;

namespace MicroServices.Tracker.Service.NpOn.ITrackerService.Contracts;

[ServiceContract]
public interface ITrackerLogService
{
    [OperationContract]
    Task<CommonResponse> PushLogAsync(TrackerLogCommand command);

    [OperationContract]
    Task<CommonResponse> PushLogsAsync(List<TrackerLogCommand> commands);
}
