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

        return appointments
            .SelectMany(a => a.AppointmentServices)
            .GroupBy(aps => new { aps.ServiceId, Name = aps.Service.Name })
            .Select(g => new FinancialByServiceItemDto(
                g.Key.ServiceId,
                g.Key.Name,
                g.Count(),
                g.Sum(aps => aps.Service.Price)))
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }
}
