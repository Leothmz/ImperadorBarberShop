using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Services;

public class ReminderBackgroundServiceTests
{
    private readonly IAppSettingsRepository _settings       = Substitute.For<IAppSettingsRepository>();
    private readonly IAppointmentRepository _appointments   = Substitute.For<IAppointmentRepository>();
    private readonly INotificationService   _notifications  = Substitute.For<INotificationService>();
    private readonly IUnitOfWork            _unitOfWork     = Substitute.For<IUnitOfWork>();
    private readonly IServiceScopeFactory   _scopeFactory   = Substitute.For<IServiceScopeFactory>();
    private readonly IServiceScope          _scope          = Substitute.For<IServiceScope>();
    private readonly IServiceProvider       _provider       = Substitute.For<IServiceProvider>();

    private readonly ReminderBackgroundService _svc;

    public ReminderBackgroundServiceTests()
    {
        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_provider);

        _provider.GetService(typeof(IAppSettingsRepository)).Returns(_settings);
        _provider.GetService(typeof(IAppointmentRepository)).Returns(_appointments);
        _provider.GetService(typeof(INotificationService)).Returns(_notifications);
        _provider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);

        _svc = new ReminderBackgroundService(_scopeFactory, NullLogger<ReminderBackgroundService>.Instance);
    }

    private static Appointment BuildAcceptedAppointment()
    {
        return Appointment.Create(
            "João", "+5511999990000",
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(45),
            30, null,
            Array.Empty<Guid>());
    }

    [Fact]
    public async Task ProcessReminders_AppointmentsInWindow_SendsReminderAndMarksAsSent()
    {
        // Arrange
        _settings.GetAsync("notifications:reminderMinutesBefore", Arg.Any<CancellationToken>())
            .Returns("60");

        var appt = BuildAcceptedAppointment();
        _appointments
            .GetPendingRemindersAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { appt });

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        // Act
        await _svc.ProcessRemindersAsync(CancellationToken.None);

        // Assert
        await _notifications.Received(1).SendReminderAsync(appt, Arg.Any<CancellationToken>());
        appt.ReminderSentAt.Should().NotBeNull("MarkReminderSent should have been called");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessReminders_NoAppointments_NothingSent()
    {
        // Arrange
        _settings.GetAsync("notifications:reminderMinutesBefore", Arg.Any<CancellationToken>())
            .Returns("60");

        _appointments
            .GetPendingRemindersAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        // Act
        await _svc.ProcessRemindersAsync(CancellationToken.None);

        // Assert
        await _notifications.DidNotReceive().SendReminderAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessReminders_AppSettingsMissing_DefaultsTo60Minutes()
    {
        // Arrange — GetAsync returns null (key not configured)
        _settings.GetAsync("notifications:reminderMinutesBefore", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var now = DateTime.UtcNow;
        DateTime? capturedWindowEnd = null;

        _appointments
            .GetPendingRemindersAsync(Arg.Do<DateTime>(d => capturedWindowEnd = d), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        // Act
        await _svc.ProcessRemindersAsync(CancellationToken.None);

        // Assert: window end should be approximately UtcNow + 60 minutes
        capturedWindowEnd.Should().NotBeNull();
        capturedWindowEnd!.Value.Should().BeCloseTo(now.AddMinutes(60), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProcessReminders_OneFailsOtherSucceeds_ContinuesProcessing()
    {
        // Arrange
        _settings.GetAsync("notifications:reminderMinutesBefore", Arg.Any<CancellationToken>())
            .Returns("60");

        var appt1 = BuildAcceptedAppointment();
        var appt2 = BuildAcceptedAppointment();

        _appointments
            .GetPendingRemindersAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { appt1, appt2 });

        // First appointment throws; second should still be processed
        _notifications
            .SendReminderAsync(appt1, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("WhatsApp down")));

        _notifications
            .SendReminderAsync(appt2, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        // Act — should not throw
        await _svc.ProcessRemindersAsync(CancellationToken.None);

        // Assert: second appointment was still processed
        await _notifications.Received(1).SendReminderAsync(appt2, Arg.Any<CancellationToken>());
        appt2.ReminderSentAt.Should().NotBeNull("second appointment should have been marked");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
