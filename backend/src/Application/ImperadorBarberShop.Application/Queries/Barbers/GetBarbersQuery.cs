using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Barbers;

public record GetBarbersQuery : IRequest<List<BarberDto>>;

public class GetBarbersQueryHandler : IRequestHandler<GetBarbersQuery, List<BarberDto>>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IMapper _mapper;

    public GetBarbersQueryHandler(IBarberRepository barberRepository, IMapper mapper)
    {
        _barberRepository = barberRepository;
        _mapper = mapper;
    }

    public async Task<List<BarberDto>> Handle(GetBarbersQuery request, CancellationToken cancellationToken)
    {
        var barbers = await _barberRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<BarberDto>>(barbers);
    }
}
