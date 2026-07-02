using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Services;

public class NotificationServiceTests
{
    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly IWhatsAppService _wa = Substitute.For<IWhatsAppService>();
    private readonly IAppSettingsRepository _settings = Substitute.For<IAppSettingsRepository>();
    private readonly NotificationService _svc;

    public NotificationServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FrontendUrl"] = "http://localhost:3000" })
            .Build();
        _svc = new NotificationService(_email, _wa, _settings, config);
    }

    private void SetChannels(string channels, string? barberPhone = null)
    {
        _settings.GetAsync("notifications:channels", Arg.Any<CancellationToken>()).Returns(channels);
        _settings.GetAsync("whatsapp:notificationPhone", Arg.Any<CancellationToken>()).Returns(barberPhone);
    }

    private static (Appointment appt, Barber barber, List<Service> services) Build()
    {
        var user = User.CreateBarber("Carlos", "carlos@test.com", "hash");
        var barber = Barber.Create(user.Id);
        var svc = Service.Create("Corte", "Desc", 30, 35m);
        var appt = Appointment.Create("João", "+5511999990000", barber.Id,
            DateTime.UtcNow.AddDays(1), 30, null, new[] { svc.Id });
        return (appt, barber, new List<Service> { svc });
    }

    [Fact]
    public async Task Created_EmailOnly_CallsEmailNotWhatsApp()
    {
        SetChannels("email");
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _email.Received(1).SendAppointmentCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _wa.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Created_WhatsAppNoBarberPhone_SendsToClientOnly()
    {
        SetChannels("whatsapp", barberPhone: null);
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _wa.Received(1).SendAsync(appt.ClientPhone, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAppointmentCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Created_WhatsAppWithBarberPhone_SendsTwice()
    {
        SetChannels("whatsapp", barberPhone: "+5511988880000");
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _wa.Received(2).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Created_BothChannels_CallsBoth()
    {
        SetChannels("email,whatsapp");
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _email.Received(1).SendAppointmentCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _wa.Received(1).SendAsync(appt.ClientPhone, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelled_WhatsApp_SendsCancelMessageToClient()
    {
        SetChannels("whatsapp");
        var appt = Appointment.Create("João", "+5511999990000", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1), 30, null, Array.Empty<Guid>());
        await _svc.SendAppointmentCancelledAsync(appt, CancellationToken.None);
        await _wa.Received(1).SendAsync(
            appt.ClientPhone,
            Arg.Is<string>(m => m.Contains("cancelado")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Completed_WhatsApp_SendsReviewLinkToClient()
    {
        SetChannels("whatsapp");
        var appt = Appointment.Create("João", "+5511999990000", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1), 30, null, Array.Empty<Guid>());
        await _svc.SendAppointmentCompletedAsync(appt, CancellationToken.None);
        await _wa.Received(1).SendAsync(
            appt.ClientPhone,
            Arg.Is<string>(m => m.Contains(appt.AccessToken)),
            Arg.Any<CancellationToken>());
    }
}
