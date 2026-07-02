using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Admin;

public record GetAdminBarbersQuery : IRequest<List<AdminBarberDto>>;

public class GetAdminBarbersQueryHandler : IRequestHandler<GetAdminBarbersQuery, List<AdminBarberDto>>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IMapper _mapper;

    public GetAdminBarbersQueryHandler(IBarberRepository barberRepository, IMapper mapper)
    {
        _barberRepository = barberRepository;
        _mapper = mapper;
    }

    public async Task<List<AdminBarberDto>> Handle(GetAdminBarbersQuery request, CancellationToken cancellationToken)
    {
        var barbers = await _barberRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<AdminBarberDto>>(barbers);
    }
}
