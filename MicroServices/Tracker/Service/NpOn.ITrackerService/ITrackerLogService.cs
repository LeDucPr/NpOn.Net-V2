using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand.Commands;

namespace MicroServices.Tracker.Service.NpOn.ITrackerService;

[ServiceContract]
public interface ITrackerLogService
{
    [OperationContract]
    Task<CommonResponse> PushLogs(TrackerLogAddCommand[]? commands);
}
