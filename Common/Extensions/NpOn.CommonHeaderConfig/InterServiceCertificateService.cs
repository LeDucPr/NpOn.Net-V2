using System.Collections.Concurrent;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Common.Extensions.NpOn.HeaderConfig;

public interface IInterServiceCertificateService
{
    X509Certificate2? GetClientCertificate();
    X509Certificate2? GetServerCertificate();
    X509Certificate2? GetCACertificate();
    bool ValidateClientCertificate(X509Certificate2? clientCert);
    bool ValidateServerCertificate(X509Certificate2? serverCert);
    void ReloadCertificates();
    bool TryExtractServiceIdentity(X509Certificate2 cert, out string? identity);
}

public class InterServiceCertificateService : IInterServiceCertificateService
{
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private const int MaxValidationCacheSize = 4096;

    private sealed record CachedCertValidation(
        bool IsValid,
        DateTime NotBeforeUtc,
        DateTime NotAfterUtc,
        string? Identity);

    private readonly string? _clientCertPath;
    private readonly string? _clientCertPassword;
    private readonly string? _serverCertPath;
    private readonly string? _serverCertPassword;
    private readonly string? _caCertPath;
    private readonly ILogger<InterServiceCertificateService> _logger;
    private readonly HashSet<string>? _allowedIdentities;
    private readonly ConcurrentDictionary<string, CachedCertValidation> _validationCache = new(StringComparer.Ordinal);

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
        ILogger<InterServiceCertificateService>? logger = null,
        IReadOnlyCollection<string>? allowedIdentities = null)
    {
        _clientCertPath = clientCertPath;
        _clientCertPassword = clientCertPassword;
        _serverCertPath = serverCertPath;
        _serverCertPassword = serverCertPassword;
        _caCertPath = caCertPath;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InterServiceCertificateService>.Instance;
        _allowedIdentities = allowedIdentities is { Count: > 0 }
            ? new HashSet<string>(allowedIdentities.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase)
            : null;
    }

    public X509Certificate2? GetClientCertificate()
    {
        return LoadCachedCertificate(ref _cachedClientCert, _clientCertPath, _clientCertPassword, "client");
    }

    public X509Certificate2? GetServerCertificate()
    {
        return LoadCachedCertificate(ref _cachedServerCert, _serverCertPath, _serverCertPassword, "server");
    }

    public X509Certificate2? GetCACertificate()
    {
        return LoadCachedCertificate(ref _cachedCaCert, _caCertPath, null, "CA");
    }

    public void ReloadCertificates()
    {
        lock (_lock)
        {
            Volatile.Write(ref _cachedClientCert, null);
            Volatile.Write(ref _cachedServerCert, null);
            Volatile.Write(ref _cachedCaCert, null);
            _validationCache.Clear();
        }
    }

    public bool ValidateClientCertificate(X509Certificate2? clientCert)
    {
        return ValidatePeerCertificate(clientCert, isClientCert: true);
    }

    public bool ValidateServerCertificate(X509Certificate2? serverCert)
    {
        return ValidatePeerCertificate(serverCert, isClientCert: false);
    }

    public bool TryExtractServiceIdentity(X509Certificate2 cert, out string? identity)
    {
        identity = null;
        if (cert == null)
            return false;

        var cn = cert.GetNameInfo(X509NameType.SimpleName, false);
        if (!string.IsNullOrWhiteSpace(cn))
        {
            identity = cn.Trim();
            return true;
        }

        foreach (var candidate in EnumerateSanIdentities(cert))
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                identity = candidate;
                return true;
            }
        }

        return false;
    }

    private X509Certificate2? LoadCachedCertificate(
        ref X509Certificate2? cacheField,
        string? path,
        string? password,
        string certRole)
    {
        var existing = Volatile.Read(ref cacheField);
        if (existing != null)
            return existing;

        if (string.IsNullOrWhiteSpace(path))
            return null;

        lock (_lock)
        {
            existing = Volatile.Read(ref cacheField);
            if (existing != null)
                return existing;

            try
            {
                var loaded = string.IsNullOrEmpty(password)
                    ? X509CertificateLoader.LoadCertificateFromFile(path)
                    : X509CertificateLoader.LoadPkcs12FromFile(path, password);

                Volatile.Write(ref cacheField, loaded);
                _logger.LogInformation("Loaded {Role} certificate from {Path}", certRole, path);
                return loaded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load {Role} certificate from {Path}", certRole, path);
                return null;
            }
        }
    }

    private bool ValidatePeerCertificate(X509Certificate2? peerCert, bool isClientCert)
    {
        if (peerCert == null)
        {
            _logger.LogWarning("Peer certificate is null (isClientCert={IsClient})", isClientCert);
            return false;
        }

        var caCert = GetCACertificate();
        if (caCert == null)
        {
            _logger.LogWarning("CA certificate not available for validation");
            return false;
        }

        DateTime nowUtc = DateTime.UtcNow;
        DateTime notBeforeUtc = peerCert.NotBefore.ToUniversalTime();
        DateTime notAfterUtc = peerCert.NotAfter.ToUniversalTime();

        if (nowUtc < notBeforeUtc || nowUtc > notAfterUtc)
        {
            _logger.LogWarning("Certificate {Subject} outside validity window [{NotBefore:O}, {NotAfter:O}]",
                peerCert.Subject, notBeforeUtc, notAfterUtc);
            return false;
        }

        string cacheKey = $"{caCert.Thumbprint}:{peerCert.Thumbprint}:{(isClientCert ? 'C' : 'S')}";
        if (_validationCache.TryGetValue(cacheKey, out var cached))
        {
            if (nowUtc >= cached.NotBeforeUtc && nowUtc <= cached.NotAfterUtc)
                return cached.IsValid;

            _validationCache.TryRemove(cacheKey, out _);
        }

        bool isValid = VerifyCertificateCore(peerCert, caCert, isClientCert, nowUtc, out string? primaryIdentity);

        if (_validationCache.Count >= MaxValidationCacheSize)
            _validationCache.Clear();

        _validationCache[cacheKey] = new CachedCertValidation(isValid, notBeforeUtc, notAfterUtc, primaryIdentity);
        return isValid;
    }

    private bool VerifyCertificateCore(
        X509Certificate2 peerCert,
        X509Certificate2 caCert,
        bool isClientCert,
        DateTime nowUtc,
        out string? primaryIdentity)
    {
        TryExtractServiceIdentity(peerCert, out primaryIdentity);

        try
        {
            if (!HasRequiredEku(peerCert, isClientCert ? ClientAuthOid : ServerAuthOid))
            {
                _logger.LogWarning("Certificate {Subject} lacks required EKU for {Role}",
                    peerCert.Subject, isClientCert ? "clientAuth" : "serverAuth");
                return false;
            }

            if (isClientCert && _allowedIdentities != null && !MatchesAllowedIdentity(peerCert))
            {
                _logger.LogWarning("Certificate {Subject} identity is not in allowed identities", peerCert.Subject);
                return false;
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(caCert);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.VerificationTime = nowUtc;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);

            bool chainBuilt = chain.Build(peerCert);
            if (!chainBuilt)
            {
                foreach (var status in chain.ChainStatus)
                {
                    _logger.LogWarning("Certificate chain validation failed: {Status} - {StatusInformation}",
                        status.Status, status.StatusInformation);
                }
                return false;
            }

            if (chain.ChainElements.Count == 0 ||
                !string.Equals(chain.ChainElements[^1].Certificate.Thumbprint, caCert.Thumbprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Certificate {Subject} does not chain to configured CA {CaThumbprint}",
                    peerCert.Subject, caCert.Thumbprint);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating certificate {Subject}", peerCert.Subject);
            return false;
        }
    }

    private static bool HasRequiredEku(X509Certificate2 cert, string requiredOid)
    {
        X509EnhancedKeyUsageExtension? eku = null;
        foreach (var ext in cert.Extensions)
        {
            if (ext is X509EnhancedKeyUsageExtension ekuExt)
            {
                eku = ekuExt;
                break;
            }
        }

        if (eku == null || eku.EnhancedKeyUsages.Count == 0)
            return true;

        foreach (Oid oid in eku.EnhancedKeyUsages)
        {
            if (string.Equals(oid.Value, requiredOid, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool MatchesAllowedIdentity(X509Certificate2 cert)
    {
        if (_allowedIdentities == null)
            return true;

        var cn = cert.GetNameInfo(X509NameType.SimpleName, false);
        if (!string.IsNullOrWhiteSpace(cn) && _allowedIdentities.Contains(cn.Trim()))
            return true;

        foreach (var sanIdentity in EnumerateSanIdentities(cert))
        {
            if (_allowedIdentities.Contains(sanIdentity))
                return true;

            if (sanIdentity.StartsWith("spiffe://", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(sanIdentity, UriKind.Absolute, out var spiffeUri))
            {
                var lastSegment = spiffeUri.Segments.LastOrDefault()?.Trim('/');
                if (!string.IsNullOrWhiteSpace(lastSegment) && _allowedIdentities.Contains(lastSegment))
                    return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateSanIdentities(X509Certificate2 cert)
    {
        var results = new List<string>();
        foreach (var ext in cert.Extensions)
        {
            if (!string.Equals(ext.Oid?.Value, SubjectAlternativeNameOid, StringComparison.Ordinal))
                continue;

            if (ext is X509SubjectAlternativeNameExtension sanExt)
            {
                foreach (var dns in sanExt.EnumerateDnsNames())
                {
                    if (!string.IsNullOrWhiteSpace(dns))
                        results.Add(dns.Trim());
                }
            }

            try
            {
                var reader = new AsnReader(ext.RawData, AsnEncodingRules.DER);
                var seq = reader.ReadSequence();
                while (seq.HasData)
                {
                    var tag = seq.PeekTag();
                    if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 6)
                    {
                        var uri = seq.ReadCharacterString(UniversalTagNumber.IA5String, tag);
                        if (!string.IsNullOrWhiteSpace(uri))
                            results.Add(uri.Trim());
                    }
                    else
                    {
                        seq.ReadEncodedValue();
                    }
                }
            }
            catch
            {
                // Ignore malformed optional URI SAN parsing if DNS names already extracted
            }
        }

        return results;
    }
}
