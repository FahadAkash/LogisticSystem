using System.ComponentModel.DataAnnotations;

namespace LogisticServer.Application.DTOs.Auth;

/// <summary>
/// Request payload for registering a new user as Customer or Courier.
/// </summary>
public sealed record RegisterRequest
{
    /// <summary>User's email address.</summary>
    [Required, EmailAddress, MaxLength(255)]
    public required string Email { get; init; }

    /// <summary>User password with minimum 8 characters.</summary>
    [Required, MinLength(8), MaxLength(128)]
    public required string Password { get; init; }

    /// <summary>Full name of the user.</summary>
    [Required, MinLength(2), MaxLength(100)]
    public required string FullName { get; init; }

    /// <summary>Optional contact phone number.</summary>
    [Phone, MaxLength(30)]
    public string? Phone { get; init; }

    /// <summary>Target registration role: Customer or Courier.</summary>
    [Required]
    public required string Role { get; init; }

    /// <summary>Type of vehicle if registering as a Courier (e.g., Car, Bicycle, Motorcycle, Van).</summary>
    [MaxLength(50)]
    public string? VehicleType { get; init; }

    /// <summary>License plate number if vehicle is motorized.</summary>
    [MaxLength(30)]
    public string? PlateNumber { get; init; }

    /// <summary>Make and model description of the vehicle.</summary>
    [MaxLength(100)]
    public string? MakeModel { get; init; }

    /// <summary>Color of the vehicle.</summary>
    [MaxLength(30)]
    public string? Color { get; init; }

    /// <summary>Manufacturing year of the vehicle.</summary>
    [Range(1950, 2100)]
    public int? Year { get; init; }
}

/// <summary>
/// Request payload for authenticating with credentials.
/// </summary>
public sealed record LoginRequest
{
    /// <summary>User's email address.</summary>
    [Required, EmailAddress]
    public required string Email { get; init; }

    /// <summary>User's password.</summary>
    [Required]
    public required string Password { get; init; }
}

/// <summary>
/// Request payload for rotating an access token using a refresh token.
/// </summary>
public sealed record RefreshTokenRequest
{
    /// <summary>The active refresh token.</summary>
    [Required]
    public required string RefreshToken { get; init; }
}

/// <summary>
/// Authentication response containing JWT access token, refresh token, and user profile.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    UserResponse User
);

/// <summary>
/// User profile representation.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string? Phone,
    string Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Courier profile representation.
/// </summary>
public sealed record CourierResponse(
    Guid Id,
    string Status,
    decimal Rating,
    Guid? VehicleId,
    string? VehicleType,
    string? PlateNumber,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Short-lived ticket response for authenticating WebSocket connections.
/// </summary>
public sealed record WebSocketTicketResponse(
    string Ticket,
    int ExpiresIn,
    DateTimeOffset ExpiresAt
);

/// <summary>
/// Represents a JSON Web Key in a JWKS set per RFC 7517.
/// </summary>
public sealed record JwkKeyDto(
    string Kty,
    string Use,
    string Alg,
    string Kid,
    string N,
    string E
);

/// <summary>
/// JSON Web Key Set representation per RFC 7517.
/// </summary>
public sealed record JwksResponse(
    IReadOnlyList<JwkKeyDto> Keys
);
