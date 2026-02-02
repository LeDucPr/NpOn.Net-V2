using System.Net;
using System.Text;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

namespace Common.Applications.NpOn.CommonApplication.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection UseDefaultAuthenticationMode(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "BearerOrCookie";
                options.DefaultChallengeScheme = "BearerOrCookie";
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy =
                    EApplicationConfiguration.IsDevEnvironment.GetAppSettingConfig().AsDefaultBool()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Unspecified;
                options.Cookie.Name =
                    EApplicationConfiguration.CookieAuthenName.GetAppSettingConfig().AsDefaultString();
                options.LoginPath = string.Empty; //"api/Account/Login";
                options.LogoutPath = string.Empty; //"api/Account/Logout";
                options.AccessDeniedPath = string.Empty;
                string cookieDomain =
                    EApplicationConfiguration.CookieDomain.GetAppSettingConfig().AsDefaultString();
                if (cookieDomain.Length > 0)
                {
                    options.Cookie.Domain = cookieDomain;
                }

                options.Events.OnRedirectToLogin = context =>
                {
                    // Always return 401 Unauthorized for API instead of Redirect (to avoid CORS errors or returning an HTML login page)
                    // Since LoginPath is empty, returning 401 is the most appropriate behavior for both Dev and Prod.
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return Task.CompletedTask;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                string jwtKey = EApplicationConfiguration.JwtTokensKey.GetAppSettingConfig().AsDefaultString();
                byte[] key = Encoding.ASCII.GetBytes(jwtKey);
                string[] validIssuers = EApplicationConfiguration.ValidIssuers.GetAppSettingConfig()
                    ?.AsEmptyString().Split(",").Select(x => x.AsEmptyString()).ToArray() ?? [];
                string[] validAudiences = EApplicationConfiguration.ValidAudiences.GetAppSettingConfig()
                    ?.AsEmptyString().Split(",").Select(x => x.AsEmptyString()).ToArray() ?? [];
                bool isUseValidIssuers = validIssuers is { Length: > 0 };
                bool isUseValidAudiences = validAudiences is { Length: > 0 };

                options.RequireHttpsMetadata = false; // Use false only in dev environment
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = isUseValidIssuers, // Customize if you have specific issuers
                    ValidateAudience = isUseValidAudiences, // Customize if you have specific audiences
                    ValidIssuers = validIssuers,
                    ValidAudiences = validAudiences,
                    // ValidateLifetime = true,
                };
            })
            .AddPolicyScheme("BearerOrCookie", "Bearer or Cookie", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    string? authorization = context.Request.Headers[HeaderNames.Authorization];
                    if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
                        return JwtBearerDefaults.AuthenticationScheme;
                    return CookieAuthenticationDefaults.AuthenticationScheme;
                };
            });
        return services;
    }
}