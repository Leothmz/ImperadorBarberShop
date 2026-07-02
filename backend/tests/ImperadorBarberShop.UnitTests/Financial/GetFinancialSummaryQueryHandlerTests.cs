using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Financial;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Financial;

public class GetFinancialSummaryQueryHandlerTests
{
    private readonly IAppointmentRepository _repo = Substitute.For<IAppointmentRepository>();

    [Fact]
    public async Task Handle_NoAppointments_ReturnsZeros()
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());

        var handler = new GetFinancialSummaryQueryHandler(_repo);
        var query = new GetFinancialSummaryQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var result = await handler.Handle(query, CancellationToken.None);

        result.TotalRevenue.Should().Be(0);
        result.TotalAppointments.Should().Be(0);
        result.AverageTicket.Should().Be(0);
    }
}
