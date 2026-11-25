// using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
// using Common.Extensions.NpOn.CommonMode;

using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

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

    public static HttpsConnectionAdapterOptions ConfigureInterServiceHttpsOptions(
        this HttpsConnectionAdapterOptions httpsOptions,
        IInterServiceCertificateService certificateService,
        ClientCertificateMode clientCertificateMode)
    {
        var serverCert = certificateService.GetServerCertificate()
                         ?? throw new InvalidOperationException(
                             "Inter-service mTLS is enabled, but the server certificate could not be loaded.");

        httpsOptions.ServerCertificate = serverCert;
        httpsOptions.ServerCertificateSelector = (_, _) => certificateService.GetServerCertificate() ?? serverCert;
        httpsOptions.ClientCertificateMode = clientCertificateMode;
        httpsOptions.AllowAnyClientCertificate();
        httpsOptions.ClientCertificateValidation = (cert, _, _) =>
            certificateService.ValidateClientCertificate(cert);
        return httpsOptions;
    }

    public static ListenOptions ApplyInterServiceMtls(
        this ListenOptions listenOptions,
        IInterServiceCertificateService certificateService,
        ClientCertificateMode clientCertificateMode)
    {
        return listenOptions.UseHttps(httpsOptions =>
            httpsOptions.ConfigureInterServiceHttpsOptions(certificateService, clientCertificateMode));
    }

    public static IServiceCollection ApplyInterServiceMtls(
        this IServiceCollection services,
        ClientCertificateMode clientCertificateMode,
        string? serviceName = null)
    {
        services.AddOptions<KestrelServerOptions>()
            .Configure<IInterServiceCertificateService, IServiceProvider>((options, certService, sp) =>
            {
                var rotationService = sp.GetService<ICertificateRotationService>();
                if (rotationService != null && !string.IsNullOrWhiteSpace(serviceName))
                {
                    rotationService.EnsureCertificatesExistAsync(serviceName).GetAwaiter().GetResult();
                    certService.ReloadCertificates();
                }

                options.ConfigureHttpsDefaults(httpsOptions =>
                    httpsOptions.ConfigureInterServiceHttpsOptions(certService, clientCertificateMode));
            });
        return services;
    }
}