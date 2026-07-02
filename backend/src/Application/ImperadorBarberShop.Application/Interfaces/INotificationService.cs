using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Application.Interfaces;

public interface INotificationService
{
    Task SendAppointmentCreatedAsync(
        Appointment appointment, Barber barber, List<Service> services, CancellationToken ct = default);
    Task SendAppointmentCancelledAsync(Appointment appointment, CancellationToken ct = default);
    Task SendAppointmentCompletedAsync(Appointment appointment, CancellationToken ct = default);
    Task SendReminderAsync(Appointment appointment, CancellationToken ct = default);
}
