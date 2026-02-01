using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Microsoft.AspNetCore.ResponseCompression;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public static class CompressResponseExtensions
{
    public static IServiceCollection UseDefaultCompressMode(this IServiceCollection services)
    {
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
        return services;
    }
}