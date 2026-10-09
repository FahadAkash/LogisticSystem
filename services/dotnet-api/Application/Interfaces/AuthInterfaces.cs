using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace LogisticServer.Application.Interfaces;

public interface IJwtKeyService
{
    RsaSecurityKey GetPrivateKey();
    RsaSecurityKey GetPublicKey();
    JwksResponse GetJwks();
    string KeyId { get; }
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

public interface ITokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> roles);
    (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken();
    string HashToken(string token);
}

public interface IWebSocketTicketService
{
    Task<WebSocketTicketResponse> CreateTicketAsync(Guid userId, IEnumerable<string> roles, CancellationToken cancellationToken = default);
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface ICourierService
{
    Task<CourierResponse> ApproveCourierAsync(Guid courierId, string correlationId, CancellationToken cancellationToken = default);
}

