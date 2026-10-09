using System.ComponentModel.DataAnnotations;

namespace LogisticServer.Application.DTOs.Orders;

/// <summary>
/// Stop details for order creation (pickup or dropoff).
/// </summary>
public sealed record CreateOrderStopRequest
{
    [Required]
    [Range(1, 100)]
    public required short Sequence { get; init; }

    [Required]
    [RegularExpression("^(Pickup|Dropoff)$", ErrorMessage = "Type must be 'Pickup' or 'Dropoff'.")]
    public required string Type { get; init; }

    [Required, MinLength(3), MaxLength(255)]
    public required string Address { get; init; }

    [Required]
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public required double Latitude { get; init; }

    [Required]
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public required double Longitude { get; init; }

    [MaxLength(100)]
    public string? ContactName { get; init; }

    [Phone, MaxLength(30)]
    public string? ContactPhone { get; init; }
}

/// <summary>
/// Payload for creating a new delivery order.
/// </summary>
public sealed record CreateOrderRequest
{
    [Range(0, 10)]
    public short Priority { get; init; } = 0;

    [RegularExpression("^(Bike|Car|Van|Truck)$", ErrorMessage = "VehicleType must be Bike, Car, Van, or Truck.")]
    public string? VehicleType { get; init; }

    [MaxLength(500)]
    public string? PackageDescription { get; init; }

    [Range(0.01, 10000.0, ErrorMessage = "Weight must be between 0.01 and 10,000 kg.")]
    public decimal? PackageWeight { get; init; }

    public DateTimeOffset? RequestedPickupAt { get; init; }

    [Required, MinLength(2, ErrorMessage = "Order must contain at least 2 stops (one pickup and one dropoff).")]
    public required List<CreateOrderStopRequest> Stops { get; init; }
}

/// <summary>
/// Stop representation in order response.
/// </summary>
public sealed record OrderStopResponse(
    Guid Id,
    short Sequence,
    string Type,
    string Address,
    double Latitude,
    double Longitude,
    string? ContactName,
    string? ContactPhone,
    DateTimeOffset? ArrivedAt,
    DateTimeOffset? CompletedAt
);

/// <summary>
/// Status transition history record for an order.
/// </summary>
public sealed record OrderStatusHistoryResponse(
    long Id,
    string? FromStatus,
    string ToStatus,
    string? Reason,
    string ActorType,
    Guid? ActorId,
    DateTimeOffset OccurredAt
);

/// <summary>
/// Courier assignment read model for an order.
/// </summary>
public sealed record AssignmentResponse(
    Guid Id,
    Guid CourierId,
    string? CourierName,
    string Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset? CompletedAt
);

/// <summary>
/// Full delivery order response.
/// </summary>
public sealed record OrderResponse(
    Guid Id,
    string OrderNo,
    Guid CustomerId,
    string? CustomerName,
    string Status,
    short Priority,
    string? VehicleType,
    string? PackageDescription,
    decimal? PackageWeight,
    DateTimeOffset? RequestedPickupAt,
    Guid? AssignedCourierId,
    string? AssignedCourierName,
    string? CancelledReason,
    Guid? CancelledBy,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<OrderStopResponse> Stops,
    IReadOnlyList<OrderStatusHistoryResponse> StatusHistories,
    IReadOnlyList<AssignmentResponse> Assignments
);

/// <summary>
/// Request payload for cancelling an order.
/// </summary>
public sealed record CancelOrderRequest
{
    [MaxLength(255)]
    public string? Reason { get; init; }
}

/// <summary>
/// Generic paginated result wrapper.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

/// <summary>
/// Query filter parameters for order listing.
/// </summary>
public sealed record OrderFilterQuery
{
    public string? Status { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? CourierId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
