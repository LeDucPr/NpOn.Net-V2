// using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
// using Common.Extensions.NpOn.CommonMode;

using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public static class KestrelConfigExtensions
{
    public static IServiceCollection AddDefaultKestrelListenConfig(this IServiceCollection services,
        out HttpProtocols? protocols)
    {
        protocols = null;
        HttpProtocols? kestrelLisCfg = EApplicationConfiguration.KestrelServerOptions.GetAppSettingConfig()
            .AsEmptyString().ToEnum<HttpProtocols>();
        // Force Kestrel to use HTTP/2 for gRPC over plaintext (Server)
        if (kestrelLisCfg == null)
            return services;
        protocols = kestrelLisCfg;
        services.Configure<KestrelServerOptions>(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = (HttpProtocols)kestrelLisCfg;
            });
        });
        return services;
    }
}