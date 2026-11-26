using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using MicroServices.Account.Definitions.NpOn.ShareAccountConstant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Net.Http.Headers;

namespace Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Middlewares;

public class TokenValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ContextService contextService)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        PathString path = context.Request.Path;
        bool isMediaEnablePublicDownload =
            EApplicationConfiguration.IsMediaEnablePublicDownload.GetAppSettingConfig().AsDefaultBool();
        if (isMediaEnablePublicDownload)
        {
            string downloadPath =
                EApplicationConfiguration.MediaDownloadUrlPrefix.GetAppSettingConfig().AsEmptyString();
            if (path.StartsWithSegments(downloadPath))
            {
                await next(context);
                return;
            }
        }

        string? authorizationHeader = context.Request.Headers[HeaderNames.Authorization];
        if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
        {
            var token = authorizationHeader["Bearer ".Length..].Trim();
            if (contextService.ValidateToken(token, out var claimsPrincipal))
            {
                if (claimsPrincipal == null)
                    return;
                context.User = claimsPrincipal;
                var identity = claimsPrincipal.Identity as ClaimsIdentity;
                var createdDateClaim = identity?.FindFirst(ContextServiceCode.TokenCreatedUtc)?.Value;
                var minuteExpireClaim = identity?.FindFirst($"{ContextServiceCode.MinuteExpirePrefix}")?.Value;

                if (string.IsNullOrEmpty(createdDateClaim) || string.IsNullOrEmpty(minuteExpireClaim))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await context.Response.WriteAsync("Invalid or expired token.");
                    return; // stop pipeline
                }

                DateTime? tokenCreatedDate = createdDateClaim.FromIso8601ToDateTime(); // expired (time)
                if (!tokenCreatedDate.HasValue || tokenCreatedDate.Value.ToUniversalTime() < DateTime.UtcNow)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await context.Response.WriteAsync("Invalid or expired token.");
                    return; // stop pipeline
                }

                // Stateless verification: session and account claims come directly from the cryptographically signed JWT
                string? tokenSessionId = context.User.FindFirst(ContextServiceCode.SessionCode)?.Value;
                string? accountIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
                if (string.IsNullOrWhiteSpace(tokenSessionId) || string.IsNullOrWhiteSpace(accountIdClaim))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await context.Response.WriteAsync("Invalid or expired token.");
                    return; // stop pipeline
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsync("Invalid or expired token.");
                return; // stop pipeline
            }
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsync("Authorization header is missing or malformed.");
            return; // stop pipeline
        }

        await next(context);
    }
}

public static class TokenValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TokenValidationMiddleware>();
    }
}