using FluentValidation;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetFinancialTimelineQuery(DateOnly From, DateOnly To, string GroupBy = "day")
    : IRequest<List<FinancialTimelineItemDto>>;

public class GetFinancialTimelineQueryValidator : AbstractValidator<GetFinancialTimelineQuery>
{
    private static readonly string[] ValidGroupBy = ["day", "week", "month"];

    public GetFinancialTimelineQueryValidator()
    {
        RuleFor(x => x.GroupBy).Must(g => ValidGroupBy.Contains(g))
            .WithMessage("groupBy deve ser 'day', 'week' ou 'month'.");
    }
}

public class GetFinancialTimelineQueryHandler : IRequestHandler<GetFinancialTimelineQuery, List<FinancialTimelineItemDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetFinancialTimelineQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<List<FinancialTimelineItemDto>> Handle(
        GetFinancialTimelineQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);

        var grouped = appointments
            .GroupBy(a => GetPeriodKey(a.ScheduledAt, request.GroupBy))
            .OrderBy(g => g.Key)
            .Select(g => new FinancialTimelineItemDto(
                g.Key,
                g.SelectMany(a => a.AppointmentServices).Sum(s => s.Service?.Price ?? 0m),
                g.Count()))
            .ToList();

        return grouped;
    }

    private static string GetPeriodKey(DateTime scheduledAt, string groupBy) => groupBy switch
    {
        "month" => new DateOnly(scheduledAt.Year, scheduledAt.Month, 1).ToString("yyyy-MM-dd"),
        "week"  => GetMondayOfWeek(DateOnly.FromDateTime(scheduledAt)).ToString("yyyy-MM-dd"),
        _       => DateOnly.FromDateTime(scheduledAt).ToString("yyyy-MM-dd"),  // "day"
    };

    private static DateOnly GetMondayOfWeek(DateOnly date)
    {
        int daysFromMonday = ((int)date.DayOfWeek - 1 + 7) % 7;
        return date.AddDays(-daysFromMonday);
    }
}
