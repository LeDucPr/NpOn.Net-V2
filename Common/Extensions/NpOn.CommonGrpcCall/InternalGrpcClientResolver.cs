using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.ClientFactory;

namespace NpOn.CommonGrpcCall;

public abstract class InternalGrpcClientResolver
{
    protected abstract Func<IServiceCollection, string, Task>? RegistrationAction { get; }

    public void SelfRegisterGrpcClientLoadBalancing(IServiceCollection services, string url)
    {
        services.AddTransient<InternalGrpcInterceptor>();
        if (RegistrationAction != null)
            RegistrationAction(services, url);
    }
}

public static class InternalGrpcClientResolverExtensions
{
    public static IServiceCollection AddConnectService // other service call this method
    (this IServiceCollection services, InternalGrpcClientResolver resolver, string? url = null,
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
        if (!address.StartsWith("http")) // http/3 neeed https 
            return;

        SocketsHttpHandler socketsHttpHandler = new SocketsHttpHandler()
        {
            // keeps connection alive
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay =
                TimeSpan.FromSeconds(
                    EApplicationConfiguration.KeepAlivePingDelaySeconds.GetAppSettingConfig().AsDefaultInt()),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(
                EApplicationConfiguration.KeepAlivePingTimeoutSeconds.GetAppSettingConfig().AsDefaultInt()),
            EnableMultipleHttp2Connections = true, // allows channel to add additional HTTP/2 connections
            EnableMultipleHttp3Connections = true, // allows channel to add additional HTTP/3 connections
            MaxConnectionsPerServer = int.MaxValue,
        };
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
        grpcClientFactoryOptions.Address = new Uri(address);
        grpcClientFactoryOptions.ChannelOptionsActions.Add(option =>
        {
            option.HttpHandler = socketsHttpHandler;
            option.MaxReceiveMessageSize = int.MaxValue;
            option.MaxSendMessageSize = int.MaxValue;
            option.Credentials = ChannelCredentials.Insecure;
            option.ServiceProvider = serviceProvider;
            option.ServiceConfig = new ServiceConfig
            {
                LoadBalancingConfigs = { new RoundRobinConfig() },
                MethodConfigs = { methodConfig }
            };
        });
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
                InternalGrpcInterceptor loggerInterceptor = provider.GetRequiredService<InternalGrpcInterceptor>();
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
                    InternalGrpcInterceptor loggerInterceptor = provider.GetRequiredService<InternalGrpcInterceptor>();
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
}