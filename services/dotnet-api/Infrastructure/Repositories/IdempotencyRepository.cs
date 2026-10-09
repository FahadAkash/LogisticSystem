using LogisticServer.Application.Interfaces;
using LogisticServer.Domain.Entities;
using LogisticServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogisticServer.Infrastructure.Repositories;

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly CoreDbContext _dbContext;

    public IdempotencyRepository(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IdempotencyKey?> GetAsync(Guid userId, string key, CancellationToken cancellationToken = default)
    {
        return await _dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.UserId == userId && k.Key == key, cancellationToken);
    }

    public async Task CreateOrUpdateAsync(IdempotencyKey keyRecord, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.UserId == keyRecord.UserId && k.Key == keyRecord.Key, cancellationToken);

        if (existing == null)
        {
            await _dbContext.IdempotencyKeys.AddAsync(keyRecord, cancellationToken);
        }
        else
        {
            existing.RequestHash = keyRecord.RequestHash;
            existing.ResponseStatus = keyRecord.ResponseStatus;
            existing.ResponseBody = keyRecord.ResponseBody;
            existing.ExpiresAt = keyRecord.ExpiresAt;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

