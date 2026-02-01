using System.Security.Cryptography;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public static class KeyGenerationExtensions
{
    public static IServiceCollection UseDefaultKeyGenerationMode(this IServiceCollection services)
    {
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
        return services;
    }
}