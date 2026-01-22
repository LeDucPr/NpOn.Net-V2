using Definitions.NpOn.ProjectEnums.AccountEnums;
using MicroServices.Account.Contracts.NpOn.AccountServiceContract.Queries;
using MicroServices.Account.Service.NpOn.IAccountService;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public class HostingApp(
    ILogger<HostingApp> logger,
    // test
    IAuthenticationService authenticationService
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.AccountService AppHostedService is starting");
        await authenticationService.Login(new AccountLoginQuery
        {
            UserName = "KhaBanh",
            Password = "GvN6GbQvBxyRiZ/oNsMW+Wwsa9o=", // hash
            AuthType = EAuthentication.WebApp,
            ClientId = "WEB_TEST_C"
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.AccountService AppHostedService is stopping");
        return Task.CompletedTask;
    }
}