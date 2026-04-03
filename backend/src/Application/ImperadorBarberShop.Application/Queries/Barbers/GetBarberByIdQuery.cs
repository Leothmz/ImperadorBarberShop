using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Barbers;

public record GetBarberByIdQuery(Guid BarberId) : IRequest<BarberDto>;

public class GetBarberByIdQueryHandler : IRequestHandler<GetBarberByIdQuery, BarberDto>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IMapper _mapper;

    public GetBarberByIdQueryHandler(IBarberRepository barberRepository, IMapper mapper)
    {
        _barberRepository = barberRepository;
        _mapper = mapper;
    }

    public async Task<BarberDto> Handle(GetBarberByIdQuery request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken);
        if (barber is null)
            throw new KeyNotFoundException($"Barber '{request.BarberId}' not found.");

        return _mapper.Map<BarberDto>(barber);
    }
}
