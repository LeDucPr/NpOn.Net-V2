using Microsoft.Extensions.DependencyInjection;
using NpOn.CommonGrpcCall;

namespace MicroServices.Account.Service.NpOn.IAccountService;

public class AccountServiceClientResolver : SharedGrpcClientResolver
{
    protected override Func<IServiceCollection, string, Task> RegistrationAction =>
        (services, url) =>
        {
            services.RegisterGrpcClientLoadBalancing<IAccountInfoService>(url);
            services.RegisterGrpcClientLoadBalancing<IAuthenticationService>(url);
            services.RegisterGrpcClientLoadBalancing<IAccountPermissionService>(url);
            services.RegisterGrpcClientLoadBalancing<IAccountGroupService>(url);
            return Task.CompletedTask;
        };
}
