using Microsoft.Extensions.DependencyInjection;
using NpOn.CommonGrpcCall;

namespace MicroServices.General.Service.NpOn.IGeneralService;

public class GeneralServiceClientResolver : SharedGrpcClientResolver
{
    protected override Func<IServiceCollection, string, Task> RegistrationAction =>
        (services, url) =>
        {
            services.RegisterGrpcClientLoadBalancing<IFldMasterPgService>(url);
            return Task.CompletedTask;
        };
}
