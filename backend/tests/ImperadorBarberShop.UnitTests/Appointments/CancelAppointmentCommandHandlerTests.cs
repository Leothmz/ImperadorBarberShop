using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CancelAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CancelAppointmentCommandHandler _handler;

    public CancelAppointmentCommandHandlerTests()
    {
        _handler = new CancelAppointmentCommandHandler(_appointmentRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCancel_CancelsAppointment()
    {
        var clientId = Guid.NewGuid();
        var barberId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddHours(3); // > 2h in the future
        var appointment = Appointment.Create(clientId, barberId, scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CancelAppointmentCommand(appointment.Id, clientId), CancellationToken.None);

        await _appointmentRepository.Received(1).UpdateAsync(appointment, Arg.Any<CancellationToken>());
        appointment.Status.Should().Be(Domain.Enums.AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Appointment?)null);

        var act = () => _handler.Handle(new CancelAppointmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongClient_ThrowsUnauthorizedAccessException()
    {
        var realClientId = Guid.NewGuid();
        var otherClientId = Guid.NewGuid();
        var appointment = Appointment.Create(
            realClientId, Guid.NewGuid(), DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CancelAppointmentCommand(appointment.Id, otherClientId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_LessThan2HoursBeforeSchedule_ThrowsInvalidOperationException()
    {
        var clientId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddMinutes(90); // less than 2 hours
        var appointment = Appointment.Create(clientId, Guid.NewGuid(), scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CancelAppointmentCommand(appointment.Id, clientId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*2 hours*");
    }
}
