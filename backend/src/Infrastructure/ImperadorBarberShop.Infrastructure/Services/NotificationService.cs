using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ImperadorBarberShop.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService _email;
    private readonly IWhatsAppService _wa;
    private readonly IAppSettingsRepository _settings;
    private readonly ILogger<NotificationService> _logger;
    private readonly string _frontendUrl;

    public NotificationService(
        IEmailService email,
        IWhatsAppService wa,
        IAppSettingsRepository settings,
        IConfiguration config,
        ILogger<NotificationService> logger)
    {
        _email       = email;
        _wa          = wa;
        _settings    = settings;
        _logger      = logger;
        _frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
    }

    // Convenience constructor without logger (uses NullLogger — suitable for tests)
    public NotificationService(
        IEmailService email,
        IWhatsAppService wa,
        IAppSettingsRepository settings,
        IConfiguration config)
        : this(email, wa, settings, config,
               Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationService>.Instance)
    {
    }

    public async Task SendAppointmentCreatedAsync(
        Appointment appointment, Barber barber, List<Service> services, CancellationToken ct = default)
    {
        var channels     = await GetChannelsAsync(ct);
        var serviceNames = string.Join(", ", services.Select(s => s.Name));
        var scheduledAt  = appointment.ScheduledAt.AddHours(-3).ToString("dd/MM/yyyy HH:mm");

        if (channels.Contains("email"))
        {
            try
            {
                var barberEmail = barber.User?.Email ?? string.Empty;
                var barberName  = barber.User?.Name ?? string.Empty;
                await _email.SendAppointmentCreatedAsync(
                    barberEmail, barberName,
                    appointment.ClientName, appointment.ClientPhone,
                    appointment.ScheduledAt, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send appointment-created email");
            }
        }

        if (channels.Contains("whatsapp"))
        {
            var barberPhone = await _settings.GetAsync("whatsapp:notificationPhone", ct);
            if (!string.IsNullOrEmpty(barberPhone))
            {
                var barberMsg = $"Novo agendamento!\nCliente: {appointment.ClientName}\n" +
                                $"Telefone: {appointment.ClientPhone}\nData: {scheduledAt}\n" +
                                $"Serviços: {serviceNames}";
                try
                {
                    await _wa.SendAsync(barberPhone, barberMsg, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send appointment-created WhatsApp to barber");
                }
            }

            var clientMsg = $"Olá {appointment.ClientName}! Seu agendamento foi confirmado para {scheduledAt}. " +
                            $"Serviços: {serviceNames}";
            try
            {
                await _wa.SendAsync(appointment.ClientPhone, clientMsg, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send appointment-created WhatsApp to client");
            }
        }
    }

    public async Task SendAppointmentCancelledAsync(Appointment appointment, CancellationToken ct = default)
    {
        var channels    = await GetChannelsAsync(ct);
        var scheduledAt = appointment.ScheduledAt.AddHours(-3).ToString("dd/MM/yyyy HH:mm");

        if (channels.Contains("whatsapp"))
        {
            var clientMsg = $"Olá {appointment.ClientName}! Seu agendamento de {scheduledAt} foi cancelado.";
            try
            {
                await _wa.SendAsync(appointment.ClientPhone, clientMsg, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send appointment-cancelled WhatsApp to client");
            }

            var barberPhone = await _settings.GetAsync("whatsapp:notificationPhone", ct);
            if (!string.IsNullOrEmpty(barberPhone))
            {
                var barberMsg = $"Agendamento cancelado!\nCliente: {appointment.ClientName}\nData: {scheduledAt}";
                try
                {
                    await _wa.SendAsync(barberPhone, barberMsg, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send appointment-cancelled WhatsApp to barber");
                }
            }
        }
    }

    public async Task SendAppointmentCompletedAsync(Appointment appointment, CancellationToken ct = default)
    {
        var channels    = await GetChannelsAsync(ct);
        var reviewLink  = $"{_frontendUrl}/agendamento/{appointment.AccessToken}";

        if (channels.Contains("whatsapp"))
        {
            var msg = $"Olá {appointment.ClientName}! Obrigado pela visita. Avalie seu atendimento em: {reviewLink}";
            try
            {
                await _wa.SendAsync(appointment.ClientPhone, msg, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send appointment-completed WhatsApp to client");
            }
        }
    }

    public async Task SendReminderAsync(Appointment appointment, CancellationToken ct = default)
    {
        var channels     = await GetChannelsAsync(ct);
        var scheduledAt  = appointment.ScheduledAt.AddHours(-3).ToString("dd/MM/yyyy HH:mm");
        var serviceNames = string.Join(", ",
            appointment.AppointmentServices.Select(s => s.Service?.Name ?? string.Empty));

        if (channels.Contains("whatsapp"))
        {
            var msg = $"Olá {appointment.ClientName}! Lembrete: você tem um agendamento amanhã às {scheduledAt}. " +
                      $"Serviços: {serviceNames}";
            try
            {
                await _wa.SendAsync(appointment.ClientPhone, msg, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder WhatsApp to client");
            }
        }
    }

    private async Task<HashSet<string>> GetChannelsAsync(CancellationToken ct)
    {
        var raw = await _settings.GetAsync("notifications:channels", ct) ?? "email";
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
