using LogisticServer.Application.DTOs.Orders;
using LogisticServer.Application.Interfaces;
using LogisticServer.Domain.Entities;
using LogisticServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogisticServer.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly CoreDbContext _dbContext;

    public OrderRepository(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .Include(o => o.Customer)
                .ThenInclude(c => c.User)
            .Include(o => o.AssignedCourier)
                .ThenInclude(c => c!.User)
            .Include(o => o.Stops.OrderBy(s => s.Sequence))
            .Include(o => o.StatusHistories.OrderBy(h => h.OccurredAt))
            .Include(o => o.Assignments)
                .ThenInclude(a => a.Courier)
                    .ThenInclude(c => c.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Order>> GetPagedAsync(OrderFilterQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _dbContext.Orders
            .Include(o => o.Customer)
                .ThenInclude(c => c.User)
            .Include(o => o.AssignedCourier)
                .ThenInclude(c => c!.User)
            .Include(o => o.Stops.OrderBy(s => s.Sequence))
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            dbQuery = dbQuery.Where(o => o.Status == query.Status);
        }

        if (query.CustomerId.HasValue)
        {
            dbQuery = dbQuery.Where(o => o.CustomerId == query.CustomerId.Value || o.Customer.UserId == query.CustomerId.Value);
        }

        if (query.CourierId.HasValue)
        {
            dbQuery = dbQuery.Where(o => o.AssignedCourierId == query.CourierId.Value || (o.AssignedCourier != null && o.AssignedCourier.UserId == query.CourierId.Value));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : (query.PageSize > 100 ? 100 : query.PageSize);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await dbQuery
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }

    public async Task<bool> HasActiveOrderForCourierAsync(Guid courierId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders.AnyAsync(o =>
            (o.AssignedCourierId == courierId || (o.AssignedCourier != null && o.AssignedCourier.UserId == courierId)) &&
            (o.Status == "Assigned" || o.Status == "PickedUp"),
            cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
