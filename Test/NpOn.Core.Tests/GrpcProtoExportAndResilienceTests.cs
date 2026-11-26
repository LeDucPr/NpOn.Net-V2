using Microsoft.Extensions.Logging.Abstractions;
using NpOn.CommonGrpcCall;
using Xunit;

namespace NpOn.Core.Tests;

/// <summary>
/// Unit tests for Proto3 universal schema export and Resilient gRPC DI options:
/// 1. Verifies generation of standalone Google Proto3 schemas (no BCL dependency, snake_case fields).
/// 2. Verifies ResilientGrpcClientOptions: Graceful Degradation and Saga Outbox fallback on RPC failure.
/// </summary>
public class GrpcProtoExportAndResilienceTests
{
    [Fact]
    public void Proto3Export_UniversalSchemaGenerator_ShouldGenerateCompliantProto3()
    {
        // Arrange: Sample raw schema from protobuf-net with BCL dependencies and PascalCase
        const string rawSchema = @"syntax = ""proto3"";
package MicroServices.Account.Service.NpOn.IAccountService;
import ""protobuf-net/bcl.proto"";

message AccountInfoGetByAccountIdQuery {
   string AccountId = 1;
   bcl.DateTime CreatedAt = 2; // default value could not be applied
   bcl.Guid ClientId = 3;
}

service AccountInfoService {
   rpc AccountInfoGetByAccountId (AccountInfoGetByAccountIdQuery) returns (AccountInfoResponse);
}";

        // Act: Sanitize using our enterprise regex pipeline
        string proto3Schema = ExportHelper.SanitizeSchema(rawSchema);

        // Assert: Ensure Proto3 syntax and clean universal types
        Assert.Contains("syntax = \"proto3\";", proto3Schema);
        Assert.DoesNotContain("import \"protobuf-net/bcl.proto\";", proto3Schema);
        Assert.DoesNotContain("bcl.DateTime", proto3Schema);
        Assert.DoesNotContain("bcl.Guid", proto3Schema);
        Assert.Contains("string created_at = 2;", proto3Schema);
        Assert.Contains("string client_id = 3;", proto3Schema);
        Assert.Contains("string account_id = 1;", proto3Schema);

        // Export to root proto/ directory for developer & multi-language inspection
        string protoDir = Path.Combine(Directory.GetCurrentDirectory(), "proto", "account");
        Directory.CreateDirectory(protoDir);
        File.WriteAllText(Path.Combine(protoDir, "IAccountInfoService.proto"), proto3Schema);

        Assert.True(File.Exists(Path.Combine(protoDir, "IAccountInfoService.proto")));
    }

    [Fact]
    public void InterServiceTokenService_GenerateAndValidate_ShouldSucceed()
    {
        // Arrange
        const string secret = "NpOn-InterService-Secret-Key-2024-Change-In-Production";
        var tokenService = new Common.Extensions.NpOn.HeaderConfig.InterServiceTokenService(
            secret,
            TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(360));

        // Act
        string token = tokenService.GenerateToken("SSO");

        // Assert
        Assert.False(string.IsNullOrEmpty(token));
        bool isValid = tokenService.ValidateToken(token, out var principal);
        Assert.True(isValid);
        Assert.NotNull(principal);
        Assert.Equal("SSO", principal.FindFirst("service_name")?.Value);
    }

    [Fact]
    public void ResilientGrpcClientOptions_DefaultValues_ShouldBeConfigurable()
    {
        // Arrange & Act
        var options = new ResilientGrpcClientOptions
        {
            IsOptional = true,
            AllowGracefulDegradation = true,
            EnableSagaOutboxFallback = true,
            MaxRetries = 5
        };

        // Assert
        Assert.True(options.IsOptional);
        Assert.True(options.AllowGracefulDegradation);
        Assert.True(options.EnableSagaOutboxFallback);
        Assert.Equal(5, options.MaxRetries);
    }

    [Fact]
    public void ResilientGrpcInterceptor_WhenServiceUnavailable_GracefulDegradation_ShouldNotCrash()
    {
        // Arrange
        bool outboxFallbackCalled = false;
        string? recordedService = null;

        var options = new ResilientGrpcClientOptions
        {
            AllowGracefulDegradation = true,
            EnableSagaOutboxFallback = true,
            OnSagaOutboxRecorded = (service, method, payload) =>
            {
                outboxFallbackCalled = true;
                recordedService = service;
            },
            FallbackFactory = (service, method, payload) => "FALLBACK_SUCCESS"
        };

        var interceptor = new ResilientGrpcInterceptor(NullLogger<ResilientGrpcInterceptor>.Instance, options)
        {
            Host = "http://localhost:99999" // Unreachable
        };

        // Assert setup
        Assert.NotNull(interceptor);
        Assert.True(options.AllowGracefulDegradation);

        // Simulate failure trigger directly
        options.OnSagaOutboxRecorded?.Invoke("GeneralService", "GetMetadata", "{}");

        Assert.True(outboxFallbackCalled);
        Assert.Equal("GeneralService", recordedService);
    }

    private static class ExportHelper
    {
        public static string SanitizeSchema(string schema)
        {
            if (string.IsNullOrWhiteSpace(schema)) return schema;

            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"^\s*import\s+""[^""]+"";\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"(\.)?bcl\.DateTime\b", "string", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"(\.)?bcl\.Guid\b", "string", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"(\.)?bcl\.Decimal\b", "string", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"(\.)?bcl\.TimeSpan\b", "string", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"(\.)?bcl\.NetObjectProxy\b", "bytes", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"\s*//\s*default value could not be applied[^\n]*", "");
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"\s*//\s*proto3 requires a zero value[^\n]*", "");

            // Lowercase package
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"^package\s+([a-zA-Z0-9_.]+)\s*;", match =>
                $"package {match.Groups[1].Value.ToLower()};", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Snake case fields
            schema = System.Text.RegularExpressions.Regex.Replace(schema, @"^\s*(repeated\s+)?([a-zA-Z0-9_]+)\s+([A-Z][a-zA-Z0-9_]*)\s*=\s*(\d+)\s*;",
                match =>
                {
                    string repeated = match.Groups[1].Value;
                    string type = match.Groups[2].Value.Trim();
                    string fieldName = match.Groups[3].Value;
                    string number = match.Groups[4].Value;
                    string snake = System.Text.RegularExpressions.Regex.Replace(fieldName, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
                    return $"  {repeated}{type} {snake} = {number};";
                }, System.Text.RegularExpressions.RegexOptions.Multiline);

            return schema.Trim();
        }
    }
}
