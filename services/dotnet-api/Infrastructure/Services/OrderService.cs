using System.Text.Json;
using LogisticServer.Application.Common.Exceptions;
using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Application.Interfaces;
using LogisticServer.Domain.Entities;
using LogisticServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace LogisticServer.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly CoreDbContext _dbContext;
    private readonly IOrderRepository _orderRepository;
    private readonly ICourierRepository _courierRepository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        CoreDbContext dbContext,
        IOrderRepository orderRepository,
        ICourierRepository courierRepository,
        ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _orderRepository = orderRepository;
        _courierRepository = courierRepository;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(
        Guid customerUserId,
        CreateOrderRequest request,
        string? idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        // 1. Ensure Customer profile exists for user
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.UserId == customerUserId || c.Id == customerUserId, cancellationToken);

        if (customer == null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                UserId = customerUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _dbContext.Customers.AddAsync(customer, cancellationToken);
        }

        // 2. Validate stops count and sequence
        if (request.Stops.Count < 2)
        {
            throw new ValidationException("Order must contain at least one pickup stop and one dropoff stop.");
        }

        var pickupStop = request.Stops.FirstOrDefault(s => s.Type == "Pickup") 
            ?? request.Stops.OrderBy(s => s.Sequence).First();
        var dropoffStop = request.Stops.FirstOrDefault(s => s.Type == "Dropoff")
            ?? request.Stops.OrderBy(s => s.Sequence).Last();

        var orderId = Guid.NewGuid();
        var orderNo = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = orderId,
            OrderNo = orderNo,
            CustomerId = customer.Id,
            Status = "Created",
            Priority = request.Priority,
            VehicleType = request.VehicleType,
            PackageDescription = request.PackageDescription,
            PackageWeight = request.PackageWeight,
            RequestedPickupAt = request.RequestedPickupAt?.UtcDateTime,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        // 3. Add stops with PostGIS geometry points (SRID 4326)
        foreach (var stopReq in request.Stops.OrderBy(s => s.Sequence))
        {
            order.Stops.Add(new OrderStop
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Sequence = stopReq.Sequence,
                Type = stopReq.Type,
                Address = stopReq.Address,
                Location = new Point(stopReq.Longitude, stopReq.Latitude) { SRID = 4326 },
                ContactName = stopReq.ContactName,
                ContactPhone = stopReq.ContactPhone
            });
        }

        // 4. Record initial status history
        order.StatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId,
            FromStatus = null,
            ToStatus = "Created",
            Reason = "Order created by customer",
            ActorType = "Customer",
            ActorId = customerUserId,
            OccurredAt = now
        });

        // 5. Add to database context
        await _dbContext.Orders.AddAsync(order, cancellationToken);

        // 6. Enqueue Outbox Message in the SAME transaction per Agent.md Sections 9 & 10.1
        var eventId = Guid.NewGuid();
        var outboxPayload = new
        {
            orderId = order.Id,
            orderNo = order.OrderNo,
            customerId = customer.Id,
            priority = order.Priority,
            vehicleType = order.VehicleType,
            pickupLatitude = pickupStop.Latitude,
            pickupLongitude = pickupStop.Longitude,
            pickupAddress = pickupStop.Address,
            dropoffLatitude = dropoffStop.Latitude,
            dropoffLongitude = dropoffStop.Longitude,
            dropoffAddress = dropoffStop.Address,
            packageDescription = order.PackageDescription,
            packageWeight = order.PackageWeight,
            requestedPickupAt = order.RequestedPickupAt,
            createdAt = now
        };

        var headers = new
        {
            correlationId = correlationId,
            timestamp = now.ToString("o")
        };

        var outboxMessage = new OutboxMessage
        {
            Id = eventId,
            AggregateType = "Order",
            AggregateId = order.Id,
            Topic = "order.events",
            MessageKey = order.Id.ToString(),
            EventType = "order.created",
            EventVersion = 1,
            Payload = JsonSerializer.Serialize(outboxPayload),
            Headers = JsonSerializer.Serialize(headers),
            CreatedAt = now,
            PublishedAt = null
        };

        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);

        // 7. Commit transaction
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderNo} ({OrderId}) created with Outbox event {EventId}.", orderNo, orderId, eventId);

        return MapOrderResponse(order);
    }

    public async Task<OrderResponse> GetOrderByIdAsync(
        Guid id,
        Guid currentUserId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException($"Order with ID {id} was not found.");
        }

        // Access check
        var roleList = roles.ToList();
        if (!roleList.Contains("Admin") && !roleList.Contains("Dispatcher"))
        {
            var isOwnCustomer = order.Customer != null && (order.Customer.UserId == currentUserId || order.CustomerId == currentUserId);
            var isAssignedCourier = order.AssignedCourier != null && (order.AssignedCourier.UserId == currentUserId || order.AssignedCourierId == currentUserId);

            if (!isOwnCustomer && !isAssignedCourier)
            {
                throw new ForbiddenException("You do not have permission to view this order.");
            }
        }

        return MapOrderResponse(order);
    }

    public async Task<PagedResult<OrderResponse>> GetOrdersAsync(
        Guid currentUserId,
        IEnumerable<string> roles,
        OrderFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var roleList = roles.ToList();
        var scopedQuery = query;

        if (!roleList.Contains("Admin") && !roleList.Contains("Dispatcher"))
        {
            if (roleList.Contains("Courier"))
            {
                var courier = await _courierRepository.GetByUserIdAsync(currentUserId, cancellationToken);
                scopedQuery = scopedQuery with { CourierId = courier?.Id ?? currentUserId };
            }
            else
            {
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.UserId == currentUserId, cancellationToken);
                scopedQuery = scopedQuery with { CustomerId = customer?.Id ?? currentUserId };
            }
        }

        var paged = await _orderRepository.GetPagedAsync(scopedQuery, cancellationToken);
        var mappedItems = paged.Items.Select(MapOrderResponse).ToList();

        return new PagedResult<OrderResponse>(mappedItems, paged.Page, paged.PageSize, paged.TotalCount, paged.TotalPages);
    }

    public async Task<OrderResponse> CancelOrderAsync(
        Guid id,
        Guid currentUserId,
        IEnumerable<string> roles,
        CancelOrderRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.Stops)
            .Include(o => o.StatusHistories)
            .Include(o => o.Assignments)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException($"Order with ID {id} was not found.");
        }

        var roleList = roles.ToList();
        var isAdminOrDispatcher = roleList.Contains("Admin") || roleList.Contains("Dispatcher");

        // Access check
        if (!isAdminOrDispatcher)
        {
            var isOwnCustomer = order.Customer != null && (order.Customer.UserId == currentUserId || order.CustomerId == currentUserId);
            if (!isOwnCustomer)
            {
                throw new ForbiddenException("You can only cancel your own orders.");
            }
        }

        // Cancellability check per Agent.md 10.7
        if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Order is already cancelled.");
        }

        if (string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Cannot cancel an order that has already been delivered.");
        }

        if (string.Equals(order.Status, "PickedUp", StringComparison.OrdinalIgnoreCase) && !isAdminOrDispatcher)
        {
            throw new ConflictException("Order has already been picked up. Only an admin or dispatcher can cancel it.");
        }

        var previousStatus = order.Status;
        var now = DateTime.UtcNow;
        var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Cancelled by user" : request.Reason;
        var actorType = isAdminOrDispatcher ? (roleList.Contains("Admin") ? "Admin" : "Dispatcher") : "Customer";

        order.Status = "Cancelled";
        order.CancelledReason = reason;
        order.CancelledBy = currentUserId;
        order.UpdatedAt = now;
        order.Version += 1;

        // Record history
        order.StatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = "Cancelled",
            Reason = reason,
            ActorType = actorType,
            ActorId = currentUserId,
            OccurredAt = now
        });

        // Outbox event
        var eventId = Guid.NewGuid();
        var outboxPayload = new
        {
            orderId = order.Id,
            previousStatus = previousStatus,
            cancelledBy = currentUserId,
            actorType = actorType,
            reason = reason,
            occurredAt = now
        };

        var headers = new
        {
            correlationId = correlationId,
            timestamp = now.ToString("o")
        };

        await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
        {
            Id = eventId,
            AggregateType = "Order",
            AggregateId = order.Id,
            Topic = "order.events",
            MessageKey = order.Id.ToString(),
            EventType = "order.cancelled",
            EventVersion = 1,
            Payload = JsonSerializer.Serialize(outboxPayload),
            Headers = JsonSerializer.Serialize(headers),
            CreatedAt = now,
            PublishedAt = null
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Order {OrderId} cancelled by {ActorType} {UserId}. Outbox event {EventId} queued.", order.Id, actorType, currentUserId, eventId);

        return MapOrderResponse(order);
    }

    public async Task<OrderResponse> PickupOrderAsync(
        Guid id,
        Guid courierUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var courier = await _courierRepository.GetByUserIdAsync(courierUserId, cancellationToken);
        if (courier == null)
        {
            throw new ForbiddenException("Authenticated user is not a registered courier.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.AssignedCourier)
            .Include(o => o.Stops)
            .Include(o => o.StatusHistories)
            .Include(o => o.Assignments)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException($"Order with ID {id} was not found.");
        }

        if (order.AssignedCourierId != courier.Id)
        {
            throw new ForbiddenException("This order is not assigned to you.");
        }

        if (!string.Equals(order.Status, "Assigned", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Order cannot transition to PickedUp from '{order.Status}'. Required status is 'Assigned'.");
        }

        var now = DateTime.UtcNow;
        var previousStatus = order.Status;

        order.Status = "PickedUp";
        order.UpdatedAt = now;
        order.Version += 1;

        // Mark pickup stop completed
        var pickupStop = order.Stops.OrderBy(s => s.Sequence).FirstOrDefault(s => s.Type == "Pickup");
        if (pickupStop != null)
        {
            pickupStop.ArrivedAt ??= now;
            pickupStop.CompletedAt = now;
        }

        // Record history
        order.StatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = "PickedUp",
            Reason = "Picked up by courier",
            ActorType = "Courier",
            ActorId = courierUserId,
            OccurredAt = now
        });

        // Outbox event
        var eventId = Guid.NewGuid();
        var outboxPayload = new
        {
            orderId = order.Id,
            fromStatus = previousStatus,
            toStatus = "PickedUp",
            courierId = courier.Id,
            actorType = "Courier",
            actorId = courierUserId,
            occurredAt = now
        };

        var headers = new
        {
            correlationId = correlationId,
            timestamp = now.ToString("o")
        };

        await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
        {
            Id = eventId,
            AggregateType = "Order",
            AggregateId = order.Id,
            Topic = "order.events",
            MessageKey = order.Id.ToString(),
            EventType = "order.status.changed",
            EventVersion = 1,
            Payload = JsonSerializer.Serialize(outboxPayload),
            Headers = JsonSerializer.Serialize(headers),
            CreatedAt = now,
            PublishedAt = null
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Order {OrderId} status changed to PickedUp by courier {CourierId}.", order.Id, courier.Id);

        return MapOrderResponse(order);
    }

    public async Task<OrderResponse> DeliverOrderAsync(
        Guid id,
        Guid courierUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var courier = await _courierRepository.GetByUserIdAsync(courierUserId, cancellationToken);
        if (courier == null)
        {
            throw new ForbiddenException("Authenticated user is not a registered courier.");
        }

        var order = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.AssignedCourier)
            .Include(o => o.Stops)
            .Include(o => o.StatusHistories)
            .Include(o => o.Assignments)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
        {
            throw new NotFoundException($"Order with ID {id} was not found.");
        }

        if (order.AssignedCourierId != courier.Id)
        {
            throw new ForbiddenException("This order is not assigned to you.");
        }

        if (!string.Equals(order.Status, "PickedUp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Order cannot transition to Delivered from '{order.Status}'. Required status is 'PickedUp'.");
        }

        var now = DateTime.UtcNow;
        var previousStatus = order.Status;

        order.Status = "Delivered";
        order.CompletedAt = now;
        order.UpdatedAt = now;
        order.Version += 1;

        // Mark dropoff stop completed
        var dropoffStop = order.Stops.OrderBy(s => s.Sequence).LastOrDefault(s => s.Type == "Dropoff");
        if (dropoffStop != null)
        {
            dropoffStop.ArrivedAt ??= now;
            dropoffStop.CompletedAt = now;
        }

        // Mark active assignment completed
        var activeAssignment = order.Assignments.FirstOrDefault(a => a.CourierId == courier.Id && a.Status == "Assigned");
        if (activeAssignment != null)
        {
            activeAssignment.Status = "Completed";
            activeAssignment.CompletedAt = now;
        }

        // Record history
        order.StatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = "Delivered",
            Reason = "Delivered by courier",
            ActorType = "Courier",
            ActorId = courierUserId,
            OccurredAt = now
        });

        // Outbox event
        var eventId = Guid.NewGuid();
        var outboxPayload = new
        {
            orderId = order.Id,
            fromStatus = previousStatus,
            toStatus = "Delivered",
            courierId = courier.Id,
            actorType = "Courier",
            actorId = courierUserId,
            occurredAt = now
        };

        var headers = new
        {
            correlationId = correlationId,
            timestamp = now.ToString("o")
        };

        await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
        {
            Id = eventId,
            AggregateType = "Order",
            AggregateId = order.Id,
            Topic = "order.events",
            MessageKey = order.Id.ToString(),
            EventType = "order.status.changed",
            EventVersion = 1,
            Payload = JsonSerializer.Serialize(outboxPayload),
            Headers = JsonSerializer.Serialize(headers),
            CreatedAt = now,
            PublishedAt = null
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Order {OrderId} successfully Delivered by courier {CourierId}.", order.Id, courier.Id);

        return MapOrderResponse(order);
    }

    private static OrderResponse MapOrderResponse(Order order)
    {
        var stops = order.Stops.OrderBy(s => s.Sequence).Select(s => new OrderStopResponse(
            Id: s.Id,
            Sequence: s.Sequence,
            Type: s.Type,
            Address: s.Address,
            Latitude: s.Location?.Y ?? 0.0,
            Longitude: s.Location?.X ?? 0.0,
            ContactName: s.ContactName,
            ContactPhone: s.ContactPhone,
            ArrivedAt: s.ArrivedAt.HasValue ? new DateTimeOffset(s.ArrivedAt.Value, TimeSpan.Zero) : null,
            CompletedAt: s.CompletedAt.HasValue ? new DateTimeOffset(s.CompletedAt.Value, TimeSpan.Zero) : null
        )).ToList();

        var histories = order.StatusHistories.OrderBy(h => h.OccurredAt).Select(h => new OrderStatusHistoryResponse(
            Id: h.Id,
            FromStatus: h.FromStatus,
            ToStatus: h.ToStatus,
            Reason: h.Reason,
            ActorType: h.ActorType,
            ActorId: h.ActorId,
            OccurredAt: new DateTimeOffset(h.OccurredAt, TimeSpan.Zero)
        )).ToList();

        var assignments = order.Assignments.Select(a => new AssignmentResponse(
            Id: a.Id,
            CourierId: a.CourierId,
            CourierName: a.Courier?.User?.FullName,
            Status: a.Status,
            AssignedAt: new DateTimeOffset(a.AssignedAt, TimeSpan.Zero),
            CompletedAt: a.CompletedAt.HasValue ? new DateTimeOffset(a.CompletedAt.Value, TimeSpan.Zero) : null
        )).ToList();

        return new OrderResponse(
            Id: order.Id,
            OrderNo: order.OrderNo,
            CustomerId: order.CustomerId,
            CustomerName: order.Customer?.User?.FullName,
            Status: order.Status,
            Priority: order.Priority,
            VehicleType: order.VehicleType,
            PackageDescription: order.PackageDescription,
            PackageWeight: order.PackageWeight,
            RequestedPickupAt: order.RequestedPickupAt.HasValue ? new DateTimeOffset(order.RequestedPickupAt.Value, TimeSpan.Zero) : null,
            AssignedCourierId: order.AssignedCourierId,
            AssignedCourierName: order.AssignedCourier?.User?.FullName,
            CancelledReason: order.CancelledReason,
            CancelledBy: order.CancelledBy,
            Version: order.Version,
            CreatedAt: new DateTimeOffset(order.CreatedAt, TimeSpan.Zero),
            UpdatedAt: new DateTimeOffset(order.UpdatedAt, TimeSpan.Zero),
            CompletedAt: order.CompletedAt.HasValue ? new DateTimeOffset(order.CompletedAt.Value, TimeSpan.Zero) : null,
            Stops: stops,
            StatusHistories: histories,
            Assignments: assignments
        );
    }
}

