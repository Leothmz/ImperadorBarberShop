using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class AcceptAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AcceptAppointmentCommandHandler _handler;

    public AcceptAppointmentCommandHandlerTests()
    {
        _handler = new AcceptAppointmentCommandHandler(
            _appointmentRepository, _userRepository, _emailService, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidAccept_AcceptsAppointmentAndSendsEmail()
    {
        var barberId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var appointment = Appointment.Create(clientId, barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        var client = User.CreateClient("João", "joao@email.com", "hash");

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _userRepository.GetByIdAsync(clientId, Arg.Any<CancellationToken>()).Returns(client);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new AcceptAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Accepted);
        await _emailService.Received(1).SendAppointmentAcceptedAsync(
            client.Email, client.Name, appointment.ScheduledAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Appointment?)null);

        var act = () => _handler.Handle(
            new AcceptAppointmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsUnauthorizedAccessException()
    {
        var realBarberId = Guid.NewGuid();
        var appointment = Appointment.Create(
            Guid.NewGuid(), realBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(
            new AcceptAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_AlreadyAccepted_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create(
            Guid.NewGuid(), barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Accept(); // already accepted

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new AcceptAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

public class RejectAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RejectAppointmentCommandHandler _handler;

    public RejectAppointmentCommandHandlerTests()
    {
        _handler = new RejectAppointmentCommandHandler(
            _appointmentRepository, _userRepository, _emailService, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidReject_RejectsAppointmentAndSendsEmail()
    {
        var barberId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var appointment = Appointment.Create(clientId, barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        var client = User.CreateClient("João", "joao@email.com", "hash");

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _userRepository.GetByIdAsync(clientId, Arg.Any<CancellationToken>()).Returns(client);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new RejectAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Rejected);
        await _emailService.Received(1).SendAppointmentRejectedAsync(
            client.Email, client.Name, appointment.ScheduledAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Appointment?)null);

        var act = () => _handler.Handle(
            new RejectAppointmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsUnauthorizedAccessException()
    {
        var realBarberId = Guid.NewGuid();
        var appointment = Appointment.Create(
            Guid.NewGuid(), realBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(
            new RejectAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_AlreadyRejected_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create(
            Guid.NewGuid(), barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Reject(); // already rejected

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new RejectAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

public class CompleteAppointmentCommandHandlerTests
{
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CompleteAppointmentCommandHandler _handler;

    public CompleteAppointmentCommandHandlerTests()
    {
        _handler = new CompleteAppointmentCommandHandler(_appointmentRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidComplete_CompletesAppointment()
    {
        var barberId = Guid.NewGuid();
        var appointment = Appointment.Create(
            Guid.NewGuid(), barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Accept(); // must be Accepted to complete

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        await _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        appointment.Status.Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Handle_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        _appointmentRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Appointment?)null);

        var act = () => _handler.Handle(
            new CompleteAppointmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongBarber_ThrowsUnauthorizedAccessException()
    {
        var realBarberId = Guid.NewGuid();
        var appointment = Appointment.Create(
            Guid.NewGuid(), realBarberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });
        appointment.Accept();

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(
            new CompleteAppointmentCommand(appointment.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_PendingAppointment_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        // Still Pending — cannot complete without Accepted
        var appointment = Appointment.Create(
            Guid.NewGuid(), barberId, DateTime.UtcNow.AddDays(1), 30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetByIdAsync(appointment.Id, Arg.Any<CancellationToken>()).Returns(appointment);

        var act = () => _handler.Handle(new CompleteAppointmentCommand(appointment.Id, barberId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
