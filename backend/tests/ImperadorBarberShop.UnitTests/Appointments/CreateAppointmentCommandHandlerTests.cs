using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Appointments;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Appointments;

public class CreateAppointmentCommandHandlerTests
{
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IServiceRepository _serviceRepository = Substitute.For<IServiceRepository>();
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IClientRepository _clientRepository = Substitute.For<IClientRepository>();
    private readonly INotificationQueue _notifications = Substitute.For<INotificationQueue>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateAppointmentCommandHandler _handler;

    public CreateAppointmentCommandHandlerTests()
    {
        _handler = new CreateAppointmentCommandHandler(
            _barberRepository, _serviceRepository, _appointmentRepository, _clientRepository, _notifications, _unitOfWork);
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
    public void Handle_ValidCommand_EnqueuesNotificationInsteadOfAwaitingIt()
    {
        var barberId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte moderno", 30, 35.00m);
        SetupHappyPath(barberId, service);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { serviceId }, null);

        // O handler não pode aguardar SMTP/WhatsApp: só enfileira o envio.
        _handler.Handle(command, CancellationToken.None).IsCompletedSuccessfully.Should().BeTrue();
        _notifications.Received(1).Enqueue(Arg.Any<Func<INotificationService, CancellationToken, Task>>());
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
    public async Task Handle_TimeSlotOccupied_ThrowsConflictException()
    {
        var barberId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var barber = Barber.Create(Guid.NewGuid());
        var service = Service.Create("Corte", "Corte", 30, 35.00m);
        var existingAppt = Appointment.Create("Maria", "+5511999990001", barberId, scheduledAt, 30, null, new[] { service });

        _barberRepository.GetByIdAsync(barberId, Arg.Any<CancellationToken>()).Returns(barber);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(barberId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { existingAppt });

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, scheduledAt, new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        // ConflictException → 409, que a tela de agendamento transforma em "escolha outro horário"
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*acabou de ser reservado*");
    }

    [Fact]
    public async Task Handle_TooManyRecentRequestsFromPhone_ThrowsInvalidOperationException()
    {
        var barberId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte", 30, 35.00m);
        SetupHappyPath(barberId, service);
        var client = ExistingClient("João", "+5511999990000");
        _appointmentRepository.CountCreatedByClientSinceAsync(client.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { Guid.NewGuid() }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*vários agendamentos*");
    }

    [Fact]
    public async Task Handle_ValidCommand_SnapshotsTheServicePriceOnTheAppointment()
    {
        var barberId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte moderno", 30, 35.00m);
        SetupHappyPath(barberId, service);
        Appointment? saved = null;
        _ = _appointmentRepository.AddAsync(Arg.Do<Appointment>(a => saved = a), Arg.Any<CancellationToken>());

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { service.Id }, null);
        await _handler.Handle(command, CancellationToken.None);

        // Reajuste depois do agendamento não pode mudar o que ele valeu
        service.Update("Corte", "Corte moderno", 30, 50.00m);

        saved.Should().NotBeNull();
        var line = saved!.AppointmentServices.Should().ContainSingle().Subject;
        line.ServiceId.Should().Be(service.Id);
        line.UnitPrice.Should().Be(35.00m);
    }

    private Client ExistingClient(string name, string phone)
    {
        var client = Client.Create(name, BrazilianPhone.Parse(phone), DateTime.UtcNow.AddDays(-60));
        _clientRepository.GetByMatchKeyAsync(client.MatchKey, Arg.Any<CancellationToken>()).Returns(client);
        return client;
    }

    [Fact]
    public async Task Handle_FirstBookingFromAPhone_CreatesTheClientFromThisBooking()
    {
        var barberId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte moderno", 30, 35.00m);
        SetupHappyPath(barberId, service);
        Client? newClient = null;
        _ = _clientRepository.AddAsync(Arg.Do<Client>(c => newClient = c), Arg.Any<CancellationToken>());
        Appointment? saved = null;
        _ = _appointmentRepository.AddAsync(Arg.Do<Appointment>(a => saved = a), Arg.Any<CancellationToken>());

        // Digitado sem o nono dígito e com máscara
        var command = new CreateAppointmentCommand(
            "João", "(11) 9999-0000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { service.Id }, null);
        await _handler.Handle(command, CancellationToken.None);

        newClient.Should().NotBeNull();
        newClient!.Name.Should().Be("João");
        newClient.Phone.Should().Be("+5511999990000");
        newClient.MatchKey.Should().Be("1199990000");
        newClient.VisitCount.Should().Be(0, "agendar não é visitar");
        saved!.ClientId.Should().Be(newClient.Id);
        saved.ClientPhone.Should().Be("+5511999990000", "o agendamento guarda a forma canônica");
        await _clientRepository.Received(1).GetByMatchKeyAsync("1199990000", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturningClientTypingANewName_KeepsTheFirstNameAndLinksTheBooking()
    {
        var barberId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte moderno", 30, 35.00m);
        SetupHappyPath(barberId, service);
        var client = ExistingClient("João", "+5511999990000");
        Appointment? saved = null;
        _ = _appointmentRepository.AddAsync(Arg.Do<Appointment>(a => saved = a), Arg.Any<CancellationToken>());

        var command = new CreateAppointmentCommand(
            "Joao Silva", "+55 11 9 9999-0000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { service.Id }, null);
        await _handler.Handle(command, CancellationToken.None);

        client.Name.Should().Be("João");
        saved!.ClientId.Should().Be(client.Id);
        saved.ClientName.Should().Be("Joao Silva", "o agendamento guarda o nome desta vez");
        await _clientRepository.DidNotReceive().AddAsync(Arg.Any<Client>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SamePersonTypingThePhoneDifferently_SharesThePerPhoneCap()
    {
        var barberId = Guid.NewGuid();
        var service = Service.Create("Corte", "Corte", 30, 35.00m);
        SetupHappyPath(barberId, service);
        var client = ExistingClient("João", "+5511999990000");
        _appointmentRepository.CountCreatedByClientSinceAsync(client.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(3);

        // Mesma pessoa, sem +55 e sem o nono dígito: não abre uma cota nova
        var command = new CreateAppointmentCommand(
            "João", "11 9999-0000", barberId, DateTime.UtcNow.AddDays(1), new List<Guid> { service.Id }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*vários agendamentos*");
        await _appointmentRepository.DidNotReceive().AddAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SlotTaken_DoesNotCreateAClient()
    {
        var barberId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var service = Service.Create("Corte", "Corte", 30, 35.00m);
        SetupHappyPath(barberId, service);
        var existingAppt = Appointment.Create("Maria", "+5511999990001", barberId, scheduledAt, 30, null, new[] { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(barberId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { existingAppt });

        var command = new CreateAppointmentCommand(
            "João", "+5511999990000", barberId, scheduledAt, new List<Guid> { service.Id }, null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        await _clientRepository.DidNotReceive().AddAsync(Arg.Any<Client>(), Arg.Any<CancellationToken>());
    }
}
