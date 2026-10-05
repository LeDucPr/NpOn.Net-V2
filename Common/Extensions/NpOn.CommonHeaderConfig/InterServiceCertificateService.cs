using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Common.Extensions.NpOn.HeaderConfig;

public interface IInterServiceCertificateService
{
    X509Certificate2? GetClientCertificate();
    X509Certificate2? GetServerCertificate();
    bool ValidateClientCertificate(X509Certificate2? clientCert);
}

public class InterServiceCertificateService : IInterServiceCertificateService
{
    private readonly string? _clientCertPath;
    private readonly string? _clientCertPassword;
    private readonly string? _serverCertPath;
    private readonly string? _serverCertPassword;
    private readonly string? _caCertPath;
    private readonly ILogger<InterServiceCertificateService> _logger;

    private X509Certificate2? _cachedClientCert;
    private X509Certificate2? _cachedServerCert;
    private X509Certificate2? _cachedCaCert;
    private readonly object _lock = new();

    public InterServiceCertificateService(
        string? clientCertPath = null,
        string? clientCertPassword = null,
        string? serverCertPath = null,
        string? serverCertPassword = null,
        string? caCertPath = null,
        ILogger<InterServiceCertificateService>? logger = null)
    {
        _clientCertPath = clientCertPath;
        _clientCertPassword = clientCertPassword;
        _serverCertPath = serverCertPath;
        _serverCertPassword = serverCertPassword;
        _caCertPath = caCertPath;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InterServiceCertificateService>.Instance;
    }

    public X509Certificate2? GetClientCertificate()
    {
        if (_cachedClientCert != null)
            return _cachedClientCert;

        if (string.IsNullOrEmpty(_clientCertPath))
            return null;

        lock (_lock)
        {
            if (_cachedClientCert != null)
                return _cachedClientCert;

            try
            {
                _cachedClientCert = string.IsNullOrEmpty(_clientCertPassword)
                    ? new X509Certificate2(_clientCertPath)
                    : new X509Certificate2(_clientCertPath, _clientCertPassword);

                _logger.LogInformation("Loaded client certificate from {Path}", _clientCertPath);
                return _cachedClientCert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load client certificate from {Path}", _clientCertPath);
                return null;
            }
        }
    }

    public X509Certificate2? GetServerCertificate()
    {
        if (_cachedServerCert != null)
            return _cachedServerCert;

        if (string.IsNullOrEmpty(_serverCertPath))
            return null;

        lock (_lock)
        {
            if (_cachedServerCert != null)
                return _cachedServerCert;

            try
            {
                _cachedServerCert = string.IsNullOrEmpty(_serverCertPassword)
                    ? new X509Certificate2(_serverCertPath)
                    : new X509Certificate2(_serverCertPath, _serverCertPassword);

                _logger.LogInformation("Loaded server certificate from {Path}", _serverCertPath);
                return _cachedServerCert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load server certificate from {Path}", _serverCertPath);
                return null;
            }
        }
    }

    private X509Certificate2? GetCACertificate()
    {
        if (_cachedCaCert != null)
            return _cachedCaCert;

        if (string.IsNullOrEmpty(_caCertPath))
            return null;

        lock (_lock)
        {
            if (_cachedCaCert != null)
                return _cachedCaCert;

            try
            {
                _cachedCaCert = new X509Certificate2(_caCertPath);
                _logger.LogInformation("Loaded CA certificate from {Path}", _caCertPath);
                return _cachedCaCert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load CA certificate from {Path}", _caCertPath);
                return null;
            }
        }
    }

    public bool ValidateClientCertificate(X509Certificate2? clientCert)
    {
        if (clientCert == null)
        {
            _logger.LogWarning("Client certificate is null");
            return false;
        }

        var caCert = GetCACertificate();
        if (caCert == null)
        {
            _logger.LogWarning("CA certificate not available for validation");
            return false;
        }

        try
        {
            // Check certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.ExtraStore.Add(caCert);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.VerificationTime = DateTime.Now;
            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 30);

            var isValid = chain.Build(clientCert);
            
            if (!isValid)
            {
                foreach (var status in chain.ChainStatus)
                {
                    _logger.LogWarning("Certificate validation failed: {Status} - {StatusInformation}", 
                        status.Status, status.StatusInformation);
                }
                return false;
            }

            // Check if certificate is issued by our CA
            var issuerMatches = clientCert.Issuer.Equals(caCert.Subject, StringComparison.OrdinalIgnoreCase);
            if (!issuerMatches)
            {
                _logger.LogWarning("Certificate issuer does not match CA. Expected: {CAIssuer}, Got: {CertIssuer}", 
                    caCert.Subject, clientCert.Issuer);
                return false;
            }

            // Check certificate expiration
            if (clientCert.NotAfter < DateTime.Now)
            {
                _logger.LogWarning("Certificate has expired. Expiration: {Expiration}", clientCert.NotAfter);
                return false;
            }

            if (clientCert.NotBefore > DateTime.Now)
            {
                _logger.LogWarning("Certificate is not yet valid. Valid from: {NotBefore}", clientCert.NotBefore);
                return false;
            }

            _logger.LogInformation("Client certificate validated successfully. Subject: {Subject}", clientCert.Subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client certificate");
            return false;
        }
    }
}
