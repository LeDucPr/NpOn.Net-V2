using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;

namespace Common.Extensions.NpOn.HeaderConfig;

public static class InterServiceAuthConfigHelper
{
    public const string DefaultServiceMeshTrustDomain = "cluster.local";

    private static readonly string[] DefaultTrustedProxyIps = ["127.0.0.1", "::1"];

    public static EInterServiceAuthMode ResolveAuthMode()
    {
        var useMtlsFallback = EApplicationConfiguration.InterServiceUseMTLS.GetAppSettingConfig().AsDefaultBool();
        return ResolveAuthMode(useMtlsFallback);
    }

    public static EInterServiceAuthMode ResolveAuthMode(bool useMtlsFallback)
    {
        var configured = EApplicationConfiguration.InterServiceAuthMode.GetAppSettingConfig();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var parsed = configured.Trim().ToEnum<EInterServiceAuthMode>()
                         ?? configured.Trim().IntAsStringToEnum<EInterServiceAuthMode>();
            if (parsed.HasValue)
                return parsed.Value;
        }

        return useMtlsFallback ? EInterServiceAuthMode.Mtls : EInterServiceAuthMode.Jwt;
    }

    public static IReadOnlyList<string> GetAllowedIdentities()
    {
        return ParseCsvList(EApplicationConfiguration.InterServiceAllowedIdentities.GetAppSettingConfig());
    }

    public static IReadOnlyList<string> GetTrustedProxyIps()
    {
        var configured = ParseCsvList(EApplicationConfiguration.InterServiceTrustedProxyIps.GetAppSettingConfig());
        return configured.Count > 0 ? configured : DefaultTrustedProxyIps;
    }

    public static string GetServiceMeshTrustDomain()
    {
        var configured = EApplicationConfiguration.InterServiceServiceMeshTrustDomain.GetAppSettingConfig();
        return string.IsNullOrWhiteSpace(configured) ? DefaultServiceMeshTrustDomain : configured.Trim();
    }

    public static IReadOnlyList<string> GetServerSanHosts()
    {
        return ParseCsvList(EApplicationConfiguration.InterServiceServerSanHosts.GetAppSettingConfig());
    }

    public static IReadOnlyList<string> ParseCsvList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? [] : parts;
    }
}
