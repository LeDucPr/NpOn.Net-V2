using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Common.Extensions.NpOn.HeaderConfig;

public interface ICertificateGenerator
{
    X509Certificate2 GenerateCACertificate(string subject, int validityDays = 3650);
    X509Certificate2 GenerateClientCertificate(string subject, X509Certificate2 caCert, int validityDays = 90);
    X509Certificate2 GenerateServerCertificate(string subject, X509Certificate2 caCert, int validityDays = 90);
    void SaveCertificate(X509Certificate2 cert, string path, string? password = null);
    X509Certificate2? LoadCertificate(string path, string? password = null);
}

public class CertificateGenerator : ICertificateGenerator
{
    private readonly ILogger<CertificateGenerator> _logger;
    private readonly string _certificateStoragePath;

    public CertificateGenerator(ILogger<CertificateGenerator> logger, string? storagePath = null)
    {
        _logger = logger;
        _certificateStoragePath = !string.IsNullOrEmpty(storagePath) && !storagePath.StartsWith("/")
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, storagePath)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NpOn",
                "Certificates");

        if (!Directory.Exists(_certificateStoragePath))
        {
            Directory.CreateDirectory(_certificateStoragePath);
            _logger.LogInformation("Created certificate storage directory: {Path}", _certificateStoragePath);
        }
    }

    public X509Certificate2 GenerateCACertificate(string subject, int validityDays = 3650)
    {
        var distinguishedName = new X500DistinguishedName($"CN={subject}");
        using var rsa = RSA.Create(4096);

        var request =
            new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Add CA extensions
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, // id-kp-clientAuth
                true));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(validityDays));

        _logger.LogInformation("Generated CA certificate: {Subject}, Valid until: {Expiration}",
            subject, cert.NotAfter);

        return cert;
    }

    public X509Certificate2 GenerateClientCertificate(string subject, X509Certificate2 caCert, int validityDays = 90)
    {
        var distinguishedName = new X500DistinguishedName($"CN={subject}");
        using var rsa = RSA.Create(2048);

        var request =
            new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Add client auth extensions
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, // id-kp-clientAuth
                false));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var cert = request.Create(
            caCert,
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(validityDays),
            BitConverter.GetBytes((int)DateTime.Now.Ticks));

        _logger.LogInformation("Generated client certificate: {Subject}, Valid until: {Expiration}",
            subject, cert.NotAfter);

        return cert;
    }

    public X509Certificate2 GenerateServerCertificate(string subject, X509Certificate2 caCert, int validityDays = 90)
    {
        var distinguishedName = new X500DistinguishedName($"CN={subject}");
        using var rsa = RSA.Create(2048);

        var request =
            new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Add server auth extensions
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // id-kp-serverAuth
                false));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        // Add SAN for DNS support (for public deployment)
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(subject);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var cert = request.Create(
            caCert,
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(validityDays),
            BitConverter.GetBytes((int)DateTime.Now.Ticks));

        _logger.LogInformation("Generated server certificate: {Subject}, Valid until: {Expiration}",
            subject, cert.NotAfter);

        return cert;
    }

    public void SaveCertificate(X509Certificate2 cert, string path, string? password = null)
    {
        var fullPath = Path.IsPathRooted(path) || path.StartsWith("certs")
            ? Path.Combine(_certificateStoragePath, Path.GetFileName(path))
            : Path.Combine(_certificateStoragePath, path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (string.IsNullOrEmpty(password))
        {
            File.WriteAllBytes(fullPath, cert.Export(X509ContentType.Cert));
        }
        else
        {
            File.WriteAllBytes(fullPath, cert.Export(X509ContentType.Pfx, password));
        }

        _logger.LogInformation("Saved certificate to: {Path}", fullPath);
    }

    public X509Certificate2? LoadCertificate(string path, string? password = null)
    {
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_certificateStoragePath, path);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Certificate file not found: {Path}", fullPath);
            return null;
        }

        try
        {
            var cert = string.IsNullOrEmpty(password)
                ? new X509Certificate2(fullPath)
                : new X509Certificate2(fullPath, password);

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
}