using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Appointments;

public record GetClientAppointmentsQuery(Guid ClientId) : IRequest<List<AppointmentDto>>;

public class GetClientAppointmentsQueryHandler : IRequestHandler<GetClientAppointmentsQuery, List<AppointmentDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;

    public GetClientAppointmentsQueryHandler(IAppointmentRepository appointmentRepository, IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
    }

    public async Task<List<AppointmentDto>> Handle(GetClientAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var appointments = await _appointmentRepository.GetByClientIdAsync(request.ClientId, cancellationToken);
        return _mapper.Map<List<AppointmentDto>>(appointments);
    }
}
