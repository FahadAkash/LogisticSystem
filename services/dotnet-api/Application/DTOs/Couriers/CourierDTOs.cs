using System.ComponentModel.DataAnnotations;

namespace LogisticServer.Application.DTOs.Couriers;

/// <summary>
/// Request to change courier availability status.
/// </summary>
public sealed record UpdateCourierStatusRequest
{
    [Required]
    [RegularExpression("^(Available|Offline|Busy)$", ErrorMessage = "Status must be 'Available', 'Offline', or 'Busy'.")]
    public required string Status { get; init; }
}

/// <summary>
/// Detailed courier profile response.
/// </summary>
public sealed record CourierDetailResponse(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    string? Phone,
    string Status,
    decimal Rating,
    Guid? VehicleId,
    string? VehicleType,
    string? PlateNo,
    Guid? CurrentActiveOrderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt
);

/// <summary>
/// Query filter parameters for courier directory.
/// </summary>
public sealed record CourierFilterQuery
{
    public string? Status { get; init; }
    public string? VehicleType { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

