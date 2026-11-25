using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NpOn.CommonGrpcCall;
using Xunit;

namespace NpOn.Core.Tests;

public class InterServiceAuth3A3BTests
{
    private sealed class SpyTokenService : IInterServiceTokenService
    {
        public int GenerateCount { get; private set; }

        public string GenerateToken(string serviceName, string? additionalData = null)
        {
            GenerateCount++;
            return $"spy-token-{serviceName}";
        }

        public bool ValidateToken(string token, out ClaimsPrincipal? principal)
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("service_name", "SSO")], "SpyJwt"));
            return true;
        }
    }

    private sealed class SimpleServiceProvider(params (Type ServiceType, object Instance)[] services) : IServiceProvider
    {
        private readonly Dictionary<Type, object> _map = services.ToDictionary(x => x.ServiceType, x => x.Instance);
        public object? GetService(Type serviceType) => _map.GetValueOrDefault(serviceType);
    }

    private static string CreateTempCertDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NpOnAuthTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Best-effort temp cleanup
        }
    }

    private static X509Certificate2 CreateRogueSelfSignedCert(string issuerCn, string subjectCn, bool clientAuth)
    {
        using var rogueCaRsa = RSA.Create(2048);
        var issuer = new X500DistinguishedName($"CN={issuerCn}");
        var caReq = new CertificateRequest(issuer, rogueCaRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        caReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

        var now = DateTimeOffset.UtcNow;
        using var fakeIssuerCert = caReq.CreateSelfSigned(now.AddDays(-2), now.AddDays(30));

        using var leafRsa = RSA.Create(2048);
        var subject = new X500DistinguishedName($"CN={subjectCn}");
        var req = new CertificateRequest(subject, leafRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid(clientAuth ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1") }, false));

        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        using var certOnly = req.Create(fakeIssuerCert, now.AddDays(-1), now.AddDays(10), serial);
        using var ephemeralCert = certOnly.CopyWithPrivateKey(leafRsa);
        // Round-trip via PKCS#12 so Windows Schannel receives a non-ephemeral private key
        byte[] pfxBytes = ephemeralCert.Export(X509ContentType.Pfx, "rogue");
        return X509CertificateLoader.LoadPkcs12(pfxBytes, "rogue");
    }

    [Fact]
    public void AuthModeEnum_And_ConfigHelper_ShouldParseAllModesAndCsvSettings()
    {
        Assert.Equal(0, (int)EInterServiceAuthMode.Jwt);
        Assert.Equal(1, (int)EInterServiceAuthMode.Mtls);
        Assert.Equal(2, (int)EInterServiceAuthMode.ServiceMesh);
        Assert.Equal("Jwt", EInterServiceAuthMode.Jwt.GetDisplayName());
        Assert.Equal("Mtls", EInterServiceAuthMode.Mtls.GetDisplayName());
        Assert.Equal("ServiceMesh", EInterServiceAuthMode.ServiceMesh.GetDisplayName());

        Assert.Equal(EInterServiceAuthMode.Mtls, "mtls".ToEnum<EInterServiceAuthMode>());
        Assert.Equal(EInterServiceAuthMode.ServiceMesh, "ServiceMesh".ToEnum<EInterServiceAuthMode>());
        Assert.Equal(EInterServiceAuthMode.ServiceMesh, "2".IntAsStringToEnum<EInterServiceAuthMode>());

        Assert.Equal(["sso", "accountservice"], InterServiceAuthConfigHelper.ParseCsvList(" sso , accountservice , "));
        Assert.Equal(EInterServiceAuthMode.Mtls, InterServiceAuthConfigHelper.ResolveAuthMode(useMtlsFallback: true));
        Assert.Equal(EInterServiceAuthMode.Jwt, InterServiceAuthConfigHelper.ResolveAuthMode(useMtlsFallback: false));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InterServiceAuthMode"] = "ServiceMesh",
                ["InterServiceAllowedIdentities"] = "SSO,AccountService",
                ["InterServiceServiceMeshTrustDomain"] = "npon.mesh.local",
                ["InterServiceTrustedProxyIps"] = "127.0.0.1,10.0.0.0/8",
                ["InterServiceServerSanHosts"] = "accountservice,localhost"
            })
            .Build();

        config.InitConfigs(typeof(EApplicationConfiguration));

        Assert.Equal(EInterServiceAuthMode.ServiceMesh, InterServiceAuthConfigHelper.ResolveAuthMode());
        Assert.Equal(["SSO", "AccountService"], InterServiceAuthConfigHelper.GetAllowedIdentities());
        Assert.Equal("npon.mesh.local", InterServiceAuthConfigHelper.GetServiceMeshTrustDomain());
        Assert.Equal(["127.0.0.1", "10.0.0.0/8"], InterServiceAuthConfigHelper.GetTrustedProxyIps());
        Assert.Equal(["accountservice", "localhost"], InterServiceAuthConfigHelper.GetServerSanHosts());
    }

    [Fact]
    public async Task Option3A_CertificateGenerationAndRotation_ShouldPersistCaKeyAndReloadLeafCerts()
    {
        string dir = CreateTempCertDir();
        try
        {
            string caCrt = Path.Combine(dir, "ca.crt");
            string caPfx = Path.Combine(dir, "ca.pfx");
            string clientPfx = Path.Combine(dir, "sso-client.pfx");
            string serverPfx = Path.Combine(dir, "sso-server.pfx");
            const string pwd = "Test_Cert_Password";

            var certService = new InterServiceCertificateService(
                clientPfx, pwd, serverPfx, pwd, caCrt,
                NullLogger<InterServiceCertificateService>.Instance,
                allowedIdentities: ["SSO", "SSO-Server"]);

            var generator = new CertificateGenerator(NullLogger<CertificateGenerator>.Instance, dir);
            var rotation = new CertificateRotationService(
                generator,
                certService,
                NullLogger<CertificateRotationService>.Instance,
                caCertPath: caCrt,
                certificateValidityDays: 30,
                clientCertPath: clientPfx,
                serverCertPath: serverPfx,
                certPassword: pwd,
                caPfxPath: caPfx,
                additionalDnsNames: ["sso.internal", "127.0.0.1"]);

            await rotation.EnsureCertificatesExistAsync("SSO");

            Assert.True(File.Exists(caCrt));
            Assert.True(File.Exists(caPfx));
            Assert.True(File.Exists(clientPfx));
            Assert.True(File.Exists(serverPfx));

            var loadedCa = certService.GetCACertificate();
            var loadedClient = certService.GetClientCertificate();
            var loadedServer = certService.GetServerCertificate();

            Assert.NotNull(loadedCa);
            Assert.NotNull(loadedClient);
            Assert.NotNull(loadedServer);
            Assert.True(loadedClient.HasPrivateKey);
            Assert.True(loadedServer.HasPrivateKey);

            // Root CA must not carry clientAuth EKU
            Assert.DoesNotContain(loadedCa.Extensions, e => e is X509EnhancedKeyUsageExtension);

            Assert.True(certService.ValidateClientCertificate(loadedClient));
            Assert.True(certService.ValidateServerCertificate(loadedServer));
            Assert.True(certService.TryExtractServiceIdentity(loadedClient, out string? clientIdentity));
            Assert.Equal("SSO", clientIdentity);

            string oldClientThumbprint = loadedClient.Thumbprint;
            await rotation.RotateCertificatesAsync("SSO");

            var rotatedClient = certService.GetClientCertificate();
            Assert.NotNull(rotatedClient);
            Assert.NotEqual(oldClientThumbprint, rotatedClient.Thumbprint);
            Assert.True(certService.ValidateClientCertificate(rotatedClient));
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public void Option3A_CertificateValidation_ShouldRejectRogueIssuerSpoofWrongEkuAndUnauthorizedIdentity()
    {
        string dir = CreateTempCertDir();
        try
        {
            string caCrt = Path.Combine(dir, "ca.crt");
            string clientPfx = Path.Combine(dir, "client.pfx");
            string serverPfx = Path.Combine(dir, "server.pfx");
            const string pwd = "Test_Cert_Password";

            var generator = new CertificateGenerator(NullLogger<CertificateGenerator>.Instance, dir);
            using var caCert = generator.GenerateCACertificate("NpOn-InterService-CA", 365);
            using var validClient = generator.GenerateClientCertificate("AccountService", caCert, 90);
            using var validServer = generator.GenerateServerCertificate("AccountService", caCert, 90);
            using var unauthorizedClient = generator.GenerateClientCertificate("EvilService", caCert, 90);
            using var rogueSpoofedClient = CreateRogueSelfSignedCert("NpOn-InterService-CA", "AccountService", clientAuth: true);

            generator.SaveCertificate(caCert, caCrt);
            generator.SaveCertificate(validClient, clientPfx, pwd);
            generator.SaveCertificate(validServer, serverPfx, pwd);

            var certService = new InterServiceCertificateService(
                clientPfx, pwd, serverPfx, pwd, caCrt,
                NullLogger<InterServiceCertificateService>.Instance,
                allowedIdentities: ["AccountService", "SSO"]);

            // Valid certs pass (and second call hits thumbprint cache)
            Assert.True(certService.ValidateClientCertificate(validClient));
            Assert.True(certService.ValidateClientCertificate(validClient));
            Assert.True(certService.ValidateServerCertificate(validServer));

            // Rogue self-signed cert claiming Issuer=CN=NpOn-InterService-CA MUST be rejected
            Assert.False(certService.ValidateClientCertificate(rogueSpoofedClient));

            // Wrong EKU (server cert used as client cert, and client cert used as server cert) MUST be rejected
            Assert.False(certService.ValidateClientCertificate(validServer));
            Assert.False(certService.ValidateServerCertificate(validClient));

            // CA-signed cert whose identity is not in allowedIdentities MUST be rejected
            Assert.False(certService.ValidateClientCertificate(unauthorizedClient));
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public async Task Option3A_Middleware_ShouldBeStrictMtlsWithoutJwtFallback()
    {
        string dir = CreateTempCertDir();
        try
        {
            string caCrt = Path.Combine(dir, "ca.crt");
            var generator = new CertificateGenerator(NullLogger<CertificateGenerator>.Instance, dir);
            using var caCert = generator.GenerateCACertificate("NpOn-InterService-CA", 365);
            using var validClient = generator.GenerateClientCertificate("SSO", caCert, 90);
            generator.SaveCertificate(caCert, caCrt);

            var certService = new InterServiceCertificateService(
                null, null, null, null, caCrt,
                NullLogger<InterServiceCertificateService>.Instance,
                allowedIdentities: ["SSO"]);
            var spyJwt = new SpyTokenService();
            var sp = new SimpleServiceProvider(
                (typeof(IInterServiceCertificateService), certService),
                (typeof(IInterServiceTokenService), spyJwt));

            bool nextCalled = false;
            var middleware = new GrpcServerAuthMiddleware(
                _ => { nextCalled = true; return Task.CompletedTask; },
                sp,
                NullLogger<GrpcServerAuthMiddleware>.Instance,
                requireAuth: true,
                useMtls: true,
                authMode: EInterServiceAuthMode.Mtls,
                allowedIdentities: ["SSO"]);

            // 1. JWT-only without ClientCertificate MUST return 401 (no JWT fallback in Mtls mode)
            var jwtOnlyCtx = new DefaultHttpContext();
            jwtOnlyCtx.Request.Path = "/AccountService/Login";
            jwtOnlyCtx.Request.ContentType = "application/grpc";
            jwtOnlyCtx.Request.Headers[DefaultHeaderConstant.GrpcInterServiceAuthToken] = "valid-looking-jwt";
            await middleware.InvokeAsync(jwtOnlyCtx);
            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status401Unauthorized, jwtOnlyCtx.Response.StatusCode);

            // 2. Valid ClientCertificate passes and sets AuthMethod + ServiceName
            var mtlsCtx = new DefaultHttpContext();
            mtlsCtx.Request.Path = "/AccountService/Login";
            mtlsCtx.Request.ContentType = "application/grpc";
            mtlsCtx.Connection.ClientCertificate = validClient;
            await middleware.InvokeAsync(mtlsCtx);
            Assert.True(nextCalled);
            Assert.Equal("mTLS", mtlsCtx.Items["AuthMethod"]);
            Assert.Equal("SSO", mtlsCtx.Items["ServiceName"]);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public async Task Option3A_KestrelLoopback_MtlsAndMixedRestPort_EndToEndShouldEnforceStrictTlsAndAuth()
    {
        string dir = CreateTempCertDir();
        try
        {
            string caCrt = Path.Combine(dir, "ca.crt");
            string clientPfx = Path.Combine(dir, "sso-client.pfx");
            string serverPfx = Path.Combine(dir, "account-server.pfx");
            const string pwd = "Test_Cert_Password";

            var generator = new CertificateGenerator(NullLogger<CertificateGenerator>.Instance, dir);
            using var caCert = generator.GenerateCACertificate("NpOn-InterService-CA", 365);
            using var clientCert = generator.GenerateClientCertificate("SSO", caCert, 90);
            using var serverCert = generator.GenerateServerCertificate("AccountService", caCert, 90);
            using var rogueClientCert = CreateRogueSelfSignedCert("NpOn-InterService-CA", "SSO", clientAuth: true);

            generator.SaveCertificate(caCert, caCrt);
            generator.SaveCertificate(clientCert, clientPfx, pwd);
            generator.SaveCertificate(serverCert, serverPfx, pwd);

            var certService = new InterServiceCertificateService(
                clientPfx, pwd, serverPfx, pwd, caCrt,
                NullLogger<InterServiceCertificateService>.Instance,
                allowedIdentities: ["SSO"]);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, 0, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    listenOptions.UseHttps(https =>
                    {
                        https.ServerCertificate = certService.GetServerCertificate();
                        https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                        https.AllowAnyClientCertificate();
                    });
                });
            });
            builder.Services.AddSingleton<IInterServiceCertificateService>(certService);

            await using var app = builder.Build();
            app.UseGrpcServerAuth(
                requireAuth: true,
                authMode: EInterServiceAuthMode.Mtls,
                allowedIdentities: ["SSO"]);
            app.MapGet("/api/health", () => Results.Ok("ok"));
            app.MapPost("/AccountService/Ping", (HttpContext ctx) =>
                Results.Ok($"{ctx.Items["AuthMethod"]}:{ctx.Items["ServiceName"]}"));

            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.First();

            SocketsHttpHandler CreateHandler(X509Certificate2? presentedClientCert)
            {
                var h = new SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true,
                    SslOptions =
                    {
                        RemoteCertificateValidationCallback = (_, cert, _, _) =>
                        {
                            if (cert == null) return false;
                            if (cert is X509Certificate2 x2) return certService.ValidateServerCertificate(x2);
                            using var loaded = X509CertificateLoader.LoadCertificate(cert.GetRawCertData());
                            return certService.ValidateServerCertificate(loaded);
                        }
                    }
                };
                if (presentedClientCert != null)
                    h.SslOptions.ClientCertificates = new X509CertificateCollection { presentedClientCert };
                return h;
            }

            HttpRequestMessage CreateH2GrpcRequest()
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "/AccountService/Ping")
                {
                    Version = HttpVersion.Version20,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                    Content = new ByteArrayContent([])
                };
                req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
                return req;
            }

            // 1. Valid mTLS gRPC call succeeds (HTTP/2)
            using (var validClientHttp = new HttpClient(CreateHandler(certService.GetClientCertificate()))
                   { BaseAddress = new Uri(address), DefaultRequestVersion = HttpVersion.Version20, DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact })
            {
                using var req = CreateH2GrpcRequest();
                using var resp = await validClientHttp.SendAsync(req);
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
                Assert.Contains("mTLS:SSO", await resp.Content.ReadAsStringAsync());
            }

            // 2. Mixed REST /api endpoint without client cert passes (AllowCertificate mode)
            // 3. gRPC request without client cert returns 401
            using (var noCertHttp = new HttpClient(CreateHandler(null))
                   { BaseAddress = new Uri(address), DefaultRequestVersion = HttpVersion.Version20, DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact })
            {
                var apiResp = await noCertHttp.GetAsync("/api/health");
                Assert.Equal(HttpStatusCode.OK, apiResp.StatusCode);

                using var grpcReq = CreateH2GrpcRequest();
                var grpcResp = await noCertHttp.SendAsync(grpcReq);
                Assert.Equal(HttpStatusCode.Unauthorized, grpcResp.StatusCode);
            }

            // 4. gRPC request with rogue self-signed client cert returns 401
            using (var rogueHttp = new HttpClient(CreateHandler(rogueClientCert))
                   { BaseAddress = new Uri(address), DefaultRequestVersion = HttpVersion.Version20, DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact })
            {
                using var grpcReq = CreateH2GrpcRequest();
                var rogueResp = await rogueHttp.SendAsync(grpcReq);
                Assert.Equal(HttpStatusCode.Unauthorized, rogueResp.StatusCode);
            }

            await app.StopAsync();
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public async Task Option3B_ServiceMeshMiddleware_ShouldVerifyProxyIpTrustDomainAndAllowedIdentities()
    {
        var sp = new SimpleServiceProvider();

        GrpcServerAuthMiddleware CreateMiddleware(Action<HttpContext>? onNext = null) =>
            new(
                ctx => { onNext?.Invoke(ctx); return Task.CompletedTask; },
                sp,
                NullLogger<GrpcServerAuthMiddleware>.Instance,
                requireAuth: true,
                useMtls: false,
                authMode: EInterServiceAuthMode.ServiceMesh,
                serviceMeshTrustDomain: "npon.local",
                trustedProxyIps: ["127.0.0.1", "::1"],
                allowedIdentities: ["sso", "spiffe://npon.local/ns/prod/sa/general"]);

        // 1. Trusted loopback proxy + valid SPIFFE URI -> PASS
        bool passed = false;
        var validCtx = new DefaultHttpContext();
        validCtx.Connection.RemoteIpAddress = IPAddress.Loopback;
        validCtx.Request.Path = "/AccountService/GetInfo";
        validCtx.Request.ContentType = "application/grpc";
        validCtx.Request.Headers[DefaultHeaderConstant.GrpcForwardedClientCert] =
            "By=spiffe://npon.local/ns/prod/sa/account;Hash=abc;Subject=\"CN=sso\";URI=spiffe://npon.local/ns/prod/sa/sso";

        await CreateMiddleware(_ => passed = true).InvokeAsync(validCtx);
        Assert.True(passed);
        Assert.Equal("ServiceMesh", validCtx.Items["AuthMethod"]);
        Assert.Equal("spiffe://npon.local/ns/prod/sa/sso", validCtx.Items["SpiffeId"]);
        Assert.Equal("sso", validCtx.Items["ServiceName"]);

        // 2. Spoofed XFCC from untrusted IP -> 401
        bool spoofPassed = false;
        var spoofCtx = new DefaultHttpContext();
        spoofCtx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.50");
        spoofCtx.Request.Path = "/AccountService/GetInfo";
        spoofCtx.Request.ContentType = "application/grpc";
        spoofCtx.Request.Headers[DefaultHeaderConstant.GrpcForwardedClientCert] =
            "URI=spiffe://npon.local/ns/prod/sa/sso";

        await CreateMiddleware(_ => spoofPassed = true).InvokeAsync(spoofCtx);
        Assert.False(spoofPassed);
        Assert.Equal(StatusCodes.Status401Unauthorized, spoofCtx.Response.StatusCode);

        // 3. Wrong trust domain -> 401
        var wrongDomainCtx = new DefaultHttpContext();
        wrongDomainCtx.Connection.RemoteIpAddress = IPAddress.IPv6Loopback;
        wrongDomainCtx.Request.Path = "/AccountService/GetInfo";
        wrongDomainCtx.Request.ContentType = "application/grpc";
        wrongDomainCtx.Request.Headers[DefaultHeaderConstant.GrpcForwardedClientCert] =
            "URI=spiffe://evil.local/ns/prod/sa/sso";

        await CreateMiddleware().InvokeAsync(wrongDomainCtx);
        Assert.Equal(StatusCodes.Status401Unauthorized, wrongDomainCtx.Response.StatusCode);

        // 4. Unauthorized SPIFFE identity -> 401
        var unauthorizedCtx = new DefaultHttpContext();
        unauthorizedCtx.Connection.RemoteIpAddress = IPAddress.Loopback;
        unauthorizedCtx.Request.Path = "/AccountService/GetInfo";
        unauthorizedCtx.Request.ContentType = "application/grpc";
        unauthorizedCtx.Request.Headers[DefaultHeaderConstant.GrpcForwardedClientCert] =
            "URI=spiffe://npon.local/ns/prod/sa/rogue";

        await CreateMiddleware().InvokeAsync(unauthorizedCtx);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedCtx.Response.StatusCode);

        // 5. Missing XFCC header -> 401
        var missingHeaderCtx = new DefaultHttpContext();
        missingHeaderCtx.Connection.RemoteIpAddress = IPAddress.Loopback;
        missingHeaderCtx.Request.Path = "/AccountService/GetInfo";
        missingHeaderCtx.Request.ContentType = "application/grpc";

        await CreateMiddleware().InvokeAsync(missingHeaderCtx);
        Assert.Equal(StatusCodes.Status401Unauthorized, missingHeaderCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Option3B_KestrelLoopback_H2cEnvoyXfcc_EndToEndShouldAuthenticateAndSkipJwtGeneration()
    {
        var spyTokenService = new SpyTokenService();
        var meshHeaderConfig = new GrpcHeaderConfig(
            EGrpcEndUseType.InternalServer,
            headers: null,
            tokenService: spyTokenService,
            serviceName: "SSO",
            enablePerRequestToken: false);

        Assert.Equal(0, spyTokenService.GenerateCount);
        Assert.Null(meshHeaderConfig.GetHeader()?.GetValue(DefaultHeaderConstant.GrpcInterServiceAuthToken));

        var jwtHeaderConfig = new GrpcHeaderConfig(
            EGrpcEndUseType.InternalServer,
            headers: null,
            tokenService: spyTokenService,
            serviceName: "SSO",
            enablePerRequestToken: true);

        Assert.Equal(1, spyTokenService.GenerateCount);
        Assert.Equal("spy-token-SSO", jwtHeaderConfig.GetHeader()?.GetValue(DefaultHeaderConstant.GrpcInterServiceAuthToken));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        await using var app = builder.Build();
        app.UseGrpcServerAuth(
            requireAuth: true,
            authMode: EInterServiceAuthMode.ServiceMesh,
            serviceMeshTrustDomain: "npon.local",
            trustedProxyIps: ["127.0.0.1", "::1"],
            allowedIdentities: ["sso"]);

        app.MapPost("/AccountService/Ping", (HttpContext ctx) =>
            Results.Ok($"{ctx.Items["AuthMethod"]}:{ctx.Items["ServiceName"]}:{ctx.Items["SpiffeId"]}"));

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.First();

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(address),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        HttpRequestMessage CreateH2cGrpcRequest()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/AccountService/Ping")
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Content = new ByteArrayContent([])
            };
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            return req;
        }

        // Missing XFCC -> 401
        using (var noXfccReq = CreateH2cGrpcRequest())
        {
            var resp = await httpClient.SendAsync(noXfccReq);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        // Valid XFCC from Envoy -> 200 OK
        using (var validReq = CreateH2cGrpcRequest())
        {
            validReq.Headers.TryAddWithoutValidation(
                DefaultHeaderConstant.GrpcForwardedClientCert,
                "By=spiffe://npon.local/ns/prod/sa/account;URI=spiffe://npon.local/ns/prod/sa/sso");
            var resp = await httpClient.SendAsync(validReq);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Contains("ServiceMesh:sso:spiffe://npon.local/ns/prod/sa/sso", await resp.Content.ReadAsStringAsync());
        }

        // Untrusted domain XFCC -> 401
        using (var badDomainReq = CreateH2cGrpcRequest())
        {
            badDomainReq.Headers.TryAddWithoutValidation(
                DefaultHeaderConstant.GrpcForwardedClientCert,
                "URI=spiffe://rogue.local/ns/prod/sa/sso");
            var resp = await httpClient.SendAsync(badDomainReq);
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        await app.StopAsync();
    }
}
