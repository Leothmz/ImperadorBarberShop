namespace ImperadorBarberShop.Application.Interfaces;

public interface IEmailService
{
    Task SendAppointmentCreatedAsync(string barberEmail, string barberName, string clientName, DateTime scheduledAt, CancellationToken cancellationToken = default);
    Task SendAppointmentAcceptedAsync(string clientEmail, string clientName, DateTime scheduledAt, CancellationToken cancellationToken = default);
    Task SendAppointmentRejectedAsync(string clientEmail, string clientName, DateTime scheduledAt, CancellationToken cancellationToken = default);
}
