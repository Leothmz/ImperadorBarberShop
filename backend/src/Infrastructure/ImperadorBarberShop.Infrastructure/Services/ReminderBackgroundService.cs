using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImperadorBarberShop.Infrastructure.Services;

public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderBackgroundService> _logger;

    public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessRemindersAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    public async Task ProcessRemindersAsync(CancellationToken ct)
    {
        using var scope         = _scopeFactory.CreateScope();
        var settingsRepo        = scope.ServiceProvider.GetRequiredService<IAppSettingsRepository>();
        var appointmentRepo     = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var unitOfWork          = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var minutesStr = await settingsRepo.GetAsync("notifications:reminderMinutesBefore", ct) ?? "60";
        var minutes    = int.TryParse(minutesStr, out var m) ? m : 60;
        var windowEnd  = DateTime.UtcNow.AddMinutes(minutes);

        var appointments = await appointmentRepo.GetPendingRemindersAsync(windowEnd, ct);

        foreach (var appointment in appointments)
        {
            try
            {
                await notificationService.SendReminderAsync(appointment, ct);
                appointment.MarkReminderSent();
                await appointmentRepo.UpdateAsync(appointment, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send reminder for appointment {Id}", appointment.Id);
            }
        }
    }
}
