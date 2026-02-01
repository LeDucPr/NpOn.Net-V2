using System.Security.Claims;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Attributes;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.CommonMode;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Definitions.NpOn.ShareAccountConstant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Middlewares;

public class PermissionValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ContextService contextService, AuthenService authenService)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
            return;

        // AllowAnonymous
        var isAnonymousAllowed = endpoint.Metadata.Any(m => m.GetType() == typeof(AllowAnonymousAttribute));
        if (isAnonymousAllowed)
        {
            await next(context);
            return;
        }

        PermissionControllerAttribute? permissionControllerAttribute = endpoint.Metadata
            .OfType<PermissionControllerAttribute>()
            .FirstOrDefault();

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        bool isHasPermission; // false
        if (permissionControllerAttribute != null)
        {
            var claimsPrincipal = context.User;
            var identity = claimsPrincipal.Identity as ClaimsIdentity;
            EPermission permission = identity?.FindFirst(ContextServiceCode.Permission)?.Value.ToEnum<EPermission>() ??
                                     EPermission.Unknown;
            // else (not has base Permission)
            PermissionRequiredAttribute? pmsRequired = endpoint.Metadata
                .OfType<PermissionRequiredAttribute>()
                .FirstOrDefault();
            if (pmsRequired == null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var accountId = contextService.GetAccountIdAsString().AsDefaultString();
            var cad = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            string? hostCode = cad?.ControllerTypeInfo.Assembly.GetName().Name;
            var controller = cad?.RouteValues["controller"];
            var action = cad?.RouteValues["action"];
            if (hostCode == null || controller == null || action == null)
                return;
            string accessControllerCode = contextService.GenControllerCodeFormula(hostCode, controller, action);
           
            isHasPermission = permissionControllerAttribute.IsHasPermission(permission);
            isHasPermission = await authenService.CheckLogonPermissionExceptionControllers(
                accountId, accessControllerCode, isHasPermission);
            isHasPermission &= pmsRequired.IsHasPermission(permission);
        }
        else // not has PermissionControllerAttribute => pass
        {
            await next(context);
            return;
        }

        if (isHasPermission)
        {
            // !! If it has Global Policy required, APIs need has permission 
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}

public static class PermissionValidationMiddlewareExtensions
{
    public static IApplicationBuilder UsePermissionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PermissionValidationMiddleware>();
    }
}