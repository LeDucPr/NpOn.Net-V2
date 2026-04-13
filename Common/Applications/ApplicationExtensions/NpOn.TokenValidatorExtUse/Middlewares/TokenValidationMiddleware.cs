using System.Net;
using System.Security.Claims;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Definitions.NpOn.ShareAccountConstant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Net.Http.Headers;

namespace Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Middlewares;

public class TokenValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ContextService contextService, AuthenService authenService)
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

        string? token = null;

        // 1. Try get from Authorization Header
        string? authorizationHeader = context.Request.Headers[HeaderNames.Authorization];
        if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
        {
            token = authorizationHeader["Bearer ".Length..].Trim();
        }
        else
        {
            // 2. Try get from Cookie if Header is missing
            string cookieName = EApplicationConfiguration.CookieAuthenName.GetAppSettingConfig().AsDefaultString();
            token = context.Request.Cookies[cookieName];
        }

        if (!string.IsNullOrEmpty(token))
        {
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
                if (tokenCreatedDate < DateTime.UtcNow)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await context.Response.WriteAsync("Invalid or expired token.");
                    return; // stop pipeline
                }

                // check enabled token
                string? tokenSessionId = context.User.FindFirst(ContextServiceCode.SessionCode)?.Value;
                if (string.IsNullOrWhiteSpace(tokenSessionId))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    await context.Response.WriteAsync("Invalid or expired token.");
                    return; // stop pipeline
                }

                AccountLoginRModel? accountInfo = await authenService.GetLogonInfoBySessionId(tokenSessionId);
                if (accountInfo == null || accountInfo.TokenStatus == ETokenStatus.Inactive)
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
            await context.Response.WriteAsync("Authorization header or cookie is missing.");
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