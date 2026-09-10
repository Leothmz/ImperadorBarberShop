using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CancelAppointmentByTokenCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly INotificationQueue _notifications = Substitute.For<INotificationQueue>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    // Servidor em UTC às 21:58; na barbearia são 18:58
    private readonly FixedShopClock _clock = new(FixedShopClock.EveningUtc);
    private readonly CancelAppointmentByTokenCommandHandler _handler;

    public CancelAppointmentByTokenCommandHandlerTests()
    {
        _handler = new CancelAppointmentByTokenCommandHandler(_appointmentRepository, _notifications, _unitOfWork, _clock);
    }

    private Appointment ArrangeAppointmentAt(DateTime scheduledAt)
    {
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), scheduledAt, 30, null,
            new[] { Service.Create("Corte", "Desc", 30, 35m) });
        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        return appointment;
    }

    [Fact]
    public async Task Handle_ValidCancel_CancelsAppointment()
    {
        var appointment = ArrangeAppointmentAt(_clock.ShopNow.AddHours(3)); // > 2h in the future

        await _handler.Handle(new CancelAppointmentByTokenCommand(appointment.AccessToken), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_FourHoursAwayInShopTime_CancelsEvenThoughUtcIsCloser()
    {
        // Cenário do relatório: agendamento às 23:00, agora 18:58 na barbearia (4h02 de
        // antecedência). Contra DateTime.UtcNow (21:58) sobrava 1h02 e o cancelamento era
        // recusado — a janela de "2 horas" era, na prática, de 5.
        var appointment = ArrangeAppointmentAt(new DateTime(2026, 9, 10, 23, 0, 0));

        await _handler.Handle(new CancelAppointmentByTokenCommand(appointment.AccessToken), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CancelAppointmentByTokenCommand("bogus"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_LessThan2HoursBeforeSchedule_ThrowsInvalidOperationException()
    {
        var appointment = ArrangeAppointmentAt(_clock.ShopNow.AddMinutes(90)); // less than 2 hours

        var act = () => _handler.Handle(new CancelAppointmentByTokenCommand(appointment.AccessToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*2 horas*");
        appointment.Status.Should().Be(AppointmentStatus.Accepted);
    }

    [Fact]
    public async Task Handle_ExactlyTwoHoursBeforeSchedule_ThrowsInvalidOperationException()
    {
        var appointment = ArrangeAppointmentAt(_clock.ShopNow.AddHours(2)); // "exactly 2h" is not enough

        var act = () => _handler.Handle(new CancelAppointmentByTokenCommand(appointment.AccessToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
