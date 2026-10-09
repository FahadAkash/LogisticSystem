using System.Text.RegularExpressions;
using LogisticServer.Application.Common.Exceptions;
using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Application.Interfaces;
using LogisticServer.Domain.Entities;
using LogisticServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogisticServer.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly CoreDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        CoreDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        
        // 1. Check email uniqueness
        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail && u.DeletedAt == null, cancellationToken);
        if (emailExists)
        {
            throw new ConflictException("Email address is already registered.");
        }

        // 2. Check phone uniqueness if provided
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phoneExists = await _dbContext.Users
                .AnyAsync(u => u.Phone == request.Phone.Trim() && u.DeletedAt == null, cancellationToken);
            if (phoneExists)
            {
                throw new ConflictException("Phone number is already registered.");
            }
        }

        // 3. Validate role
        var requestedRole = request.Role.Trim();
        if (!string.Equals(requestedRole, "Customer", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requestedRole, "Courier", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Only 'Customer' and 'Courier' roles are open for self-registration.");
        }

        var canonicalRoleName = string.Equals(requestedRole, "Courier", StringComparison.OrdinalIgnoreCase) ? "Courier" : "Customer";
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == canonicalRoleName, cancellationToken);

        if (role == null)
        {
            throw new InvalidOperationException($"Role '{canonicalRoleName}' not found in database.");
        }

        // 4. Create User entity
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);

        // 5. Associate UserRole
        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        };
        _dbContext.UserRoles.Add(userRole);

        // 6. Role-specific domain entities
        if (canonicalRoleName == "Customer")
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Customers.Add(customer);
        }
        else if (canonicalRoleName == "Courier")
        {
            // Per Agent.md 10.2: Courier registration creates user, courier role, vehicle, courier profile (PendingApproval)
            var vehicleType = NormalizeVehicleType(request.VehicleType);
            var plateNo = string.IsNullOrWhiteSpace(request.PlateNumber)
                ? $"TEMP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
                : request.PlateNumber.Trim().ToUpperInvariant();

            // Ensure plate is unique
            var plateExists = await _dbContext.Vehicles.AnyAsync(v => v.PlateNo == plateNo, cancellationToken);
            if (plateExists)
            {
                throw new ConflictException($"Vehicle plate number '{plateNo}' is already registered.");
            }

            var vehicle = new Vehicle
            {
                Id = Guid.NewGuid(),
                PlateNo = plateNo,
                Type = vehicleType,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Vehicles.Add(vehicle);

            var courier = new Courier
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                VehicleId = vehicle.Id,
                LicenseNo = $"LIC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Status = "PendingApproval",
                Rating = 5.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Couriers.Add(courier);
        }

        // 7. Generate tokens
        var rolesList = new List<string> { canonicalRoleName };
        var accessToken = _tokenService.GenerateAccessToken(user, rolesList);
        var (rawRefreshToken, tokenHash, expiresAt) = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.Add(refreshTokenEntity);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully registered user {UserId} with role {Role}", user.Id, canonicalRoleName);

        var userResponse = new UserResponse(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Phone: user.Phone,
            Status: user.Status,
            Roles: rolesList,
            CreatedAt: user.CreatedAt
        );

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: 900, // 15 mins default
            User: userResponse
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && u.DeletedAt == null, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email {Email}", normalizedEmail);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Login rejected for non-active user {UserId} (Status: {Status})", user.Id, user.Status);
            throw new UnauthorizedException($"Account is {user.Status.ToLower()}. Please contact support.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var (rawRefreshToken, tokenHash, expiresAt) = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.Add(refreshTokenEntity);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} logged in successfully.", user.Id);

        var userResponse = new UserResponse(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Phone: user.Phone,
            Status: user.Status,
            Roles: roles,
            CreatedAt: user.CreatedAt
        );

        return new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: 900,
            User: userResponse
        );
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var tokenEntity = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        // Security check: If already revoked, token reuse detected - revoke all tokens for this user!
        if (tokenEntity.RevokedAt != null)
        {
            _logger.LogWarning("Revoked refresh token reuse detected for user {UserId}! Revoking all active tokens.", tokenEntity.UserId);
            var activeTokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == tokenEntity.UserId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var t in activeTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("Refresh token was previously revoked. Suspicious activity detected.");
        }

        if (tokenEntity.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Refresh token has expired.");
        }

        var user = tokenEntity.User;
        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase) || user.DeletedAt != null)
        {
            throw new UnauthorizedException("User account is inactive.");
        }

        // Rotate token
        var (newRawToken, newTokenHash, newExpiresAt) = _tokenService.GenerateRefreshToken();
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAt = newExpiresAt,
            CreatedAt = DateTime.UtcNow
        };

        tokenEntity.RevokedAt = DateTime.UtcNow;
        tokenEntity.ReplacedBy = newRefreshToken.Id;

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var newAccessToken = _tokenService.GenerateAccessToken(user, roles);

        var userResponse = new UserResponse(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Phone: user.Phone,
            Status: user.Status,
            Roles: roles,
            CreatedAt: user.CreatedAt
        );

        return new AuthResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRawToken,
            TokenType: "Bearer",
            ExpiresIn: 900,
            User: userResponse
        );
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);
        var tokenEntity = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity != null && tokenEntity.RevokedAt == null)
        {
            tokenEntity.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Refresh token {TokenId} revoked.", tokenEntity.Id);
        }
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        return new UserResponse(
            Id: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Phone: user.Phone,
            Status: user.Status,
            Roles: roles,
            CreatedAt: user.CreatedAt
        );
    }

    private static string NormalizeVehicleType(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Car";
        var clean = input.Trim().ToLowerInvariant();
        return clean switch
        {
            "bike" or "bicycle" => "Bike",
            "car" => "Car",
            "van" => "Van",
            "truck" => "Truck",
            _ => "Car"
        };
    }
}

