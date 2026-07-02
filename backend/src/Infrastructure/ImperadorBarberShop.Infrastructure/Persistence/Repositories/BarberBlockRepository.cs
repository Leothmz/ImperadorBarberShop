using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class BarberBlockRepository : IBarberBlockRepository
{
    private readonly AppDbContext _context;

    public BarberBlockRepository(AppDbContext context) => _context = context;

    public async Task<List<BarberBlock>> GetByBarberIdAsync(Guid barberId, CancellationToken ct = default)
        => await _context.BarberBlocks
            .Where(b => b.BarberId == barberId)
            .OrderBy(b => b.StartsAt)
            .ToListAsync(ct);

    public async Task<BarberBlock?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.BarberBlocks.FindAsync([id], ct);

    public async Task AddAsync(BarberBlock block, CancellationToken ct = default)
        => await _context.BarberBlocks.AddAsync(block, ct);

    public Task DeleteAsync(BarberBlock block, CancellationToken ct = default)
    {
        _context.BarberBlocks.Remove(block);
        return Task.CompletedTask;
    }

    public async Task<List<BarberBlock>> GetActiveOnDateAsync(Guid barberId, DateOnly date, CancellationToken ct = default)
    {
        var dayBit = 1 << (int)date.DayOfWeek;
        var dateAsDateTime = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var nextDay = dateAsDateTime.AddDays(1);

        return await _context.BarberBlocks
            .Where(b => b.BarberId == barberId && (
                // Pontual: StartsAt falls on the requested date
                (!b.IsRecurring && b.StartsAt >= dateAsDateTime && b.StartsAt < nextDay)
                ||
                // Recorrente: day-of-week bit matches AND within recurrenceEndsAt
                (b.IsRecurring
                    && (b.RecurrenceDays & dayBit) != 0
                    && (b.RecurrenceEndsAt == null || b.RecurrenceEndsAt >= dateAsDateTime))
            ))
            .ToListAsync(ct);
    }
}
