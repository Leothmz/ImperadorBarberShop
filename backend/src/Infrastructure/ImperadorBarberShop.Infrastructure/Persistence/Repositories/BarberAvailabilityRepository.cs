using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class BarberAvailabilityRepository : IBarberAvailabilityRepository
{
    private readonly AppDbContext _context;

    public BarberAvailabilityRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<BarberAvailability>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default)
        => await _context.BarberAvailabilities
            .Where(a => a.BarberId == barberId)
            .ToListAsync(cancellationToken);

    public async Task<BarberAvailability?> GetByBarberIdAndDayAsync(Guid barberId, DayOfWeek dayOfWeek, CancellationToken cancellationToken = default)
        => await _context.BarberAvailabilities
            .FirstOrDefaultAsync(a => a.BarberId == barberId && a.DayOfWeek == dayOfWeek, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<BarberAvailability> availabilities, CancellationToken cancellationToken = default)
        => await _context.BarberAvailabilities.AddRangeAsync(availabilities, cancellationToken);

    public async Task DeleteByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.BarberAvailabilities
            .Where(a => a.BarberId == barberId)
            .ToListAsync(cancellationToken);
        _context.BarberAvailabilities.RemoveRange(existing);
    }
}
