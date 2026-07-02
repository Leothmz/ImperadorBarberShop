using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Appointment?> GetByAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetActiveByBarberIdAndDateAsync(Guid barberId, DateOnly date, CancellationToken cancellationToken = default);
    Task<int> CountCreatedByPhoneSinceAsync(string clientPhone, DateTime since, CancellationToken cancellationToken = default);
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetCompletedByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetPendingRemindersAsync(DateTime windowStart, DateTime windowEnd, CancellationToken ct = default);
}
