using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class BarberRepository : IBarberRepository
{
    private readonly AppDbContext _context;

    public BarberRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Barber?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Barbers
            .Include(b => b.User)
            .Include(b => b.Availability)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<Barber?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _context.Barbers
            .Include(b => b.User)
            .Include(b => b.Availability)
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

    public async Task<List<Barber>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Barbers
            .Include(b => b.User)
            .Include(b => b.Availability)
            .ToListAsync(cancellationToken);

    public async Task<List<Barber>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Barbers
            .Include(b => b.User)
            .Include(b => b.Availability)
            .Where(b => b.IsActive)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Barber barber, CancellationToken cancellationToken = default)
        => await _context.Barbers.AddAsync(barber, cancellationToken);

    public Task UpdateAsync(Barber barber, CancellationToken cancellationToken = default)
    {
        _context.Barbers.Update(barber);
        return Task.CompletedTask;
    }
}
