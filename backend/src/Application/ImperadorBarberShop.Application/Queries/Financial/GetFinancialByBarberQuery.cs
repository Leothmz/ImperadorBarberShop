using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Financial;

public record GetFinancialByBarberQuery(DateOnly From, DateOnly To) : IRequest<List<FinancialByBarberItemDto>>;

public class GetFinancialByBarberQueryHandler : IRequestHandler<GetFinancialByBarberQuery, List<FinancialByBarberItemDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;

    public GetFinancialByBarberQueryHandler(IAppointmentRepository appointmentRepository)
        => _appointmentRepository = appointmentRepository;

    public async Task<List<FinancialByBarberItemDto>> Handle(GetFinancialByBarberQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var appointments = await _appointmentRepository.GetCompletedByDateRangeAsync(from, to, cancellationToken);

        return appointments
            .GroupBy(a => new { a.BarberId, Name = a.Barber.User.Name })
            .Select(g => new FinancialByBarberItemDto(
                g.Key.BarberId,
                g.Key.Name,
                g.Count(),
                g.SelectMany(a => a.AppointmentServices).Sum(s => s.Service.Price)))
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }
}
