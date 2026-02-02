using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Microsoft.AspNetCore.Authentication;

namespace Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse;

public static class UseTokenValidatorExtensions
{
    public static IServiceCollection UseTokenValidatorDefaultMode(this IServiceCollection services)
    {
        // authentication 
        services.AddTransient<AuthenticationToken>();
        services.AddTransient<ContextService>();
        services.AddTransient<AuthenService>();
        services.AddSingleton<PermissionService>();
        return services;
    }
}