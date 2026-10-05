# Inter-Service Authentication Setup

## Overview
This document describes how to set up JWT-based inter-service authentication with automatic key rotation for gRPC communication between microservices.

## Key Features
- **JWT Token Authentication**: Each service generates and validates JWT tokens for inter-service calls
- **Automatic Key Rotation**: Signing keys rotate automatically every 6 hours (configurable)
- **Time-based Keys**: Keys are derived from a base secret + time bucket, ensuring keys change over time
- **Graceful Rotation**: Previous key bucket is accepted during rotation to avoid service disruption
- **Zero Dependency on External Systems**: No need for external token servers or databases

## Configuration Steps

### 1. Update appsettings.yaml
Add the inter-service secret key to your service configuration:

```yaml
# Inter-service authentication
InterServiceSecretKey: "your-super-secret-base-key-change-this-in-production"
InterServiceTokenLifetimeMinutes: 60
InterServiceKeyRotationMinutes: 360
```

**Important**: Use a strong, unique secret key for each environment (dev, staging, prod). The key should be at least 32 characters long.

### 2. Register Services in Program.cs

```csharp
using Common.Extensions.NpOn.HeaderConfig;
using NpOn.CommonGrpcCall;

protected override Task ConfigureServices(IServiceCollection services)
{
    // Get configuration
    var interServiceSecret = EApplicationConfiguration.InterServiceSecretKey.GetAppSettingConfig().AsString();
    var tokenLifetime = TimeSpan.FromHours(
        EApplicationConfiguration.InterServiceTokenLifetimeHours.GetAppSettingConfig().AsDefaultInt(1));
    var keyRotationPeriod = TimeSpan.FromHours(
        EApplicationConfiguration.InterServiceKeyRotationHours.GetAppSettingConfig().AsDefaultInt(6));
    
    // Register token service as singleton (same instance across all requests)
    services.AddSingleton<IInterServiceTokenService>(new InterServiceTokenService(
        interServiceSecret,
        tokenLifetime,
        keyRotationPeriod));

    // Register GrpcHeaderConfig with token service
    var serviceName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsString();
    services.AddScoped<GrpcHeaderConfig>(sp => 
    {
        var tokenService = sp.GetRequiredService<IInterServiceTokenService>();
        return new GrpcHeaderConfig(
            EGrpcEndUseType.InternalServer,
            null,
            tokenService,
            serviceName);
    });

    // ... rest of your configuration
}
```

### 3. Add Server Auth Middleware to Pipeline

The middleware is automatically added to `CommonProgram.ConfigureBasePipeline()` when `IsUseGrpcStandardMode` is enabled. No manual configuration is needed in individual services.

If you need to customize the behavior, you can override `ConfigureBasePipeline` in your service:

```csharp
protected override void ConfigureBasePipeline(WebApplication app)
{
    base.ConfigureBasePipeline(app);
    
    // Custom middleware configuration if needed
}
```

**Note**: The middleware is added after `UseAuthentication` and `UseAuthorization` in the base pipeline, ensuring it works correctly with the existing authentication/authorization flow.

### 4. Update Client-side Interceptor

If you have custom client interceptors that extend `GrpcInterceptorBase`, you can override the `WriteHeader()` method to add custom headers if needed. However, the token is now automatically added by `GrpcHeaderConfig` when `IInterServiceTokenService` is provided.

## How It Works

### Token Generation (Client Side)
1. When a service makes a gRPC call to another service, `GrpcHeaderConfig` generates a JWT token
2. Token includes:
   - Service name (caller identity)
   - Timestamp (when token was generated)
   - Key rotation time (which key bucket was used)
   - Optional additional data
3. Token is signed with the current time-based signing key
4. Token is added to gRPC metadata header: `x-inter-service-token`

### Token Validation (Server Side)
1. `GrpcServerAuthInterceptor` intercepts incoming gRPC calls
2. Extracts token from `x-inter-service-token` header
3. Validates token signature using current and previous signing keys
4. If valid, allows the request to proceed
5. If invalid or missing, returns `Unauthenticated` status

### Key Rotation
- Keys are generated based on: `SHA256(base_secret + time_bucket)`
- Time bucket = `current_timestamp / rotation_period_in_seconds`
- Every 6 hours (default), the time bucket changes, generating a new signing key
- Previous key is accepted for the rotation period to handle clock skew and ongoing requests
- No manual intervention needed - keys rotate automatically

## Security Considerations

1. **Secret Key Management**: Store the secret key securely (use environment variables, Azure Key Vault, AWS Secrets Manager, etc.)
2. **Different Keys per Environment**: Use different secret keys for dev, staging, and production
3. **HTTPS in Production**: Always use HTTPS/TLS for gRPC in production
4. **Network Security**: Combine with network-level security (VPC, firewall rules)
5. **Monitor Failed Auth**: Log and monitor authentication failures for security incidents

## Migration Path

### Phase 1: Add Token Generation (Client Side)
- Register `IInterServiceTokenService` in all services
- Update `GrpcHeaderConfig` registration to include token service
- Services will now send tokens, but servers won't validate yet

### Phase 2: Add Token Validation (Server Side)
- Register `GrpcServerAuthInterceptor` in all services
- Configure servers to require authentication
- Test inter-service communication

### Phase 3: Enable Strict Mode
- Set `requireAuth: true` in production
- Monitor for any authentication failures
- Roll back if issues occur

## Troubleshooting

### "Invalid or missing inter-service authentication token"
- Check if both services have the same `InterServiceSecretKey`
- Verify `IInterServiceTokenService` is registered in both services
- Check service name configuration
- Review logs for detailed error information

### Clock synchronization issues
- Ensure all service machines have synchronized clocks (NTP)
- The 5-minute clock skew allowance should handle minor drift
- Consider increasing `ClockSkew` if needed

### Key rotation causing failures
- Increase `InterServiceKeyRotationHours` if rotation is too frequent
- Check that all services are running with the same time zone settings
- Monitor during rotation periods
