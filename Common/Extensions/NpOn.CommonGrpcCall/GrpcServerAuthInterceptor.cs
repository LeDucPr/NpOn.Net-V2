using Common.Extensions.NpOn.HeaderConfig;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace NpOn.CommonGrpcCall;

public class GrpcServerAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GrpcServerAuthMiddleware> _logger;
    private readonly bool _requireAuth;
    private readonly bool _useMtls;

    public GrpcServerAuthMiddleware(
        RequestDelegate next,
        IServiceProvider serviceProvider,
        ILogger<GrpcServerAuthMiddleware> logger,
        bool requireAuth = true,
        bool useMtls = false)
    {
        _next = next;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _requireAuth = requireAuth;
        _useMtls = useMtls;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation if auth is disabled
        if (!_requireAuth)
        {
            await _next(context);
            return;
        }

        // Try mTLS certificate validation first if enabled
        if (_useMtls)
        {
            var certificateService = _serviceProvider.GetService(typeof(IInterServiceCertificateService)) as IInterServiceCertificateService;
            if (certificateService != null)
            {
                var clientCert = context.Connection.ClientCertificate;
                if (clientCert != null)
                {
                    var isValid = certificateService.ValidateClientCertificate(clientCert);
                    if (isValid)
                    {
                        context.Items["AuthMethod"] = "mTLS";
                        context.Items["ClientCertSubject"] = clientCert.Subject;
                        _logger.LogInformation("Authenticated via mTLS from {RemoteIpAddress}, Subject: {Subject}",
                            context.Connection.RemoteIpAddress, clientCert.Subject);
                        await _next(context);
                        return;
                    }
                    else
                    {
                        _logger.LogWarning("mTLS certificate validation failed from {RemoteIpAddress}",
                            context.Connection.RemoteIpAddress);
                    }
                }
            }
        }

        // Fallback to JWT token validation
        if (_serviceProvider.GetService(typeof(IInterServiceTokenService)) is IInterServiceTokenService tokenService)
        {
            var token = context.Request.Headers[DefaultHeaderConstant.GrpcInterServiceAuthToken].FirstOrDefault();
            var serviceName = context.Request.Headers[DefaultHeaderConstant.GrpcInterServiceServiceName].FirstOrDefault();

            if (!string.IsNullOrEmpty(token))
            {
                var isValid = tokenService.ValidateToken(token, out var principal);

                if (isValid && principal != null)
                {
                    context.Items["AuthMethod"] = "JWT";
                    var serviceClaim = principal.FindFirst("service_name");
                    context.Items["ServiceName"] = serviceClaim?.Value ?? serviceName;
                    _logger.LogInformation("Authenticated via JWT from {ServiceName} at {RemoteIpAddress}",
                        context.Items["ServiceName"],
                        context.Connection.RemoteIpAddress);
                    await _next(context);
                    return;
                }
                else
                {
                    _logger.LogWarning("JWT token validation failed from {RemoteIpAddress}",
                        context.Connection.RemoteIpAddress);
                }
            }
        }

        // All authentication methods failed
        _logger.LogWarning("Authentication failed from {RemoteIpAddress}. No valid certificate or token provided.",
            context.Connection.RemoteIpAddress);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
}

public static class GrpcServerAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseGrpcServerAuth(this IApplicationBuilder builder, bool requireAuth = true, bool useMtls = false)
    {
        return builder.UseMiddleware<GrpcServerAuthMiddleware>(requireAuth, useMtls);
    }
}
