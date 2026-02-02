using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Microsoft.AspNetCore.HttpOverrides;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public static class ForwardHeaderExtensions
{
    [Obsolete("Obsolete")]
    public static IServiceCollection UseDefaultForwardHeaderOptionMode(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            bool isUseMultiDomainInHost =
                EApplicationConfiguration.IsUseMultiDomainInHost.GetAppSettingConfig().AsDefaultBool();
            if (isUseMultiDomainInHost)
                options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;
            // [FIX] Configure ForwardedHeaders to correctly identify the Protocol (HTTPS) when running behind a Reverse Proxy (Docker/Nginx)
            options.KnownNetworks.Clear(); // Trust internal docker networks
            options.KnownProxies.Clear();
        });
        return services;
    }
}