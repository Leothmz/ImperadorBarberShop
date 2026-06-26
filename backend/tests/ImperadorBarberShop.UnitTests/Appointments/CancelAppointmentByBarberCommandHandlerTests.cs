using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CancelAppointmentByBarberCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CancelAppointmentByBarberCommandHandler _handler;

    public CancelAppointmentByBarberCommandHandlerTests()
    {
        _handler = new CancelAppointmentByBarberCommandHandler(_appointmentRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCancel_CancelsAppointment()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CancelAppointmentByBarberCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
        await _appointmentRepository.Received(1).UpdateAsync(appointment, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CancelAppointmentByBarberCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsForbiddenException()
    {
        var realBarberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", realBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CancelAppointmentByBarberCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Cancel();
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CancelAppointmentByBarberCommand(appointment.Id, barberId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
