using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LogisticServer.Application.Interfaces;
using LogisticServer.Configuration;
using LogisticServer.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace LogisticServer.Infrastructure.Security;

public class TokenService : ITokenService
{
    private readonly JwtConfig _config;
    private readonly IJwtKeyService _keyService;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenService(JwtConfig config, IJwtKeyService keyService)
    {
        _config = config;
        _keyService = keyService;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public string GenerateAccessToken(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.FullName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        var signingCredentials = new SigningCredentials(
            _keyService.GetPrivateKey(),
            SecurityAlgorithms.RsaSha256
        );

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_config.AccessTokenLifetimeMinutes),
            Issuer = _config.Issuer,
            Audience = _config.Audience,
            SigningCredentials = signingCredentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    public (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var rawToken = Base64UrlEncoder.Encode(randomBytes);
        var tokenHash = HashToken(rawToken);
        var expiresAt = DateTime.UtcNow.AddDays(_config.RefreshTokenLifetimeDays);

        return (rawToken, tokenHash, expiresAt);
    }

    public string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
