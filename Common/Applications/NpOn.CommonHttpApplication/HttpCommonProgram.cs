using Common.Applications.NpOn.CommonApplication;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;

namespace Common.Applications.NpOn.CommonHttpApplication;

public abstract class HttpCommonProgram : CommonProgram
{
    protected new readonly string[] Args;

    protected HttpCommonProgram(string[] args) : base(args)
    {
        Args = args;
    }

    protected override Task ConfigureServices(IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new GuidEmptyAsNullConverter());
        });
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