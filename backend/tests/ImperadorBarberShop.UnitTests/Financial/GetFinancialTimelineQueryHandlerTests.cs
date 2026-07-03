using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Financial;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Financial;

public class GetFinancialTimelineQueryHandlerTests
{
    private readonly IAppointmentRepository _repo = Substitute.For<IAppointmentRepository>();

    private static Appointment MakeCompleted(DateTime scheduledAt)
    {
        var appt = Appointment.Create("João", "+55119", Guid.NewGuid(),
            scheduledAt, 30, null, [Guid.NewGuid()]);
        appt.Complete();
        return appt;
    }

    [Fact]
    public async Task Handle_GroupByDay_GroupsCorrectly()
    {
        var day1 = new DateTime(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc);
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { MakeCompleted(day1), MakeCompleted(day2) });

        var handler = new GetFinancialTimelineQueryHandler(_repo);
        var result = await handler.Handle(
            new GetFinancialTimelineQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "day"),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Period.Should().Be("2026-07-10");
        result[1].Period.Should().Be("2026-07-11");
    }

    [Fact]
    public async Task Handle_GroupByMonth_GroupsCorrectly()
    {
        var julyDate = new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);
        var augustDate = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment> { MakeCompleted(julyDate), MakeCompleted(augustDate) });

        var handler = new GetFinancialTimelineQueryHandler(_repo);
        var result = await handler.Handle(
            new GetFinancialTimelineQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 31), "month"),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Period.Should().Be("2026-07-01");
        result[1].Period.Should().Be("2026-08-01");
    }

    [Fact]
    public async Task Handle_NoAppointments_ReturnsEmpty()
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var handler = new GetFinancialTimelineQueryHandler(_repo);
        var result = await handler.Handle(
            new GetFinancialTimelineQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "day"),
            CancellationToken.None);

        result.Should().BeEmpty();
    }
}
