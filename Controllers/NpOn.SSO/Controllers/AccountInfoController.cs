using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Controllers.NpOn.SSO.Mappings.Account;
using Controllers.NpOn.SSO.Requests;
using Controllers.NpOn.SSO.Validators;
using Microsoft.AspNetCore.Mvc;
using NpOn.Protos.Account.V1;

namespace Controllers.NpOn.SSO.Controllers;

public class AccountInfoController(
    ContextService contextService,
    AccountInfoProtoService.AccountInfoProtoServiceClient accountInfoProtoClient,
    ILogger<AccountController> logger
) : BaseSsoController(logger, contextService)
{
    private readonly ContextService _contextService = contextService;

    [HttpPost]
    public async Task<CommonApiResponse<object>> GetAccountInfo()
    {
        return await ProcessRequest<object>(async (response) =>
        {
            string accountId = _contextService.GetAccountIdAsString() ?? string.Empty;
            if (string.IsNullOrEmpty(accountId))
            {
                response.SetFail("AccountId not null");
                return;
            }

            var accountInfoResponse = await accountInfoProtoClient.AccountInfoGetByAccountIdAsync(
                new AccountIdRequestProto
                {
                    AccountId = accountId,
                });

            if (!accountInfoResponse.Status)
            {
                response.SetFail("AccountInfo not found");
                return;
            }

            response.Data = new
            {
                Model = accountInfoResponse.Data?.ToModel(),
            };
            response.SetSuccess();
        });
    }

    [HttpPost]
    public async Task<CommonApiResponse<string>> AddOrChangeAccountInfo([FromBody] AccountInfoAddOrChangeRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var validator = AccountInfoAddOrChangeRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            string accountId = _contextService.GetAccountIdAsString() ?? string.Empty;
            if (string.IsNullOrEmpty(accountId))
            {
                response.SetFail("AccountId not null");
                return;
            }

            var accountInfoResponse = await accountInfoProtoClient.AccountInfoAddOrChangeAsync(
                request.ToProtoCommand(accountId));
            if (!accountInfoResponse.Status)
            {
                response.SetFail(request.Id == null || request.Id == Guid.Empty ? "add fail" : "update fail");
                return;
            }

            response.Data = request.Id == null || request.Id == Guid.Empty ? "add success" : "update success";
            response.SetSuccess();
        });
    }
    
    [HttpPost]
    public async Task<CommonApiResponse<string>> AddOrChangeAccountAddress([FromBody] AccountAddressesAddOrChangeRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var validator = AccountAddressesAddOrChangeRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            Guid accountId = _contextService.GetAccountIdAsString().AsDefaultGuid();
            if (accountId == Guid.Empty)
            {
                response.SetFail("AccountId not null");
                return;
            }

            var protoReq = new AccountAddressesAddOrChangeRequestProto();
            protoReq.Items.Add(request.ToProtoCommand(accountId));
            var accountInfoResponse = await accountInfoProtoClient.AccountAddressesAddOrChangeAsync(protoReq);
            if (!accountInfoResponse.Status)
            {
                response.SetFail("Address Add/Change fail");
                return;
            }

            response.Data = "Address Add/Change success";
            response.SetSuccess();
        });
    }
}