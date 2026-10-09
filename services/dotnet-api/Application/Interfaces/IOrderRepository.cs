using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Domain.Entities;

namespace LogisticServer.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Order?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Order>> GetPagedAsync(OrderFilterQuery query, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<bool> HasActiveOrderForCourierAsync(Guid courierId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
