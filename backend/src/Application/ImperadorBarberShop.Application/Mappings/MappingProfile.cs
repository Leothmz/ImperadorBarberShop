using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Service, ServiceDto>();

        CreateMap<Barber, BarberDto>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.User.Name))
            .ForMember(d => d.Email, o => o.MapFrom(s => s.User.Email))
            .ForMember(d => d.Availability, o => o.MapFrom(s => s.Availability));

        CreateMap<BarberAvailability, BarberAvailabilityDto>();

        CreateMap<Appointment, AppointmentDto>()
            .ForMember(d => d.BarberName, o => o.MapFrom(s => s.Barber.User.Name))
            .ForMember(d => d.Services, o => o.MapFrom(s => s.AppointmentServices.Select(a => a.Service)));

        CreateMap<Appointment, AppointmentManageDto>()
            .ForMember(d => d.BarberName, o => o.MapFrom(s => s.Barber.User.Name))
            .ForMember(d => d.Services, o => o.MapFrom(s => s.AppointmentServices.Select(a => a.Service)));

        CreateMap<Review, ReviewDto>();
    }
}
