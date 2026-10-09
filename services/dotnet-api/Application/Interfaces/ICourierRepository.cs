using LogisticServer.Application.DTOs.Couriers;
using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Domain.Entities;

namespace LogisticServer.Application.Interfaces;

public interface ICourierRepository
{
    Task<Courier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Courier?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Courier?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Courier>> GetPagedAsync(CourierFilterQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Courier>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
