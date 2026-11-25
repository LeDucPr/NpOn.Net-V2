using Common.Applications.NpOn.CommonApplication.Builders;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonApplication.Parameters;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.IdentityModel.Logging;
using NpOn.CommonGrpcCall;

namespace Common.Applications.NpOn.CommonApplication;

public abstract class CommonProgram
{
    protected readonly string[] Args;
    protected virtual bool UseControllers => true;

    protected CommonProgram(string[] args)
    {
        Args = args;
    }

    protected async Task RunAsync()
    {
        var builder = CreateDefaultBuilder(Args);
        await builder.Services.AddCollectionServices(async (services) =>
        {
#pragma warning disable CS0618 // Type or member is obsolete
            ConfigureBaseServices(services);
#pragma warning restore CS0618 // Type or member is obsolete
            await ConfigureServices(services);

            // Add certificate rotation background service if mTLS mode is enabled
            var authMode = InterServiceAuthConfigHelper.ResolveAuthMode();
            var autoGenerate = EApplicationConfiguration.InterServiceAutoGenerateCertificates.GetAppSettingConfig()
                .AsDefaultBool(true);
            if (authMode == EInterServiceAuthMode.Mtls && autoGenerate)
            {
                var serviceName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();
                services.AddHostedService(sp => new CertificateRotationBackgroundService(
                    sp.GetRequiredService<ICertificateRotationService>(),
                    serviceName,
                    sp.GetRequiredService<ILogger<CertificateRotationBackgroundService>>()));
            }

            return services;
        });

        var app = builder.Build();

        await app.AddAppConfig(async (appConfig) =>
        {
            ConfigureBasePipeline(appConfig);
            await ConfigurePipeline(appConfig);
            return appConfig;
        });
        await app.RunAsync(); // run
    }


    #region For Enable Overrid Methods

    /// <summary>
    /// Configures services that are common to all applications.
    /// </summary>
    [Obsolete("Obsolete")]
    protected virtual void ConfigureBaseServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor(); // accessor

        services
            .UseDefaultForwardHeaderOptionMode() // forward header options /// Obsolete
            .UserLoggerDefaultMode() // logger
            .UseDefaultCompressMode(); // compress response

        services
            .UseDefaultKeyGenerationMode() // key generation
            .UseDefaultAuthorizationMode() // authorization
            .UseDefaultAuthenticationMode(); // authentication

        services.AddCors();

#if DEBUG
        if (EApplicationConfiguration.IsDevEnvironment.GetAppSettingConfig().AsDefaultBool()) // debug
            IdentityModelEventSource.ShowPII = true;
#endif

        // Register inter-service authentication services
        RegisterInterServiceAuthServices(services);
    }

    private void RegisterInterServiceAuthServices(IServiceCollection services)
    {
        var authMode = InterServiceAuthConfigHelper.ResolveAuthMode();

        if (authMode == EInterServiceAuthMode.Mtls)
        {
            var serviceName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();
            var storagePath = EApplicationConfiguration.InterServiceCertificateStoragePath.GetAppSettingConfig()
                .AsDefaultString("/certs");
            var clientCertPath = EApplicationConfiguration.InterServiceClientCertPath.GetAppSettingConfig()
                .AsDefaultString();
            var clientCertPassword = EApplicationConfiguration.InterServiceClientCertPassword.GetAppSettingConfig()
                .AsDefaultString();
            var serverCertPath = EApplicationConfiguration.InterServiceServerCertPath.GetAppSettingConfig()
                .AsDefaultString();
            var serverCertPassword = EApplicationConfiguration.InterServiceServerCertPassword.GetAppSettingConfig()
                .AsDefaultString();
            var caCertPath = EApplicationConfiguration.InterServiceCACertPath.GetAppSettingConfig()
                .AsDefaultString("/certs/ca.crt");
            var allowedIdentities = InterServiceAuthConfigHelper.GetAllowedIdentities();
            var sanHosts = InterServiceAuthConfigHelper.GetServerSanHosts();

            services.AddSingleton<IInterServiceCertificateService>(new InterServiceCertificateService(
                clientCertPath,
                clientCertPassword,
                serverCertPath,
                serverCertPassword,
                caCertPath,
                allowedIdentities: allowedIdentities));

            var autoGenerate = EApplicationConfiguration.InterServiceAutoGenerateCertificates.GetAppSettingConfig()
                .AsDefaultBool(true);
            if (autoGenerate)
            {
                var validityDays = EApplicationConfiguration.InterServiceCertificateValidityDays.GetAppSettingConfig()
                    .AsDefaultInt(90);
                var effectivePassword = !string.IsNullOrWhiteSpace(serverCertPassword)
                    ? serverCertPassword
                    : clientCertPassword;

                services.AddSingleton<ICertificateGenerator>(sp => new CertificateGenerator(
                    sp.GetRequiredService<ILogger<CertificateGenerator>>(),
                    storagePath));

                services.AddSingleton<ICertificateRotationService>(sp => new CertificateRotationService(
                    sp.GetRequiredService<ICertificateGenerator>(),
                    sp.GetRequiredService<IInterServiceCertificateService>(),
                    sp.GetRequiredService<ILogger<CertificateRotationService>>(),
                    caCertPath,
                    validityDays,
                    clientCertPath: clientCertPath,
                    serverCertPath: serverCertPath,
                    certPassword: effectivePassword,
                    additionalDnsNames: sanHosts));
            }

            var clientCertMode = UseControllers
                ? ClientCertificateMode.AllowCertificate
                : ClientCertificateMode.RequireCertificate;
            services.ApplyInterServiceMtls(clientCertMode, serviceName);
        }
        else if (authMode == EInterServiceAuthMode.ServiceMesh)
        {
            // Option 3B: Envoy sidecar handles mTLS/SPIFFE externally and forwards plaintext HTTP/2 (h2c) + XFCC
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        // Register JWT token service (active in Jwt mode; retained in DI for backward-compatible GrpcHeaderConfig resolution)
        var interServiceSecret =
            EApplicationConfiguration.InterServiceSecretKey.GetAppSettingConfig().AsDefaultString();
        var tokenLifetimeMinutes = EApplicationConfiguration.InterServiceTokenLifetimeMinutes.GetAppSettingConfig()
            .AsDefaultInt();
        var keyRotationMinutes = EApplicationConfiguration.InterServiceKeyRotationMinutes.GetAppSettingConfig()
            .AsDefaultInt();
        services.AddSingleton<IInterServiceTokenService>(new InterServiceTokenService(
            string.IsNullOrWhiteSpace(interServiceSecret) ? "NpOn-InterService-Secret-Key-2024-Change-In-Production" : interServiceSecret,
            tokenLifetimeMinutes > 0 ? TimeSpan.FromMinutes(tokenLifetimeMinutes) : TimeSpan.FromHours(1),
            keyRotationMinutes > 0 ? TimeSpan.FromMinutes(keyRotationMinutes) : TimeSpan.FromHours(6)));
    }

    /// <summary>
    /// Configures services specific to the derived application.
    /// </summary>
    protected abstract Task ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Configures the common parts of the HTTP request pipeline.
    /// </summary>
    protected virtual void ConfigureBasePipeline(WebApplication app)
    {
        // [FIX] Phải đặt đầu pipeline để xác định đúng Scheme (HTTP/HTTPS) trước khi Authentication chạy
        app.UseForwardedHeaders();

        app.UseRouting();

        app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        if (EApplicationConfiguration.IsUseResponseCompression.GetAppSettingConfig().AsDefaultBool())
            app.UseResponseCompression();

        // CORS middleware must be placed after UseRouting and before UseAuthentication/UseAuthorization
        // to correctly handle preflight OPTIONS requests.
        string corsPolicy = EApplicationConfiguration.CorsPolicy.GetAppSettingConfig().AsDefaultString();
        if (!string.IsNullOrWhiteSpace(corsPolicy))
        {
            app.UseCors(corsPolicy);
        }

        var authMode = InterServiceAuthConfigHelper.ResolveAuthMode();
        if (authMode == EInterServiceAuthMode.Mtls)
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // Inter-service authentication middleware for gRPC
        if (EApplicationConfiguration.IsUseGrpcStandardMode.GetAppSettingConfig().AsDefaultBool())
        {
            var requireAuth = EApplicationConfiguration.InterServiceRequireAuth.GetAppSettingConfig().AsDefaultBool(true);
            var trustDomain = InterServiceAuthConfigHelper.GetServiceMeshTrustDomain();
            var trustedProxyIps = InterServiceAuthConfigHelper.GetTrustedProxyIps();
            var allowedIdentities = InterServiceAuthConfigHelper.GetAllowedIdentities();
            app.UseGrpcServerAuth(
                requireAuth: requireAuth,
                authMode: authMode,
                serviceMeshTrustDomain: trustDomain,
                trustedProxyIps: trustedProxyIps,
                allowedIdentities: allowedIdentities);
        }

        if (UseControllers)
        {
            app.MapControllers();
        }

        string appName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();
        app.MapGet("/", () => appName).AllowAnonymous();
    }

    /// <summary>
    /// Configures the HTTP request pipeline specific to the derived application (e.g., mapping gRPC services).
    /// </summary>
    protected abstract Task ConfigurePipeline(WebApplication app);

    #endregion For Enable Overrid Methods


    #region Private Methods

    private WebApplicationBuilder CreateDefaultBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // add custom config (only new version) // appsettings.YAML
        builder.Configuration.Sources.Clear(); // clear appsettings.JSON
        builder.Configuration.AddYamlFile("appsettings.yaml", optional: true, reloadOnChange: true) // imperative
            .AddEnvironmentVariables() // override Docker Compose 
            // .AddYamlFile($"appsettings.{builder.Environment.EnvironmentName}.yaml", optional: true)
            ;

        builder.Configuration.InitConfigs(
            typeof(EApplicationConfiguration),
            typeof(EUrlConfiguration)
        );

        var authMode = InterServiceAuthConfigHelper.ResolveAuthMode();
        // host-domain-start
        string hostDomain = builder.Configuration.TryGetConfig(EApplicationConfiguration.HostDomain).AsDefaultString();
        var hostPort = builder.Configuration.TryGetConfig(EApplicationConfiguration.HostPort).AsDefaultInt();
        if (string.IsNullOrWhiteSpace(hostDomain))
            throw new Exception(EWebApplicationError.HostDomain.GetDisplayName());

        hostDomain = hostDomain.Trim().TrimEnd('/');
        if (authMode == EInterServiceAuthMode.Mtls &&
            hostDomain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            hostDomain = $"https://{hostDomain["http://".Length..]}";
        }
        else if (authMode != EInterServiceAuthMode.Mtls &&
                 hostDomain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            hostDomain = $"http://{hostDomain["https://".Length..]}";
        }

        bool hasExplicitPort = Uri.TryCreate(hostDomain, UriKind.Absolute, out var parsedUri) && !parsedUri.IsDefaultPort;
        string listenUrl = hostPort > 0 && !hasExplicitPort ? $"{hostDomain}:{hostPort}" : hostDomain;
        builder.WebHost.UseUrls(listenUrl);
        return builder;
    }

    #endregion Private Methods
}