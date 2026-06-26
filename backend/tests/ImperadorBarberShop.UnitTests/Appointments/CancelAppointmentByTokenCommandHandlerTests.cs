using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CancelAppointmentByTokenCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CancelAppointmentByTokenCommandHandler _handler;

    public CancelAppointmentByTokenCommandHandlerTests()
    {
        _handler = new CancelAppointmentByTokenCommandHandler(_appointmentRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCancel_CancelsAppointment()
    {
        var scheduledAt = DateTime.UtcNow.AddHours(3); // > 2h in the future
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

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
        var scheduledAt = DateTime.UtcNow.AddMinutes(90); // less than 2 hours
        var appointment = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByAccessTokenAsync(appointment.AccessToken, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CancelAppointmentByTokenCommand(appointment.AccessToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*2 hours*");
    }
}
