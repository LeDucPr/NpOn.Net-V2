using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand;

namespace MicroServices.Tracker.Service.NpOn.ITrackerService;

[ServiceContract]
public interface ITrackerLogService
{
    [OperationContract]
    Task<CommonResponse> PushLogAsync(TrackerLogCommand command);

    [OperationContract]
    Task<CommonResponse> PushLogsAsync(TrackerLogCommand[]? commands);
}
