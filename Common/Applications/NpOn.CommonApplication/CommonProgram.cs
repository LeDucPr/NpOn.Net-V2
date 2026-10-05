using Common.Applications.NpOn.CommonApplication.Builders;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonApplication.Parameters;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
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
        builder.Configuration.InitConfigs(
            typeof(EApplicationConfiguration),
            typeof(EUrlConfiguration)
        );
        await builder.Services.AddCollectionServices(async (services) =>
        {
#pragma warning disable CS0618 // Type or member is obsolete
            ConfigureBaseServices(services);
#pragma warning restore CS0618 // Type or member is obsolete
            await ConfigureServices(services);

            // Add certificate rotation background service if mTLS is enabled
            var useMtls = EApplicationConfiguration.InterServiceUseMTLS.GetAppSettingConfig().AsDefaultBool();
            var autoGenerate = EApplicationConfiguration.InterServiceAutoGenerateCertificates.GetAppSettingConfig()
                .AsDefaultBool(true);
            if (useMtls && autoGenerate)
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
        // Register certificate service for mTLS
        var useMtls = EApplicationConfiguration.InterServiceUseMTLS.GetAppSettingConfig().AsDefaultBool();
        if (useMtls)
        {
            var clientCertPath = EApplicationConfiguration.InterServiceClientCertPath.GetAppSettingConfig()
                .AsDefaultString();
            var clientCertPassword = EApplicationConfiguration.InterServiceClientCertPassword.GetAppSettingConfig()
                .AsDefaultString();
            var serverCertPath = EApplicationConfiguration.InterServiceServerCertPath.GetAppSettingConfig()
                .AsDefaultString();
            var serverCertPassword = EApplicationConfiguration.InterServiceServerCertPassword.GetAppSettingConfig()
                .AsDefaultString();
            var caCertPath = EApplicationConfiguration.InterServiceCACertPath.GetAppSettingConfig().AsDefaultString();

            services.AddSingleton<IInterServiceCertificateService>(new InterServiceCertificateService(
                clientCertPath,
                clientCertPassword,
                serverCertPath,
                serverCertPassword,
                caCertPath));

            // Register certificate generator for auto-generation
            var autoGenerate = EApplicationConfiguration.InterServiceAutoGenerateCertificates.GetAppSettingConfig()
                .AsDefaultBool();
            if (autoGenerate)
            {
                var storagePath = EApplicationConfiguration.InterServiceCertificateStoragePath.GetAppSettingConfig()
                    .AsDefaultString("/certs");
                var validityDays = EApplicationConfiguration.InterServiceCertificateValidityDays.GetAppSettingConfig()
                    .AsDefaultInt(90);

                services.AddSingleton<ICertificateGenerator>(sp => new CertificateGenerator(
                    sp.GetRequiredService<ILogger<CertificateGenerator>>(),
                    storagePath));

                services.AddSingleton<ICertificateRotationService>(sp => new CertificateRotationService(
                    sp.GetRequiredService<ICertificateGenerator>(),
                    sp.GetRequiredService<IInterServiceCertificateService>(),
                    sp.GetRequiredService<ILogger<CertificateRotationService>>(),
                    caCertPath,
                    validityDays));
            }
        }

        // Register JWT token service as fallback
        var interServiceSecret =
            EApplicationConfiguration.InterServiceSecretKey.GetAppSettingConfig().AsDefaultString();
        var tokenLifetimeMinutes = EApplicationConfiguration.InterServiceTokenLifetimeMinutes.GetAppSettingConfig()
            .AsDefaultInt();
        var keyRotationMinutes = EApplicationConfiguration.InterServiceKeyRotationMinutes.GetAppSettingConfig()
            .AsDefaultInt();
        services.AddSingleton<IInterServiceTokenService>(new InterServiceTokenService(
            interServiceSecret,
            TimeSpan.FromMinutes(tokenLifetimeMinutes),
            TimeSpan.FromMinutes(keyRotationMinutes)));
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

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        // Inter-service authentication middleware for gRPC
        if (EApplicationConfiguration.IsUseGrpcStandardMode.GetAppSettingConfig().AsDefaultBool())
        {
            var useMtls = EApplicationConfiguration.InterServiceUseMTLS.GetAppSettingConfig().AsDefaultBool();
            app.UseGrpcServerAuth(requireAuth: true, useMtls: useMtls);
        }

        if (UseControllers)
        {
            app.MapControllers();
        }

        string appName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();
        app.MapGet("/", () => appName);
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

        // host-domain-start
        string hostDomain = builder.Configuration.TryGetConfig(EApplicationConfiguration.HostDomain).AsDefaultString();
        var hostPort = builder.Configuration.TryGetConfig(EApplicationConfiguration.HostPort).AsDefaultInt();
        if (hostPort > 0)
            hostDomain = $"{hostDomain}:{hostPort}";
        if (string.IsNullOrWhiteSpace(hostDomain))
            throw new Exception(EWebApplicationError.HostDomain.GetDisplayName());
        builder.WebHost.UseUrls($"{hostDomain}:{hostPort}");
        return builder;
    }

    #endregion Private Methods
}