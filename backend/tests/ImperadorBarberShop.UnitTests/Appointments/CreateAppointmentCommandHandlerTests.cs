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
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateAppointmentCommandHandler _handler;

    public CreateAppointmentCommandHandlerTests()
    {
        _handler = new CreateAppointmentCommandHandler(
            _barberRepository, _serviceRepository, _appointmentRepository, _emailService, _unitOfWork);
    }

    private void SetupHappyPath(Guid barberId, Service service)
    {
        var barberUser = User.CreateBarber("Carlos", "carlos@email.com", "hash");
        var barber = Barber.Create(barberUser.Id);
        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(barberId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());
        _appointmentRepository.CountCreatedByPhoneSinceAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsIdAndAccessToken()
    {
        var barberId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte moderno", 30, 35.00m);
        SetupHappyPath(barberId, service);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { serviceId }, null);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        result.AccessToken.Should().NotBeNullOrEmpty();
        await _appointmentRepository.Received(1).AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BarberNotFound_ThrowsKeyNotFoundException()
    {
        _barberRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Barber?)null);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", Guid.NewGuid(), DateTime.UtcNow.AddDays(1), new List<Guid> { Guid.NewGuid() }, null);

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
            .Returns(new List<Service>());

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_TimeSlotOccupied_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var barber = Barber.Create(Guid.NewGuid());
        var service = Service.Create("Corte", "Corte", 30, 35.00m);
        var existingAppt = Appointment.Create("Maria", "+5511999990001", barberId, scheduledAt, 30, null, new[] { Guid.NewGuid() });

        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(barberId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { existingAppt });
        _appointmentRepository.CountCreatedByPhoneSinceAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, scheduledAt, new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not available*");
    }

    [Fact]
    public async Task Handle_TooManyRecentRequestsFromPhone_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte", 30, 35.00m);
        SetupHappyPath(barberId, service);
        _appointmentRepository.CountCreatedByPhoneSinceAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Too many*");
    }
}
