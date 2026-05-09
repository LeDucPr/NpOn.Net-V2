using Microsoft.Extensions.DependencyInjection;
using NpOn.CommonGrpcCall;

namespace MicroServices.Tracker.Service.NpOn.ITrackerService;

public class TrackerServiceClientResolver : SharedGrpcClientResolver
{
    protected override Func<IServiceCollection, string, Task> RegistrationAction =>
        (services, url) =>
        {
            services.RegisterGrpcClientLoadBalancing<ITrackerLogService>(url);
            return Task.CompletedTask;
        };
}
