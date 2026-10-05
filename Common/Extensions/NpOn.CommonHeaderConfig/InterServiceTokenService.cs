using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Common.Extensions.NpOn.HeaderConfig;

public interface IInterServiceTokenService
{
    string GenerateToken(string serviceName, string? additionalData = null);
    bool ValidateToken(string token, out ClaimsPrincipal? principal);
}

public class InterServiceTokenService : IInterServiceTokenService
{
    private readonly string _baseSecretKey;
    private readonly TimeSpan _tokenLifetime;
    private readonly TimeSpan _keyRotationPeriod;
    private readonly object _lock = new();
    private DateTime _currentKeyRotationTime;
    private string _currentSigningKey;

    public InterServiceTokenService(
        string baseSecretKey,
        TimeSpan? tokenLifetime = null,
        TimeSpan? keyRotationPeriod = null)
    {
        _baseSecretKey = baseSecretKey;
        _tokenLifetime = tokenLifetime ?? TimeSpan.FromHours(1);
        _keyRotationPeriod = keyRotationPeriod ?? TimeSpan.FromHours(6);
        _currentKeyRotationTime = DateTime.UtcNow;
        _currentSigningKey = GenerateTimeBasedKey(_currentKeyRotationTime);
    }

    private string GenerateTimeBasedKey(DateTime timestamp)
    {
        // Generate key based on time bucket (e.g., every 6 hours)
        var timeBucket = new DateTimeOffset(timestamp).ToUnixTimeSeconds() / (long)_keyRotationPeriod.TotalSeconds;
        var combined = $"{_baseSecretKey}_{timeBucket}";

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private void EnsureKeyRotation()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (now - _currentKeyRotationTime >= _keyRotationPeriod)
            {
                _currentKeyRotationTime = now;
                _currentSigningKey = GenerateTimeBasedKey(now);
            }
        }
    }

    public string GenerateToken(string serviceName, string? additionalData = null)
    {
        EnsureKeyRotation();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_currentSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim("service_name", serviceName),
            new Claim("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("key_rotation_time",
                _currentKeyRotationTime.ToUniversalTime().ToString(CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrEmpty(additionalData))
        {
            claims.Add(new Claim("additional_data", additionalData));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(_tokenLifetime),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool ValidateToken(string token, out ClaimsPrincipal? principal)
    {
        principal = null;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_currentSigningKey);

            // Also try previous key bucket to handle rotation overlap
            var previousKeyTime = _currentKeyRotationTime.Add(-_keyRotationPeriod);
            var previousKey = GenerateTimeBasedKey(previousKeyTime);
            var previousKeyBytes = Encoding.UTF8.GetBytes(previousKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = new[]
                {
                    new SymmetricSecurityKey(key),
                    new SymmetricSecurityKey(previousKeyBytes)
                },
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromMinutes(5) // Allow some clock drift
            };

            principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }
}