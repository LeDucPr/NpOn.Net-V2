using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Controllers.NpOn.SSO.Requests;
using Controllers.NpOn.SSO.Validators;
using Microsoft.AspNetCore.Mvc;
using NpOn.Protos.Account.V1;

namespace Controllers.NpOn.SSO.Controllers;

public class AccountGroupController(
    ILogger<AccountController> logger,
    ContextService contextService,
    AccountGroupProtoService.AccountGroupProtoServiceClient accountGroupProtoClient
) : BaseSsoController(logger, contextService)
{
    [HttpPost]
    public async Task<CommonApiResponse<string>> AddOrChangeGroupAndMember(
        [FromBody] AccountGroupAddOrChangeRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var validator = AccountGroupAddOrChangeRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            var protoReq = new AccountGroupAddOrChangeCommand
            {
                Leader = request.Leader.ToString(),
            };
            var groupId = request.GroupId.AsDefaultGuid();
            if (groupId != Guid.Empty) protoReq.GroupId = groupId.ToString();
            if (request.GroupName != null) protoReq.GroupName = request.GroupName;
            if (request.Members is { Length: > 0 }) protoReq.Members.AddRange(request.Members.Select(x => x.ToString()));
            if (request.GroupTypes is { Length: > 0 }) protoReq.GroupTypes.AddRange(request.GroupTypes.Select(x => (int)x));

            var addOrChangeResponse = await accountGroupProtoClient.GroupAddOrChangeAsync(protoReq);

            if (!addOrChangeResponse.Status)
            {
                response.SetFail("Add Or Change Group fail");
                return;
            }

            response.Data = "Add Or Change Group Success";
            response.SetSuccess();
        });
    }

    [HttpPost]
    public async Task<CommonApiResponse<string>> GroupCopy([FromBody] AccountGroupCopyRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var validator = AccountGroupCopyRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            var protoReq = new AccountGroupCopyCommand
            {
                GroupIdNeedCopy = request.GroupIdNeedCopy.ToString(),
            };
            foreach (var x in request.Components)
            {
                var comp = new AccountGroupCopyComponentCommand
                {
                    Leader = x.Leader.ToString(),
                };
                if (x.GroupName != null) comp.GroupName = x.GroupName;
                if (x.GroupTypes is { Length: > 0 }) comp.GroupTypes.AddRange(x.GroupTypes.Select(t => (int)t));
                if (x.MemberAdds is { Length: > 0 }) comp.MemberAdds.AddRange(x.MemberAdds.Select(m => m.ToString()));
                if (x.MemberExcludes is { Length: > 0 }) comp.MemberExcludes.AddRange(x.MemberExcludes.Select(m => m.ToString()));
                protoReq.Components.Add(comp);
            }

            var addOrChangeResponse = await accountGroupProtoClient.GroupCopyAsync(protoReq);

            if (!addOrChangeResponse.Status)
            {
                response.SetFail("(Delete Group)/(Delete Group Or Member) fail");
                return;
            }

            response.Data = "Add New Groups Success";
            response.SetSuccess();
        });
    }

    [HttpPost]
    public async Task<CommonApiResponse<string>> DeleteGroupOrMember([FromBody] AccountGroupDeleteRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var validator = AccountGroupDeleteRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            var protoReq = new AccountGroupDeleteCommand
            {
                GroupId = request.GroupId.ToString(),
                Leader = request.Leader.ToString(),
            };
            if (request.GroupName != null) protoReq.GroupName = request.GroupName;
            if (request.Members is { Length: > 0 }) protoReq.Members.AddRange(request.Members.Select(x => x.ToString()));

            var addOrChangeResponse = await accountGroupProtoClient.GroupOrMemberDeleteAsync(protoReq);

            if (!addOrChangeResponse.Status)
            {
                response.SetFail("(Delete Group)/(Delete Group Or Member) fail");
                return;
            }

            response.Data = "Delete Group Success";
            response.SetSuccess();
        });
    }
}