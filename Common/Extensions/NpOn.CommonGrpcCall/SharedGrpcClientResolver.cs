using System.Security.Cryptography.X509Certificates;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.ClientFactory;

namespace NpOn.CommonGrpcCall;

public abstract class SharedGrpcClientResolver
{
    protected abstract Func<IServiceCollection, string, Task>? RegistrationAction { get; }

    public void SelfRegisterGrpcClientLoadBalancing(IServiceCollection services, string url)
    {
        services.AddTransient<SharedGrpcInterceptor>();
        if (RegistrationAction != null)
            RegistrationAction(services, url);
    }
}

public static class SharedGrpcClientResolverExtensions
{
    public static IServiceCollection AddConnectService // other service call this method
    (this IServiceCollection services, SharedGrpcClientResolver resolver, string? url = null,
        EUrlConfiguration? eUrlConfiguration = null)
    {
        if (eUrlConfiguration != null)
        {
            string configuration = ((EUrlConfiguration)eUrlConfiguration).GetAppSettingConfig().AsEmptyString();
            url = configuration;
        }

        if (!string.IsNullOrWhiteSpace(url))
            resolver.SelfRegisterGrpcClientLoadBalancing(services, url);
        return services;
    }

    private static void ConfigGrpcClientOptions(GrpcClientFactoryOptions grpcClientFactoryOptions, string address,
        IServiceProvider serviceProvider)
    {
        if (!address.StartsWith("http", StringComparison.OrdinalIgnoreCase)) // http/3 need https 
            return;

        EInterServiceAuthMode authMode = InterServiceAuthConfigHelper.ResolveAuthMode();
        string effectiveAddress = ResolveEffectiveAddress(address, authMode);

        int keepAliveDelaySec = EApplicationConfiguration.KeepAlivePingDelaySeconds.GetAppSettingConfig().AsDefaultInt();
        int keepAliveTimeoutSec = EApplicationConfiguration.KeepAlivePingTimeoutSeconds.GetAppSettingConfig().AsDefaultInt();

        SocketsHttpHandler socketsHttpHandler = new SocketsHttpHandler()
        {
            // keeps connection alive
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            EnableMultipleHttp2Connections = true, // allows channel to add additional HTTP/2 connections
            EnableMultipleHttp3Connections = authMode != EInterServiceAuthMode.ServiceMesh,
            MaxConnectionsPerServer = int.MaxValue
        };

        if (keepAliveDelaySec > 0)
            socketsHttpHandler.KeepAlivePingDelay = TimeSpan.FromSeconds(keepAliveDelaySec);
        if (keepAliveTimeoutSec > 0)
            socketsHttpHandler.KeepAlivePingTimeout = TimeSpan.FromSeconds(keepAliveTimeoutSec);

        if (authMode == EInterServiceAuthMode.Mtls)
        {
            socketsHttpHandler.PooledConnectionLifetime = TimeSpan.FromMinutes(15);
            var certService = serviceProvider.GetService(typeof(IInterServiceCertificateService)) as IInterServiceCertificateService;
            var clientCert = certService?.GetClientCertificate();
            if (clientCert != null)
            {
                socketsHttpHandler.SslOptions.ClientCertificates = new X509CertificateCollection { clientCert };
            }

            socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback = (_, cert, _, _) =>
            {
                if (certService == null || cert == null)
                    return false;

                if (cert is X509Certificate2 x509)
                    return certService.ValidateServerCertificate(x509);

                using var loaded = X509CertificateLoader.LoadCertificate(cert.GetRawCertData());
                return certService.ValidateServerCertificate(loaded);
            };
        }
        else if (authMode == EInterServiceAuthMode.Jwt)
        {
            socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback = delegate { return true; };
        }

        var methodConfig = new MethodConfig
        {
            Names = { MethodName.Default },
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 5,
                InitialBackoff = TimeSpan.FromSeconds(1),
                MaxBackoff = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 1.5,
                RetryableStatusCodes = { StatusCode.Unavailable }
            }
        };
        grpcClientFactoryOptions.Address = new Uri(effectiveAddress);
        grpcClientFactoryOptions.ChannelOptionsActions.Add(option =>
        {
            option.HttpHandler = socketsHttpHandler;
            option.MaxReceiveMessageSize = int.MaxValue;
            option.MaxSendMessageSize = int.MaxValue;
            if (authMode != EInterServiceAuthMode.Mtls)
            {
                option.Credentials = ChannelCredentials.Insecure;
                option.UnsafeUseInsecureChannelCallCredentials = true; // internal h2c mode (Jwt or ServiceMesh)
            }
            option.ServiceProvider = serviceProvider;
            option.ServiceConfig = new ServiceConfig
            {
                LoadBalancingConfigs = { new RoundRobinConfig() },
                MethodConfigs = { methodConfig }
            };
        });
    }

    private static string ResolveEffectiveAddress(string address, EInterServiceAuthMode authMode)
    {
        if (authMode == EInterServiceAuthMode.Mtls &&
            address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + address["http://".Length..];
        }

        if (authMode == EInterServiceAuthMode.ServiceMesh &&
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "http://" + address["https://".Length..];
        }

        return address;
    }

    public static IServiceCollection RegisterGrpcClientLoadBalancing<T>(this IServiceCollection services, string? url)
        where T : class
    {
        if (url is not { Length: > 0 })
        {
            return services;
        }

        services.AddCodeFirstGrpcClient<T>((provider, options) => { ConfigGrpcClientOptions(options, url, provider); })
            .AddInterceptor(provider =>
            {
                SharedGrpcInterceptor loggerInterceptor = provider.GetRequiredService<SharedGrpcInterceptor>();
                loggerInterceptor.Host = url;
                return loggerInterceptor;
            });
        return services;
    }

    public static IServiceCollection RegisterGrpcClientLoadBalancing<T>(this IServiceCollection services, string? url,
        string? name)
        where T : class
    {
        if (url is not { Length: > 0 })
        {
            return services;
        }

        if (name?.Length > 0)
        {
            services.AddCodeFirstGrpcClient<T>(name,
                    (provider, options) => { ConfigGrpcClientOptions(options, url, provider); })
                .AddInterceptor(provider =>
                {
                    SharedGrpcInterceptor loggerInterceptor = provider.GetRequiredService<SharedGrpcInterceptor>();
                    loggerInterceptor.Host = url;
                    return loggerInterceptor;
                });
        }
        else
        {
            RegisterGrpcClientLoadBalancing<T>(services, url);
        }

        return services;
    }

    /// <summary>
    /// Registers a resilient, fault-tolerant gRPC client with options for optional DI, graceful degradation, and Saga Outbox fallback.
    /// Prevents caller service crashes when target service is offline or unconfigured.
    /// </summary>
    public static IServiceCollection RegisterResilientGrpcClient<T>(
        this IServiceCollection services,
        string? url,
        Action<ResilientGrpcClientOptions>? configure = null)
        where T : class
    {
        var options = new ResilientGrpcClientOptions();
        configure?.Invoke(options);

        if (string.IsNullOrWhiteSpace(url))
        {
            if (options.IsOptional)
            {
                // Optional dependency: do not crash on missing URL.
                return services;
            }
            throw new ArgumentException($"gRPC service URL for '{typeof(T).Name}' must not be null or empty when IsOptional is false.");
        }

        services.AddSingleton(options);
        services.AddTransient<ResilientGrpcInterceptor>();

        services.AddCodeFirstGrpcClient<T>((provider, clientOptions) => { ConfigGrpcClientOptions(clientOptions, url, provider); })
            .AddInterceptor(provider =>
            {
                var resilientInterceptor = provider.GetRequiredService<ResilientGrpcInterceptor>();
                resilientInterceptor.Host = url;
                return resilientInterceptor;
            })
            .AddInterceptor(provider =>
            {
                SharedGrpcInterceptor loggerInterceptor = provider.GetRequiredService<SharedGrpcInterceptor>();
                loggerInterceptor.Host = url;
                return loggerInterceptor;
            });

        return services;
    }
}
