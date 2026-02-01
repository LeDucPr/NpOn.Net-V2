using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Grpc.Net.Client.Balancer;
using ProtoBuf.Grpc.Server;

namespace NpOn.AddGrpcAppExtUse;

public static class AddGrpcServiceCollectionExtensions
{
    public static IServiceCollection AddGrpcDefaultMode(this IServiceCollection services)
    {
        // common grpc
        services.AddCodeFirstGrpc(config =>
        {
            config.ResponseCompressionLevel = System.IO.Compression.CompressionLevel.NoCompression;
            config.MaxReceiveMessageSize = int.MaxValue;
            config.MaxSendMessageSize = int.MaxValue;
            //config.Interceptors.Add<>();
        });

        // internal service ??
        if (EApplicationConfiguration.IsUseEnableUnencryptedMode.GetAppSettingConfig().AsDefaultBool())
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        int dnsRefreshSeconds = EApplicationConfiguration.DnsRefreshInterval.GetAppSettingConfig().AsDefaultInt();
        services.AddSingleton<ResolverFactory>(
            new DnsResolverFactory(refreshInterval: TimeSpan.FromSeconds(dnsRefreshSeconds)));
        services.AddGrpc();
        return services;
    }
}