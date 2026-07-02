using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Services;

public record GetServicesQuery(bool IncludeInactive = false) : IRequest<List<ServiceDto>>;

public class GetServicesQueryHandler : IRequestHandler<GetServicesQuery, List<ServiceDto>>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddonRepository _addonRepository;
    private readonly IMapper _mapper;

    public GetServicesQueryHandler(
        IServiceRepository serviceRepository,
        IServiceAddonRepository addonRepository,
        IMapper mapper)
    {
        _serviceRepository = serviceRepository;
        _addonRepository = addonRepository;
        _mapper = mapper;
    }

    public async Task<List<ServiceDto>> Handle(GetServicesQuery request, CancellationToken cancellationToken)
    {
        var services = request.IncludeInactive
            ? await _serviceRepository.GetAllAsync(cancellationToken)
            : await _serviceRepository.GetAllActiveAsync(cancellationToken);
        var serviceIds = services.Select(s => s.Id).ToList();
        var allAddons = await _addonRepository.GetByParentIdsAsync(serviceIds, cancellationToken);
        var addonsByParent = allAddons
            .GroupBy(a => a.ParentServiceId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.AddonService).ToList());

        return services.Select(s =>
        {
            var dto = _mapper.Map<ServiceDto>(s);
            var addons = addonsByParent.TryGetValue(s.Id, out var list)
                ? list.Select(a => _mapper.Map<ServiceDto>(a) with { Addons = [] }).ToList()
                : new List<ServiceDto>();
            return dto with { Addons = addons };
        }).ToList();
    }
}
