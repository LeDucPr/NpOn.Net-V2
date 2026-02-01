using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums.MarkerAttributes;
using Microsoft.Extensions.Configuration;

namespace Common.Extensions.NpOn.CommonMode;

public static class ApplicationConfigMode
{
    private static readonly IDictionary<Enum, string> Configs = new Dictionary<Enum, string>();

    public static void InitConfigs(this IConfiguration configuration, params Type[] enumTypes)
    {
        foreach (var type in enumTypes)
        {
            if (!type.IsEnum) continue;

            if (!type.IsDefined(typeof(AppConfigAttribute), false))
                throw new InvalidOperationException($"{type.Name} need attribute [AppConfig] to init app config");

            InitInternal(configuration, type);
        }
    }

    private static void InitInternal(IConfiguration configuration, Type enumType)
    {
        foreach (Enum key in Enum.GetValues(enumType))
        {
            if (Configs.ContainsKey(key)) continue;
            string keyConfig = key.GetDisplayName();
            string value;

            // The indexer `configuration[keyConfig]` can be unreliable for nested keys with some providers.
            // The most robust way is to traverse the sections manually.
            if (keyConfig.Contains(':'))
            {
                IConfigurationSection section = null;
                foreach (var part in keyConfig.Split(':'))
                {
                    section = (section == null) ? configuration.GetSection(part) : section.GetSection(part);
                }
                value = section?.Value;
                if (value?.Contains("Protocols") ?? false)
                {
                    var ccc = value;
                }
            }
            else
            {
                value = configuration[keyConfig];
            }

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