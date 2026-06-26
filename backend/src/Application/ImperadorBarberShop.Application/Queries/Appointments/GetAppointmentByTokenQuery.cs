using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Appointments;

public record GetAppointmentByTokenQuery(string AccessToken) : IRequest<AppointmentManageDto>;

public class GetAppointmentByTokenQueryHandler : IRequestHandler<GetAppointmentByTokenQuery, AppointmentManageDto>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IMapper _mapper;

    public GetAppointmentByTokenQueryHandler(IAppointmentRepository appointmentRepository, IMapper mapper)
    {
        _appointmentRepository = appointmentRepository;
        _mapper = mapper;
    }

    public async Task<AppointmentManageDto> Handle(GetAppointmentByTokenQuery request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByAccessTokenAsync(request.AccessToken, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException("Appointment not found for the given token.");

        return _mapper.Map<AppointmentManageDto>(appointment);
    }
}
