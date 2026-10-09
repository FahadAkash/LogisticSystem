using System.Text.Json;
using LogisticServer.Application.Common.Exceptions;
using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Application.DTOs.Couriers;
using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Application.Interfaces;
using LogisticServer.Domain.Entities;
using LogisticServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LogisticServer.Infrastructure.Services;

public class CourierService : ICourierService
{
    private readonly CoreDbContext _dbContext;
    private readonly ICourierRepository _courierRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<CourierService> _logger;

    public CourierService(
        CoreDbContext dbContext,
        ICourierRepository courierRepository,
        IOrderRepository orderRepository,
        ILogger<CourierService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _dbContext = dbContext;
        _courierRepository = courierRepository;
        _orderRepository = orderRepository;
        _logger = logger;
        _redis = redis;
    }

    public async Task<CourierResponse> ApproveCourierAsync(
        Guid courierId, 
        string correlationId, 
        CancellationToken cancellationToken = default)
    {
        var courier = await _dbContext.Couriers
            .Include(c => c.User)
            .Include(c => c.Vehicle)
            .FirstOrDefaultAsync(c => c.Id == courierId || c.UserId == courierId, cancellationToken);

        if (courier == null)
        {
            throw new NotFoundException($"Courier with ID {courierId} was not found.");
        }

        if (!string.Equals(courier.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Courier cannot be approved because current status is '{courier.Status}'.");
        }

        // 1. Transition status from PendingApproval to Offline
        courier.Status = "Offline";
        courier.ApprovedAt = DateTime.UtcNow;
        courier.UpdatedAt = DateTime.UtcNow;
        courier.Version += 1;

        // 2. Build Kafka outbox message in the SAME transaction per Agent.md Sections 9 & 10.2
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var payloadObj = new
        {
            courierId = courier.Id,
            fullName = courier.User.FullName,
            email = courier.User.Email,
            phone = courier.User.Phone,
            vehicleId = courier.VehicleId,
            vehicleType = courier.Vehicle?.Type ?? "Car",
            plateNumber = courier.Vehicle?.PlateNo,
            status = "Offline"
        };

        var headersObj = new
        {
            correlationId = correlationId,
            timestamp = occurredAt.ToString("o")
        };

        var outboxMessage = new OutboxMessage
        {
            Id = eventId,
            AggregateType = "Courier",
            AggregateId = courier.Id,
            Topic = "courier.events",
            MessageKey = courier.Id.ToString(),
            EventType = "courier.registered",
            EventVersion = 1,
            Payload = JsonSerializer.Serialize(payloadObj),
            Headers = JsonSerializer.Serialize(headersObj),
            CreatedAt = occurredAt,
            PublishedAt = null
        };

        _dbContext.OutboxMessages.Add(outboxMessage);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Courier {CourierId} approved by admin. Status set to Offline. Outbox event {EventId} queued.", courier.Id, eventId);

        return new CourierResponse(
            Id: courier.Id,
            Status: courier.Status,
            Rating: courier.Rating,
            VehicleId: courier.VehicleId,
            VehicleType: courier.Vehicle?.Type,
            PlateNumber: courier.Vehicle?.PlateNo,
            CreatedAt: courier.CreatedAt
        );
    }

    public async Task<CourierDetailResponse> GetCourierByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var courier = await _courierRepository.GetWithDetailsAsync(id, cancellationToken);
        if (courier == null)
        {
            throw new NotFoundException($"Courier with ID {id} was not found.");
        }

        return MapDetailResponse(courier);
    }

    public async Task<CourierDetailResponse> GetCourierByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var courier = await _courierRepository.GetWithDetailsAsync(userId, cancellationToken);
        if (courier == null)
        {
            throw new NotFoundException($"Courier profile for user {userId} was not found.");
        }

        return MapDetailResponse(courier);
    }

    public async Task<PagedResult<CourierDetailResponse>> GetCouriersAsync(CourierFilterQuery query, CancellationToken cancellationToken = default)
    {
        var paged = await _courierRepository.GetPagedAsync(query, cancellationToken);
        var mapped = paged.Items.Select(MapDetailResponse).ToList();
        return new PagedResult<CourierDetailResponse>(mapped, paged.Page, paged.PageSize, paged.TotalCount, paged.TotalPages);
    }

    public async Task<IReadOnlyList<CourierDetailResponse>> GetPendingCouriersAsync(CancellationToken cancellationToken = default)
    {
        var items = await _courierRepository.GetPendingAsync(cancellationToken);
        return items.Select(MapDetailResponse).ToList();
    }

    public async Task<CourierDetailResponse> UpdateStatusAsync(
        Guid courierId,
        Guid requesterUserId,
        IEnumerable<string> roles,
        string newStatus,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var courier = await _dbContext.Couriers
            .Include(c => c.User)
            .Include(c => c.Vehicle)
            .FirstOrDefaultAsync(c => c.Id == courierId || c.UserId == courierId, cancellationToken);

        if (courier == null)
        {
            throw new NotFoundException($"Courier with ID {courierId} was not found.");
        }

        var roleList = roles.ToList();
        var isAdmin = roleList.Contains("Admin");

        // Verify courier owns this record unless Admin
        if (!isAdmin && courier.UserId != requesterUserId && courier.Id != requesterUserId)
        {
            throw new ForbiddenException("You cannot modify another courier's status.");
        }

        // Cannot update if still PendingApproval
        if (string.Equals(courier.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Courier account is awaiting admin approval and cannot update status.");
        }

        // Agent.md Rule 10.3: A courier with an active order cannot go offline
        if (string.Equals(newStatus, "Offline", StringComparison.OrdinalIgnoreCase))
        {
            var hasActiveOrder = await _orderRepository.HasActiveOrderForCourierAsync(courier.Id, cancellationToken);
            if (hasActiveOrder)
            {
                throw new ConflictException("A courier with an active assigned order cannot go offline.");
            }
        }

        var previousStatus = courier.Status;
        var now = DateTime.UtcNow;

        courier.Status = newStatus;
        courier.UpdatedAt = now;
        courier.Version += 1;

        // 1. Outbox message for courier.status.changed in SAME transaction
        var eventId = Guid.NewGuid();
        var payloadObj = new
        {
            courierId = courier.Id,
            fromStatus = previousStatus,
            toStatus = newStatus,
            vehicleType = courier.Vehicle?.Type ?? "Car",
            updatedAt = now
        };

        var headersObj = new
        {
            correlationId = correlationId,
            timestamp = now.ToString("o")
        };

        await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
        {
            Id = eventId,
            AggregateType = "Courier",
            AggregateId = courier.Id,
            Topic = "courier.events",
            MessageKey = courier.Id.ToString(),
            EventType = "courier.status.changed",
            EventVersion = 1,
            Payload = JsonSerializer.Serialize(payloadObj),
            Headers = JsonSerializer.Serialize(headersObj),
            CreatedAt = now,
            PublishedAt = null
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 2. Synchronize Redis state and GEO set per Agent.md Rule 9 & 10.3
        try
        {
            if (_redis != null && _redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                if (string.Equals(newStatus, "Offline", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove from GEO set and delete state hash
                    await db.SortedSetRemoveAsync("couriers:geo", courier.Id.ToString());
                    await db.KeyDeleteAsync($"courier:{courier.Id}:state");
                }
                else if (string.Equals(newStatus, "Available", StringComparison.OrdinalIgnoreCase))
                {
                    await db.HashSetAsync($"courier:{courier.Id}:state", new HashEntry[]
                    {
                        new("status", "Available"),
                        new("vehicle_type", courier.Vehicle?.Type ?? "Car"),
                        new("updated_at", now.ToString("o"))
                    });
                }
                else if (string.Equals(newStatus, "Busy", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove from GEO set when busy so dispatch engine doesn't offer more orders
                    await db.SortedSetRemoveAsync("couriers:geo", courier.Id.ToString());
                    await db.HashSetAsync($"courier:{courier.Id}:state", new HashEntry[]
                    {
                        new("status", "Busy"),
                        new("updated_at", now.ToString("o"))
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Redis cache for courier {CourierId} status change.", courier.Id);
        }

        _logger.LogInformation("Courier {CourierId} transitioned from '{FromStatus}' to '{ToStatus}'.", courier.Id, previousStatus, newStatus);

        return MapDetailResponse(courier);
    }

    private static CourierDetailResponse MapDetailResponse(Courier courier)
    {
        var activeOrder = courier.AssignedOrders
            ?.FirstOrDefault(o => o.Status == "Assigned" || o.Status == "PickedUp");

        return new CourierDetailResponse(
            Id: courier.Id,
            UserId: courier.UserId,
            FullName: courier.User?.FullName ?? string.Empty,
            Email: courier.User?.Email ?? string.Empty,
            Phone: courier.User?.Phone,
            Status: courier.Status,
            Rating: courier.Rating,
            VehicleId: courier.VehicleId,
            VehicleType: courier.Vehicle?.Type,
            PlateNo: courier.Vehicle?.PlateNo,
            CurrentActiveOrderId: activeOrder?.Id,
            CreatedAt: new DateTimeOffset(courier.CreatedAt, TimeSpan.Zero),
            ApprovedAt: courier.ApprovedAt.HasValue ? new DateTimeOffset(courier.ApprovedAt.Value, TimeSpan.Zero) : null
        );
    }
}
