using LogisticServer.Application.DTOs.Orders;

namespace LogisticServer.Application.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(
        Guid customerUserId,
        CreateOrderRequest request,
        string? idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> GetOrderByIdAsync(
        Guid id,
        Guid currentUserId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OrderResponse>> GetOrdersAsync(
        Guid currentUserId,
        IEnumerable<string> roles,
        OrderFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> CancelOrderAsync(
        Guid id,
        Guid currentUserId,
        IEnumerable<string> roles,
        CancelOrderRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> PickupOrderAsync(
        Guid id,
        Guid courierUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> DeliverOrderAsync(
        Guid id,
        Guid courierUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
