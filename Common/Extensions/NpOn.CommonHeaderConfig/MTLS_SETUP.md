# mTLS Inter-Service Authentication Setup

## Overview
mTLS (Mutual TLS) provides certificate-based authentication between services. Each service has its own client certificate signed by a shared CA, and servers validate client certificates.

## Advantages over JWT
- **No shared secret** - Each service has unique certificate
- **Automatic rotation** - Certificates can be rotated independently
- **Stronger security** - X.509 certificates with private keys
- **Standard protocol** - Built into TLS, no custom implementation needed
- **Fine-grained control** - Can revoke individual certificates

## Certificate Structure
```
/certs/
├── ca.crt                           # Shared CA certificate (all services)
├── general-client.pfx               # GeneralService client certificate
├── general-server.pfx               # GeneralService server certificate
├── sso-client.pfx                   # SSO client certificate
├── sso-server.pfx                   # SSO server certificate
├── account-client.pfx               # AccountService client certificate
├── account-server.pfx               # AccountService server certificate
├── tracker-client.pfx               # TrackerService client certificate
├── tracker-server.pfx               # TrackerService server certificate
└── migration-client.pfx             # MigrationService client certificate
    migration-server.pfx             # MigrationService server certificate
```

## Configuration Steps

### 1. Generate Certificates

Using OpenSSL:

```bash
# Generate CA certificate
openssl genrsa -out ca.key 4096
openssl req -new -x509 -days 3650 -key ca.key -out ca.crt -subj "/CN=NpOn-InterService-CA"

# Generate client certificate for each service
# GeneralService
openssl genrsa -out general-client.key 2048
openssl req -new -key general-client.key -out general-client.csr -subj "/CN=GeneralService"
openssl x509 -req -days 365 -in general-client.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out general-client.crt
openssl pkcs12 -export -out general-client.pfx -inkey general-client.key -in general-client.crt -password pass:NpOn_V2_Cert_Password

# GeneralService Server Certificate
openssl genrsa -out general-server.key 2048
openssl req -new -key general-server.key -out general-server.csr -subj "/CN=GeneralService-Server"
openssl x509 -req -days 365 -in general-server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out general-server.crt
openssl pkcs12 -export -out general-server.pfx -inkey general-server.key -in general-server.crt -password pass:NpOn_V2_Cert_Password

# SSO Client Certificate
openssl genrsa -out sso-client.key 2048
openssl req -new -key sso-client.key -out sso-client.csr -subj "/CN=SSO"
openssl x509 -req -days 365 -in sso-client.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out sso-client.crt
openssl pkcs12 -export -out sso-client.pfx -inkey sso-client.key -in sso-client.crt -password pass:NpOn_V2_Cert_Password

# SSO Server Certificate
openssl genrsa -out sso-server.key 2048
openssl req -new -key sso-server.key -out sso-server.csr -subj "/CN=SSO-Server"
openssl x509 -req -days 365 -in sso-server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out sso-server.crt
openssl pkcs12 -export -out sso-server.pfx -inkey sso-server.key -in sso-server.crt -password pass:NpOn_V2_Cert_Password

# AccountService Client Certificate
openssl genrsa -out account-client.key 2048
openssl req -new -key account-client.key -out account-client.csr -subj "/CN=AccountService"
openssl x509 -req -days 365 -in account-client.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out account-client.crt
openssl pkcs12 -export -out account-client.pfx -inkey account-client.key -in account-client.crt -password pass:NpOn_V2_Cert_Password

# AccountService Server Certificate
openssl genrsa -out account-server.key 2048
openssl req -new -key account-server.key -out account-server.csr -subj "/CN=AccountService-Server"
openssl x509 -req -days 365 -in account-server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out account-server.crt
openssl pkcs12 -export -out account-server.pfx -inkey account-server.key -in account-server.crt -password pass:NpOn_V2_Cert_Password

# TrackerService Client Certificate
openssl genrsa -out tracker-client.key 2048
openssl req -new -key tracker-client.key -out tracker-client.csr -subj "/CN=TrackerService"
openssl x509 -req -days 365 -in tracker-client.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out tracker-client.crt
openssl pkcs12 -export -out tracker-client.pfx -inkey tracker-client.key -in tracker-client.crt -password pass:NpOn_V2_Cert_Password

# TrackerService Server Certificate
openssl genrsa -out tracker-server.key 2048
openssl req -new -key tracker-server.key -out tracker-server.csr -subj "/CN=TrackerService-Server"
openssl x509 -req -days 365 -in tracker-server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out tracker-server.crt
openssl pkcs12 -export -out tracker-server.pfx -inkey tracker-server.key -in tracker-server.crt -password pass:NpOn_V2_Cert_Password

# MigrationService Client Certificate
openssl genrsa -out migration-client.key 2048
openssl req -new -key migration-client.key -out migration-client.csr -subj "/CN=MigrationService"
openssl x509 -req -days 365 -in migration-client.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out migration-client.crt
openssl pkcs12 -export -out migration-client.pfx -inkey migration-client.key -in migration-client.crt -password pass:NpOn_V2_Cert_Password

# MigrationService Server Certificate
openssl genrsa -out migration-server.key 2048
openssl req -new -key migration-server.key -out migration-server.csr -subj "/CN=MigrationService-Server"
openssl x509 -req -days 365 -in migration-server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out migration-server.crt
openssl pkcs12 -export -out migration-server.pfx -inkey migration-server.key -in migration-server.crt -password pass:NpOn_V2_Cert_Password
```

### 2. Update appsettings.yaml

```yaml
# Inter-service authentication - mTLS
InterServiceUseMTLS: true
InterServiceClientCertPath: "/certs/client.pfx"
InterServiceClientCertPassword: "NpOn_V2_Cert_Password"
InterServiceServerCertPath: "/certs/server.pfx"
InterServiceServerCertPassword: "NpOn_V2_Cert_Password"
InterServiceCACertPath: "/certs/ca.crt"

# Fallback JWT (optional, for gradual migration)
InterServiceSecretKey: "NpOn-InterService-Secret-Key-2024-Change-In-Production"
InterServiceTokenLifetimeMinutes: 60
InterServiceKeyRotationMinutes: 360
```

### 3. Register Certificate Service in Program.cs

```csharp
protected override Task ConfigureServices(IServiceCollection services)
{
    // Register certificate service
    var useMTLS = EApplicationConfiguration.InterServiceUseMTLS.GetAppSettingConfig().AsDefaultBool(false);
    if (useMTLS)
    {
        var clientCertPath = EApplicationConfiguration.InterServiceClientCertPath.GetAppSettingConfig().AsDefaultString();
        var clientCertPassword = EApplicationConfiguration.InterServiceClientCertPassword.GetAppSettingConfig().AsDefaultString();
        var serverCertPath = EApplicationConfiguration.InterServiceServerCertPath.GetAppSettingConfig().AsDefaultString();
        var serverCertPassword = EApplicationConfiguration.InterServiceServerCertPassword.GetAppSettingConfig().AsDefaultString();
        var caCertPath = EApplicationConfiguration.InterServiceCACertPath.GetAppSettingConfig().AsDefaultString();

        services.AddSingleton<IInterServiceCertificateService>(new InterServiceCertificateService(
            clientCertPath,
            clientCertPassword,
            serverCertPath,
            serverCertPassword,
            caCertPath));
    }

    // Register JWT token service as fallback
    var interServiceSecret = EApplicationConfiguration.InterServiceSecretKey.GetAppSettingConfig().AsDefaultString();
    var tokenLifetimeMinutes = EApplicationConfiguration.InterServiceTokenLifetimeMinutes.GetAppSettingConfig().AsDefaultInt(60);
    var keyRotationMinutes = EApplicationConfiguration.InterServiceKeyRotationMinutes.GetAppSettingConfig().AsDefaultInt(360);
    services.AddSingleton<IInterServiceTokenService>(new InterServiceTokenService(
        interServiceSecret,
        TimeSpan.FromMinutes(tokenLifetimeMinutes),
        TimeSpan.FromMinutes(keyRotationMinutes)));

    // Configure Kestrel for mTLS
    if (useMTLS)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            var serverCertPath = EApplicationConfiguration.InterServiceServerCertPath.GetAppSettingConfig().AsDefaultString();
            var serverCertPassword = EApplicationConfiguration.InterServiceServerCertPassword.GetAppSettingConfig().AsDefaultString();
            var caCertPath = EApplicationConfiguration.InterServiceCACertPath.GetAppSettingConfig().AsDefaultString();

            options.ConfigureHttpsDefaults(httpsOptions =>
            {
                httpsOptions.ServerCertificate = new X509Certificate2(serverCertPath, serverCertPassword);
                httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                httpsOptions.ClientCertificateValidation = (cert, chain, errors) =>
                {
                    var certService = services.BuildServiceProvider().GetRequiredService<IInterServiceCertificateService>();
                    return certService.ValidateClientCertificate(cert);
                };
            });
        });
    }

    // ... rest of configuration
}
```

### 4. Configure gRPC Client to Use Client Certificate

Update the gRPC client configuration to attach the client certificate:

```csharp
// In your gRPC client resolver or channel creation
var certService = serviceProvider.GetRequiredService<IInterServiceCertificateService>();
var clientCert = certService.GetClientCertificate();

if (clientCert != null)
{
    var handler = new SocketsHttpHandler();
    handler.SslOptions.ClientCertificates = new X509CertificateCollection { clientCert };
    
    var channel = GrpcChannel.ForAddress(serviceUrl, new GrpcChannelOptions
    {
        HttpHandler = handler
    });
}
```

## Migration Path

### Phase 1: Add Certificate Support (No Enforcement)
- Set `InterServiceUseMTLS: false`
- Register certificate service
- Configure Kestrel to accept but not require certificates
- Services can use either JWT or certificates

### Phase 2: Enable mTLS with JWT Fallback
- Set `InterServiceUseMTLS: true`
- Server validates certificates if present
- Falls back to JWT if no certificate
- Gradually deploy certificates to services

### Phase 3: Require mTLS Only
- Remove JWT fallback
- Set `ClientCertificateMode.RequireCertificate`
- All services must use certificates

## Security Best Practices

1. **Certificate Rotation**: Rotate certificates every 90-180 days
2. **Key Storage**: Store certificate passwords in secure vaults (Azure Key Vault, AWS Secrets Manager)
3. **Certificate Revocation**: Implement CRL or OCSP for revocation
4. **Certificate Attributes**: Include service name in certificate subject or SAN
5. **Network Security**: Combine with network-level security (VPC, firewall rules)
6. **Monitoring**: Log certificate validation failures for security incidents

## Troubleshooting

### "Client certificate validation failed"
- Check if client certificate is signed by the correct CA
- Verify certificate hasn't expired
- Check certificate chain validation

### "Failed to load client certificate"
- Verify certificate path is correct
- Check certificate password
- Ensure file permissions allow reading

### "mTLS certificate validation failed"
- Check CA certificate path
- Verify server certificate is valid
- Check clock synchronization (certificate validity dates)
