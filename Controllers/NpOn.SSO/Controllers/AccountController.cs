using System.Security.Cryptography;
using System.Text;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Attributes;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Controllers.NpOn.SSO.Mappings.Account;
using Controllers.NpOn.SSO.Requests;
using Controllers.NpOn.SSO.OutputModels;
using Controllers.NpOn.SSO.Validators;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Definitions.NpOn.ShareAccountConstant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Controllers.NpOn.SSO.InterfaceGrpcControllers;
using NpOn.Protos.Account.V1;

namespace Controllers.NpOn.SSO.Controllers;

public class AccountController(
    ILogger<AccountController> logger,
    ContextService contextService,
    AuthenticationProtoService.AuthenticationProtoServiceClient authenticationProtoClient,
    AccountPermissionProtoService.AccountPermissionProtoServiceClient accountPermissionProtoClient
) : BaseSsoController(logger, contextService), IAccountGrpcController
{
    private readonly ContextService _contextService = contextService;

    [Obsolete("Obsolete")]
    [AllowAnonymous]
    [HttpPost]
    public async Task<CommonApiResponse<object>> Signup([FromBody] AccountSignupRequest request)
    {
        return await ProcessRequest<object>(async (response) =>
        {
            var validator = AccountSignupRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            var protoReq = new AccountSignupCommand
            {
                AuthType = (int)request.AuthType,
                ClientId = _contextService.ClientId,
                Email = request.Email.AsEmptyString(),
                PhoneNumber = request.PhoneNumber.AsEmptyString(),
                UserName = request.UserName.AsEmptyString(),
                Password = CreateHashPassword(request.Password).AsEmptyString(),
                LoginType = (int)ELoginType.Default,
                FullName = request.FullName.AsEmptyString(),
            };
            var signupIp = _contextService.GetIp();
            if (!string.IsNullOrEmpty(signupIp)) protoReq.SignupIp = signupIp;
            if (request.DeviceInfo != null) protoReq.DeviceSignupInfo = request.DeviceInfo;
            if (request.AppId != null) protoReq.AuthenApplicationId = request.AppId;
            if (request.AvatarUrl != null) protoReq.AvatarUrl = request.AvatarUrl;

            var signupResponse = await authenticationProtoClient.SignupAsync(protoReq);

            if (!signupResponse.Status)
            {
                string errMessages = signupResponse.ErrorMessages.AsArrayJoin();
                response.SetFail(!string.IsNullOrWhiteSpace(errMessages) ? errMessages : "Signup fail");
                return;
            }

            response.Data = new
            {
                Model = signupResponse.Data?.ToModel(),
            };
            response.SetSuccess();
        });
    }


    [PermissionController(EPermission.Administrator)]
    [PermissionRequired]
    [HttpPost]
    public async Task<CommonApiResponse<string>> ChangeAccountStatus([FromBody] AccountChangeStatusRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var signupResponse = await authenticationProtoClient.ChangeAccountStatusAsync(
                new AccountSetStatusCommand
                {
                    AccountId = request.AccountId.ToString(),
                    AccountStatus = (int)request.AccountStatus,
                });

            if (!signupResponse.Status)
            {
                string errMessages = signupResponse.ErrorMessages.AsArrayJoin();
                response.SetFail(!string.IsNullOrWhiteSpace(errMessages) ? errMessages : "Change Account Status fail");
                return;
            }

            response.Data = "Change Account Status successful";
            response.SetSuccess();
        });
    }


    [PermissionController(EPermission.Administrator)]
    [PermissionRequired]
    [HttpPost]
    [Obsolete("Obsolete")]
    public async Task<CommonApiResponse<object>> ChangePassword([FromBody] ChangeAccountPasswordRequest request)
    {
        return await ProcessRequest<object>(async (response) =>
        {
            var validator = ChangeAccountPasswordRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            string? newPassword = request.NewPassword;
            if (string.IsNullOrEmpty(newPassword))
                newPassword = GeneratePassword();
            var signupResponse = await authenticationProtoClient.ChangeAccountPasswordAsync(
                new AccountChangePasswordCommand
                {
                    AccountId = request.AccountId.ToString(),
                    NewPassword = CreateHashPassword(newPassword).AsEmptyString(),
                });

            if (!signupResponse.Status || !signupResponse.Data)
            {
                response.SetFail("Change Account Password fail");
                return;
            }

            response.Data = request.NewPassword;
            response.SetSuccess();
        });
    }


    [Obsolete("Obsolete")]
    [AllowAnonymous]
    [HttpPost]
    public async Task<CommonApiResponse<AccountLoginResponseWrapper>> Login([FromBody] AccountLoginRequest request)
    {
        return await ProcessRequest<AccountLoginResponseWrapper>(async (response) =>
        {
            var validator = AccountLoginRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            bool isUseMultiDevice = !string.IsNullOrWhiteSpace(request.DeviceInfo) &&
                                    request.AuthType == EAuthentication.WebApp;
            var protoQuery = new AccountLoginQuery
            {
                ClientId = _contextService.ClientId,
                Password = CreateHashPassword(request.Password),
                AuthType = (int)request.AuthType,
                IsEnableMultiDevice = isUseMultiDevice
            };
            if (request.Email != null) protoQuery.Email = request.Email;
            if (request.PhoneNumber != null) protoQuery.PhoneNumber = request.PhoneNumber;
            if (request.UserName != null) protoQuery.UserName = request.UserName;
            if (request.DeviceInfo != null) protoQuery.DeviceLoginInfo = request.DeviceInfo;
            if (request.LoginType.HasValue) protoQuery.LoginType = (int)request.LoginType.Value;
            var ip = _contextService.GetIp();
            if (!string.IsNullOrEmpty(ip)) protoQuery.Ip = ip;
            if (request.AppId != null) protoQuery.AuthenApplicationId = request.AppId;

            var accountLoginResponse = await authenticationProtoClient.LoginAsync(protoQuery);
            if (!accountLoginResponse.Status)
            {
                response.SetFail(accountLoginResponse.ErrorMessages);
                return;
            }

            if (accountLoginResponse.Data == null || string.IsNullOrEmpty(accountLoginResponse.Data.Token))
            {
                response.SetFail("Login fail");
                return;
            }

            response.Data = new AccountLoginResponseWrapper
            {
                Model = accountLoginResponse.Data.ToModel(),
            };
            response.SetSuccess();
        });
    }


    [HttpPost]
    public async Task<CommonApiResponse<object>> RefreshToken([FromBody] AccountRefreshTokenRequest request)
    {
        return await ProcessRequest<object>(async (response) =>
        {
            var validator = AccountRefreshTokenValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            var protoQuery = new AccountRefreshTokenQuery
            {
                RefreshToken = request.RefreshToken,
                AuthType = (int)request.AuthType,
                SessionId = _contextService.GetSessionKey().AsDefaultString(),
            };
            if (request.DeviceInfo != null) protoQuery.DeviceInfo = request.DeviceInfo;
            if (request.LoginType.HasValue) protoQuery.LoginType = (int)request.LoginType.Value;
            var uid = _contextService.GetAccountIdAsString();
            if (uid != null) protoQuery.ProcessUid = uid;

            var accountLoginResponse = await authenticationProtoClient.RefreshTokenAsync(protoQuery);
            if (!accountLoginResponse.Status)
            {
                response.SetFail(accountLoginResponse.ErrorMessages);
                return;
            }

            if (accountLoginResponse.Data == null || string.IsNullOrEmpty(accountLoginResponse.Data.Token))
            {
                response.SetFail("Login invalid");
                return;
            }

            response.Data = new
            {
                Model = accountLoginResponse.Data.ToModel(),
            };
            response.SetSuccess();
        });
    }


    [HttpPost]
    public async Task<CommonApiResponse<string>> Logout([FromBody] AccountLogoutRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var protoQuery = new AccountLogoutQuery
            {
                SessionId = _contextService.GetSessionKey().AsDefaultString(),
            };
            var uid = _contextService.GetAccountIdAsString();
            if (uid != null) protoQuery.ProcessUid = uid;

            var logoutResponse = await authenticationProtoClient.LogOutAsync(protoQuery);

            response.Data = logoutResponse.Status ? "Logout successful" : "Logout fail";
            if (!logoutResponse.Status)
            {
                response.SetFail(logoutResponse.ErrorMessages);
                return;
            }

            response.SetSuccess();
        });
    }


    [PermissionController(EPermission.Administrator)]
    [PermissionRequired("Add/Change Permission for user (API)", EPermission.Administrator)]
    [HttpPost]
    public async Task<CommonApiResponse<string>> ChangePermission(
        [FromBody] AccountPermissionAddOrChangeRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var validator = AccountPermissionRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            var protoReq = new AccountPermissionExceptionsAddOrChangeCommand();
            foreach (var controller in request.Controllers)
            {
                protoReq.Items.Add(new AccountPermissionExceptionAddOrChangeCommand
                {
                    AccountId = request.AccountId.ToString(),
                    ControllerCode = controller.ControllerCode,
                    AccessPermission = (int)controller.AccessPermission,
                });
            }

            var addOrChangePermissionResponse =
                await accountPermissionProtoClient.AddOrChangeAccountPermissionExceptionAsync(protoReq);

            response.Data = addOrChangePermissionResponse.Status
                ? "Change Permission successful"
                : "Change Permission fail";
            if (!addOrChangePermissionResponse.Status)
            {
                response.SetFail(addOrChangePermissionResponse.ErrorMessages);
                return;
            }

            response.SetSuccess();
        });
    }

    [PermissionController(EPermission.Administrator)]
    [PermissionRequired]
    [HttpPost]
    public async Task<CommonApiResponse<string>> ChangePermissionMany(
        [FromBody] AccountPermissionExceptionAddOrChangeManyRequest request)
    {
        return await ProcessRequest<string>(async (response) =>
        {
            var validator = AccountPermissionExceptionAddOrChangeManyRequestValidator.ValidateRequest(request);
            if (!validator.IsValid)
            {
                response.SetFail(validator.Errors.Select(p => p.ToString()));
                return;
            }

            var protoReq = new AccountPermissionExceptionAddOrChangeManyCommand();
            if (request.AccountIds is { Length: > 0 })
                protoReq.AccountIds.AddRange(request.AccountIds.Select(x => x.ToString()));
            if (request.GroupIds is { Length: > 0 })
                protoReq.GroupIds.AddRange(request.GroupIds.Select(x => x.ToString()));
            if (request.Controllers is { Length: > 0 })
            {
                foreach (var c in request.Controllers)
                {
                    protoReq.ControllerComponents.Add(new AccountPermissionExceptionAddOrChangeManyControllerCodeCommand
                    {
                        ControllerCode = c.ControllerCode,
                        AccessPermission = (int)c.AccessPermission,
                    });
                }
            }

            var addOrChangePermissionResponse =
                await accountPermissionProtoClient.AddOrChangeManyAccountPermissionExceptionAsync(protoReq);

            response.Data = addOrChangePermissionResponse.Status
                ? "Change Permission successful"
                : "Change Permission fail";
            if (!addOrChangePermissionResponse.Status)
            {
                response.SetFail(addOrChangePermissionResponse.ErrorMessages);
                return;
            }

            response.SetSuccess();
        });
    }


    #region private func

    [Obsolete("Obsolete")]
    private string CreateHashPassword(string password, bool isAddMd5 = true)
    {
        if (string.IsNullOrWhiteSpace(password))
            return string.Empty;
        if (isAddMd5)
            password = CreateMd5(password).ToLower();
        byte[] salt = Encoding.UTF8.GetBytes(ContextServiceCode.DefaultSaltPassword);
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 369);
        byte[] hash = pbkdf2.GetBytes(20); // 160 bit
        return Convert.ToBase64String(hash);
    }

    private static string GeneratePassword(int length = 20)
    {
        const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
        const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string specialChars = "!@#$%^&*()-_=+[]{};:,.<>?";
        string allChars = lowerChars + upperChars + digits + specialChars;
        int minChars = 8;
        if (length < minChars)
            throw new ArgumentException($"Password length must be at least {minChars} to include all character types.");

        var random = new Random();
        var sb = new StringBuilder();

        sb.Append(lowerChars[random.Next(lowerChars.Length)]);
        sb.Append(upperChars[random.Next(upperChars.Length)]);
        sb.Append(digits[random.Next(digits.Length)]);
        sb.Append(specialChars[random.Next(specialChars.Length)]);

        for (int i = sb.Length; i < length; i++) // random
            sb.Append(allChars[random.Next(allChars.Length)]);
        return new string(sb.ToString().OrderBy(_ => random.Next()).ToArray());
    }

    // for old system account
    private static string CreateMd5(string input)
    {
        using MD5 md5 = MD5.Create();
        byte[] inputBytes = Encoding.ASCII.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes);
    }

    #endregion private func

}