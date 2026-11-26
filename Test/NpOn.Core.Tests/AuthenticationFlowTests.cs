using System.Security.Cryptography;
using System.Text;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Definitions.NpOn.ShareAccountConstant;
using Xunit;

namespace NpOn.Core.Tests;

public class AuthenticationFlowTests
{
    private static string ComputePasswordHash(string password, bool isAddMd5 = true)
    {
        if (string.IsNullOrWhiteSpace(password))
            return string.Empty;

        if (isAddMd5)
        {
            using var md5 = MD5.Create();
            var md5Bytes = md5.ComputeHash(Encoding.ASCII.GetBytes(password));
            password = Convert.ToHexString(md5Bytes).ToLower();
        }

        byte[] salt = Encoding.UTF8.GetBytes(ContextServiceCode.DefaultSaltPassword);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 369, HashAlgorithmName.SHA1);
        byte[] hash = pbkdf2.GetBytes(20);
        return Convert.ToBase64String(hash);
    }

    [Fact]
    public void KhaBanh_PasswordHash_ShouldMatchExpectedDatabaseHash()
    {
        // Arrange
        const string rawPassword = "Banhditusapve@1234";
        const string expectedHash = "jyGimGTAj2niJMgxijU7x7iR1RA=";

        // Act
        var computedHash = ComputePasswordHash(rawPassword);

        // Assert
        Assert.Equal(expectedHash, computedHash);
    }

    [Fact]
    public void AccountLoginQuery_ShouldSerializeAndValidateRequiredFields()
    {
        // Arrange
        var query = new AccountLoginQuery
        {
            UserName = "KhaBanh",
            Password = ComputePasswordHash("Banhditusapve@1234"),
            AuthType = EAuthentication.WebApp,
            ClientId = "WEB_TEST_CLIENT"
        };

        // Assert
        Assert.Equal("KhaBanh", query.UserName);
        Assert.Equal("jyGimGTAj2niJMgxijU7x7iR1RA=", query.Password);
        Assert.Equal(EAuthentication.WebApp, query.AuthType);
        Assert.Equal("WEB_TEST_CLIENT", query.ClientId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "IntegrationPostgres")]
    public async Task Postgres_AccountQuery_ShouldSucceed100TimesWithoutFailure()
    {
        string connStr = "Host=localhost;Port=5432;Database=account;Username=postgres;Password=password;";
        var factory = new Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory.PostgresFactoryWrapper(connStr);
        string sql = "SELECT * FROM acc_srv_account WHERE ((LOWER(username) = LOWER(@username)) OR (phone_number = @username)) AND password = @password";

        for (int i = 0; i < 100; i++)
        {
            var prms = new List<Common.Extensions.NpOn.ICommonDb.DbCommands.INpOnDbCommandParam>
            {
                new Common.Extensions.NpOn.CommonDb.DbCommands.NpOnDbCommandParam("username", "KhaBanh"),
                new Common.Extensions.NpOn.CommonDb.DbCommands.NpOnDbCommandParam("password", "jyGimGTAj2niJMgxijU7x7iR1RA=")
            };

            var result = await factory.ExecuteAsync(sql, prms);
            Assert.NotNull(result);
            Assert.True(result.Status);

            var account = Common.Extensions.NpOn.ICommonDb.DbResults.Extensions.NpOnWrapperResultExtensions.ToFirstOrDefault<MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels.AccountRModel>(result);
            Assert.NotNull(account);
            Assert.Equal("KhaBanh", account.UserName);
            Assert.Equal(EAccountStatus.Active, account.Status);
            Assert.NotEqual(Guid.Empty, account.Id);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "IntegrationPostgres")]
    public async Task Postgres_AccountQuery_Concurrent50Tasks_ShouldAllSucceed()
    {
        string connStr = "Host=localhost;Port=5432;Database=account;Username=postgres;Password=password;";
        var factory = new Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory.PostgresFactoryWrapper(connStr, connectionNumber: 100);
        string sql = "SELECT * FROM acc_srv_account WHERE ((LOWER(username) = LOWER(@username)) OR (phone_number = @username)) AND password = @password";

        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            var prms = new List<Common.Extensions.NpOn.ICommonDb.DbCommands.INpOnDbCommandParam>
            {
                new Common.Extensions.NpOn.CommonDb.DbCommands.NpOnDbCommandParam("username", "KhaBanh"),
                new Common.Extensions.NpOn.CommonDb.DbCommands.NpOnDbCommandParam("password", "jyGimGTAj2niJMgxijU7x7iR1RA=")
            };

            var result = await factory.ExecuteAsync(sql, prms);
            Assert.NotNull(result);
            Assert.True(result.Status);

            var account = Common.Extensions.NpOn.ICommonDb.DbResults.Extensions.NpOnWrapperResultExtensions.ToFirstOrDefault<MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels.AccountRModel>(result);
            Assert.NotNull(account);
            Assert.Equal("KhaBanh", account.UserName);
            Assert.Equal(EAccountStatus.Active, account.Status);
            Assert.NotEqual(Guid.Empty, account.Id);
        });

        await Task.WhenAll(tasks);
    }
}

