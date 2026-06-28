using Microsoft.Extensions.DependencyInjection;
using NpOn.CommonGrpcCall;

namespace MicroServices.Migration.Service.NpOn.IMigrationService;

public class MigrationServiceClientResolver : SharedGrpcClientResolver
{
    protected override Func<IServiceCollection, string, Task> RegistrationAction =>
        (services, url) =>
        {
            services.RegisterGrpcClientLoadBalancing<ICassandraMigration>(url);
            return Task.CompletedTask;
        };
}