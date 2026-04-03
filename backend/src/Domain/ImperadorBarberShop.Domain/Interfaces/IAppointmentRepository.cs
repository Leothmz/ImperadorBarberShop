using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
    Task<List<Appointment>> GetActiveByBarberIdAndDateAsync(Guid barberId, DateOnly date, CancellationToken cancellationToken = default);
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);
}
