using System.Text.Json;
using LogisticServer.Application.Common.Exceptions;
using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Application.Interfaces;
using LogisticServer.Domain.Entities;
using LogisticServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogisticServer.Infrastructure.Services;

public class CourierService : ICourierService
{
    private readonly CoreDbContext _dbContext;
    private readonly ILogger<CourierService> _logger;

    public CourierService(CoreDbContext dbContext, ILogger<CourierService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
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
}
