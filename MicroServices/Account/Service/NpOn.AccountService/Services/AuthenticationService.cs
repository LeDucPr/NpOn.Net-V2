using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Common.Applications.NpOn.CommonApplication.Services;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.CommonScope;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Events;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Senders;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Events;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Contracts.NpOn.AccountServiceDomain.Domains;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Definitions.NpOn.ShareAccountConstant;
using MicroServices.Account.Service.NpOn.IAccountService;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages.Messages;
using Microsoft.IdentityModel.Tokens;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public class AuthenticationService(
    IPostgresFactoryWrapper baseRepository,
    IAuthenticationStorageAdapter authenticationStorageAdapter,
    // IAccountInfoStorageAdapter accountInfoStorageAdapter,
    IZeroMqTwoWayFactory zeroMqTwoWayFactory,
    IAccountPermissionService accountPermissionService,
    IAccountTokenAndPermissionRedisRepository redisRepository,
    IRabbitMqProducer rabbitMqProducer,
    // IKafkaProducer kafkaProducer,
    ILogger<CommonService> logger
) : CommonService(logger), IAuthenticationService
{
    private readonly int _expireTokenMinutes =
        EApplicationConfiguration.LoginTokenExpiresTime.GetAppSettingConfig().AsDefaultInt() != 0
            ? EApplicationConfiguration.LoginTokenExpiresTime.GetAppSettingConfig().AsDefaultInt()
            : 480;

    #region Signup And Accept

    public async Task<CommonResponse<AccountLoginRModel>> Signup(AccountSignupCommand command)
    {
        return await CommonProcess<AccountLoginRModel>(async (response) =>
        {
            List<AccountRModel>? existAccounts =
                await authenticationStorageAdapter.AccountGetByNumberPhoneOrEmailOrUsername(command.PhoneNumber,
                    command.Email,
                    command.UserName);

            if (existAccounts is { Count: > 0 }) // existed
            {
                if (existAccounts.Any(x => x.PhoneNumber == command.PhoneNumber))
                    response.SetFail("NumberPhone is Existed");
                if (existAccounts.Any(x => x.UserName == command.UserName))
                    response.SetFail("UserName is Existed");
                if (existAccounts.Any(x => x.Email == command.Email))
                    response.SetFail("UserName is Existed");
                return;
            }

            Contracts.NpOn.AccountServiceDomain.Domains.Account account =
                new Contracts.NpOn.AccountServiceDomain.Domains.Account(command);
            if (!(await baseRepository.Add([account]))?.Status ?? false)
            {
                response.SetFail("Add new Account fail");
                return;
            }

            CommonResponse<AccountLoginRModel> loginResponse = await Login(new AccountLoginQuery
            {
                UserName = command.UserName,
                Password = command.Password,
                AuthType = command.AuthType,
                ClientId = command.ClientId,
            });

            if (!loginResponse.Status)
            {
                response.SetFail("Save Account Login fail after create account");
                return;
            }

            response.Data = loginResponse.Data;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<bool>> ChangeAccountStatus(AccountSetStatusCommand command)
    {
        return await CommonProcess<bool>(async (response, pipelineScope) =>
        {
            string accountIdAsString = command.AccountId.AsDefaultString();
            AccountRModel? existAccounts =
                await authenticationStorageAdapter.AccountGetById(accountIdAsString);
            if (existAccounts == null)
            {
                response.SetFail("Account not found");
                response.Data = false;
                return;
            }

            if (existAccounts.Status == command.AccountStatus)
            {
                response.Data = true;
                response.SetSuccess();
                return;
            }

            Contracts.NpOn.AccountServiceDomain.Domains.Account accountChangeStatus =
                new Contracts.NpOn.AccountServiceDomain.Domains.Account(existAccounts);
            accountChangeStatus.ChangeStatus(command);
            // if (!(await baseRepository.Update([accountChangeStatus]))?.Status ?? false)
            // {
            //     response.SetFail($"Account Change Status {command.AccountStatus.AsDefaultString()} fail");
            //     response.Data = false;
            //     return;
            // }

            // NpOnDbTransactionPipeline transactionPipeline = (new NpOnDbTransactionPipeline())
            //     .Register(baseRepository)
            //     .Register(baseRepository.CommandBuilder([accountChangeStatus], ERepositoryAction.Update));
            // if (!(await transactionPipeline.Invoke()).IsCompleted)
            // {
            //     response.SetFail($"Account Change Status {command.AccountStatus.AsDefaultString()} fail");
            //     response.Data = false;
            //     return;
            // }

            await pipelineScope.Next(
                new NpOnDbTransactionPipeline()
                    .Register(baseRepository)
                    .Register(baseRepository.CommandBuilder([accountChangeStatus], ERepositoryAction.Update)));

            var isDeleteCachePermissionCache =
                await redisRepository.DeleteCachingPermissionExceptionsByAccountId(accountIdAsString);
            if (!isDeleteCachePermissionCache)
            {
                response.SetFail($"Account Change Status Cache {command.AccountStatus.AsDefaultString()} fail");
                response.Data = false;
                return;
            }

            // await pipelineScope.Next(
            //     new NpOnServiceTransactionPipeline().Register(
            //         Login(new AccountLoginQuery
            //         {
            //             UserName = "KhaBanh",
            //             Password = "jyGimGTAj2niJMgxijU7x7iR1RA=",
            //             AuthType = EAuthentication.WebApp,
            //             ClientId = "WEB_TEST_C"
            //         }).ContinueWith(t => t.Result.Status) // giữ Task<bool>
            //     )
            // );
            // hoặc 
            await pipelineScope.Next(
                new NpOnServiceTransactionPipeline().Register(
                    Task.Run(async () =>
                    {
                        var resp = await Login(new AccountLoginQuery
                        {
                            UserName = "KhaBanh",
                            Password = "jyGimGTAj2niJMgxijU7x7iR1RA=",
                            AuthType = EAuthentication.WebApp,
                            ClientId = "WEB_TEST_C"
                        });
                        return resp.Status;
                    })
                )
            );
            await pipelineScope.Next(null);


            response.Data = true;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<bool>> ChangeAccountPassword(AccountChangePasswordCommand command)
    {
        return await CommonProcess<bool>(async (response) =>
        {
            string accountIdAsString = command.AccountId.AsDefaultString();
            AccountRModel? existAccounts =
                await authenticationStorageAdapter.AccountGetById(accountIdAsString);
            if (existAccounts == null)
            {
                response.SetFail("Account not found");
                response.Data = false;
                return;
            }

            Contracts.NpOn.AccountServiceDomain.Domains.Account accountChangeStatus =
                new Contracts.NpOn.AccountServiceDomain.Domains.Account(existAccounts);
            accountChangeStatus.ChangeNewPassword(command);
            if (!(await baseRepository.Update([accountChangeStatus]))?.Status ?? false)
            {
                response.SetFail($"Account Change Password fail");
                response.Data = false;
                return;
            }

            response.Data = true;
            response.SetSuccess();
        });
    }

    #endregion Signup And Accept

    public async Task<CommonResponse<AccountLoginRModel>> Login(AccountLoginQuery query)
    {
        return await CommonProcess<AccountLoginRModel>(async (response) =>
        {
            AccountRModel? accountRModel =
                await authenticationStorageAdapter.AccountGetByUsernameAndPassword(
                    query.UserName.AsDefaultString(), query.Password.AsDefaultString());
            if (accountRModel == null || accountRModel.Status != EAccountStatus.Active)
            {
                response.SetFail("Account not found");
                return;
            }

            AccountLoginRModel accountLoginRModel = CreateToken(
                accountRModel, query.AuthType /*, ELoginType.Default*/);

            // Gọi hàm và vứt đuôi .FireAndForget() vào là xong, gọn gàng, tự khởi động ngầm
            zeroMqTwoWayFactory.TrySendTo(
                new[] { EUrlConfiguration.TrackerServiceUrl }, 
                new TrackerTest2WayRequestCommand { Action = "CC" }
            ).FireAndForget();
            
            
            if (query.IsEnableMultiDevice)
            {
            }

            // kafkaProducer.AddEvent(new KafkaEvent<AccountSaveLoginEvent>()
            // {
            //     MessageContent = accountLoginRModel.ToLoginEvent()
            // });

            rabbitMqProducer.AddEvent(new RabbitMqEvent<AccountSaveLoginEvent>()
            {
                MessageContent = accountLoginRModel.ToLoginEvent()
            });
            response.Data = accountLoginRModel;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<AccountLoginRModel>> RefreshToken(AccountRefreshTokenQuery query)
    {
        return await CommonProcess<AccountLoginRModel>(async (response) =>
        {
            List<AccountLoginRModel>?
                accountInfoObjects =
                    await authenticationStorageAdapter.AccountLoginInfoGetByRefreshToken(query.RefreshToken);

            if (accountInfoObjects is not { Count: > 0 })
            {
                response.SetFail("RefreshToken not found");
                return;
            }

            AccountLoginRModel accountRModel = accountInfoObjects.First();
            if (accountRModel.SessionId != query.SessionId || accountRModel.TokenStatus != ETokenStatus.Active)
            {
                response.SetFail("SessionId does not match");
                return;
            }

            // logout for old session
            rabbitMqProducer.AddEvent(new RabbitMqEvent<AccountSaveLogoutEvent>()
            {
                MessageContent = accountRModel.ToLogoutEvent(),
            });

            // get account to sync for new session 

            string accountId = accountRModel.AccountId.AsDefaultString();
            AccountRModel? accountObject =
                await authenticationStorageAdapter.AccountGetById(accountId);
            if (accountObject == null)
            {
                response.SetFail("Account not found");
                return;
            }

            AccountLoginRModel accountLoginRModel = CreateToken(
                accountObject, query.AuthType /*, ELoginType.Default*/);

            rabbitMqProducer.AddEvent(new RabbitMqEvent<AccountSaveLoginEvent>()
            {
                MessageContent = accountLoginRModel.ToLoginEvent()
            });

            response.Data = accountLoginRModel;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<AccountLoginRModel>> GetLogonTokenBySessionId(
        AccountGetLogonInfoBySessionIdQuery query)
    {
        return await CommonProcess<AccountLoginRModel>(async (response) =>
        {
            AccountLoginRModel? accountLoginInfoRModel =
                await authenticationStorageAdapter.AccountLoginInfoGetBySessionId(query.SessionId);
            if (accountLoginInfoRModel == null)
            {
                response.SetFail("AccountLoginInfo not found");
                return;
            }

            response.Data = accountLoginInfoRModel;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<bool>> IsValidLogonPermissionExceptionControllers(
        AccountPermissionExceptionCachingCheckValidQuery query)
    {
        return await CommonProcess<bool>(async (response) =>
        {
            long currentCatalogVersion = await redisRepository.GetPermissionCatalogVersion();
            AccountPermissionExceptionCacheState? state =
                await redisRepository.GetAccountPermissionState(query.AccountId);

            // Cache miss / expiry / post-deploy catalog bump -> read-through self-heal from DB
            if (state == null || state.CatalogVersion != currentCatalogVersion)
            {
                var healed = await accountPermissionService.HealAndRefreshPermissionState(query.AccountId);
                state = healed.Status ? healed.Data : null;
            }

            if (state == null || !state.IsAccountActive)
            {
                response.Data = false;
                response.SetSuccess();
                return;
            }

            AccountPermissionExceptionCacheItem? matchedException = state.Exceptions.FirstOrDefault(x =>
                string.Equals(x.ControllerCode, query.ControllerCode, StringComparison.Ordinal));
            if (matchedException != null)
            {
                response.Data = matchedException.AccessPermission == EPermissionAccessController.Enable;
                response.SetSuccess();
                return;
            }

            // No explicit exception -> check against live Account BasePermissionMask & current synced controller flags
            var controllerResp = await accountPermissionService.GetPermissionControllerByCode(query.ControllerCode);
            if (controllerResp.Status && controllerResp.Data != null)
            {
                response.Data = AccountPermissionService.HasEffectiveBasePermission(
                    state.BasePermissionMask,
                    controllerResp.Data.Permission,
                    controllerResp.Data.RequiredPermission);
                response.SetSuccess();
                return;
            }

            // Fail closed when controller metadata is missing or lookup failed — never trust potentially stale JWT claims.
            response.Data = false;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<string>> LogOut(AccountLogoutQuery query)
    {
        return await CommonProcess<string>(async (response) =>
        {
            AccountLoginRModel? accountLoginInfoRModel =
                await authenticationStorageAdapter.AccountLoginInfoGetBySessionId(query.SessionId);
            if (accountLoginInfoRModel == null)
            {
                response.SetFail("Invalid Token");
                return;
            }

            if (accountLoginInfoRModel.TokenStatus == ETokenStatus.Active)
            {
                rabbitMqProducer.AddEvent(new RabbitMqEvent<AccountSaveLogoutEvent>()
                {
                    MessageContent = accountLoginInfoRModel.ToLogoutEvent(),
                });
            }

            response.Data = "Logout successful";
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse> SaveLogin(AccountSaveLoginEvent @event)
    {
        return await CommonProcess(async (response) =>
        {
            AccountLoginRModel accountLoginRModel = @event.ToObject();
            AccountLogin domain = new AccountLogin(accountLoginRModel);
            if (!(await baseRepository.Add([domain]))?.Status ?? false)
            {
                response.SetFail($"AccountLoginInfo save login fail");
                return;
            }

            string accountId = @event.AccountId.AsDefaultString();

            // Self-heal obsolete exceptions on login and warm Redis permission cache state
            await accountPermissionService.HealAndRefreshPermissionState(accountId);

            response.SetSuccess();
        });
    }

    public async Task<CommonResponse> SaveLogout(AccountSaveLogoutEvent @event)
    {
        return await CommonProcess(async (response) =>
        {
            AccountLoginRModel accountLogin = @event.ToObject();
            AccountLogin domain = new AccountLogin(accountLogin);
            domain.Logout();

            if (!(await baseRepository.Update([domain]))?.Status ?? false)
            {
                response.SetFail($"AccountLoginInfo save logout fail");
                return;
            }

            response.SetSuccess();
        });
    }

    #region Private Method

    private AccountLoginRModel CreateToken(
        AccountRModel account,
        EAuthentication authType,
        ELoginType loginType = ELoginType.Default
    )
    {
        string sessionKey =
            $"{ContextServiceCode.SessionIdPrefix}-{account.UserName}-{CommonUtilityMode.GenerateGuidAsString()}";
        int minuteExpire = _expireTokenMinutes;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(EApplicationConfiguration.JwtTokensKey.GetAppSettingConfig()
            .AsDefaultString());
        List<Claim> claims =
        [
            new(ContextServiceCode.SessionCode, sessionKey),
            new(ContextServiceCode.TokenCreatedUtc, DateTime.UtcNow.AddMinutes(minuteExpire).ToIso8601()),
            new(ContextServiceCode.Permission, account.Permission.EnumAsInt().AsDefaultString()),
            new($"{ContextServiceCode.MinuteExpirePrefix}", minuteExpire.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, account.UserName),
            new(ContextServiceCode.LoginTypeEnumCode, loginType.EnumAsInt().AsDefaultString()),
            new(JwtRegisteredClaimNames.Sid, account.Id.AsDefaultString()),
            new(JwtHeaderParameterNames.Typ, loginType.GetDisplayName())
        ];

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(minuteExpire),
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        string tokenValue = tokenHandler.WriteToken(token);

        // set
        AccountLoginRModel accountLogin =
            new AccountLoginRModel
            {
                // Id = default,
                AccountId = account.Id,
                UserName = account.UserName,
                Password = account.Password,
                FullName = account.FullName,
                PhoneNumber = account.PhoneNumber,
                Email = account.Email,
                AvatarUrl = account.AvatarUrl,
                AuthType = authType,
                LoginType = loginType,
                SessionId = sessionKey,
                MinuteExpire = minuteExpire,
                RefreshToken = CommonUtilityMode.GenerateGuidAsString(),
                Permission = account.Permission,
                TokenStatus = ETokenStatus.Active,
                Token = tokenValue,
            };

        return accountLogin;
    }

    #endregion Private Method
}

public static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        // Nếu task xảy ra lỗi, bốc exception ra để log lại, tránh làm sập ứng dụng
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                foreach (var ex in t.Exception.Flatten().InnerExceptions)
                {
                    // Console.WriteLine($"[Background Task Crash]: {ex.Message}");
                }
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}