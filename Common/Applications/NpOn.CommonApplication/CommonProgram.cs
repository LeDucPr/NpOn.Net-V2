using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Common.Extensions.NpOn.CommonApplication.Builders;
using Common.Extensions.NpOn.CommonApplication.Parameters;
using Common.Extensions.NpOn.CommonApplication.Utils;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.CommonWebApplication.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Serilog;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

namespace Common.Extensions.NpOn.CommonApplication;

public abstract class CommonProgram
{
    protected readonly string[] Args;

    protected CommonProgram(string[] args)
    {
        Args = args;
    }

    protected async Task RunAsync()
    {
        var builder = CreateDefaultBuilder(Args);
        builder.Configuration.InitGlobal();
        await builder.Services.AddCollectionServices(async (services) =>
        {
            ConfigureBaseServices(services);
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

        // [FIX] Configure ForwardedHeaders to correctly identify the Protocol (HTTPS) when running behind a Reverse Proxy (Docker/Nginx)

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            bool isUseMultiDomainInHost =
                EApplicationConfiguration.IsUseMultiDomainInHost.GetAppSettingConfig().AsDefaultBool();
            if (isUseMultiDomainInHost)
                options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;
            options.KnownNetworks.Clear(); // Trust internal docker networks
            options.KnownProxies.Clear();
        });

        // cors
        string corsConfig = EApplicationConfiguration.CORS.GetAppSettingConfig().AsEmptyString();
        if (corsConfig.Length > 0)
        {
            services.AddCors(options =>
            {
                string[] configs = corsConfig
                    .Split(',')
                    .Select(p => p.Trim())
                    .ToArray();
                string autoAddCredential =
                    EApplicationConfiguration.AutoAddCredential.GetAppSettingConfig().AsDefaultString();
                if (autoAddCredential is { Length : > 0 })
                    configs = configs.Select(p => p.StartsWith(autoAddCredential) ? p : $"{autoAddCredential}://" + p)
                        .ToArray();

                options.AddPolicy(EApplicationConfiguration.CorsPolicy.GetAppSettingConfig().AsDefaultString(),
                    policyBuilder =>
                    {
                        // If config contains "*" -> Allow all Origins (including file://) + Credentials
                        //  Use SetIsOriginAllowed to allow 'null' origin (local file) and others dynamically
                        if (configs.Contains("*"))
                            policyBuilder.SetIsOriginAllowed(_ => true);
                        else
                            policyBuilder.WithOrigins(configs);
                        policyBuilder.AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();

                        if (EApplicationConfiguration.IsUseTusMedia.GetAppSettingConfig().AsDefaultBool())
                        {
                            policyBuilder.WithExposedHeaders("Upload-Offset", "Location", "Upload-Length",
                                "Tus-Version",
                                "Tus-Resumable", "Tus-Max-Size", "Tus-Extension", "Upload-Metadata",
                                "Upload-Defer-Length", "Upload-Concat", "X-Media-Download-Url");
                        }
                    }
                );
            });
        }

        // logger
        services.AddSingleton<ILogAction, LogAction>();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: "logs/app-.txt",
                rollingInterval: RollingInterval.Day, // Auto create new file in next day 
                shared: true, // no lock
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] (Instance:{InstanceId}) {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
        services.AddLogging(p => p.AddSerilog(Log.Logger)); // add log (console)

        // // authentication 
        // services.AddTransient<AuthenticationToken>();
        // services.AddTransient<ContextService>();
        // services.AddTransient<AuthenService>();
        // services.AddSingleton<PermissionService>();

        // // common controllers
        // services.AddControllers().AddJsonOptions(options =>
        // {
        //     options.JsonSerializerOptions.DefaultIgnoreCondition =
        //         System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        //     options.JsonSerializerOptions.Converters.Add(new GuidEmptyAsNullConverter());
        // });


        if (EApplicationConfiguration.IsUseResponseCompression.GetAppSettingConfig().AsDefaultBool())
        {
            IEnumerable<string> mimeTypes = ResponseCompressionDefaults.MimeTypes;
            if (EApplicationConfiguration.IsUseResponseCompressionExt.GetAppSettingConfig().AsDefaultBool())
            {
                // Add the data types you want to compress
                mimeTypes = mimeTypes.Concat(["application/octet-stream", "application/json"]);
            }

            services.AddResponseCompression(options =>
            {
                //.NET disables compression over HTTPS for security reasons (BREACH attack)
                options.EnableForHttps = true;
                options.MimeTypes = mimeTypes;
            });
        }


        // // common grpc
        // services.AddCodeFirstGrpc(config =>
        // {
        //     config.ResponseCompressionLevel = System.IO.Compression.CompressionLevel.NoCompression;
        //     config.MaxReceiveMessageSize = int.MaxValue;
        //     config.MaxSendMessageSize = int.MaxValue;
        //     //config.Interceptors.Add<>();
        // });
        // services.RegisterGrpcClientLoadBalancing(); // add DI multi Services
        // AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        // int dnsRsvF = EApplicationConfiguration.DnsResolverFactory.GetAppSettingConfig().AsDefaultInt();
        // services.AddSingleton<ResolverFactory>(new DnsResolverFactory(refreshInterval: TimeSpan.FromSeconds(dnsRsvF)));
        // services.AddGrpc();


        // // rabbitMq
        // bool isUseRabbitMq = EApplicationConfiguration.IsUseRabbitMq.GetAppSettingConfig().AsDefaultBool();
        // if (isUseRabbitMq)
        // {
        //     string rabbitCnStr = EApplicationConfiguration.RabbitMqConnection.GetAppSettingConfig().AsDefaultString();
        //     string exName = EApplicationConfiguration.RabbitMqExchangeName.GetAppSettingConfig().AsDefaultString();
        //     RabbitMqConnection rabbitMqConnection = new RabbitMqConnection(rabbitCnStr, exName);
        //     // Connection and Producer of RabbitMQ must be Singleton to keep TCP connection
        //     services.AddSingleton<IRabbitMqConnection>(rabbitMqConnection);
        //     services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();
        // }

        // // kafka
        // bool isUseKafka = EApplicationConfiguration.IsUseKafka.GetAppSettingConfig().AsDefaultBool();
        // if (isUseKafka)
        // {
        //     string kafkaCnStr = EApplicationConfiguration.KafkaConnection.GetAppSettingConfig().AsDefaultString();
        //     string topicName = EApplicationConfiguration.KafkaTopicName.GetAppSettingConfig().AsDefaultString();
        //     // kafka auth
        //     string saslUsername = EApplicationConfiguration.SaslUsername.GetAppSettingConfig().AsDefaultString();
        //     string saslPassword = EApplicationConfiguration.SaslPassword.GetAppSettingConfig().AsDefaultString();
        //
        //     KafkaClientConfigBuilder configBuilder = new KafkaClientConfigBuilder();
        //     configBuilder.SetServerUrl(kafkaCnStr);
        //     if (!string.IsNullOrEmpty(saslUsername) && !string.IsNullOrEmpty(saslPassword))
        //         // SASL authentication (currently only this mechanism is supported =)))
        //         configBuilder.SetUseSasl(saslUsername, saslPassword, SaslMechanism.ScramSha256);
        //     KafkaTopic kafkaTopic = KafkaTopic.Create(configBuilder.Build(), topicName)
        //         .GetAwaiter()
        //         .GetResult();
        //     services.AddSingleton<IKafkaTopic>(kafkaTopic);
        //     services.AddSingleton<IKafkaProducer, KafkaProducer>();
        // }

        ////// Controller
        // authorization 
        services.AddAuthorization();
        // authorization policy
        services.AddAuthorization(options =>
        {
            var defaultAuthorizationPolicyBuilder = new AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme);
            defaultAuthorizationPolicyBuilder = defaultAuthorizationPolicyBuilder.RequireAuthenticatedUser();
            options.DefaultPolicy = defaultAuthorizationPolicyBuilder.Build();
        });

        // authentication
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "BearerOrCookie";
                options.DefaultChallengeScheme = "BearerOrCookie";
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy =
                    EApplicationConfiguration.IsDevEnvironment.GetAppSettingConfig().AsDefaultBool()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Unspecified;
                options.Cookie.Name =
                    EApplicationConfiguration.CookieAuthenName.GetAppSettingConfig().AsDefaultString();
                options.LoginPath = string.Empty; //"api/Account/Login";
                options.LogoutPath = string.Empty; //"api/Account/Logout";
                options.AccessDeniedPath = string.Empty;
                string cookieDomain =
                    EApplicationConfiguration.CookieDomain.GetAppSettingConfig().AsDefaultString();
                if (cookieDomain.Length > 0)
                {
                    options.Cookie.Domain = cookieDomain;
                }

                options.Events.OnRedirectToLogin = context =>
                {
                    // Always return 401 Unauthorized for API instead of Redirect (to avoid CORS errors or returning an HTML login page)
                    // Since LoginPath is empty, returning 401 is the most appropriate behavior for both Dev and Prod.
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return Task.CompletedTask;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                string jwtKey = EApplicationConfiguration.JwtTokensKey.GetAppSettingConfig().AsDefaultString();
                byte[] key = Encoding.ASCII.GetBytes(jwtKey);
                string[] validIssuers = EApplicationConfiguration.ValidIssuers.GetAppSettingConfig()
                    ?.AsEmptyString().Split(",").Select(x => x.AsEmptyString()).ToArray() ?? [];
                string[] validAudiences = EApplicationConfiguration.ValidAudiences.GetAppSettingConfig()
                    ?.AsEmptyString().Split(",").Select(x => x.AsEmptyString()).ToArray() ?? [];
                bool isUseValidIssuers = validIssuers is { Length: > 0 };
                bool isUseValidAudiences = validAudiences is { Length: > 0 };

                options.RequireHttpsMetadata = false; // Use false only in dev environment
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = isUseValidIssuers, // Customize if you have specific issuers
                    ValidateAudience = isUseValidAudiences, // Customize if you have specific audiences
                    ValidIssuers = validIssuers,
                    ValidAudiences = validAudiences,
                    // ValidateLifetime = true,
                };
            })
            .AddPolicyScheme("BearerOrCookie", "Bearer or Cookie", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    string? authorization = context.Request.Headers[HeaderNames.Authorization];
                    if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
                        return JwtBearerDefaults.AuthenticationScheme;
                    return CookieAuthenticationDefaults.AuthenticationScheme;
                };
            });
#if DEBUG
        if (EApplicationConfiguration.IsDevEnvironment.GetAppSettingConfig().AsDefaultBool()) // debug
            IdentityModelEventSource.ShowPII = true;
#endif

        string appName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();
        bool disableAutoKeyGen = !EApplicationConfiguration.IsUseDataProtectionAutomaticKeyGeneration
            .GetAppSettingConfig().AsDefaultBool();
        string keyPath;
        if (OperatingSystem.IsLinux() && Directory.Exists("/home/app"))
            keyPath = Path.Combine("/home/app", ".aspnet", appName, "DataProtection-Keys");
        else // Windows hoặc Linux Desktop thông thường
            keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName,
                "DataProtection-Keys");
        try
        {
            if (!Directory.Exists(keyPath))
                Directory.CreateDirectory(keyPath);
        }
        catch (Exception ex)
        {
            // Create folder (permission denied)
            Console.WriteLine($"[Error] Could not create KeyPath: {keyPath}. Exception: {ex.Message}");
            // gán lại keyPath về thư mục tạm (Temp) để né crash khởi động
            keyPath = Path.Combine(Path.GetTempPath(), appName, "Keys");
            Directory.CreateDirectory(keyPath);
        }

        IDataProtectionBuilder dataProtectionBuilder = services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
            .SetApplicationName(appName)
            // nếu dùng chung host trong dùng một cổng thì cần dùng config name chung cho các app 
            // và bật IsUseMultiDomainInHost -> true -> (cấu hình cho enable ForwardedHeaders.XForwardedHost)
            .UseCustomCryptographicAlgorithms(new ManagedAuthenticatedEncryptorConfiguration()
            {
                EncryptionAlgorithmType = typeof(Aes),
                EncryptionAlgorithmKeySize = 256,
                ValidationAlgorithmType = typeof(HMACSHA512)
            });
        if (disableAutoKeyGen)
            dataProtectionBuilder.DisableAutomaticKeyGeneration();
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
        app.MapControllers();

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

public class GuidEmptyAsNullConverter : System.Text.Json.Serialization.JsonConverter<Guid?>
{
    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value == null || value == Guid.Empty)
        {
            // null → JSON engine will be ignored property WhenWritingNull
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }

    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetGuid();
    }
}