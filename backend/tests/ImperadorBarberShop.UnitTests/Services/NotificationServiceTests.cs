using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
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
            DateTime.UtcNow.AddDays(1), 30, null, new[] { svc });
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
    public async Task Created_MissingChannelKey_DefaultsToEmail()
    {
        _settings.GetAsync("notifications:channels", Arg.Any<CancellationToken>()).Returns((string?)null);
        var (appt, barber, services) = Build();
        await _svc.SendAppointmentCreatedAsync(appt, barber, services, CancellationToken.None);
        await _email.Received(1).SendAppointmentCreatedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _wa.DidNotReceiveWithAnyArgs().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelled_WhatsApp_SendsCancelMessageToClient()
    {
        SetChannels("whatsapp");
        var appt = Appointment.Create("João", "+5511999990000", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1), 30, null, Array.Empty<Service>());
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
            DateTime.UtcNow.AddDays(1), 30, null, Array.Empty<Service>());
        await _svc.SendAppointmentCompletedAsync(appt, CancellationToken.None);
        await _wa.Received(1).SendAsync(
            appt.ClientPhone,
            Arg.Is<string>(m => m.Contains(appt.AccessToken)),
            Arg.Any<CancellationToken>());
    }
    // ScheduledAt já é horário de parede da barbearia. O AddHours(-3) antigo tratava o
    // valor como UTC e mandava ao cliente um horário 3 horas adiantado (23:00 → 20:00).
    [Fact]
    public async Task Cancelled_WhatsApp_ShowsTheBookedWallClockTime()
    {
        SetChannels("whatsapp");
        var appt = Appointment.Create("João", "+5511999990000", Guid.NewGuid(),
            new DateTime(2026, 9, 10, 23, 0, 0), 30, null, Array.Empty<Service>());

        await _svc.SendAppointmentCancelledAsync(appt, CancellationToken.None);

        await _wa.Received(1).SendAsync(
            appt.ClientPhone,
            Arg.Is<string>(m => m.Contains("10/09/2026 23:00")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reminder_WhatsApp_ShowsTheBookedWallClockTime()
    {
        SetChannels("whatsapp");
        var appt = Appointment.Create("João", "+5511999990000", Guid.NewGuid(),
            new DateTime(2026, 9, 10, 23, 0, 0), 30, null, Array.Empty<Service>());

        await _svc.SendReminderAsync(appt, CancellationToken.None);

        await _wa.Received(1).SendAsync(
            appt.ClientPhone,
            Arg.Is<string>(m => m.Contains("10/09/2026 23:00")),
            Arg.Any<CancellationToken>());
    }

    private static Client ReinviteTarget()
        => Client.Create("João", BrazilianPhone.Parse("11 9999-0000"), DateTime.UtcNow.AddDays(-60));

    private NotificationService ServiceWithFrontendUrl(string? frontendUrl)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FrontendUrl"] = frontendUrl })
            .Build();
        return new NotificationService(_email, _wa, _settings, config);
    }

    [Fact]
    public async Task Reinvite_WhatsApp_SendsTheInviteWithTheSiteUrlToTheClientsCanonicalPhone()
    {
        SetChannels("email,whatsapp");
        var client = ReinviteTarget();

        await _svc.SendClientReinviteAsync(client, CancellationToken.None);

        await _wa.Received(1).SendAsync(
            "+5511999990000",
            "Olá João! Sentimos sua falta no O Imperador. Que tal agendar seu próximo corte? http://localhost:3000",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reinvite_EmailOnly_SendsNothing()
    {
        SetChannels("email");

        await _svc.SendClientReinviteAsync(ReinviteTarget(), CancellationToken.None);

        await _wa.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Reinvite_WithoutFrontendUrl_SendsJustTheText()
    {
        SetChannels("whatsapp");
        var svc = ServiceWithFrontendUrl(null);

        await svc.SendClientReinviteAsync(ReinviteTarget(), CancellationToken.None);

        await _wa.Received(1).SendAsync(
            Arg.Any<string>(),
            "Olá João! Sentimos sua falta no O Imperador. Que tal agendar seu próximo corte?",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reinvite_FrontendUrlListingSeveralOrigins_LinksTheFirst()
    {
        SetChannels("whatsapp");
        var svc = ServiceWithFrontendUrl("https://imperador.com.br/, https://www.imperador.com.br");

        await svc.SendClientReinviteAsync(ReinviteTarget(), CancellationToken.None);

        await _wa.Received(1).SendAsync(
            Arg.Any<string>(),
            Arg.Is<string>(m => m.EndsWith("corte? https://imperador.com.br")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reinvite_WhatsAppFailure_IsSwallowed()
    {
        SetChannels("whatsapp");
        _wa.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("Evolution fora do ar")));

        var act = () => _svc.SendClientReinviteAsync(ReinviteTarget(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
