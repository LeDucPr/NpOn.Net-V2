using Common.Applications.NpOn.CommonApplication.Utils;
using Serilog;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public static class LoggerMode
{
    public static IServiceCollection UserLoggerDefaultMode(this IServiceCollection services)
    {
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
        return services;
    }
}