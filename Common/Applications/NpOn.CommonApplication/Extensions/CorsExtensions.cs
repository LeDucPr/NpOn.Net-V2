using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection UseCorsDefaultMode(this IServiceCollection services)
    {
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
        return services;
    } 
}