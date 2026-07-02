using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CompleteAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CompleteAppointmentCommandHandler _handler;

    public CompleteAppointmentCommandHandlerTests()
    {
        _handler = new CompleteAppointmentCommandHandler(_appointmentRepository, _notificationService, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidComplete_CompletesAppointment()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Appointment?)null);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsForbiddenException()
    {
        var realBarberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", realBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Complete();
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WithPaymentMethod_SetsPaymentOnCompletion()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create("João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), 30, null, [Guid.NewGuid()]);
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId, PaymentMethod.Pix), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.PaymentMethod.Should().Be(PaymentMethod.Pix);
    }
}
