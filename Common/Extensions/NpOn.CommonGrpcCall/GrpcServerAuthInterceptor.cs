using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.HeaderConfig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NpOn.CommonGrpcCall;

public class GrpcServerAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GrpcServerAuthMiddleware> _logger;
    private readonly bool _requireAuth;
    private readonly EInterServiceAuthMode _authMode;
    private readonly string _serviceMeshTrustDomain;
    private readonly IReadOnlyList<IpCidrRule> _trustedProxies;
    private readonly HashSet<string>? _allowedIdentities;

    public GrpcServerAuthMiddleware(
        RequestDelegate next,
        IServiceProvider serviceProvider,
        ILogger<GrpcServerAuthMiddleware> logger,
        bool requireAuth = true,
        bool useMtls = false)
        : this(
            next,
            serviceProvider,
            logger,
            requireAuth,
            useMtls,
            useMtls ? EInterServiceAuthMode.Mtls : EInterServiceAuthMode.Jwt,
            null,
            null,
            null)
    {
    }

    public GrpcServerAuthMiddleware(
        RequestDelegate next,
        IServiceProvider serviceProvider,
        ILogger<GrpcServerAuthMiddleware> logger,
        bool requireAuth,
        bool useMtls,
        EInterServiceAuthMode authMode,
        string? serviceMeshTrustDomain = null,
        IReadOnlyCollection<string>? trustedProxyIps = null,
        IReadOnlyCollection<string>? allowedIdentities = null)
    {
        _ = useMtls;
        _next = next;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _requireAuth = requireAuth;
        _authMode = authMode;
        _serviceMeshTrustDomain = !string.IsNullOrWhiteSpace(serviceMeshTrustDomain)
            ? serviceMeshTrustDomain.Trim()
            : InterServiceAuthConfigHelper.GetServiceMeshTrustDomain();

        var proxyList = trustedProxyIps is { Count: > 0 }
            ? trustedProxyIps
            : InterServiceAuthConfigHelper.GetTrustedProxyIps();
        _trustedProxies = ParseProxyRules(proxyList);

        var identityList = allowedIdentities ?? InterServiceAuthConfigHelper.GetAllowedIdentities();
        _allowedIdentities = identityList.Count > 0
            ? new HashSet<string>(identityList.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase)
            : null;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_requireAuth)
        {
            await _next(context);
            return;
        }

        // GrpcServerAuthMiddleware strictly protects inter-service gRPC traffic.
        var endpoint = context.GetEndpoint();
        bool isAllowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null;
        bool isGrpc = context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true;
        bool isRestApi = context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

        if (isAllowAnonymous || isRestApi || !isGrpc)
        {
            await _next(context);
            return;
        }

        bool authenticated = _authMode switch
        {
            EInterServiceAuthMode.Mtls => TryAuthenticateMtls(context),
            EInterServiceAuthMode.ServiceMesh => TryAuthenticateServiceMesh(context),
            _ => TryAuthenticateJwt(context)
        };

        if (authenticated)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }

    private bool TryAuthenticateMtls(HttpContext context)
    {
        if (_serviceProvider.GetService(typeof(IInterServiceCertificateService)) is not IInterServiceCertificateService certificateService)
        {
            _logger.LogWarning("mTLS authentication failed: IInterServiceCertificateService is not registered.");
            return false;
        }

        var clientCert = context.Connection.ClientCertificate;
        if (clientCert == null)
        {
            _logger.LogWarning("mTLS authentication failed from {RemoteIpAddress}: no client certificate presented.",
                context.Connection.RemoteIpAddress);
            return false;
        }

        if (!certificateService.ValidateClientCertificate(clientCert))
        {
            _logger.LogWarning("mTLS certificate validation failed from {RemoteIpAddress}, Subject={Subject}",
                context.Connection.RemoteIpAddress, clientCert.Subject);
            return false;
        }

        certificateService.TryExtractServiceIdentity(clientCert, out string? identity);
        if (!IsIdentityAllowed(identity, clientCert.Subject))
        {
            _logger.LogWarning("mTLS identity {Identity} is not authorized from {RemoteIpAddress}",
                identity ?? clientCert.Subject, context.Connection.RemoteIpAddress);
            return false;
        }

        context.Items["AuthMethod"] = "mTLS";
        context.Items["ClientCertSubject"] = clientCert.Subject;
        if (!string.IsNullOrWhiteSpace(identity))
            context.Items["ServiceName"] = identity;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Authenticated via mTLS from {RemoteIpAddress}, Subject: {Subject}",
                context.Connection.RemoteIpAddress, clientCert.Subject);
        }

        return true;
    }

    private bool TryAuthenticateServiceMesh(HttpContext context)
    {
        if (!IsTrustedProxy(context.Connection.RemoteIpAddress))
        {
            _logger.LogWarning("ServiceMesh authentication failed: untrusted remote IP {RemoteIpAddress}",
                context.Connection.RemoteIpAddress);
            return false;
        }

        var xfccHeader = context.Request.Headers[DefaultHeaderConstant.GrpcForwardedClientCert].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(xfccHeader))
        {
            _logger.LogWarning("ServiceMesh authentication failed from {RemoteIpAddress}: missing {Header}",
                context.Connection.RemoteIpAddress, DefaultHeaderConstant.GrpcForwardedClientCert);
            return false;
        }

        if (!TryParseXfcc(xfccHeader, _serviceMeshTrustDomain, out var spiffeId, out var serviceName, out var subject))
        {
            _logger.LogWarning("ServiceMesh authentication failed from {RemoteIpAddress}: invalid or untrusted XFCC header",
                context.Connection.RemoteIpAddress);
            return false;
        }

        if (!IsIdentityAllowed(spiffeId, serviceName, subject))
        {
            _logger.LogWarning("ServiceMesh identity {SpiffeId} / {ServiceName} is not authorized",
                spiffeId ?? subject, serviceName);
            return false;
        }

        context.Items["AuthMethod"] = "ServiceMesh";
        if (!string.IsNullOrWhiteSpace(spiffeId))
            context.Items["SpiffeId"] = spiffeId;
        if (!string.IsNullOrWhiteSpace(serviceName))
            context.Items["ServiceName"] = serviceName;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Authenticated via ServiceMesh from {ServiceName} ({SpiffeId})",
                serviceName, spiffeId);
        }

        return true;
    }

    private bool TryAuthenticateJwt(HttpContext context)
    {
        if (_serviceProvider.GetService(typeof(IInterServiceTokenService)) is not IInterServiceTokenService tokenService)
        {
            _logger.LogWarning("JWT authentication failed from {RemoteIpAddress}: IInterServiceTokenService not registered.",
                context.Connection.RemoteIpAddress);
            return false;
        }

        var token = context.Request.Headers[DefaultHeaderConstant.GrpcInterServiceAuthToken].FirstOrDefault();
        var headerServiceName = context.Request.Headers[DefaultHeaderConstant.GrpcInterServiceServiceName].FirstOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("JWT authentication failed from {RemoteIpAddress}: missing token.",
                context.Connection.RemoteIpAddress);
            return false;
        }

        if (!tokenService.ValidateToken(token, out var principal) || principal == null)
        {
            _logger.LogWarning("JWT token validation failed from {RemoteIpAddress}",
                context.Connection.RemoteIpAddress);
            return false;
        }

        var serviceClaim = principal.FindFirst("service_name")?.Value;
        var resolvedService = !string.IsNullOrWhiteSpace(serviceClaim) ? serviceClaim : headerServiceName;

        if (!IsIdentityAllowed(resolvedService))
        {
            _logger.LogWarning("JWT identity {ServiceName} is not authorized from {RemoteIpAddress}",
                resolvedService, context.Connection.RemoteIpAddress);
            return false;
        }

        context.Items["AuthMethod"] = "JWT";
        context.Items["ServiceName"] = resolvedService;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Authenticated via JWT from {ServiceName} at {RemoteIpAddress}",
                resolvedService, context.Connection.RemoteIpAddress);
        }

        return true;
    }

    private bool IsIdentityAllowed(params string?[] candidates)
    {
        if (_allowedIdentities == null || _allowedIdentities.Count == 0)
            return true;

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && _allowedIdentities.Contains(candidate.Trim()))
                return true;
        }

        return false;
    }

    private bool IsTrustedProxy(IPAddress? remoteIp)
    {
        if (remoteIp == null)
            return false;

        var normalized = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;
        foreach (var rule in _trustedProxies)
        {
            if (rule.Matches(normalized))
                return true;
        }

        return false;
    }

    private static List<IpCidrRule> ParseProxyRules(IReadOnlyCollection<string> rules)
    {
        var parsed = new List<IpCidrRule>(rules.Count);
        foreach (var raw in rules)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var trimmed = raw.Trim();
            int slashIdx = trimmed.IndexOf('/');
            if (slashIdx > 0)
            {
                if (IPAddress.TryParse(trimmed[..slashIdx], out var netIp) &&
                    int.TryParse(trimmed[(slashIdx + 1)..], out int prefixLen))
                {
                    var normalizedNet = netIp.IsIPv4MappedToIPv6 ? netIp.MapToIPv4() : netIp;
                    parsed.Add(new IpCidrRule(normalizedNet, prefixLen));
                }
            }
            else if (IPAddress.TryParse(trimmed, out var singleIp))
            {
                var normalizedIp = singleIp.IsIPv4MappedToIPv6 ? singleIp.MapToIPv4() : singleIp;
                int maxBits = normalizedIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
                parsed.Add(new IpCidrRule(normalizedIp, maxBits));
            }
        }

        return parsed;
    }

    internal static bool TryParseXfcc(
        string header,
        string expectedTrustDomain,
        out string? spiffeId,
        out string? serviceName,
        out string? subject)
    {
        spiffeId = null;
        serviceName = null;
        subject = null;

        // Parse first XFCC element (Envoy prepends/passes semicolon-separated key=value pairs)
        string? candidateUri = null;
        string? candidateSubject = null;

        foreach (var pair in SplitOutsideQuotes(header, ';', ','))
        {
            int eqIdx = pair.IndexOf('=');
            if (eqIdx <= 0)
                continue;

            var key = pair[..eqIdx].Trim();
            var val = Unquote(pair[(eqIdx + 1)..].Trim());

            if (key.Equals("URI", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(val))
            {
                candidateUri ??= val;
            }
            else if (key.Equals("Subject", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(val))
            {
                candidateSubject ??= val;
            }
        }

        if (!string.IsNullOrWhiteSpace(candidateUri))
        {
            if (!Uri.TryCreate(candidateUri, UriKind.Absolute, out var uri) ||
                !uri.Scheme.Equals("spiffe", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedTrustDomain) &&
                !uri.Host.Equals(expectedTrustDomain, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            spiffeId = uri.AbsoluteUri;
            serviceName = ExtractServiceFromSpiffePath(uri);
            subject = candidateSubject;
            return !string.IsNullOrWhiteSpace(serviceName);
        }

        if (!string.IsNullOrWhiteSpace(candidateSubject))
        {
            subject = candidateSubject;
            serviceName = ExtractCnFromSubject(candidateSubject) ?? candidateSubject;
            return !string.IsNullOrWhiteSpace(serviceName);
        }

        return false;
    }

    private static string? ExtractServiceFromSpiffePath(Uri spiffeUri)
    {
        var path = spiffeUri.AbsolutePath.Trim('/');
        if (string.IsNullOrEmpty(path))
            return null;

        int lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    private static string? ExtractCnFromSubject(string subject)
    {
        foreach (var part in subject.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return part[3..].Trim();
        }

        return null;
    }

    private static IEnumerable<string> SplitOutsideQuotes(string input, char sep1, char sep2)
    {
        bool inQuotes = false;
        int start = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '"' && (i == 0 || input[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && (c == sep1 || c == sep2))
            {
                if (i > start)
                    yield return input[start..i];
                start = i + 1;
            }
        }

        if (start < input.Length)
            yield return input[start..];
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];
        return value;
    }

    private readonly struct IpCidrRule(IPAddress network, int prefixLength)
    {
        private readonly byte[] _networkBytes = network.GetAddressBytes();
        private readonly int _prefixLength = Math.Clamp(prefixLength, 0, network.GetAddressBytes().Length * 8);

        public bool Matches(IPAddress candidate)
        {
            if (candidate.AddressFamily != network.AddressFamily)
                return false;

            byte[] candidateBytes = candidate.GetAddressBytes();
            int fullBytes = _prefixLength / 8;
            int remainingBits = _prefixLength % 8;

            for (int i = 0; i < fullBytes; i++)
            {
                if (_networkBytes[i] != candidateBytes[i])
                    return false;
            }

            if (remainingBits > 0)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                if ((_networkBytes[fullBytes] & mask) != (candidateBytes[fullBytes] & mask))
                    return false;
            }

            return true;
        }
    }
}

public static class GrpcServerAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseGrpcServerAuth(
        this IApplicationBuilder builder,
        bool requireAuth = true,
        bool useMtls = false)
    {
        return builder.UseMiddleware<GrpcServerAuthMiddleware>(requireAuth, useMtls);
    }

    public static IApplicationBuilder UseGrpcServerAuth(
        this IApplicationBuilder builder,
        bool requireAuth,
        EInterServiceAuthMode authMode,
        string? serviceMeshTrustDomain = null,
        IReadOnlyCollection<string>? trustedProxyIps = null,
        IReadOnlyCollection<string>? allowedIdentities = null)
    {
        return builder.UseMiddleware<GrpcServerAuthMiddleware>(
            requireAuth,
            authMode == EInterServiceAuthMode.Mtls,
            authMode,
            serviceMeshTrustDomain ?? string.Empty,
            trustedProxyIps ?? Array.Empty<string>(),
            allowedIdentities ?? Array.Empty<string>());
    }
}
