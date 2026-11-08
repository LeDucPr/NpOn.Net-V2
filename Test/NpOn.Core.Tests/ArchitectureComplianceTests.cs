using Common.Extensions.NpOn.ICommonDb.DbResults.Grpc;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using Xunit;

namespace NpOn.Core.Tests;

public class ArchitectureComplianceTests
{
    [Fact]
    public void ReadModels_MustInheritFromNpOnBaseGrpcObject()
    {
        // Assert: Read models should inherit from NpOnBaseGrpcObject to ensure zero-allocation IL mapper compatibility
        Assert.True(typeof(NpOnBaseGrpcObject).IsAssignableFrom(typeof(AccountRModel)));
    }

    [Fact]
    public void ReadModels_FieldMapper_MustRegisterRequiredKeys()
    {
        // Arrange
        var model = new AccountRModel
        {
            Id = Guid.NewGuid(),
            UserName = "check_field_mapper",
            Password = "pwd"
        };

        // Act
        model.CreateDefaultFieldMapper();

        // Assert
        Assert.NotNull(model.FieldMap);
        Assert.True(model.FieldMap.ContainsKey("Id"));
        Assert.True(model.FieldMap.ContainsKey("UserName"));
        Assert.True(model.FieldMap.ContainsKey("Password"));
        Assert.Equal("id", model.FieldMap["Id"]);
        Assert.Equal("username", model.FieldMap["UserName"]);
    }
}

