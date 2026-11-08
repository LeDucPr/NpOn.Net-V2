using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NpOn.CommonGrpcCall;
using Xunit;

namespace NpOn.Core.Tests;

public class GrpcServerAuthMiddlewareTests
{
    private class DummyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public async Task InvokeAsync_RestApiEndpoint_ShouldPassThroughWithoutMtlsOrToken()
    {
        // Arrange
        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new GrpcServerAuthMiddleware(
            next,
            new DummyServiceProvider(),
            NullLogger<GrpcServerAuthMiddleware>.Instance,
            requireAuth: true,
            useMtls: true);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/Account/Login";
        context.Request.ContentType = "application/json";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "REST API call should pass through GrpcServerAuthMiddleware to reach controllers.");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AllowAnonymousEndpoint_ShouldPassThrough()
    {
        // Arrange
        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new GrpcServerAuthMiddleware(
            next,
            new DummyServiceProvider(),
            NullLogger<GrpcServerAuthMiddleware>.Instance,
            requireAuth: true,
            useMtls: true);

        var context = new DefaultHttpContext();
        context.Request.Path = "/some-service/anonymous";
        context.Request.ContentType = "application/grpc";

        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "AnonymousEndpoint");
        context.SetEndpoint(endpoint);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled, "Endpoint with [AllowAnonymous] should bypass GrpcServerAuthMiddleware.");
    }

    [Fact]
    public async Task InvokeAsync_InternalGrpcRequest_WithoutCertOrToken_ShouldReturn401()
    {
        // Arrange
        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new GrpcServerAuthMiddleware(
            next,
            new DummyServiceProvider(),
            NullLogger<GrpcServerAuthMiddleware>.Instance,
            requireAuth: true,
            useMtls: true);

        var context = new DefaultHttpContext();
        context.Request.Path = "/MicroServices.Account.Service.NpOn.IAccountService.IAuthenticationService/SomeInternalAction";
        context.Request.ContentType = "application/grpc";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.False(nextCalled, "Internal gRPC request without mTLS or JWT should be blocked.");
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }
}

