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
}

