using Grpc.Core;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Service.NpOn.AccountService.Mappers;
using MicroServices.Account.Service.NpOn.IAccountService;
using NpOn.Protos.Account.V1;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public sealed class AuthenticationProtoGrpcService(IAuthenticationService authenticationService)
    : AuthenticationProtoService.AuthenticationProtoServiceBase
{
    public override async Task<AccountLoginReplyProto> Signup(
        AccountSignupRequestProto request,
        ServerCallContext context)
    {
        var result = await authenticationService.Signup(AccountProtoMapper.ToSignupCommand(request));
        return AccountProtoMapper.ToLoginReply(result);
    }

    public override async Task<BoolReplyProto> ChangeAccountStatus(
        AccountSetStatusRequestProto request,
        ServerCallContext context)
    {
        var result = await authenticationService.ChangeAccountStatus(AccountProtoMapper.ToSetStatusCommand(request));
        return AccountProtoMapper.ToBoolReply(result);
    }

    public override async Task<BoolReplyProto> ChangeAccountPassword(
        AccountChangePasswordRequestProto request,
        ServerCallContext context)
    {
        var result = await authenticationService.ChangeAccountPassword(AccountProtoMapper.ToChangePasswordCommand(request));
        return AccountProtoMapper.ToBoolReply(result);
    }

    public override async Task<AccountLoginReplyProto> Login(
        AccountLoginRequestProto request,
        ServerCallContext context)
    {
        var result = await authenticationService.Login(AccountProtoMapper.ToLoginQuery(request));
        return AccountProtoMapper.ToLoginReply(result);
    }

    public override async Task<AccountLoginReplyProto> RefreshToken(
        AccountRefreshTokenRequestProto request,
        ServerCallContext context)
    {
        var result = await authenticationService.RefreshToken(AccountProtoMapper.ToRefreshTokenQuery(request));
        return AccountProtoMapper.ToLoginReply(result);
    }

    public override async Task<AccountLoginReplyProto> GetLogonTokenBySessionId(
        AccountSessionRequestProto request,
        ServerCallContext context)
    {
        var result = await authenticationService.GetLogonTokenBySessionId(new AccountGetLogonInfoBySessionIdQuery
        {
            SessionId = request.SessionId,
            ProcessUId = request.HasProcessUid ? request.ProcessUid : null
        });
        return AccountProtoMapper.ToLoginReply(result);
    }

    public override async Task<BoolReplyProto> IsValidLogonPermissionExceptionControllers(
        AccountPermissionExceptionCheckRequestProto request,
        ServerCallContext context)
    {
        var result = await authenticationService.IsValidLogonPermissionExceptionControllers(
            new AccountPermissionExceptionCachingCheckValidQuery
            {
                AccountId = request.AccountId,
                ControllerCode = request.ControllerCode,
                IsHasBasePermission = request.IsHasBasePermission
            });
        return AccountProtoMapper.ToBoolReply(result);
    }

    public override async Task<StringReplyProto> LogOut(
        AccountSessionRequestProto request,
        ServerCallContext context)
    {
        var result = await authenticationService.LogOut(new AccountLogoutQuery
        {
            SessionId = request.SessionId,
            ProcessUId = request.HasProcessUid ? request.ProcessUid : null
        });
        return AccountProtoMapper.ToStringReply(result);
    }
}

public sealed class AccountInfoProtoGrpcService(IAccountInfoService accountInfoService)
    : AccountInfoProtoService.AccountInfoProtoServiceBase
{
    public override async Task<AccountInfoListReplyProto> AccountInfoGetByAccountIds(
        AccountIdsRequestProto request,
        ServerCallContext context)
    {
        var ids = request.AccountIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToArray();
        var result = await accountInfoService.AccountInfoGetByAccountIds(new AccountInfoGetByAccountIdsQuery
        {
            AccountIds = ids
        });
        return AccountProtoMapper.ToInfoListReply(result);
    }

    public override async Task<AccountInfoSingleReplyProto> AccountInfoGetByAccountId(
        AccountIdRequestProto request,
        ServerCallContext context)
    {
        var result = await accountInfoService.AccountInfoGetByAccountId(new AccountInfoGetByAccountIdQuery
        {
            AccountId = request.AccountId
        });
        return AccountProtoMapper.ToInfoSingleReply(result);
    }

    public override async Task<CommonStatusReplyProto> AccountInfoAddOrChange(
        AccountInfoAddOrChangeRequestProto request,
        ServerCallContext context)
    {
        var result = await accountInfoService.AccountInfoAddOrChange(
            AccountProtoMapper.ToInfoAddOrChangeCommand(request));
        return AccountProtoMapper.ToStatusReply(result);
    }

    public override async Task<CommonStatusReplyProto> AccountAddressesAddOrChange(
        AccountAddressesAddOrChangeRequestProto request,
        ServerCallContext context)
    {
        var commands = request.Items.Select(AccountProtoMapper.ToAddressCommand).ToArray();
        var result = await accountInfoService.AccountAddressesAddOrChange(commands);
        return AccountProtoMapper.ToStatusReply(result);
    }
}

public sealed class AccountGroupProtoGrpcService(IAccountGroupService accountGroupService)
    : AccountGroupProtoService.AccountGroupProtoServiceBase
{
    public override async Task<CommonStatusReplyProto> GroupAddOrChange(
        AccountGroupAddOrChangeRequestProto request,
        ServerCallContext context)
    {
        var result = await accountGroupService.GroupAddOrChange(
            AccountProtoMapper.ToGroupAddOrChangeCommand(request));
        return AccountProtoMapper.ToStatusReply(result);
    }

    public override async Task<CommonStatusReplyProto> GroupCopy(
        AccountGroupCopyRequestProto request,
        ServerCallContext context)
    {
        var result = await accountGroupService.GroupCopy(AccountProtoMapper.ToGroupCopyCommand(request));
        return AccountProtoMapper.ToStatusReply(result);
    }

    public override async Task<CommonStatusReplyProto> Search(
        AccountGroupSearchRequestProto request,
        ServerCallContext context)
    {
        var result = await accountGroupService.Search(AccountProtoMapper.ToGroupSearchQuery(request));
        return AccountProtoMapper.ToStatusReply(result);
    }

    public override async Task<CommonStatusReplyProto> GroupOrMemberDelete(
        AccountGroupDeleteRequestProto request,
        ServerCallContext context)
    {
        var result = await accountGroupService.GroupOrMemberDelete(
            AccountProtoMapper.ToGroupDeleteCommand(request));
        return AccountProtoMapper.ToStatusReply(result);
    }
}

public sealed class AccountPermissionProtoGrpcService(IAccountPermissionService accountPermissionService)
    : AccountPermissionProtoService.AccountPermissionProtoServiceBase
{
    public override async Task<AccountPermissionExceptionListReplyProto> AccountPermissionExceptionQuickGetByAccountId(
        AccountIdRequestProto request,
        ServerCallContext context)
    {
        var result = await accountPermissionService.AccountPermissionExceptionQuickGetByAccountId(
            new AccountPermissionExceptionGetByAccountIdQuery
            {
                AccountId = request.AccountId
            });
        return AccountProtoMapper.ToPermissionExceptionReply(result);
    }

    public override async Task<BoolReplyProto> SyncPermissionWithController(
        SyncPermissionControllersRequestProto request,
        ServerCallContext context)
    {
        var commands = request.Items.Select(x => new AccountPermissionControllerAddOrChangeCommand
        {
            HostCode = x.HostCode,
            VersionId = Guid.TryParse(x.VersionId, out var v) ? v : Guid.Empty,
            ControllerName = x.ControllerName,
            Permission = (EPermission)x.Permission,
            Description = x.HasDescription ? x.Description : null
        }).ToArray();

        var result = await accountPermissionService.SyncPermissionWithController(commands);
        return AccountProtoMapper.ToBoolReply(result);
    }

    public override async Task<BoolReplyProto> ClearOldControllersVersion(
        ClearOldControllersVersionRequestProto request,
        ServerCallContext context)
    {
        var result = await accountPermissionService.ClearOldControllersVersion(
            new AccountPermissionControllerDeleteByHostCodeAndVersionIdCommand
            {
                HostCode = request.HostCode,
                VersionId = Guid.TryParse(request.VersionId, out var v) ? v : Guid.Empty
            });
        return AccountProtoMapper.ToBoolReply(result);
    }

    public override async Task<BoolReplyProto> AddOrChangeAccountPermissionException(
        AddOrChangeAccountPermissionExceptionsRequestProto request,
        ServerCallContext context)
    {
        var commands = request.Items.Select(x => new AccountPermissionExceptionAddOrChangeCommand
        {
            AccountId = Guid.TryParse(x.AccountId, out var g) ? g : Guid.Empty,
            ControllerCode = x.ControllerCode,
            AccessPermission = (EPermissionAccessController)x.AccessPermission
        }).ToArray();

        var result = await accountPermissionService.AddOrChangeAccountPermissionException(commands);
        return AccountProtoMapper.ToBoolReply(result);
    }

    public override async Task<BoolReplyProto> AddOrChangeManyAccountPermissionException(
        AddOrChangeManyAccountPermissionExceptionRequestProto request,
        ServerCallContext context)
    {
        var command = new AccountPermissionExceptionAddOrChangeManyCommand
        {
            AccountIds = request.AccountIds.Count > 0
                ? request.AccountIds.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToArray()
                : null,
            GroupIds = request.GroupIds.Count > 0
                ? request.GroupIds.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToArray()
                : null,
            ControllerComponents = request.ControllerComponents.Count > 0
                ? request.ControllerComponents.Select(c => new AccountPermissionExceptionAddOrChangeManyControllerCodeCommand
                {
                    ControllerCode = c.ControllerCode,
                    AccessPermission = (EPermissionAccessController)c.AccessPermission
                }).ToArray()
                : null
        };

        var result = await accountPermissionService.AddOrChangeManyAccountPermissionException(command);
        return AccountProtoMapper.ToBoolReply(result);
    }
}
