using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums.MarkerAttributes;
using Microsoft.Extensions.Configuration;

namespace Common.Extensions.NpOn.CommonMode;

public static class ApplicationConfigMode
{
    private static readonly IDictionary<Enum, string> Configs = new Dictionary<Enum, string>();

    extension(IConfiguration configuration)
    {
        // public void InitAllConfigs()
        // {
        //     var enumTypes = AppDomain.CurrentDomain.GetAssemblies()
        //         .SelectMany(a => a.GetTypes())
        //         .Where(t => t.IsEnum && t.IsDefined(typeof(AppConfigAttribute), false));
        //
        //     foreach (var enumType in enumTypes)
        //     {
        //         InitInternal(configuration, enumType);
        //     }
        // }

        public void InitConfigs(params Type[] enumTypes)
        {
            foreach (var type in enumTypes)
            {
                if (!type.IsEnum) continue;

                if (!type.IsDefined(typeof(AppConfigAttribute), false))
                    throw new InvalidOperationException($"{type.Name} need attribute [AppConfig] to init app config");

                InitInternal(configuration, type);
            }
        }
    }

    private static void InitInternal(IConfiguration configuration, Type enumType)
    {
        foreach (Enum key in Enum.GetValues(enumType))
        {
            if (Configs.ContainsKey(key)) continue;
            string keyConfig = key.GetDisplayName();
            var value = configuration.GetSection(keyConfig).Value;

            Configs.Add(key, value ?? string.Empty);
        }
    }

    public static string? GetAppSettingConfig(this EApplicationConfiguration e) => GetInternal(e);
    public static string? GetAppSettingConfig(this EUrlConfiguration e) => GetInternal(e);

    private static string? GetInternal(Enum e)
    {
        Configs.TryGetValue(e, out var value);
        return value;
    }
}