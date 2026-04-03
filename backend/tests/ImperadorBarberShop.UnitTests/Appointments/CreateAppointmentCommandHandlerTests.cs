using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CreateAppointmentCommandHandlerTests
{
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IServiceRepository _serviceRepository = Substitute.For<IServiceRepository>();
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateAppointmentCommandHandler _handler;

    public CreateAppointmentCommandHandlerTests()
    {
        _handler = new CreateAppointmentCommandHandler(
            _barberRepository, _serviceRepository, _appointmentRepository,
            _emailService, _userRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsAppointmentId()
    {
        var barberId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddDays(1);

        var barberUser = User.CreateBarber("Carlos", "carlos@email.com", "hash");
        var barber = Barber.Create(barberUser.Id);
        var service = Service.Create("Corte", "Corte moderno", 30, 35.00m);
        var client = User.CreateClient("João", "joao@email.com", "hash");

        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(barberId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());
        _userRepository.GetByIdAsync(clientId, Arg.Any<CancellationToken>()).Returns(client);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var command = new CreateAppointmentCommand(clientId, barberId, scheduledAt, new List<Guid> { serviceId }, null);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _appointmentRepository.Received(1).AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BarberNotFound_ThrowsKeyNotFoundException()
    {
        _barberRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Barber?)null);

        var command = new CreateAppointmentCommand(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(1),
            new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_ServicesNotFound_ThrowsKeyNotFoundException()
    {
        var barberId = Guid.NewGuid();
        var barber = Barber.Create(Guid.NewGuid());
        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service>()); // empty — mismatch with requested ids

        var command = new CreateAppointmentCommand(
            Guid.NewGuid(), barberId, DateTime.UtcNow.AddDays(1),
            new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_TimeSlotOccupied_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10);

        var barber = Barber.Create(Guid.NewGuid());
        var service = Service.Create("Corte", "Corte", 30, 35.00m);

        // Existing appointment occupies 10:00 - 10:30
        var existingAppt = Appointment.Create(
            Guid.NewGuid(), barberId, scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(barberId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { existingAppt });

        var command = new CreateAppointmentCommand(
            clientId, barberId, scheduledAt, new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not available*");
    }
}
