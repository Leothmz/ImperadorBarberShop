using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IBarberAvailabilityRepository
{
    Task<List<BarberAvailability>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
    Task<BarberAvailability?> GetByBarberIdAndDayAsync(Guid barberId, DayOfWeek dayOfWeek, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<BarberAvailability> availabilities, CancellationToken cancellationToken = default);
    Task DeleteByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
}
