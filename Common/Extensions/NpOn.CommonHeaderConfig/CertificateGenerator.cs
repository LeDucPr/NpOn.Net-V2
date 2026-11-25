using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Common.Extensions.NpOn.HeaderConfig;

public interface ICertificateGenerator
{
    X509Certificate2 GenerateCACertificate(string subject, int validityDays = 3650);

    X509Certificate2 GenerateClientCertificate(
        string subject,
        X509Certificate2 caCert,
        int validityDays = 90,
        IEnumerable<string>? additionalDnsNames = null);

    X509Certificate2 GenerateServerCertificate(
        string subject,
        X509Certificate2 caCert,
        int validityDays = 90,
        IEnumerable<string>? additionalDnsNames = null);

    void SaveCertificate(X509Certificate2 cert, string path, string? password = null);
    X509Certificate2? LoadCertificate(string path, string? password = null);
}

public class CertificateGenerator : ICertificateGenerator
{
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    private readonly ILogger<CertificateGenerator> _logger;
    private readonly string _certificateStoragePath;
    private readonly string? _requestedStoragePath;
    private readonly bool _fellBackToTemp;

    public CertificateGenerator(ILogger<CertificateGenerator> logger, string? storagePath = null)
    {
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(storagePath))
        {
            _certificateStoragePath = Path.IsPathRooted(storagePath)
                ? storagePath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, storagePath);
            _requestedStoragePath = _certificateStoragePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var baseDir = !string.IsNullOrWhiteSpace(appData) ? appData : Path.GetTempPath();
            _certificateStoragePath = Path.Combine(baseDir, "NpOn", "Certificates");
        }

        try
        {
            if (!Directory.Exists(_certificateStoragePath))
            {
                Directory.CreateDirectory(_certificateStoragePath);
                _logger.LogInformation("Created certificate storage directory: {Path}", _certificateStoragePath);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _fellBackToTemp = true;
            _certificateStoragePath = Path.Combine(Path.GetTempPath(), "NpOn", "Certificates");
            if (!Directory.Exists(_certificateStoragePath))
            {
                Directory.CreateDirectory(_certificateStoragePath);
                _logger.LogInformation("Created certificate fallback storage directory: {Path}", _certificateStoragePath);
            }
        }
    }

    public X509Certificate2 GenerateCACertificate(string subject, int validityDays = 3650)
    {
        var distinguishedName = new X500DistinguishedName($"CN={subject}");
        using var rsa = RSA.Create(4096);

        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Root CA must NOT restrict EKU to clientAuth only so it can issue both serverAuth and clientAuth certs
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var now = DateTimeOffset.UtcNow;
        var cert = request.CreateSelfSigned(now.AddDays(-2), now.AddDays(validityDays));

        _logger.LogInformation("Generated CA certificate: {Subject}, Valid until: {Expiration}",
            subject, cert.NotAfter);

        return cert;
    }

    public X509Certificate2 GenerateClientCertificate(
        string subject,
        X509Certificate2 caCert,
        int validityDays = 90,
        IEnumerable<string>? additionalDnsNames = null)
    {
        var distinguishedName = new X500DistinguishedName($"CN={subject}");
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(ClientAuthOid) }, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var sanBuilder = BuildSanExtension(subject, includeLocalhost: false, additionalDnsNames);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var now = DateTimeOffset.UtcNow;
        var notBefore = now.AddDays(-1) < caCert.NotBefore ? caCert.NotBefore : now.AddDays(-1);
        var notAfter = now.AddDays(validityDays) > caCert.NotAfter ? caCert.NotAfter : now.AddDays(validityDays);
        using var certOnly = request.Create(caCert, notBefore, notAfter, CreateSerialNumber());
        var cert = certOnly.CopyWithPrivateKey(rsa);

        _logger.LogInformation("Generated client certificate: {Subject}, Valid until: {Expiration}",
            subject, cert.NotAfter);

        return cert;
    }

    public X509Certificate2 GenerateServerCertificate(
        string subject,
        X509Certificate2 caCert,
        int validityDays = 90,
        IEnumerable<string>? additionalDnsNames = null)
    {
        var distinguishedName = new X500DistinguishedName($"CN={subject}");
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(ServerAuthOid) }, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var sanBuilder = BuildSanExtension(subject, includeLocalhost: true, additionalDnsNames);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var now = DateTimeOffset.UtcNow;
        var notBefore = now.AddDays(-1) < caCert.NotBefore ? caCert.NotBefore : now.AddDays(-1);
        var notAfter = now.AddDays(validityDays) > caCert.NotAfter ? caCert.NotAfter : now.AddDays(validityDays);
        using var certOnly = request.Create(caCert, notBefore, notAfter, CreateSerialNumber());
        var cert = certOnly.CopyWithPrivateKey(rsa);

        _logger.LogInformation("Generated server certificate: {Subject}, Valid until: {Expiration}",
            subject, cert.NotAfter);

        return cert;
    }

    public void SaveCertificate(X509Certificate2 cert, string path, string? password = null)
    {
        var fullPath = ResolveCertificatePath(path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        byte[] data = string.IsNullOrEmpty(password)
            ? cert.Export(X509ContentType.Cert)
            : cert.Export(X509ContentType.Pfx, password);

        File.WriteAllBytes(fullPath, data);
        _logger.LogInformation("Saved certificate to: {Path}", fullPath);
    }

    public X509Certificate2? LoadCertificate(string path, string? password = null)
    {
        var fullPath = ResolveCertificatePath(path);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Certificate file not found: {Path}", fullPath);
            return null;
        }

        try
        {
            var cert = string.IsNullOrEmpty(password)
                ? X509CertificateLoader.LoadCertificateFromFile(fullPath)
                : X509CertificateLoader.LoadPkcs12FromFile(fullPath, password);

            _logger.LogInformation("Loaded certificate from: {Path}, Subject: {Subject}, Expires: {Expiration}",
                fullPath, cert.Subject, cert.NotAfter);

            return cert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate from: {Path}", fullPath);
            return null;
        }
    }

    private string ResolveCertificatePath(string path)
    {
        if (!Path.IsPathRooted(path))
            return Path.Combine(_certificateStoragePath, path);

        if (_fellBackToTemp && !string.IsNullOrEmpty(_requestedStoragePath) &&
            path.StartsWith(_requestedStoragePath, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(_certificateStoragePath, Path.GetFileName(path));
        }

        return path;
    }

    private static byte[] CreateSerialNumber()
    {
        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F; // Ensure positive ASN.1 INTEGER
        return serial;
    }

    private static SubjectAlternativeNameBuilder BuildSanExtension(
        string subject,
        bool includeLocalhost,
        IEnumerable<string>? additionalDnsNames)
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        var seenDns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIps = new HashSet<IPAddress>();
        var seenUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddSanEntry(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
                return;

            var trimmed = entry.Trim();
            if (IPAddress.TryParse(trimmed, out var ip))
            {
                if (seenIps.Add(ip))
                    sanBuilder.AddIpAddress(ip);
            }
            else if (trimmed.StartsWith("spiffe://", StringComparison.OrdinalIgnoreCase) &&
                     Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                if (seenUris.Add(uri.AbsoluteUri))
                    sanBuilder.AddUri(uri);
            }
            else if (seenDns.Add(trimmed))
            {
                sanBuilder.AddDnsName(trimmed);
            }
        }

        AddSanEntry(subject);
        if (includeLocalhost)
        {
            AddSanEntry("localhost");
            if (seenIps.Add(IPAddress.Loopback))
                sanBuilder.AddIpAddress(IPAddress.Loopback);
            if (seenIps.Add(IPAddress.IPv6Loopback))
                sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        }

        if (additionalDnsNames != null)
        {
            foreach (var name in additionalDnsNames)
                AddSanEntry(name);
        }

        return sanBuilder;
    }
}
