using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetFinancialSummaryQuery(DateOnly From, DateOnly To) : IRequest<FinancialSummaryDto>;

public class GetFinancialSummaryQueryHandler : IRequestHandler<GetFinancialSummaryQuery, FinancialSummaryDto>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetFinancialSummaryQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<FinancialSummaryDto> Handle(GetFinancialSummaryQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);
        var total = appointments.Count;
        var revenue = appointments
            .SelectMany(a => a.AppointmentServices)
            .Sum(s => s.Service.Price);
        var average = total > 0 ? revenue / total : 0m;

        return new FinancialSummaryDto(revenue, total, Math.Round(average, 2), request.From, request.To);
    }
}
