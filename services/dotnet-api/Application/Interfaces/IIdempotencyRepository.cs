using LogisticServer.Domain.Entities;

namespace LogisticServer.Application.Interfaces;

public interface IIdempotencyRepository
{
    Task<IdempotencyKey?> GetAsync(Guid userId, string key, CancellationToken cancellationToken = default);
    Task CreateOrUpdateAsync(IdempotencyKey keyRecord, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
