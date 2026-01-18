using Microsoft.Extensions.DependencyInjection;
using NpOn.CommonGrpcCall;

namespace MicroServices.Account.Service.NpOn.IAccountService;

public class AccountServiceClientResolver : InternalGrpcClientResolver
{
    protected override Func<IServiceCollection, string, Task> RegistrationAction =>
        (services, url) =>
        {
            services.RegisterGrpcClientLoadBalancing<IAccountInfoService>(url);
            services.RegisterGrpcClientLoadBalancing<IAuthenticationService>(url);
            services.RegisterGrpcClientLoadBalancing<IAccountPermissionService>(url);
            services.RegisterGrpcClientLoadBalancing<IAccountGroupService>(url);
            services.RegisterGrpcClientLoadBalancing<IAccountMenuService>(url);
            services.RegisterGrpcClientLoadBalancing<IAccountMenuPermissionService>(url);
            return Task.CompletedTask;
        };
}
 