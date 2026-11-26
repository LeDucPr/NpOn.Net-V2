using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Attributes;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Middlewares;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Events;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Senders;
using Common.Infrastructures.NpOn.RedisExtCm.Results;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using Grpc.Core;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Events;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Contracts.NpOn.AccountServiceDomain.Domains;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Definitions.NpOn.ShareAccountConstant;
using MicroServices.Account.Service.NpOn.AccountService.Services;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using ProtoAccount = NpOn.Protos.Account.V1;
using StackExchange.Redis;
using AccountDomain = MicroServices.Account.Contracts.NpOn.AccountServiceDomain.Domains.Account;

namespace NpOn.Core.Tests;

public class PermissionSelfHealingTests
{
    private const string TestJwtSecret = "NpOn-Permission-SelfHealing-Test-Jwt-Key-At-Least-64-Bytes-Long-2026!";

    private static readonly object ConfigInitLock = new();

    private static void EnsureJwtConfigInitialized()
    {
        lock (ConfigInitLock)
        {
            var cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtTokensKey"] = TestJwtSecret,
                    ["LoginTokenExpiresTime"] = "60",
                    ["RefreshTokenExpiredTime"] = "1440",
                })
                .Build();
            cfg.InitConfigs(typeof(EApplicationConfiguration));
        }
    }

    private sealed class PermissionTestEnv
    {
        public InMemoryPermissionStore Store { get; } = new();
        public InMemoryRedisFactoryWrapper RedisWrapper { get; } = new();
        public AccountTokenAndPermissionRedisRepository RedisRepo { get; }
        public InMemoryPostgresFactoryWrapper Postgres { get; }
        public InMemoryAuthStorageAdapter AuthStorage { get; }
        public InMemoryPermissionStorageAdapter PermStorage { get; }
        public InMemoryGroupStorageAdapter GroupStorage { get; }
        public AccountPermissionService PermissionServiceBackend { get; }
        public AuthenticationService AuthServiceBackend { get; }

        public PermissionTestEnv()
        {
            EnsureJwtConfigInitialized();
            RedisRepo = new AccountTokenAndPermissionRedisRepository(RedisWrapper);
            Postgres = new InMemoryPostgresFactoryWrapper(Store);
            AuthStorage = new InMemoryAuthStorageAdapter(Store);
            PermStorage = new InMemoryPermissionStorageAdapter(Store);
            GroupStorage = new InMemoryGroupStorageAdapter(Store);

            PermissionServiceBackend = new AccountPermissionService(
                AuthStorage,
                PermStorage,
                RedisRepo,
                GroupStorage,
                Postgres,
                NullLogger<AccountPermissionService>.Instance);

            AuthServiceBackend = new AuthenticationService(
                Postgres,
                AuthStorage,
                new NoOpZeroMqTwoWayFactory(),
                PermissionServiceBackend,
                RedisRepo,
                new NoOpRabbitMqProducer(),
                NullLogger<AccountPermissionService>.Instance);
        }

        public AuthenService CreateGatewayAuthenService() =>
            new(new InProcessAuthenticationProtoClient(AuthServiceBackend));

        public PermissionService CreateGatewayPermissionService(
            IReadOnlyList<ActionDescriptor> descriptors,
            bool failSync = false) =>
            new(
                CreateContextService(),
                new InProcessAccountPermissionProtoClient(PermissionServiceBackend, failSync),
                new StaticActionDescriptorCollectionProvider(descriptors),
                NullLogger<PermissionService>.Instance);

        public ContextService CreateContextService(HttpContext? httpContext = null)
        {
            var accessor = new HttpContextAccessor { HttpContext = httpContext };
            var services = new ServiceCollection().BuildServiceProvider();
            return new ContextService(
                NullLogger<ContextService>.Instance,
                accessor,
                services,
                CreateGatewayAuthenService(),
                new NoOpLogAction());
        }
    }

    // -------------------------------------------------------------------------
    // 1. Controller Sync & Lifecycle (T01 - T03)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task T01_SyncNewControllers_InsertsAndBumpsCatalogVersion()
    {
        var env = new PermissionTestEnv();
        long initialCatalogVersion = await env.RedisRepo.GetPermissionCatalogVersion();
        Assert.Equal(0, initialCatalogVersion);

        var descriptors = new ActionDescriptor[]
        {
            BuildControllerDescriptor("Orders", "GetList", EPermission.User | EPermission.Administrator,
                new PermissionRequiredAttribute("Read orders", EPermission.User)),
            BuildControllerDescriptor("Admin", "DeleteUser", EPermission.Administrator,
                new PermissionRequiredAttribute("Delete user", EPermission.Administrator)),
        };

        var gatewaySync = env.CreateGatewayPermissionService(descriptors);
        bool synced = await gatewaySync.AutoSyncPermissionController();
        Assert.True(synced);

        string assemblyHost = typeof(SampleApiController).Assembly.GetName().Name!;
        string ordersCode = $"{assemblyHost}.Orders.GetList";
        string adminCode = $"{assemblyHost}.Admin.DeleteUser";

        var savedOrders = env.Store.GetController(ordersCode);
        var savedAdmin = env.Store.GetController(adminCode);

        Assert.NotNull(savedOrders);
        Assert.Equal(EPermission.User | EPermission.Administrator, savedOrders!.Permission);
        Assert.Equal(EPermission.User, savedOrders.RequiredPermission);
        Assert.Equal("Read orders", savedOrders.Description);

        Assert.NotNull(savedAdmin);
        Assert.Equal(EPermission.Administrator, savedAdmin!.Permission);
        Assert.Equal(EPermission.Administrator, savedAdmin.RequiredPermission);

        long bumpedCatalogVersion = await env.RedisRepo.GetPermissionCatalogVersion();
        Assert.True(bumpedCatalogVersion > initialCatalogVersion);
    }

    [Fact]
    public async Task T02_ClearOldControllers_RemovesDeletedOrRenamedAndCleansOrphanedExceptions()
    {
        var env = new PermissionTestEnv();
        string host = typeof(SampleApiController).Assembly.GetName().Name!;
        Guid userId = Guid.NewGuid();
        env.Store.UpsertAccount(userId, "alice", EPermission.User, EAccountStatus.Active);

        // Old deploy synced V1 controllers + user exceptions (including renamed/removed legacy route)
        Guid oldVersion = Guid.NewGuid();
        string activeCode = $"{host}.Orders.GetList";
        string removedCode = $"{host}.Orders.LegacyEndpoint";
        string legacyOrphanCode = "HCare.SSO.Legacy.Action";

        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = host, VersionId = oldVersion, ControllerName = activeCode,
                Permission = EPermission.Administrator
            },
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = host, VersionId = oldVersion, ControllerName = removedCode,
                Permission = EPermission.Administrator
            }
        ]);

        env.Store.UpsertException(userId, activeCode, EPermissionAccessController.Enable);
        env.Store.UpsertException(userId, removedCode, EPermissionAccessController.Enable);
        env.Store.UpsertException(userId, legacyOrphanCode, EPermissionAccessController.Disable);

        // Redeploy only activeCode (removedCode is gone)
        var gatewaySync = env.CreateGatewayPermissionService([
            BuildControllerDescriptor("Orders", "GetList", EPermission.Administrator,
                new PermissionRequiredAttribute("Read orders"))
        ]);

        long versionBefore = await env.RedisRepo.GetPermissionCatalogVersion();
        bool ok = await gatewaySync.AutoSyncPermissionController();
        Assert.True(ok);

        Assert.NotNull(env.Store.GetController(activeCode));
        Assert.Null(env.Store.GetController(removedCode));

        // Orphaned exceptions (removedCode + legacyOrphanCode) must be purged; activeCode Enable exception remains
        var dbExceptions = env.Store.GetExceptionsForAccount(userId);
        Assert.Single(dbExceptions);
        Assert.Equal(activeCode, dbExceptions[0].ControllerCode);

        long versionAfter = await env.RedisRepo.GetPermissionCatalogVersion();
        Assert.True(versionAfter > versionBefore);
    }

    [Fact]
    public async Task T03_AutoSync_BatchFailure_DoesNotClearExistingControllers()
    {
        var env = new PermissionTestEnv();
        string host = typeof(SampleApiController).Assembly.GetName().Name!;
        Guid existingVersion = Guid.NewGuid();
        string existingCode = $"{host}.Orders.Existing";

        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = host, VersionId = existingVersion, ControllerName = existingCode,
                Permission = EPermission.Administrator
            }
        ]);

        var gatewaySync = env.CreateGatewayPermissionService([
            BuildControllerDescriptor("Orders", "NewRoute", EPermission.User, new PermissionRequiredAttribute("New"))
        ], failSync: true);

        bool result = await gatewaySync.AutoSyncPermissionController();
        Assert.False(result);
        Assert.Equal(0, env.Store.ClearOldVersionCallCount);
        Assert.NotNull(env.Store.GetController(existingCode));
    }

    // -------------------------------------------------------------------------
    // 2. Deploy Permission-Attribute Flips (With Live OLD JWT) (T04 - T07)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task T04_DeployTightening_BlockedUserBeforeDeploy_StaysBlockedAfterDeploy()
    {
        var env = new PermissionTestEnv();
        const string host = "NpOn.SSO";
        const string endpointCode = "NpOn.SSO.Orders.Export";
        Guid userAId = Guid.NewGuid();
        Guid userBId = Guid.NewGuid();

        env.Store.UpsertAccount(userAId, "userA", EPermission.User, EAccountStatus.Active);
        env.Store.UpsertAccount(userBId, "userB", EPermission.User, EAccountStatus.Active);

        // Before deploy: User allowed, but UserA has explicit Disable exception
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = host, VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.User | EPermission.Administrator
            }
        ]);
        env.Store.UpsertException(userAId, endpointCode, EPermissionAccessController.Disable);

        // Both UserA and UserB warm Redis cache at login
        await env.AuthServiceBackend.SaveLogin(CreateLoginEvent(userAId, EPermission.User));
        await env.AuthServiceBackend.SaveLogin(CreateLoginEvent(userBId, EPermission.User));

        // Pre-deploy check: UserA blocked, UserB allowed
        Assert.False((await CheckPerm(env, userAId, endpointCode, isHasBasePerm: true)).Data);
        Assert.True((await CheckPerm(env, userBId, endpointCode, isHasBasePerm: true)).Data);

        // Also assert even BEFORE self-heal triggers (simulating stale cached state with Disable),
        // tightened base perm (isHasBasePerm=false) NEVER flips Disable into allow!
        var userAStateBeforeHeal = await env.RedisRepo.GetAccountPermissionState(userAId.ToString());
        Assert.NotNull(userAStateBeforeHeal);
        Assert.Contains(userAStateBeforeHeal!.Exceptions,
            e => e.ControllerCode == endpointCode && e.AccessPermission == EPermissionAccessController.Disable);

        // Deploy tightens endpoint to Administrator ONLY
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = host, VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator
            }
        ]);

        // (a) UserA stays blocked, even if an old caller passes IsHasBasePermission=true or false
        Assert.False((await CheckPerm(env, userAId, endpointCode, isHasBasePerm: false)).Data);
        // (b) UserB holding OLD live JWT is blocked immediately (even if stale middleware check had true)
        Assert.False((await CheckPerm(env, userBId, endpointCode, isHasBasePerm: true)).Data);
        // (c) Self-healing removed UserA's now-redundant Disable row
        Assert.Empty(env.Store.GetExceptionsForAccount(userAId));
    }

    [Fact]
    public async Task T05_DeployLoosening_EnabledUserBeforeDeploy_StaysAllowedAfterDeploy()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.Orders.Create";
        Guid userCId = Guid.NewGuid();
        env.Store.UpsertAccount(userCId, "userC", EPermission.User, EAccountStatus.Active);

        // Before deploy: Admin only; UserC has explicit Enable exception
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator
            }
        ]);
        env.Store.UpsertException(userCId, endpointCode, EPermissionAccessController.Enable);
        await env.AuthServiceBackend.SaveLogin(CreateLoginEvent(userCId, EPermission.User));
        Assert.True((await CheckPerm(env, userCId, endpointCode, isHasBasePerm: false)).Data);

        // Deploy widens endpoint to Administrator | User
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator | EPermission.User
            }
        ]);

        // UserC stays allowed cleanly and redundant Enable row was self-healed/deleted
        Assert.True((await CheckPerm(env, userCId, endpointCode, isHasBasePerm: true)).Data);
        Assert.Empty(env.Store.GetExceptionsForAccount(userCId));
    }

    [Fact]
    public async Task T06_RequiredPermission_TighteningAndExplicitEnablePrecedence()
    {
        var env = new PermissionTestEnv();
        string host = typeof(SampleApiController).Assembly.GetName().Name!;
        string endpointCode = $"{host}.Account.ChangePermission";
        Guid regularUserId = Guid.NewGuid();
        Guid disabledAdminId = Guid.NewGuid();

        env.Store.UpsertAccount(regularUserId, "regUser", EPermission.User, EAccountStatus.Active);
        env.Store.UpsertAccount(disabledAdminId, "disAdmin", EPermission.Administrator, EAccountStatus.Active);

        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = host,
                VersionId = Guid.NewGuid(),
                ControllerName = endpointCode,
                Permission = EPermission.User | EPermission.Administrator,
                RequiredPermission = EPermission.Administrator
            }
        ]);

        // (a) Tightened RequiredPermission blocks regular User without exception (403)
        int statusBlocked = await InvokePermissionMiddleware(
            env, regularUserId, EPermission.User, "Account", "ChangePermission",
            new PermissionControllerAttribute(EPermission.User | EPermission.Administrator),
            new PermissionRequiredAttribute("Manage perms", EPermission.Administrator));
        Assert.Equal(StatusCodes.Status403Forbidden, statusBlocked);

        // (b) Explicit Enable exception grants access EVEN when user lacks PermissionRequired flags (200)
        await env.PermissionServiceBackend.AddOrChangeAccountPermissionException([
            new AccountPermissionExceptionAddOrChangeCommand
            {
                AccountId = regularUserId,
                ControllerCode = endpointCode,
                AccessPermission = EPermissionAccessController.Enable
            }
        ]);

        int statusEnabled = await InvokePermissionMiddleware(
            env, regularUserId, EPermission.User, "Account", "ChangePermission",
            new PermissionControllerAttribute(EPermission.User | EPermission.Administrator),
            new PermissionRequiredAttribute("Manage perms", EPermission.Administrator));
        Assert.Equal(StatusCodes.Status200OK, statusEnabled);

        // (c) Explicit Disable always blocks Administrator (403)
        await env.PermissionServiceBackend.AddOrChangeAccountPermissionException([
            new AccountPermissionExceptionAddOrChangeCommand
            {
                AccountId = disabledAdminId,
                ControllerCode = endpointCode,
                AccessPermission = EPermissionAccessController.Disable
            }
        ]);

        int statusDisabledAdmin = await InvokePermissionMiddleware(
            env, disabledAdminId, EPermission.Administrator, "Account", "ChangePermission",
            new PermissionControllerAttribute(EPermission.User | EPermission.Administrator),
            new PermissionRequiredAttribute("Manage perms", EPermission.Administrator));
        Assert.Equal(StatusCodes.Status403Forbidden, statusDisabledAdmin);
    }

    [Fact]
    public async Task T07_MissingPermissionRequiredAttribute_Returns403()
    {
        var env = new PermissionTestEnv();
        Guid adminId = Guid.NewGuid();
        env.Store.UpsertAccount(adminId, "admin", EPermission.Administrator, EAccountStatus.Active);

        int statusCode = await InvokePermissionMiddleware(
            env, adminId, EPermission.Administrator, "Account", "NoRequiredAttr",
            new PermissionControllerAttribute(EPermission.Administrator),
            requiredAttr: null);

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    // -------------------------------------------------------------------------
    // 3. Account Role & Status Changes (With Live OLD JWT) (T08 - T10)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task T08_AccountRoleDowngrade_OldJwtLive_ImmediatelyBlocked()
    {
        var env = new PermissionTestEnv();
        const string adminEndpoint = "NpOn.SSO.Account.ChangePassword";
        const string userEndpoint = "NpOn.SSO.Orders.View";
        Guid accountId = Guid.NewGuid();

        // Originally Administrator with Disable exception on adminEndpoint
        env.Store.UpsertAccount(accountId, "demotedUser", EPermission.Administrator, EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = adminEndpoint,
                Permission = EPermission.Administrator
            },
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = userEndpoint,
                Permission = EPermission.User
            }
        ]);
        env.Store.UpsertException(accountId, adminEndpoint, EPermissionAccessController.Disable);

        // Downgrade role in DB to User + invalidate cache while old JWT still claims Administrator (isHasBasePerm=true)
        env.Store.UpsertAccount(accountId, "demotedUser", EPermission.User, EAccountStatus.Active);
        await env.RedisRepo.DeleteCachingPermissionExceptionsByAccountId(accountId.ToString());

        // Old JWT claims Admin (isHasBasePerm: true), MUST be rejected based on live BasePermissionMask (User)!
        Assert.False((await CheckPerm(env, accountId, adminEndpoint, isHasBasePerm: true)).Data);
        // Redundant Disable exception on adminEndpoint should have self-healed away
        Assert.Empty(env.Store.GetExceptionsForAccount(accountId));
        // User endpoint is allowed based on live BasePermissionMask = User
        Assert.True((await CheckPerm(env, accountId, userEndpoint, isHasBasePerm: false)).Data);
    }

    [Fact]
    public async Task T09_AccountRoleUpgrade_OldJwtLive_HealsRedundantEnableExceptions()
    {
        var env = new PermissionTestEnv();
        const string adminEndpoint = "NpOn.SSO.Account.ChangePassword";
        Guid accountId = Guid.NewGuid();

        env.Store.UpsertAccount(accountId, "promotedUser", EPermission.User, EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = adminEndpoint,
                Permission = EPermission.Administrator
            }
        ]);
        env.Store.UpsertException(accountId, adminEndpoint, EPermissionAccessController.Enable);

        // Upgrade in DB to Administrator and force cache refresh/miss
        env.Store.UpsertAccount(accountId, "promotedUser", EPermission.Administrator, EAccountStatus.Active);
        await env.RedisRepo.DeleteCachingPermissionExceptionsByAccountId(accountId.ToString());

        // Old JWT still says User (isHasBasePerm=false), yet live state allows and cleans redundant Enable
        Assert.True((await CheckPerm(env, accountId, adminEndpoint, isHasBasePerm: false)).Data);
        Assert.Empty(env.Store.GetExceptionsForAccount(accountId));
    }

    [Fact]
    public async Task T10_AccountDeactivated_OldJwtLive_ImmediatelyRejected()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.Orders.View";
        Guid accountId = Guid.NewGuid();

        env.Store.UpsertAccount(accountId, "bannedUser", EPermission.Administrator, EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator
            }
        ]);
        await env.AuthServiceBackend.SaveLogin(CreateLoginEvent(accountId, EPermission.Administrator));
        Assert.True((await CheckPerm(env, accountId, endpointCode, isHasBasePerm: true)).Data);

        // Ban account in DB -> invalidate Redis cache
        await env.AuthServiceBackend.ChangeAccountStatus(new AccountSetStatusCommand
        {
            AccountId = accountId,
            AccountStatus = EAccountStatus.Unactive
        });

        Assert.False((await CheckPerm(env, accountId, endpointCode, isHasBasePerm: true)).Data);
    }

    // -------------------------------------------------------------------------
    // 4. Bitwise [Flags], FirstOrDefault, Unknown & Pagination Bugs (T11 - T14)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task T11_FirstOrDefaultRegression_ExceptionOnlyAffectsMatchingController()
    {
        var env = new PermissionTestEnv();
        const string e1 = "NpOn.SSO.Controller.E1";
        const string e2 = "NpOn.SSO.Controller.E2";
        Guid userWithDisableId = Guid.NewGuid();
        Guid userWithEnableId = Guid.NewGuid();

        env.Store.UpsertAccount(userWithDisableId, "uDisable", EPermission.Administrator, EAccountStatus.Active);
        env.Store.UpsertAccount(userWithEnableId, "uEnable", EPermission.User, EAccountStatus.Active);

        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = e1,
                Permission = EPermission.Administrator
            },
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = e2,
                Permission = EPermission.Administrator
            }
        ]);

        await env.PermissionServiceBackend.AddOrChangeAccountPermissionException([
            new AccountPermissionExceptionAddOrChangeCommand
            {
                AccountId = userWithDisableId, ControllerCode = e1, AccessPermission = EPermissionAccessController.Disable
            }
        ]);
        await env.PermissionServiceBackend.AddOrChangeAccountPermissionException([
            new AccountPermissionExceptionAddOrChangeCommand
            {
                AccountId = userWithEnableId, ControllerCode = e1, AccessPermission = EPermissionAccessController.Enable
            }
        ]);

        // Disable on E1 must NOT block E2; Enable on E1 must NOT unlock E2
        Assert.False((await CheckPerm(env, userWithDisableId, e1, true)).Data);
        Assert.True((await CheckPerm(env, userWithDisableId, e2, true)).Data);

        Assert.True((await CheckPerm(env, userWithEnableId, e1, false)).Data);
        Assert.False((await CheckPerm(env, userWithEnableId, e2, false)).Data);
    }

    [Fact]
    public async Task T12_MultiFlagBitwise_MatchesOverlappingFlags()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.MultiFlag.Endpoint";
        Guid accountId = Guid.NewGuid();

        // Account has SystemAdministrator (1) | User (64) = 65
        // Controller has Administrator (2) | User (64) = 66
        // Overlap: User (64) -> base permission MUST match via bitwise check!
        env.Store.UpsertAccount(accountId, "multiFlag", EPermission.SystemAdministrator | EPermission.User,
            EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator | EPermission.User
            }
        ]);

        // Redundant Enable should be discarded cleanly (not added to DB); Disable should be persisted!
        var enableResp = await env.PermissionServiceBackend.AddOrChangeAccountPermissionException([
            new AccountPermissionExceptionAddOrChangeCommand
            {
                AccountId = accountId, ControllerCode = endpointCode, AccessPermission = EPermissionAccessController.Enable
            }
        ]);
        Assert.True(enableResp.Status);
        Assert.Empty(env.Store.GetExceptionsForAccount(accountId));

        var disableResp = await env.PermissionServiceBackend.AddOrChangeAccountPermissionException([
            new AccountPermissionExceptionAddOrChangeCommand
            {
                AccountId = accountId, ControllerCode = endpointCode, AccessPermission = EPermissionAccessController.Disable
            }
        ]);
        Assert.True(disableResp.Status);
        Assert.Single(env.Store.GetExceptionsForAccount(accountId));
        Assert.False((await CheckPerm(env, accountId, endpointCode, true)).Data);
    }

    [Fact]
    public async Task T13_UnknownPermissionUser_CanReceiveEnableException()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.UnknownUser.Endpoint";
        Guid unknownUserId = Guid.NewGuid();

        env.Store.UpsertAccount(unknownUserId, "guest", EPermission.Unknown, EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator
            }
        ]);

        var addResp = await env.PermissionServiceBackend.AddOrChangeAccountPermissionException([
            new AccountPermissionExceptionAddOrChangeCommand
            {
                AccountId = unknownUserId, ControllerCode = endpointCode, AccessPermission = EPermissionAccessController.Enable
            }
        ]);
        Assert.True(addResp.Status);
        Assert.Single(env.Store.GetExceptionsForAccount(unknownUserId));
        Assert.True((await CheckPerm(env, unknownUserId, endpointCode, false)).Data);
    }

    [Fact]
    public async Task T14_AddOrChangeMany_AccountIdsAndGroupIds_DoesNotSkipGroupPage0()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.Many.Endpoint";
        Guid directUserId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid groupMemberId = Guid.NewGuid();

        env.Store.UpsertAccount(directUserId, "direct", EPermission.User, EAccountStatus.Active);
        env.Store.UpsertAccount(groupMemberId, "member", EPermission.Unknown, EAccountStatus.Active);
        env.Store.AddGroupMembers(groupId, [groupMemberId, Guid.Empty]);

        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator
            }
        ]);

        var batchResp = await env.PermissionServiceBackend.AddOrChangeManyAccountPermissionException(
            new AccountPermissionExceptionAddOrChangeManyCommand
            {
                AccountIds = [directUserId],
                GroupIds = [groupId],
                ControllerComponents =
                [
                    new AccountPermissionExceptionAddOrChangeManyControllerCodeCommand
                    {
                        ControllerCode = endpointCode,
                        AccessPermission = EPermissionAccessController.Enable
                    }
                ]
            });

        Assert.True(batchResp.Status);
        Assert.Contains(0, env.GroupStorage.RequestedPageIndices);
        Assert.Single(env.Store.GetExceptionsForAccount(directUserId));
        Assert.Single(env.Store.GetExceptionsForAccount(groupMemberId));
    }

    // -------------------------------------------------------------------------
    // 5. Redis Cache Miss, Sentinel & CatalogVersion Self-Healing (T15 - T18)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task T15_RedisCacheMiss_WithDbDisableException_ReadThroughHealsAndBlocks()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.Miss.Disable";
        Guid userId = Guid.NewGuid();

        env.Store.UpsertAccount(userId, "userDisableMiss", EPermission.User, EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.User
            }
        ]);
        env.Store.UpsertException(userId, endpointCode, EPermissionAccessController.Disable);

        // Redis key completely missing (cache miss / expired)
        await env.RedisRepo.DeleteCachingPermissionExceptionsByAccountId(userId.ToString());
        Assert.Null(await env.RedisRepo.GetAccountPermissionState(userId.ToString()));

        var check = await CheckPerm(env, userId, endpointCode, isHasBasePerm: true);
        Assert.True(check.Status);
        Assert.False(check.Data); // Must NOT silently invert into allow!

        // Repopulated in Redis with explicit Disable
        var warmed = await env.RedisRepo.GetAccountPermissionState(userId.ToString());
        Assert.NotNull(warmed);
        Assert.Single(warmed!.Exceptions);
        Assert.Equal(EPermissionAccessController.Disable, warmed.Exceptions[0].AccessPermission);
    }

    [Fact]
    public async Task T16_RedisCacheMiss_WithDbEnableException_ReadThroughHealsAndAllows()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.Miss.Enable";
        Guid userId = Guid.NewGuid();

        env.Store.UpsertAccount(userId, "userEnableMiss", EPermission.User, EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator
            }
        ]);
        env.Store.UpsertException(userId, endpointCode, EPermissionAccessController.Enable);

        // Simulate legacy string[] cache format in Redis -> forces self-heal
        await env.RedisWrapper.SetAsBaseResult(
            $"{AccountCachingCode.PrefixCachingAccountPermissionException}{userId}",
            $"[\"{endpointCode}\"]");

        var check = await CheckPerm(env, userId, endpointCode, isHasBasePerm: false);
        Assert.True(check.Status);
        Assert.True(check.Data);

        var healed = await env.RedisRepo.GetAccountPermissionState(userId.ToString());
        Assert.NotNull(healed);
        Assert.Equal(EPermissionAccessController.Enable, healed!.Exceptions[0].AccessPermission);
    }

    [Fact]
    public async Task T17_EmptyExceptionsSentinel_AvoidsRepeatedDbQueries()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.Sentinel.Endpoint";
        Guid userId = Guid.NewGuid();

        env.Store.UpsertAccount(userId, "cleanUser", EPermission.User, EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.User
            }
        ]);

        // Initial check misses Redis, warms sentinel state (Exceptions = [])
        Assert.True((await CheckPerm(env, userId, endpointCode, true)).Data);
        int dbReadsAfterWarmup = env.Store.ExceptionDbReadCount;
        Assert.Equal(1, dbReadsAfterWarmup);

        var cached = await env.RedisRepo.GetAccountPermissionState(userId.ToString());
        Assert.NotNull(cached);
        Assert.Empty(cached!.Exceptions);

        // Next 5 checks must hit Redis sentinel without querying acc_srv_account_permission_exception
        for (int i = 0; i < 5; i++)
        {
            Assert.True((await CheckPerm(env, userId, endpointCode, true)).Data);
        }

        Assert.Equal(dbReadsAfterWarmup, env.Store.ExceptionDbReadCount);
    }

    [Fact]
    public async Task T18_CatalogVersionMismatch_TriggersAutoRebuild()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.Catalog.Endpoint";
        Guid userId = Guid.NewGuid();

        env.Store.UpsertAccount(userId, "catUser", EPermission.User, EAccountStatus.Active);
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.User
            }
        ]);
        await env.AuthServiceBackend.SaveLogin(CreateLoginEvent(userId, EPermission.User));
        long v1 = (await env.RedisRepo.GetAccountPermissionState(userId.ToString()))!.CatalogVersion;

        // Next deploy updates controller to Administrator and bumps CatalogVersion
        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator
            }
        ]);

        var afterCheck = await CheckPerm(env, userId, endpointCode, isHasBasePerm: true);
        Assert.False(afterCheck.Data);

        var rebuilt = await env.RedisRepo.GetAccountPermissionState(userId.ToString());
        Assert.NotNull(rebuilt);
        Assert.True(rebuilt!.CatalogVersion > v1);
    }

    // -------------------------------------------------------------------------
    // 6. Token Expiry, Security & Concurrency Stress (T19 - T20)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task T19_TokenValidation_UtcExpiryAndTamper()
    {
        var env = new PermissionTestEnv();
        var nextCalled = false;
        var middleware = new TokenValidationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // 1. Valid future token
        string validToken = CreateJwtToken(Guid.NewGuid(), EPermission.User, DateTime.UtcNow.AddMinutes(30));
        var validCtx = new DefaultHttpContext();
        validCtx.Request.Headers.Authorization = $"Bearer {validToken}";
        await middleware.InvokeAsync(validCtx, env.CreateContextService(validCtx));
        Assert.True(nextCalled);

        // 2. Expired TokenCreatedUtc claim (still inside JWT exp, caught by ISO8601 UTC check)
        nextCalled = false;
        string expiredClaimToken = CreateJwtToken(Guid.NewGuid(), EPermission.User, DateTime.UtcNow.AddMinutes(-5),
            jwtExpOverride: DateTime.UtcNow.AddMinutes(10));
        var expiredCtx = new DefaultHttpContext();
        expiredCtx.Request.Headers.Authorization = $"Bearer {expiredClaimToken}";
        await middleware.InvokeAsync(expiredCtx, env.CreateContextService(expiredCtx));
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, expiredCtx.Response.StatusCode);

        // 3. Tampered signature
        string tamperedToken = validToken[..^4] + "XXXX";
        var tamperedCtx = new DefaultHttpContext();
        tamperedCtx.Request.Headers.Authorization = $"Bearer {tamperedToken}";
        await middleware.InvokeAsync(tamperedCtx, env.CreateContextService(tamperedCtx));
        Assert.Equal(StatusCodes.Status401Unauthorized, tamperedCtx.Response.StatusCode);

        // 4. [AllowAnonymous] bypasses token check
        nextCalled = false;
        var anonCtx = new DefaultHttpContext();
        anonCtx.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()), "anon"));
        await middleware.InvokeAsync(anonCtx, env.CreateContextService(anonCtx));
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task T20_ConcurrentValidationAndSelfHeal_64Tasks_ThreadSafe()
    {
        var env = new PermissionTestEnv();
        const string endpointCode = "NpOn.SSO.Stress.Endpoint";
        Guid userId = Guid.NewGuid();
        Guid adminId = Guid.NewGuid();

        env.Store.UpsertAccount(userId, "stressUser", EPermission.User, EAccountStatus.Active);
        env.Store.UpsertAccount(adminId, "stressAdmin", EPermission.Administrator, EAccountStatus.Active);

        await env.PermissionServiceBackend.SyncPermissionWithController([
            new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = "NpOn.SSO", VersionId = Guid.NewGuid(), ControllerName = endpointCode,
                Permission = EPermission.Administrator
            }
        ]);
        env.Store.UpsertException(userId, endpointCode, EPermissionAccessController.Enable);

        var tasks = Enumerable.Range(0, 64).Select(async idx =>
        {
            if (idx % 16 == 0)
            {
                await env.RedisRepo.DeleteCachingPermissionExceptionsByAccountId(userId.ToString());
                await env.RedisRepo.BumpPermissionCatalogVersion();
            }

            var userRes = await CheckPerm(env, userId, endpointCode, false);
            var adminRes = await CheckPerm(env, adminId, endpointCode, true);
            Assert.True(userRes.Status && userRes.Data);
            Assert.True(adminRes.Status && adminRes.Data);
        });

        await Task.WhenAll(tasks);

        var finalUserState = await env.RedisRepo.GetAccountPermissionState(userId.ToString());
        Assert.NotNull(finalUserState);
        Assert.Single(finalUserState!.Exceptions);
    }

    // -------------------------------------------------------------------------
    // Helpers & In-Memory Doubles
    // -------------------------------------------------------------------------

    private static Task<CommonResponse<bool>> CheckPerm(
        PermissionTestEnv env, Guid accountId, string controllerCode, bool isHasBasePerm) =>
        env.AuthServiceBackend.IsValidLogonPermissionExceptionControllers(
            new AccountPermissionExceptionCachingCheckValidQuery
            {
                AccountId = accountId.ToString(),
                ControllerCode = controllerCode,
                IsHasBasePermission = isHasBasePerm
            });

    private static AccountSaveLoginEvent CreateLoginEvent(Guid accountId, EPermission permission) => new()
    {
        AccountId = accountId,
        UserName = "test",
        Password = "pwd",
        AuthType = EAuthentication.WebApp,
        LoginType = ELoginType.Default,
        SessionId = $"session-{accountId}",
        MinuteExpire = 60,
        Permission = permission,
        TokenStatus = ETokenStatus.Active
    };

    private static string CreateJwtToken(
        Guid accountId, EPermission permission, DateTime tokenCreatedUtcExpiry, DateTime? jwtExpOverride = null)
    {
        byte[] key = Encoding.UTF8.GetBytes(TestJwtSecret);
        List<Claim> claims =
        [
            new(ContextServiceCode.SessionCode, $"sid-{accountId}"),
            new(ContextServiceCode.TokenCreatedUtc, tokenCreatedUtcExpiry.ToIso8601()),
            new(ContextServiceCode.Permission, permission.EnumAsInt().AsDefaultString()),
            new(ContextServiceCode.MinuteExpirePrefix, "60"),
            new(JwtRegisteredClaimNames.UniqueName, "test-user"),
            new(JwtRegisteredClaimNames.Sid, accountId.ToString())
        ];
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = jwtExpOverride ?? DateTime.UtcNow.AddMinutes(60),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512)
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static async Task<int> InvokePermissionMiddleware(
        PermissionTestEnv env,
        Guid accountId,
        EPermission claimPermission,
        string controller,
        string action,
        PermissionControllerAttribute? controllerAttr,
        PermissionRequiredAttribute? requiredAttr)
    {
        var metadata = new List<object>();
        var cad = BuildControllerDescriptor(controller, action, controllerAttr?.GetAllPermissionAsOne ?? EPermission.Unknown, requiredAttr);
        metadata.Add(cad);
        if (controllerAttr != null) metadata.Add(controllerAttr);
        if (requiredAttr != null) metadata.Add(requiredAttr);

        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sid, accountId.ToString()),
            new Claim(ContextServiceCode.Permission, claimPermission.EnumAsInt().AsDefaultString()),
            new Claim(ContextServiceCode.SessionCode, $"sid-{accountId}"),
        ], "TestAuth"));
        ctx.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), $"{controller}.{action}"));

        bool nextExecuted = false;
        var middleware = new PermissionValidationMiddleware(_ =>
        {
            nextExecuted = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(ctx, env.CreateContextService(ctx), env.CreateGatewayAuthenService());
        return nextExecuted ? StatusCodes.Status200OK : ctx.Response.StatusCode;
    }

    private static ControllerActionDescriptor BuildControllerDescriptor(
        string controller, string action, EPermission ctrlPerm, PermissionRequiredAttribute? reqAttr)
    {
        var metadata = new List<object>();
        if (ctrlPerm != EPermission.Unknown)
            metadata.Add(new PermissionControllerAttribute(ctrlPerm));
        if (reqAttr != null)
            metadata.Add(reqAttr);

        return new ControllerActionDescriptor
        {
            ControllerName = controller,
            ActionName = action,
            AttributeRouteInfo = new AttributeRouteInfo { Template = $"api/{controller}/{action}" },
            RouteValues = new Dictionary<string, string?>
            {
                ["controller"] = controller,
                ["action"] = action
            },
            ControllerTypeInfo = typeof(SampleApiController).GetTypeInfo(),
            EndpointMetadata = metadata
        };
    }

    private sealed class SampleApiController { }

    private sealed class InMemoryPermissionStore
    {
        private readonly object _lock = new();
        private readonly Dictionary<Guid, AccountRModel> _accounts = new();
        private readonly Dictionary<string, AccountPermissionControllerRModel> _controllers = new(StringComparer.Ordinal);
        private readonly Dictionary<(Guid AccountId, string Code), AccountPermissionExceptionRModel> _exceptions = new();
        private readonly Dictionary<Guid, List<Guid>> _groups = new();

        public int ClearOldVersionCallCount;
        public int ExceptionDbReadCount;

        public void UpsertAccount(Guid id, string userName, EPermission permission, EAccountStatus status)
        {
            lock (_lock)
            {
                _accounts[id] = new AccountRModel
                {
                    Id = id,
                    UserName = userName,
                    Password = "pwd",
                    Permission = permission,
                    Status = status
                };
            }
        }

        public AccountRModel? GetAccount(Guid id)
        {
            lock (_lock) return _accounts.GetValueOrDefault(id);
        }

        public List<AccountRModel> GetAccounts(IEnumerable<Guid> ids)
        {
            lock (_lock) return ids.Where(_accounts.ContainsKey).Select(id => _accounts[id]).ToList();
        }

        public void MergeControllers(IEnumerable<AccountPermissionController> items)
        {
            lock (_lock)
            {
                foreach (var c in items)
                {
                    _controllers[c.Code] = new AccountPermissionControllerRModel
                    {
                        HostCode = c.HostCode,
                        VersionId = c.VersionId,
                        Code = c.Code,
                        Permission = c.Permission,
                        RequiredPermission = c.RequiredPermission,
                        Description = c.Description ?? string.Empty
                    };
                }
            }
        }

        public void DeleteOldControllers(string hostCode, Guid keepVersionId)
        {
            lock (_lock)
            {
                ClearOldVersionCallCount++;
                var stale = _controllers.Values
                    .Where(c => string.Equals(c.HostCode, hostCode, StringComparison.Ordinal) && c.VersionId != keepVersionId)
                    .Select(c => c.Code)
                    .ToList();
                foreach (var code in stale)
                    _controllers.Remove(code);
            }
        }

        public AccountPermissionControllerRModel? GetController(string code)
        {
            lock (_lock) return _controllers.GetValueOrDefault(code);
        }

        public List<AccountPermissionControllerRModel> GetControllers(IEnumerable<string> codes)
        {
            lock (_lock)
            {
                return codes.Where(_controllers.ContainsKey).Select(c => _controllers[c]).ToList();
            }
        }

        public void UpsertException(Guid accountId, string code, EPermissionAccessController access)
        {
            lock (_lock)
            {
                _exceptions[(accountId, code)] = new AccountPermissionExceptionRModel
                {
                    AccountId = accountId,
                    ControllerCode = code,
                    AccessPermission = access
                };
            }
        }

        public void MergeExceptions(IEnumerable<AccountPermissionException> items)
        {
            lock (_lock)
            {
                foreach (var item in items)
                    UpsertException(item.AccountId, item.ControllerCode, item.AccessPermission);
            }
        }

        public void DeleteExceptions(IEnumerable<AccountPermissionException> items)
        {
            lock (_lock)
            {
                foreach (var item in items)
                    _exceptions.Remove((item.AccountId, item.ControllerCode));
            }
        }

        public void DeleteOrphanedExceptions()
        {
            lock (_lock)
            {
                var orphanedKeys = _exceptions.Keys.Where(k => !_controllers.ContainsKey(k.Code)).ToList();
                foreach (var k in orphanedKeys)
                    _exceptions.Remove(k);
            }
        }

        public void ReconcileExceptionsByCodes(IEnumerable<string> codes)
        {
            lock (_lock)
            {
                var codeSet = codes.ToHashSet(StringComparer.Ordinal);
                var toRemove = new List<(Guid AccountId, string Code)>();
                foreach (var kv in _exceptions)
                {
                    if (!codeSet.Contains(kv.Key.Code)) continue;
                    if (!_controllers.TryGetValue(kv.Key.Code, out var ctrl))
                    {
                        toRemove.Add(kv.Key);
                        continue;
                    }

                    var acc = _accounts.GetValueOrDefault(kv.Key.AccountId);
                    long mask = AccountPermissionService.ToPermissionMask(acc);
                    bool hasBase = AccountPermissionService.HasEffectiveBasePermission(mask, ctrl.Permission, ctrl.RequiredPermission);
                    if ((hasBase && kv.Value.AccessPermission == EPermissionAccessController.Enable)
                        || (!hasBase && kv.Value.AccessPermission == EPermissionAccessController.Disable))
                    {
                        toRemove.Add(kv.Key);
                    }
                }

                foreach (var key in toRemove)
                    _exceptions.Remove(key);
            }
        }

        public List<AccountPermissionExceptionRModel> GetExceptionsForAccount(Guid accountId, bool trackRead = false)
        {
            lock (_lock)
            {
                if (trackRead)
                    Interlocked.Increment(ref ExceptionDbReadCount);
                return _exceptions.Values.Where(x => x.AccountId == accountId).ToList();
            }
        }

        public void AddGroupMembers(Guid groupId, Guid[] members)
        {
            lock (_lock)
            {
                _groups[groupId] = members.ToList();
            }
        }

        public List<AccountGroupRModel> GetGroupPage(string[] groupIds, int pageSize, int pageIndex)
        {
            lock (_lock)
            {
                var rows = groupIds
                    .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                    .Where(_groups.ContainsKey)
                    .SelectMany(g => _groups[g].Select(m => new AccountGroupRModel
                    {
                        GroupId = g,
                        Leader = g,
                        Member = m,
                        GroupName = "TestGroup"
                    }))
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToList();
                return rows;
            }
        }
    }

    private sealed class InMemoryPermissionStorageAdapter(InMemoryPermissionStore store) : IAccountPermissionStorageAdapter
    {
        public Task<List<AccountPermissionExceptionRModel>?> AccountPermissionExceptionGetByAccountId(string accountId) =>
            Task.FromResult<List<AccountPermissionExceptionRModel>?>(
                Guid.TryParse(accountId, out var g) ? store.GetExceptionsForAccount(g, trackRead: true) : []);

        public Task<bool> AccountPermissionExceptionDeleteOldVersionByHostCode(string hostCode, string versionId)
        {
            if (Guid.TryParse(versionId, out var vid))
                store.DeleteOldControllers(hostCode, vid);
            return Task.FromResult(true);
        }

        public Task<List<AccountPermissionExceptionRModel>?> AccountPermissionExceptionGetByAccountIdAndControllerCodes(
            string accountId, string[] controllerCodes)
        {
            if (!Guid.TryParse(accountId, out var g))
                return Task.FromResult<List<AccountPermissionExceptionRModel>?>([]);
            var set = controllerCodes.ToHashSet(StringComparer.Ordinal);
            return Task.FromResult<List<AccountPermissionExceptionRModel>?>(
                store.GetExceptionsForAccount(g).Where(x => set.Contains(x.ControllerCode)).ToList());
        }

        public Task<List<AccountPermissionControllerRModel>?> AccountPermissionControllerGetByCodes(string[]? codes) =>
            Task.FromResult<List<AccountPermissionControllerRModel>?>(
                codes is { Length: > 0 } ? store.GetControllers(codes) : []);

        public Task<bool> AccountPermissionExceptionDeleteOrphaned()
        {
            store.DeleteOrphanedExceptions();
            return Task.FromResult(true);
        }

        public Task<bool> AccountPermissionExceptionsReconcileByControllerCodes(string[] controllerCodes)
        {
            store.ReconcileExceptionsByCodes(controllerCodes);
            return Task.FromResult(true);
        }
    }

    private sealed class InMemoryAuthStorageAdapter(InMemoryPermissionStore store) : IAuthenticationStorageAdapter
    {
        public Task<List<AccountRModel>?> AccountGetByNumberPhoneOrEmailOrUsername(string phoneNumber, string email, string username) =>
            Task.FromResult<List<AccountRModel>?>([]);
        public Task<AccountRModel?> AccountGetByUsernameAndPassword(string username, string password) =>
            Task.FromResult<AccountRModel?>(null);
        public Task<List<AccountLoginRModel>?> AccountLoginInfoGetByRefreshToken(string refreshToken) =>
            Task.FromResult<List<AccountLoginRModel>?>([]);
        public Task<AccountRModel?> AccountGetById(string accountId) =>
            Task.FromResult(Guid.TryParse(accountId, out var g) ? store.GetAccount(g) : null);
        public Task<List<AccountRModel>?> AccountGetByIds(string[]? accountIds) =>
            Task.FromResult<List<AccountRModel>?>(
                accountIds == null
                    ? []
                    : store.GetAccounts(accountIds.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty)));
        public Task<AccountLoginRModel?> AccountLoginInfoGetBySessionId(string sessionId) =>
            Task.FromResult<AccountLoginRModel?>(null);
    }

    private sealed class InMemoryGroupStorageAdapter(InMemoryPermissionStore store) : IAccountGroupStorageAdapter
    {
        public List<int> RequestedPageIndices { get; } = [];

        public Task<List<AccountGroupRModel>?> AccountGroupGetByGroupIds(string[] groupIds, int pageSize, int pageIndex)
        {
            RequestedPageIndices.Add(pageIndex);
            return Task.FromResult<List<AccountGroupRModel>?>(store.GetGroupPage(groupIds, pageSize, pageIndex));
        }
    }

    private sealed class InMemoryRedisFactoryWrapper : IRedisFactoryWrapper
    {
        private readonly ConcurrentDictionary<string, string> _kv = new(StringComparer.Ordinal);

        public string? FactoryOptionCode => "in-memory-redis";
        public EDb GetDbType() => EDb.Redis;

        public Task<INpOnWrapperResult?> SetAsBaseResult(string key, string value, TimeSpan? expiry = null)
        {
            _kv[key] = value;
            var res = new NpOnWrapperResult();
            res.SetSuccess();
            return Task.FromResult<INpOnWrapperResult?>(res);
        }

        public Task<RedisValueWrapper?> Get(string key) =>
            Task.FromResult(_kv.TryGetValue(key, out var val)
                ? new RedisValueWrapper(new RedisValueContainer(new RedisValue(val)))
                : null);

        public Task<INpOnWrapperResult?> DeleteManyAsBaseResult(params string[] keys)
        {
            foreach (var k in keys) _kv.TryRemove(k, out _);
            var res = new NpOnWrapperResult();
            res.SetSuccess();
            return Task.FromResult<INpOnWrapperResult?>(res);
        }

        public Task<INpOnWrapperResult?> GetAsBaseResult(string key) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> DeleteAsBaseResult(string key) => DeleteManyAsBaseResult(key);
        public Task<RedisValueWrapper?> Set(string key, string value, TimeSpan? expiry = null) => throw new NotImplementedException();
        public Task<RedisValueWrapper?> Delete(string key) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> GetManyAsBaseResult(params string[] keys) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> SetManyAsBaseResult(Dictionary<string, string> keyValues, TimeSpan? expiry = null) => throw new NotImplementedException();
        public Task<RedisValueWrapper?> GetMany(params string[] keys) => throw new NotImplementedException();
        public Task<RedisValueWrapper?> SetMany(Dictionary<string, string> keyValues, TimeSpan? expiry = null) => throw new NotImplementedException();
        public Task<RedisValueWrapper?> DeleteMany(params string[] keys) => throw new NotImplementedException();
        public Task<RedisValueWrapper?> CustomizeString(string commandText) => throw new NotImplementedException();
        public Task<RedisValueWrapper?> CustomizeCommand(INpOnDbCommand command) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> CustomizeStringAsBaseResult(string commandText) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> CustomizeCommandAsBaseResult(INpOnDbCommand command) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Publish(string channel) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Subscribe(Common.Extensions.NpOn.BaseDbFactory.Broadcasts.BaseBroadcastMessage message) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteAsync(IBaseNpOnDbCommand dbCommand) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<INpOnDbCommandParam> parameters) => throw new NotImplementedException();
        public Task<List<T>> QueryAsync<T>(IBaseNpOnDbCommand dbCommand) where T : class => throw new NotImplementedException();
        public Task<List<T>> QueryAsync<T>(string queryString, List<INpOnDbCommandParam>? parameters = null) where T : class => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteFuncParams(string funcName, List<INpOnDbCommandParam>? parameters) => throw new NotImplementedException();
        public Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>?> ExecuteWithTransaction(IEnumerable<IBaseNpOnDbCommand> commands) => throw new NotImplementedException();
    }

    private sealed class InMemoryPostgresFactoryWrapper(InMemoryPermissionStore store) : IPostgresFactoryWrapper
    {
        public string? FactoryOptionCode => "in-memory-postgres";
        public EDb GetDbType() => EDb.Postgres;

        public Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain
        {
            var res = new NpOnWrapperResult();
            res.SetSuccess();
            return Task.FromResult<INpOnWrapperResult?>(res);
        }

        public Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain
        {
            foreach (var d in domains)
            {
                if (d is AccountDomain acc)
                    store.UpsertAccount(acc.Id, acc.UserName, acc.Permission ?? EPermission.Unknown, acc.Status);
            }
            var res = new NpOnWrapperResult();
            res.SetSuccess();
            return Task.FromResult<INpOnWrapperResult?>(res);
        }

        public Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain
        {
            var list = domains.ToList();
            if (list.OfType<AccountPermissionController>().Any())
                store.MergeControllers(list.OfType<AccountPermissionController>());
            if (list.OfType<AccountPermissionException>().Any())
                store.MergeExceptions(list.OfType<AccountPermissionException>());

            var res = new NpOnWrapperResult();
            res.SetSuccess();
            return Task.FromResult<INpOnWrapperResult?>(res);
        }

        public Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain
        {
            var list = domains.ToList();
            if (list.OfType<AccountPermissionException>().Any())
                store.DeleteExceptions(list.OfType<AccountPermissionException>());

            var res = new NpOnWrapperResult();
            res.SetSuccess();
            return Task.FromResult<INpOnWrapperResult?>(res);
        }

        public Task<INpOnWrapperResult?> ExecuteAsync(IBaseNpOnDbCommand dbCommand)
        {
            var res = new NpOnWrapperResult();
            res.SetSuccess();
            return Task.FromResult<INpOnWrapperResult?>(res);
        }

        public IBaseNpOnDbCommand CommandBuilder<T>(IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain
        {
            var list = domains.ToList();
            if (actionType == ERepositoryAction.Update && list.OfType<AccountDomain>().Any())
            {
                foreach (var acc in list.OfType<AccountDomain>())
                    store.UpsertAccount(acc.Id, acc.UserName, acc.Permission ?? EPermission.Unknown, acc.Status);
            }
            return new Common.Extensions.NpOn.CommonDb.DbCommands.NpOnDbCommand(EDb.Postgres, "INMEMORY");
        }

        public Task<INpOnWrapperResult?> Execute(Common.Extensions.NpOn.CommonDb.DbCommands.NpOnDbExecuteCommand npOnExecuteCommand) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> BulkCopy(INpOnTableWrapper table, string tableName) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<INpOnDbCommandParam> parameters) => throw new NotImplementedException();
        public Task<List<T>> QueryAsync<T>(IBaseNpOnDbCommand dbCommand) where T : class => throw new NotImplementedException();
        public Task<List<T>> QueryAsync<T>(string queryString, List<INpOnDbCommandParam>? parameters = null) where T : class => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteFuncParams(string funcName, List<INpOnDbCommandParam>? parameters) => throw new NotImplementedException();
        public Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>?> ExecuteWithTransaction(IEnumerable<IBaseNpOnDbCommand> commands) => throw new NotImplementedException();
    }

    private sealed class InProcessAuthenticationProtoClient(AuthenticationService backend)
        : ProtoAccount.AuthenticationProtoService.AuthenticationProtoServiceClient
    {
        public override AsyncUnaryCall<ProtoAccount.CommonBoolResponseProto> IsValidLogonPermissionExceptionControllersAsync(
            ProtoAccount.AccountPermissionExceptionCachingCheckValidQuery request, CallOptions options)
        {
            var task = backend.IsValidLogonPermissionExceptionControllers(
                new AccountPermissionExceptionCachingCheckValidQuery
                {
                    AccountId = request.AccountId,
                    ControllerCode = request.ControllerCode,
                    IsHasBasePermission = request.IsHasBasePermission
                }).ContinueWith(t => new ProtoAccount.CommonBoolResponseProto { Status = t.Result.Status, Data = t.Result.Data });

            return new AsyncUnaryCall<ProtoAccount.CommonBoolResponseProto>(
                task, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
        }
    }

    private sealed class InProcessAccountPermissionProtoClient(AccountPermissionService backend, bool failSync)
        : ProtoAccount.AccountPermissionProtoService.AccountPermissionProtoServiceClient
    {
        public override AsyncUnaryCall<ProtoAccount.CommonBoolResponseProto> SyncPermissionWithControllerAsync(
            ProtoAccount.AccountPermissionControllersSyncCommand request, CallOptions options)
        {
            if (failSync)
            {
                return new AsyncUnaryCall<ProtoAccount.CommonBoolResponseProto>(
                    Task.FromResult(new ProtoAccount.CommonBoolResponseProto { Status = false, Data = false, ErrorMessages = { "Simulated sync failure" } }),
                    Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
            }

            var cmds = request.Items.Select(x => new AccountPermissionControllerAddOrChangeCommand
            {
                HostCode = x.HostCode,
                VersionId = Guid.TryParse(x.VersionId, out var vid) ? vid : Guid.Empty,
                ControllerName = x.ControllerName,
                Permission = (EPermission)x.Permission,
                Description = x.HasDescription ? x.Description : null,
                RequiredPermission = x.HasRequiredPermission ? (EPermission)x.RequiredPermission : null
            }).ToArray();

            var task = backend.SyncPermissionWithController(cmds)
                .ContinueWith(t => new ProtoAccount.CommonBoolResponseProto { Status = t.Result.Status, Data = t.Result.Data });

            return new AsyncUnaryCall<ProtoAccount.CommonBoolResponseProto>(
                task, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
        }

        public override AsyncUnaryCall<ProtoAccount.CommonBoolResponseProto> ClearOldControllersVersionAsync(
            ProtoAccount.AccountPermissionControllerDeleteByHostCodeAndVersionIdCommand request, CallOptions options)
        {
            var task = backend.ClearOldControllersVersion(
                new AccountPermissionControllerDeleteByHostCodeAndVersionIdCommand
                {
                    HostCode = request.HostCode,
                    VersionId = Guid.TryParse(request.VersionId, out var vid) ? vid : Guid.Empty
                }).ContinueWith(t => new ProtoAccount.CommonBoolResponseProto { Status = t.Result.Status, Data = t.Result.Data });

            return new AsyncUnaryCall<ProtoAccount.CommonBoolResponseProto>(
                task, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
        }
    }

    private sealed class StaticActionDescriptorCollectionProvider(IReadOnlyList<ActionDescriptor> items)
        : IActionDescriptorCollectionProvider
    {
        public ActionDescriptorCollection ActionDescriptors { get; } = new(items, 1);
    }

    private sealed class NoOpRabbitMqProducer : IRabbitMqProducer
    {
        public void AddEvent(IRabbitMqEvent @event, bool isCompress = false) { }
    }

    private sealed class NoOpZeroMqTwoWayFactory : IZeroMqTwoWayFactory
    {
        public Task<IZeroMqTwoWayProvider> CreateClientAsync(EUrlConfiguration urlConfig) => throw new NotImplementedException();
        public IZeroMqTwoWayProvider? TryGet(EUrlConfiguration urlConfiguration) => null;
        public Task<bool> TrySendTo<TRequest>(IEnumerable<EUrlConfiguration>? configurations, TRequest request) => Task.FromResult(true);
    }

    private sealed class NoOpLogAction : ILogAction
    {
        public Task Push(string serviceName, string action, object? request, object? response, object? userInfo,
            TimeSpan executeTime, string requestUrl = "", string connectionId = "", string requestId = "",
            object? trackingProcessTime = null) => Task.CompletedTask;
        public Task PushError(string serviceName, string action, object? request, Exception exception, object? userInfo,
            TimeSpan executeTime, string requestUrl = "", string connectionId = "", string requestId = "",
            object? trackingProcessTime = null) => Task.CompletedTask;
        public Task PushError(string serviceName, string action, object? request, object? response, Exception exception,
            object? userInfo, TimeSpan executeTime, string requestUrl = "", string connectionId = "", string requestId = "",
            object? trackingProcessTime = null) => Task.CompletedTask;
        public void Tracking((string Key, string Value)[]? parameters, int minuteCacheExpire, TimeSpan executeTime,
            Exception? exception, string? keyCache, string requestUrl, string requestPath, string connectionId,
            string requestId, object? tracking) { }
        public void Tracking(string componentId, int pageIndex, int pageSize, (string Key, string Value)[]? parameters,
            int minuteCacheExpire, TimeSpan executeTime, Exception? exception, string? keyCache, string requestUrl,
            string requestPath, string connectionId, string requestId, object? tracking) { }
        public void Tracking(string pageId, int deviceType, Dictionary<string, string>? conditions, long? minuteCacheExpire,
            TimeSpan executeTime, Exception? exception, int httpStatusCode, string? keyCache, string requestUrl,
            string requestId, string connectionId) { }
    }
}
