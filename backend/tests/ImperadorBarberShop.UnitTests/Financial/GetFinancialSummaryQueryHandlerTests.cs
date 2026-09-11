using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Financial;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
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

    private static Appointment Completed(AppointmentPayment? payment, params (string Name, decimal Price)[] services)
    {
        var appt = Appointment.Create("João", "+5511999990000", Guid.NewGuid(), new DateTime(2026, 7, 10, 10, 0, 0), 30, null,
            services.Select(s => Service.Create(s.Name, "Desc", 30, s.Price)));
        appt.Complete(payment);
        return appt;
    }

    private void Returns(List<Appointment> appointments, decimal expenses = 0m)
    {
        _repo.GetCompletedByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(appointments);
        _expenseRepo.GetTotalByDateRangeAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(expenses);
    }

    [Fact]
    public async Task Handle_PlanAppointments_CountInRevenueAndVisitsButNotInTheAverageTicket()
    {
        Returns(
        [
            Completed(AppointmentPayment.Normal(PaymentMethod.Pix), ("Corte", 35m), ("Barba", 25m)),
            Completed(null, ("Corte", 35m)),
            Completed(AppointmentPayment.PlanPayment(150m, PaymentMethod.Cartão), ("Corte", 35m), ("Barba", 25m)),
            Completed(AppointmentPayment.PlanRecurrence(), ("Corte", 35m)),
        ], expenses: 45m);

        var result = await new GetFinancialSummaryQueryHandler(_repo, _expenseRepo).Handle(
            new GetFinancialSummaryQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)), CancellationToken.None);

        result.TotalRevenue.Should().Be(245m, "60 + 35 avulsos, 150 cobrados no plano, 0 da recorrência");
        result.TotalAppointments.Should().Be(4, "a recorrência também é atendimento concluído");
        result.AverageTicket.Should().Be(47.50m, "só os avulsos: (60 + 35) / 2");
        result.NetRevenue.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_OnlyPlanAppointments_AverageTicketIsZero()
    {
        Returns(
        [
            Completed(AppointmentPayment.PlanPayment(150m, PaymentMethod.Pix), ("Corte", 35m)),
            Completed(AppointmentPayment.PlanRecurrence(), ("Corte", 35m)),
        ]);

        var result = await new GetFinancialSummaryQueryHandler(_repo, _expenseRepo).Handle(
            new GetFinancialSummaryQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)), CancellationToken.None);

        result.TotalRevenue.Should().Be(150m);
        result.TotalAppointments.Should().Be(2);
        result.AverageTicket.Should().Be(0m);
    }
}
