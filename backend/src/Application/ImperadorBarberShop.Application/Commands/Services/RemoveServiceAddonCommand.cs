using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Services;

public record RemoveServiceAddonCommand(Guid ParentServiceId, Guid AddonServiceId) : IRequest;

public class RemoveServiceAddonCommandHandler : IRequestHandler<RemoveServiceAddonCommand>
{
    private readonly IServiceAddonRepository _addonRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveServiceAddonCommandHandler(
        IServiceAddonRepository addonRepository,
        IUnitOfWork unitOfWork)
    {
        _addonRepository = addonRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RemoveServiceAddonCommand request, CancellationToken cancellationToken)
    {
        var addon = await _addonRepository.GetAsync(
            request.ParentServiceId, request.AddonServiceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Add-on link between {request.ParentServiceId} and {request.AddonServiceId} not found.");
        _addonRepository.Remove(addon);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
