using Common.Applications.NpOn.CommonApplication;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Grpc.Net.Client.Balancer;
using ProtoBuf.Grpc.Server;

namespace Common.Applications.NpOn.CommonGrpcApplication;

public abstract class GrpcCommonProgram(string[] args) : CommonProgram(args)
{
    protected override Task ConfigureServices(IServiceCollection services)
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
        
        return Task.CompletedTask;
    }

    protected override void ConfigureBasePipeline(WebApplication app)
    {
        string appName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();
        app.MapGet("/", () => $"NpOn.{appName}");
        base.ConfigureBasePipeline(app);
    }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        // Add Map Grpc Service ??
        return Task.CompletedTask;
    }
}