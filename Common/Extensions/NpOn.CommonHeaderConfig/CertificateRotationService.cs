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
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly IInterServiceCertificateService _certificateService;
    private readonly ILogger<CertificateRotationService> _logger;
    private readonly string _caCertPath;
    private readonly int _certificateValidityDays;
    private readonly int _rotationCheckIntervalHours;

    public CertificateRotationService(
        ICertificateGenerator certificateGenerator,
        IInterServiceCertificateService certificateService,
        ILogger<CertificateRotationService> logger,
        string caCertPath = "/certs/ca.crt",
        int certificateValidityDays = 90,
        int rotationCheckIntervalHours = 24)
    {
        _certificateGenerator = certificateGenerator;
        _certificateService = certificateService;
        _logger = logger;
        _caCertPath = caCertPath;
        _certificateValidityDays = certificateValidityDays;
        _rotationCheckIntervalHours = rotationCheckIntervalHours;
    }

    public async Task EnsureCertificatesExistAsync(string serviceName)
    {
        _logger.LogInformation("Checking certificates for service: {ServiceName}", serviceName);

        // Check or generate CA certificate
        var caCert = _certificateGenerator.LoadCertificate(_caCertPath);
        if (caCert == null)
        {
            _logger.LogInformation("CA certificate not found, generating new CA certificate");
            caCert = _certificateGenerator.GenerateCACertificate("NpOn-InterService-CA", 3650);
            _certificateGenerator.SaveCertificate(caCert, _caCertPath);
        }
        else
        {
            _logger.LogInformation("CA certificate found, expires: {Expiration}", caCert.NotAfter);
        }

        // Check or generate client certificate
        var clientCertPath = $"/certs/{serviceName.ToLower()}-client.pfx";
        var clientCert = _certificateGenerator.LoadCertificate(clientCertPath, "NpOn_V2_Cert_Password");
        
        if (clientCert == null || clientCert.NotAfter < DateTime.Now.AddDays(7))
        {
            _logger.LogInformation("Client certificate missing or expiring soon, generating new one");
            var newClientCert = _certificateGenerator.GenerateClientCertificate(serviceName, caCert, _certificateValidityDays);
            _certificateGenerator.SaveCertificate(newClientCert, clientCertPath, "NpOn_V2_Cert_Password");
        }
        else
        {
            _logger.LogInformation("Client certificate valid until: {Expiration}", clientCert.NotAfter);
        }

        // Check or generate server certificate
        var serverCertPath = $"/certs/{serviceName.ToLower()}-server.pfx";
        var serverCert = _certificateGenerator.LoadCertificate(serverCertPath, "NpOn_V2_Cert_Password");
        
        if (serverCert == null || serverCert.NotAfter < DateTime.Now.AddDays(7))
        {
            _logger.LogInformation("Server certificate missing or expiring soon, generating new one");
            var newServerCert = _certificateGenerator.GenerateServerCertificate($"{serviceName}-Server", caCert, _certificateValidityDays);
            _certificateGenerator.SaveCertificate(newServerCert, serverCertPath, "NpOn_V2_Cert_Password");
        }
        else
        {
            _logger.LogInformation("Server certificate valid until: {Expiration}", serverCert.NotAfter);
        }

        await Task.CompletedTask;
    }

    public async Task RotateCertificatesAsync(string serviceName)
    {
        _logger.LogInformation("Rotating certificates for service: {ServiceName}", serviceName);

        var caCert = _certificateGenerator.LoadCertificate(_caCertPath);
        if (caCert == null)
        {
            _logger.LogError("Cannot rotate certificates: CA certificate not found");
            return;
        }

        // Generate new client certificate
        var clientCertPath = $"/certs/{serviceName.ToLower()}-client.pfx";
        var newClientCert = _certificateGenerator.GenerateClientCertificate(serviceName, caCert, _certificateValidityDays);
        _certificateGenerator.SaveCertificate(newClientCert, clientCertPath, "NpOn_V2_Cert_Password");

        // Generate new server certificate
        var serverCertPath = $"/certs/{serviceName.ToLower()}-server.pfx";
        var newServerCert = _certificateGenerator.GenerateServerCertificate($"{serviceName}-Server", caCert, _certificateValidityDays);
        _certificateGenerator.SaveCertificate(newServerCert, serverCertPath, "NpOn_V2_Cert_Password");

        _logger.LogInformation("Certificate rotation completed for service: {ServiceName}", serviceName);
        await Task.CompletedTask;
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

        // Initial check
        await _rotationService.EnsureCertificatesExistAsync(_serviceName);

        // Periodic rotation check
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
