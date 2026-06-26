namespace ImperadorBarberShop.Application.Interfaces;

public interface IEmailService
{
    Task SendAppointmentCreatedAsync(string barberEmail, string barberName, string clientName, string clientPhone, DateTime scheduledAt, CancellationToken cancellationToken = default);
}
