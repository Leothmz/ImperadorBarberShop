using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class ServiceRepository : IServiceRepository
{
    private readonly AppDbContext _context;

    public ServiceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Service>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Services
            .Where(s => s.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<List<Service>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Services.ToListAsync(cancellationToken);

    public async Task<List<Service>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        => await _context.Services
            .Where(s => ids.Contains(s.Id) && s.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services.FindAsync(new object[] { id }, cancellationToken);

    public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
        => await _context.Services.AddAsync(service, cancellationToken);

    public Task UpdateAsync(Service service, CancellationToken cancellationToken = default)
    {
        _context.Services.Update(service);
        return Task.CompletedTask;
    }
}
