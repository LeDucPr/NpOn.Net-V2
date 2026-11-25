using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Common.Extensions.NpOn.HeaderConfig;

public interface ICertificateRotationService
{
    Task EnsureCertificatesExistAsync(string serviceName);
    Task RotateCertificatesAsync(string serviceName);
}

public class CertificateRotationService : ICertificateRotationService
{
    private const string DefaultCertPassword = "NpOn_V2_Cert_Password";
    private const int RenewalWindowDays = 7;

    private readonly ICertificateGenerator _certificateGenerator;
    private readonly IInterServiceCertificateService _certificateService;
    private readonly ILogger<CertificateRotationService> _logger;
    private readonly string _caCertPath;
    private readonly string _caPfxPath;
    private readonly int _certificateValidityDays;
    private readonly string? _clientCertPath;
    private readonly string? _serverCertPath;
    private readonly string _certPassword;
    private readonly IReadOnlyCollection<string>? _additionalDnsNames;

    public CertificateRotationService(
        ICertificateGenerator certificateGenerator,
        IInterServiceCertificateService certificateService,
        ILogger<CertificateRotationService> logger,
        string caCertPath = "/certs/ca.crt",
        int certificateValidityDays = 90,
        int rotationCheckIntervalHours = 24,
        string? clientCertPath = null,
        string? serverCertPath = null,
        string? certPassword = null,
        string? caPfxPath = null,
        IReadOnlyCollection<string>? additionalDnsNames = null)
    {
        _ = rotationCheckIntervalHours;
        _certificateGenerator = certificateGenerator;
        _certificateService = certificateService;
        _logger = logger;
        _caCertPath = string.IsNullOrWhiteSpace(caCertPath) ? "/certs/ca.crt" : caCertPath;
        _caPfxPath = !string.IsNullOrWhiteSpace(caPfxPath)
            ? caPfxPath
            : Path.ChangeExtension(_caCertPath, ".pfx");
        _certificateValidityDays = certificateValidityDays > 0 ? certificateValidityDays : 90;
        _clientCertPath = clientCertPath;
        _serverCertPath = serverCertPath;
        _certPassword = string.IsNullOrWhiteSpace(certPassword) ? DefaultCertPassword : certPassword;
        _additionalDnsNames = additionalDnsNames;
    }

    public Task EnsureCertificatesExistAsync(string serviceName)
    {
        _logger.LogInformation("Checking certificates for service: {ServiceName}", serviceName);

        var caCert = LoadOrCreateCaCertificate();
        if (caCert == null || !caCert.HasPrivateKey)
        {
            _logger.LogWarning("CA certificate does not contain a private key; skipping leaf certificate generation");
            return Task.CompletedTask;
        }

        string clientPath = ResolveLeafPath(_clientCertPath, serviceName, "client");
        string serverPath = ResolveLeafPath(_serverCertPath, serviceName, "server");
        var dnsNames = BuildServiceSanHosts(serviceName);
        bool reloadedNeeded = false;

        var clientCert = _certificateGenerator.LoadCertificate(clientPath, _certPassword);
        if (clientCert == null || clientCert.NotAfter.ToUniversalTime() < DateTime.UtcNow.AddDays(RenewalWindowDays))
        {
            _logger.LogInformation("Client certificate missing or expiring soon, generating new one");
            var newClientCert = _certificateGenerator.GenerateClientCertificate(
                serviceName, caCert, _certificateValidityDays, dnsNames);
            _certificateGenerator.SaveCertificate(newClientCert, clientPath, _certPassword);
            reloadedNeeded = true;
        }
        else
        {
            _logger.LogInformation("Client certificate valid until: {Expiration}", clientCert.NotAfter);
        }

        var serverCert = _certificateGenerator.LoadCertificate(serverPath, _certPassword);
        if (serverCert == null || serverCert.NotAfter.ToUniversalTime() < DateTime.UtcNow.AddDays(RenewalWindowDays))
        {
            _logger.LogInformation("Server certificate missing or expiring soon, generating new one");
            var newServerCert = _certificateGenerator.GenerateServerCertificate(
                serviceName, caCert, _certificateValidityDays, dnsNames);
            _certificateGenerator.SaveCertificate(newServerCert, serverPath, _certPassword);
            reloadedNeeded = true;
        }
        else
        {
            _logger.LogInformation("Server certificate valid until: {Expiration}", serverCert.NotAfter);
        }

        if (reloadedNeeded)
            _certificateService.ReloadCertificates();

        return Task.CompletedTask;
    }

    public Task RotateCertificatesAsync(string serviceName)
    {
        _logger.LogInformation("Rotating certificates for service: {ServiceName}", serviceName);

        var caCert = _certificateGenerator.LoadCertificate(_caPfxPath, _certPassword)
                     ?? _certificateGenerator.LoadCertificate(_caCertPath);
        if (caCert == null || !caCert.HasPrivateKey)
        {
            _logger.LogError("Cannot rotate certificates: CA certificate with private key not found at {CaPfxPath}", _caPfxPath);
            return Task.CompletedTask;
        }

        string clientPath = ResolveLeafPath(_clientCertPath, serviceName, "client");
        string serverPath = ResolveLeafPath(_serverCertPath, serviceName, "server");
        var dnsNames = BuildServiceSanHosts(serviceName);

        var newClientCert = _certificateGenerator.GenerateClientCertificate(
            serviceName, caCert, _certificateValidityDays, dnsNames);
        _certificateGenerator.SaveCertificate(newClientCert, clientPath, _certPassword);

        var newServerCert = _certificateGenerator.GenerateServerCertificate(
            serviceName, caCert, _certificateValidityDays, dnsNames);
        _certificateGenerator.SaveCertificate(newServerCert, serverPath, _certPassword);

        _certificateService.ReloadCertificates();
        _logger.LogInformation("Certificate rotation completed for service: {ServiceName}", serviceName);
        return Task.CompletedTask;
    }

    private X509Certificate2? LoadOrCreateCaCertificate()
    {
        var caWithKey = _certificateGenerator.LoadCertificate(_caPfxPath, _certPassword);
        if (caWithKey != null && caWithKey.HasPrivateKey)
        {
            if (_certificateGenerator.LoadCertificate(_caCertPath) == null)
                _certificateGenerator.SaveCertificate(caWithKey, _caCertPath);

            _logger.LogInformation("CA certificate loaded from {Path}, expires: {Expiration}", _caPfxPath, caWithKey.NotAfter);
            return caWithKey;
        }

        var publicCa = _certificateGenerator.LoadCertificate(_caCertPath);
        if (publicCa != null)
        {
            _logger.LogInformation("Public CA certificate found at {Path}, expires: {Expiration}", _caCertPath, publicCa.NotAfter);
            return publicCa;
        }

        _logger.LogInformation("CA certificate not found, generating new CA certificate");
        var newCa = _certificateGenerator.GenerateCACertificate("NpOn-InterService-CA", 3650);
        _certificateGenerator.SaveCertificate(newCa, _caPfxPath, _certPassword);
        _certificateGenerator.SaveCertificate(newCa, _caCertPath);
        return newCa;
    }

    private static string ResolveLeafPath(string? configuredPath, string serviceName, string role)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        return $"/certs/{serviceName.ToLowerInvariant()}-{role}.pfx";
    }

    private IReadOnlyList<string> BuildServiceSanHosts(string serviceName)
    {
        var hosts = new List<string>
        {
            serviceName,
            serviceName.ToLowerInvariant(),
            "localhost"
        };

        var configuredSans = _additionalDnsNames ?? InterServiceAuthConfigHelper.GetServerSanHosts();
        if (configuredSans.Count > 0)
            hosts.AddRange(configuredSans);

        return hosts;
    }
}

public class CertificateRotationBackgroundService : BackgroundService
{
    private readonly ICertificateRotationService _rotationService;
    private readonly string _serviceName;
    private readonly ILogger<CertificateRotationBackgroundService> _logger;
    private readonly TimeSpan _rotationCheckInterval;

    public CertificateRotationBackgroundService(
        ICertificateRotationService rotationService,
        string serviceName,
        ILogger<CertificateRotationBackgroundService> logger,
        int rotationCheckIntervalHours = 24)
    {
        _rotationService = rotationService;
        _serviceName = serviceName;
        _logger = logger;
        _rotationCheckInterval = TimeSpan.FromHours(rotationCheckIntervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Certificate rotation background service started for: {ServiceName}", _serviceName);

        await _rotationService.EnsureCertificatesExistAsync(_serviceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_rotationCheckInterval, stoppingToken);
                await _rotationService.EnsureCertificatesExistAsync(_serviceName);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during certificate rotation check for: {ServiceName}", _serviceName);
            }
        }

        _logger.LogInformation("Certificate rotation background service stopped for: {ServiceName}", _serviceName);
    }
}
