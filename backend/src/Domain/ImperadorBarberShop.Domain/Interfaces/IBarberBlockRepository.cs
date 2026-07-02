using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IBarberBlockRepository
{
    Task<List<BarberBlock>> GetByBarberIdAsync(Guid barberId, CancellationToken ct = default);
    Task<BarberBlock?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(BarberBlock block, CancellationToken ct = default);
    Task DeleteAsync(BarberBlock block, CancellationToken ct = default);
    /// <summary>Returns all blocks active on the given date (pontual matching date, or recurring matching day-of-week and within recurrenceEndsAt).</summary>
    Task<List<BarberBlock>> GetActiveOnDateAsync(Guid barberId, DateOnly date, CancellationToken ct = default);
}
