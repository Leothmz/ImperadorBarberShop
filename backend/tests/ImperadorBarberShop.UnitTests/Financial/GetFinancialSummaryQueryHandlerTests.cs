using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Financial;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Financial;

public class GetFinancialSummaryQueryHandlerTests
{
    private readonly IAppointmentRepository _repo = Substitute.For<IAppointmentRepository>();
    private readonly IExpenseRepository _expenseRepo = Substitute.For<IExpenseRepository>();

    [Fact]
    public async Task Handle_NoAppointments_ReturnsZeros()
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());
        _expenseRepo.GetTotalByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(0m);

        var handler = new GetFinancialSummaryQueryHandler(_repo, _expenseRepo);
        var query = new GetFinancialSummaryQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var result = await handler.Handle(query, CancellationToken.None);

        result.TotalRevenue.Should().Be(0);
        result.TotalAppointments.Should().Be(0);
        result.AverageTicket.Should().Be(0);
        result.TotalExpenses.Should().Be(0);
        result.NetRevenue.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithExpenses_ComputesNetRevenue()
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Appointment>());
        _expenseRepo.GetTotalByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(200m);

        var handler = new GetFinancialSummaryQueryHandler(_repo, _expenseRepo);
        var result = await handler.Handle(
            new GetFinancialSummaryQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)),
            CancellationToken.None);

        result.TotalExpenses.Should().Be(200m);
        result.NetRevenue.Should().Be(-200m);
    }
}
