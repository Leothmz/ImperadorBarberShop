using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Services;

public record AddServiceAddonCommand(Guid ParentServiceId, Guid AddonServiceId) : IRequest;

public class AddServiceAddonCommandHandler : IRequestHandler<AddServiceAddonCommand>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddonRepository _addonRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddServiceAddonCommandHandler(
        IServiceRepository serviceRepository,
        IServiceAddonRepository addonRepository,
        IUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _addonRepository = addonRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AddServiceAddonCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentServiceId == request.AddonServiceId)
            throw new ArgumentException("A service cannot be its own add-on.");

        _ = await _serviceRepository.GetByIdAsync(request.ParentServiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service {request.ParentServiceId} not found.");

        _ = await _serviceRepository.GetByIdAsync(request.AddonServiceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Service {request.AddonServiceId} not found.");

        var existing = await _addonRepository.GetAsync(
            request.ParentServiceId, request.AddonServiceId, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("This add-on link already exists.");

        var addon = ServiceAddon.Create(request.ParentServiceId, request.AddonServiceId);
        await _addonRepository.AddAsync(addon, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
