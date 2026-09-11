using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Appointment?> GetByAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetActiveByBarberIdAndDateAsync(Guid barberId, DateOnly date, CancellationToken cancellationToken = default);
    Task<int> CountCreatedByClientSinceAsync(Guid clientId, DateTime since, CancellationToken cancellationToken = default);
    /// <summary>Quais destes clientes têm agendamento Accepted marcado depois de <paramref name="now"/>.</summary>
    /// <param name="now">Horário de parede da barbearia — o mesmo relógio de ScheduledAt.</param>
    Task<HashSet<Guid>> GetClientIdsWithUpcomingAsync(IReadOnlyCollection<Guid> clientIds, DateTime now, CancellationToken cancellationToken = default);
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetCompletedByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    /// <param name="now">Horário de parede da barbearia — o mesmo relógio de ScheduledAt.</param>
    Task<List<Appointment>> GetPendingRemindersAsync(DateTime now, DateTime windowStart, DateTime windowEnd, CancellationToken ct = default);
    Task<bool> AnyByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
    Task<bool> AnyByServiceIdAsync(Guid serviceId, CancellationToken cancellationToken = default);
}
