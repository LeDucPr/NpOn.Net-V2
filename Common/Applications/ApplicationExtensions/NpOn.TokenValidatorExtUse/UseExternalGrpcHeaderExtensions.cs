using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.HeaderConfig;

namespace Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse;

public static class UseExternalGrpcHeaderExtensions
{
    public static IServiceCollection AddExternalGrpcHeaderConfig(
        this IServiceCollection services,
        Action<ExternalGrpcHeaderOptions>? setupAction = null)
    {
        var options = new ExternalGrpcHeaderOptions();
        setupAction?.Invoke(options);

        // Register GrpcHeaderConfig as Scoped for ExternalServer mode
        services.AddScoped<GrpcHeaderConfig>(provider =>
        {
            var headerDict = new Dictionary<string, string>(options.DefaultHeaders);

            // Phân giải các trường cần thiết thông qua ContextService
            var contextService = provider.GetService<ContextService>();
            if (contextService != null)
            {
                var sessionId = contextService.GetSessionKey();
                if (!string.IsNullOrEmpty(sessionId))
                {
                    headerDict["x-npon-session-id"] = sessionId;
                }

                var ip = contextService.GetIp();
                if (!string.IsNullOrEmpty(ip))
                {
                    headerDict["x-forwarded-for"] = ip;
                }

                var userId = contextService.GetAccountIdAsString();
                if (!string.IsNullOrEmpty(userId))
                {
                    headerDict["x-npon-user-id"] = userId;
                }

                var languageId = contextService.LanguageId;
                if (!string.IsNullOrEmpty(languageId))
                {
                    headerDict["accept-language"] = languageId;
                }

                var clientId = contextService.ClientId;
                if (!string.IsNullOrEmpty(clientId))
                {
                    headerDict["x-npon-client-id"] = clientId;
                }
            }

            return new GrpcHeaderConfig(EGrpcEndUseType.ExternalServer, headerDict);
        });

        return services;
    }

    public static IServiceCollection AddExternalGrpcHeaderDefaultMode(this IServiceCollection services)
    {
        // Cấu hình cứng Dict default trong khi chưa hỗ trợ JSON sang YAML
        return services.AddExternalGrpcHeaderConfig(options =>
        {
            options.DefaultHeaders.Add("x-npon-origin", "external-grpc");
        });
    }
}
