using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetFinancialByServiceQuery(DateOnly From, DateOnly To) : IRequest<List<FinancialByServiceItemDto>>;

public class GetFinancialByServiceQueryHandler : IRequestHandler<GetFinancialByServiceQuery, List<FinancialByServiceItemDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetFinancialByServiceQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<List<FinancialByServiceItemDto>> Handle(GetFinancialByServiceQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);

        var rows = appointments
            .Where(a => !a.IsPlan)
            .SelectMany(a => a.AppointmentServices)
            .GroupBy(aps => new { aps.ServiceId, Name = aps.Service.Name })
            .Select(g => new FinancialByServiceItemDto(
                g.Key.ServiceId,
                g.Key.Name,
                g.Count(),
                g.Sum(aps => aps.UnitPrice)))
            .ToList();

        // Plano entra uma vez por atendimento, pelo valor cobrado: somar também os serviços dele
        // contaria o mesmo atendimento duas vezes. Os serviços seguem visíveis no atendimento.
        var plans = appointments.Where(a => a.IsPlan).ToList();
        if (plans.Count > 0)
            rows.Add(new FinancialByServiceItemDto(
                FinancialByServiceItemDto.PlanServiceId,
                FinancialByServiceItemDto.PlanServiceName,
                plans.Count,
                plans.Sum(a => a.EffectiveAmount)));

        return rows.OrderByDescending(x => x.Revenue).ToList();
    }
}
