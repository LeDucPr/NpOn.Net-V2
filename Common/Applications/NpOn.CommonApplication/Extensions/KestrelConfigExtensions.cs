// using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
// using Common.Extensions.NpOn.CommonMode;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public static class KestrelConfigExtensions
{
    public static IServiceCollection AddDefaultKestrelListenConfig(this IServiceCollection services)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
        });
        return services;

        // HttpProtocols? kestrelLisCfg = EApplicationConfiguration.KestrelServerOptions.GetAppSettingConfig()
        //     .AsEmptyString().ToEnum<HttpProtocols>();
        // var cc = EApplicationConfiguration.KestrelServerOptions.GetAppSettingConfig()
        //     .AsEmptyString();
        // // Force Kestrel to use HTTP/2 for gRPC over plaintext (Server)
        // if (kestrelLisCfg == null)
        //     return services;
        // services.Configure<KestrelServerOptions>(options =>
        // {
        //     options.ConfigureEndpointDefaults(listenOptions =>
        //     {
        //         listenOptions.Protocols = (HttpProtocols)kestrelLisCfg;
        //     });
        // });
        // return services;
    }
}