using LogisticServer.Application.DTOs.Couriers;
using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Application.Interfaces;
using LogisticServer.Domain.Entities;
using LogisticServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogisticServer.Infrastructure.Repositories;

public class CourierRepository : ICourierRepository
{
    private readonly CoreDbContext _dbContext;

    public CourierRepository(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Courier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Couriers
            .Include(c => c.User)
            .Include(c => c.Vehicle)
            .FirstOrDefaultAsync(c => c.Id == id || c.UserId == id, cancellationToken);
    }

    public async Task<Courier?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Couriers
            .Include(c => c.User)
            .Include(c => c.Vehicle)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    public async Task<Courier?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Couriers
            .Include(c => c.User)
            .Include(c => c.Vehicle)
            .Include(c => c.AssignedOrders.Where(o => o.Status == "Assigned" || o.Status == "PickedUp"))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id || c.UserId == id, cancellationToken);
    }

    public async Task<PagedResult<Courier>> GetPagedAsync(CourierFilterQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.Couriers
            .Include(c => c.User)
            .Include(c => c.Vehicle)
            .Include(c => c.AssignedOrders.Where(o => o.Status == "Assigned" || o.Status == "PickedUp"))
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            dbQuery = dbQuery.Where(c => c.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.VehicleType))
        {
            dbQuery = dbQuery.Where(c => c.Vehicle != null && c.Vehicle.Type == query.VehicleType);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : (query.PageSize > 100 ? 100 : query.PageSize);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await dbQuery
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Courier>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<IReadOnlyList<Courier>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Couriers
            .Include(c => c.User)
            .Include(c => c.Vehicle)
            .Where(c => c.Status == "PendingApproval")
            .OrderBy(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

