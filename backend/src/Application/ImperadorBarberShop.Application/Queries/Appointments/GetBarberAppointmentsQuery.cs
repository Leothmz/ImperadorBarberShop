using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Appointments;

public record GetBarberAppointmentsQuery(Guid BarberId) : IRequest<List<AppointmentDto>>;

public class GetBarberAppointmentsQueryHandler : IRequestHandler<GetBarberAppointmentsQuery, List<AppointmentDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;

    public GetBarberAppointmentsQueryHandler(IAppointmentRepository appointmentRepository, IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
    }

    public async Task<List<AppointmentDto>> Handle(GetBarberAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var appointments = await _appointmentRepository.GetByBarberIdAsync(request.BarberId, cancellationToken);
        return _mapper.Map<List<AppointmentDto>>(appointments);
    }
}
