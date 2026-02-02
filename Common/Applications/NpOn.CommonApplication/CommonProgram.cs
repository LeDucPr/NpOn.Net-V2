using Common.Applications.NpOn.CommonApplication.Builders;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonApplication.Parameters;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Logging;

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
            .UseDefaultForwardHeaderOptionMode() // forward header options
            .UserLoggerDefaultMode() // logger
            .UseDefaultCompressMode(); // compress response

        services
            .UseDefaultKeyGenerationMode() // key generation
            .UseDefaultAuthorizationMode() // authorization 
            .UseDefaultAuthenticationMode(); // authentication
#if DEBUG
        if (EApplicationConfiguration.IsDevEnvironment.GetAppSettingConfig().AsDefaultBool()) // debug
            IdentityModelEventSource.ShowPII = true;
#endif
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

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
            });
        });

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