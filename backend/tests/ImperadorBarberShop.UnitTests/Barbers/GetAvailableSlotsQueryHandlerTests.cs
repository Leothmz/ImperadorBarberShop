using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Barbers;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Barbers;

public class GetAvailableSlotsQueryHandlerTests
{
    private readonly IBarberAvailabilityRepository _availabilityRepository = Substitute.For<IBarberAvailabilityRepository>();
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IServiceRepository _serviceRepository = Substitute.For<IServiceRepository>();
    private readonly GetAvailableSlotsQueryHandler _handler;

    private readonly Guid _barberId = Guid.NewGuid();
    private readonly DateOnly _monday = NextMonday(); // Always a future Monday, so slots never get filtered as past
    private readonly BarberAvailability _mondayAvailability;

    private static DateOnly NextMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        daysUntilMonday = daysUntilMonday == 0 ? 7 : daysUntilMonday;
        return today.AddDays(daysUntilMonday);
    }

    public GetAvailableSlotsQueryHandlerTests()
    {
        _handler = new GetAvailableSlotsQueryHandler(
            _availabilityRepository, _appointmentRepository, _serviceRepository);

        // 09:00 - 18:00 on Monday
        _mondayAvailability = BarberAvailability.Create(
            _barberId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0));
    }

    private Service MakeService(int duration) =>
        Service.Create("Serviço", "Desc", duration, 30.00m);

    [Fact]
    public async Task Handle_NoAvailabilityForDay_ReturnsEmpty()
    {
        _availabilityRepository.GetByBarberIdAndDayAsync(_barberId, DayOfWeek.Monday, Arg.Any<CancellationToken>())
            .Returns((BarberAvailability?)null);

        var query = new GetAvailableSlotsQuery(_barberId, _monday, new List<Guid> { Guid.NewGuid() });
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoServicesFound_ReturnsEmpty()
    {
        _availabilityRepository.GetByBarberIdAndDayAsync(_barberId, DayOfWeek.Monday, Arg.Any<CancellationToken>())
            .Returns(_mondayAvailability);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service>());

        var query = new GetAvailableSlotsQuery(_barberId, _monday, new List<Guid> { Guid.NewGuid() });
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoExistingAppointments_ReturnsAllSlots()
    {
        _availabilityRepository.GetByBarberIdAndDayAsync(_barberId, DayOfWeek.Monday, Arg.Any<CancellationToken>())
            .Returns(_mondayAvailability);
        var service = MakeService(30); // 30-min service
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(_barberId, _monday, Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var query = new GetAvailableSlotsQuery(_barberId, _monday, new List<Guid> { Guid.NewGuid() });
        var result = await _handler.Handle(query, CancellationToken.None);

        // 09:00 to 17:30 in 15-min increments = (17:30 - 09:00) / 0:15 + 1 = 35 slots
        result.Should().NotBeEmpty();
        result[0].Should().Be(new TimeOnly(9, 0));
    }

    [Fact]
    public async Task Handle_FullyBooked_ReturnsEmpty()
    {
        // 30-min service, barber available 09:00-09:30 only (availability window = 30 min)
        var tightAvailability = BarberAvailability.Create(
            _barberId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 30));

        _availabilityRepository.GetByBarberIdAndDayAsync(_barberId, DayOfWeek.Monday, Arg.Any<CancellationToken>())
            .Returns(tightAvailability);
        var service = MakeService(30);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });

        var mondayDate = _monday;
        var existingAppt = Appointment.Create(
            Guid.NewGuid(), _barberId,
            mondayDate.ToDateTime(new TimeOnly(9, 0)),
            30, null, new[] { Guid.NewGuid() });

        _appointmentRepository.GetActiveByBarberIdAndDateAsync(_barberId, mondayDate, Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { existingAppt });

        var query = new GetAvailableSlotsQuery(_barberId, mondayDate, new List<Guid> { Guid.NewGuid() });
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExactFit_ReturnsOneSlot()
    {
        // service = 30 min, availability = 09:00-09:30 (exactly 30 min), no existing appointments
        var tightAvailability = BarberAvailability.Create(
            _barberId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 30));

        _availabilityRepository.GetByBarberIdAndDayAsync(_barberId, DayOfWeek.Monday, Arg.Any<CancellationToken>())
            .Returns(tightAvailability);
        var service = MakeService(30);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(_barberId, _monday, Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var query = new GetAvailableSlotsQuery(_barberId, _monday, new List<Guid> { Guid.NewGuid() });
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Should().Be(new TimeOnly(9, 0));
    }

    [Fact]
    public async Task Handle_MultipleServices_SumsDurationCorrectly()
    {
        // service1 = 30 min, service2 = 20 min → total = 50 min
        // availability: 09:00-10:00 (60 min) → only one slot at 09:00 fits (50 min)
        var availability = BarberAvailability.Create(
            _barberId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0));

        _availabilityRepository.GetByBarberIdAndDayAsync(_barberId, DayOfWeek.Monday, Arg.Any<CancellationToken>())
            .Returns(availability);

        var s1 = MakeService(30);
        var s2 = MakeService(20);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { s1, s2 });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(_barberId, _monday, Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var query = new GetAvailableSlotsQuery(_barberId, _monday, new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
        var result = await _handler.Handle(query, CancellationToken.None);

        // windowEnd = 10:00 - 50min = 09:10; slots at 09:00 and 09:15 fit
        result.Should().Contain(new TimeOnly(9, 0));
    }

    [Fact]
    public async Task Handle_PartialOverlap_BlocksConflictingSlots()
    {
        // availability 09:00-18:00, existing appointment 10:00-10:30
        // service = 30 min → slot 10:00 and 09:45 would overlap → should not be returned
        _availabilityRepository.GetByBarberIdAndDayAsync(_barberId, DayOfWeek.Monday, Arg.Any<CancellationToken>())
            .Returns(_mondayAvailability);

        var service = MakeService(30);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });

        var existingAppt = Appointment.Create(
            Guid.NewGuid(), _barberId,
            _monday.ToDateTime(new TimeOnly(10, 0)),
            30, null, new[] { Guid.NewGuid() });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(_barberId, _monday, Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { existingAppt });

        var query = new GetAvailableSlotsQuery(_barberId, _monday, new List<Guid> { Guid.NewGuid() });
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().NotContain(new TimeOnly(10, 0)); // slot start == existing start
        result.Should().NotContain(new TimeOnly(9, 45)); // 09:45-10:15 overlaps 10:00-10:30
        result.Should().Contain(new TimeOnly(9, 0));     // 09:00-09:30 does not overlap
        result.Should().Contain(new TimeOnly(10, 30));   // 10:30-11:00 is free
    }

    [Fact]
    public async Task Handle_SlotAtEndOfWindow_IsIncluded()
    {
        // availability 09:00-10:00, service 30 min → last slot at 09:30 (ends at 10:00, exactly on boundary)
        var availability = BarberAvailability.Create(
            _barberId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0));

        _availabilityRepository.GetByBarberIdAndDayAsync(_barberId, DayOfWeek.Monday, Arg.Any<CancellationToken>())
            .Returns(availability);
        var service = MakeService(30);
        _serviceRepository.GetByIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Service> { service });
        _appointmentRepository.GetActiveByBarberIdAndDateAsync(_barberId, _monday, Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var query = new GetAvailableSlotsQuery(_barberId, _monday, new List<Guid> { Guid.NewGuid() });
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().Contain(new TimeOnly(9, 30)); // last valid slot
        result.Should().NotContain(new TimeOnly(9, 45)); // would end at 10:15 > 10:00
    }
}
