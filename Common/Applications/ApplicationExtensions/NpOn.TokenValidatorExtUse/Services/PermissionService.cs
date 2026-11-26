using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Attributes;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using NpOn.Protos.Account.V1;

namespace Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;

public class PermissionService(
    ContextService contextService,
    AccountPermissionProtoService.AccountPermissionProtoServiceClient accountPermissionProtoClient,
    IActionDescriptorCollectionProvider actionDescriptorCollectionProvider,
    ILogger<PermissionService> logger)
{
    public async Task<bool> AutoSyncPermissionController()
    {
        var endpoints = actionDescriptorCollectionProvider.ActionDescriptors.Items
            .Where(ad => ad.AttributeRouteInfo != null) // Only take endpoint has route attribute
            .ToList();
        var cad = actionDescriptorCollectionProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(cad => cad.AttributeRouteInfo != null)
            .ToList().FirstOrDefault();

        string? cadName = cad?.ControllerTypeInfo.Assembly.GetName().Name;
        if (string.IsNullOrEmpty(cadName))
        {
            string err = "No ControllerActionDescriptor found.";
            logger.LogInformation(err);
            throw new Exception(err);
        }

        var syncTasks = new List<Task<bool>>();
        Guid controllerStartVersionId = Guid.NewGuid();
        List<AccountPermissionControllerAddOrChangeCommand> commands = [];
        foreach (var endpoint in endpoints)
        {
            bool isAnonymousAllowed = endpoint.EndpointMetadata
                .Any(m => m.GetType() == typeof(AllowAnonymousAttribute));
            if (isAnonymousAllowed)
                continue;

            PermissionControllerAttribute? permissionControllerAttribute = endpoint.EndpointMetadata
                .OfType<PermissionControllerAttribute>()
                .FirstOrDefault();

            PermissionRequiredAttribute? permissionRequiredAttribute = endpoint.EndpointMetadata
                .OfType<PermissionRequiredAttribute>()
                .FirstOrDefault();


            var action = endpoint.RouteValues["action"];
            var controller = endpoint.RouteValues["controller"];
            if (controller == null || action == null)
                continue;
            string controllerCode = contextService.GenControllerCodeFormula(cadName, controller, action);

            if (permissionControllerAttribute != null)
            {
                if (permissionRequiredAttribute == null)
                {
                    string err =
                        $"Hey fucking bro! You need to ADD [PermissionRequiredAttribute] before [PermissionControllerAttribute in {controllerCode}";
                    logger.LogError(err);
                    throw new Exception(err);
                }

                if (permissionControllerAttribute.IsHasPermission(EPermission.Unknown))
                {
                    string err =
                        $"Hey fucking bro! You need to DELETE - EPermission.Unknown [PermissionControllerAttribute in {controllerCode} or ADD Others Permission in {controllerCode}";
                    logger.LogError(err);
                    throw new Exception(err);
                }
            }
            else
                continue;

            // var httpMethods = endpoint.EndpointMetadata.OfType<HttpMethodMetadata>().SelectMany(m => m.HttpMethods).Distinct();

            var command = new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = cadName,
                VersionId = controllerStartVersionId.ToString(),
                ControllerName = controllerCode, // code
                Permission = (long)permissionControllerAttribute.GetAllPermissionAsOne,
                Description = permissionRequiredAttribute.Description ?? string.Empty,
                RequiredPermission = (long)permissionRequiredAttribute.GetAllPermissionAsOne,
            };
            commands.Add(command);
            if (commands.Count == 500)
            {
                syncTasks.Add(StartSyncPermissionWithController(commands.ToArray()));
                commands = [];
            }
        }

        if (commands.Count > 0)
            syncTasks.Add(StartSyncPermissionWithController(commands.ToArray()));

        logger.LogInformation("Waiting for all {TaskCount} permission sync batches to complete...", syncTasks.Count);
        bool[] syncResults = await Task.WhenAll(syncTasks);
        if (syncResults.Any(ok => !ok))
        {
            logger.LogError(
                "One or more permission sync batches failed for host '{HostCode}'. Skipping ClearOldControllersVersion to prevent deleting active routes.",
                cadName);
            return false;
        }

        logger.LogInformation("All permission sync batches completed successfully.");
        return await SyncClearPermissionWithController(controllerStartVersionId, cadName);
    }

    private async Task<bool> StartSyncPermissionWithController(AccountPermissionControllerAddOrChangeCommand[] commands)
    {
        logger.LogInformation("Starting to sync a batch of {CommandCount} permissions...", commands.Length);
        var req = new AccountPermissionControllersSyncCommand();
        req.Items.AddRange(commands);
        var reply = await accountPermissionProtoClient.SyncPermissionWithControllerAsync(req);
        if (!reply.Status || !reply.Data)
        {
            logger.LogError("Sync batch failed: {Errors}", string.Join("; ", reply.ErrorMessages));
            return false;
        }

        logger.LogInformation("Sync batch finished.");
        return true;
    }

    private async Task<bool> SyncClearPermissionWithController(Guid currentVersionId, string hostCode)
    {
        logger.LogInformation(
            "Starting to clear old permission versions for host '{HostCode}' except for current version '{CurrentVersionId}'.",
            hostCode, currentVersionId);
        var reply = await accountPermissionProtoClient.ClearOldControllersVersionAsync(
            new AccountPermissionControllerDeleteByHostCodeAndVersionIdCommand
            {
                HostCode = hostCode,
                VersionId = currentVersionId.ToString(),
            });
        if (!reply.Status || !reply.Data)
        {
            logger.LogError("ClearOldControllersVersion failed for host '{HostCode}': {Errors}",
                hostCode, string.Join("; ", reply.ErrorMessages));
            return false;
        }

        logger.LogInformation("Finished clearing old permission versions.");
        return true;
    }
}