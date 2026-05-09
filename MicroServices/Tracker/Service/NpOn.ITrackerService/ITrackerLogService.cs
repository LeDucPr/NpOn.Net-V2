using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;

namespace MicroServices.Tracker.Service.NpOn.ITrackerService;

[ServiceContract]
public interface ITrackerLogService
{
    [OperationContract]
    Task<CommonResponse> PushLogAsync(TrackerLogCommand command);

    [OperationContract]
    Task<CommonResponse> PushLogsAsync(TrackerLogCommand[]? commands);
}
