using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Services;

public record DeactivateServiceCommand(Guid Id) : IRequest;

public class DeactivateServiceCommandHandler : IRequestHandler<DeactivateServiceCommand>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateServiceCommandHandler(IServiceRepository serviceRepository, IUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeactivateServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service {request.Id} not found.");
        service.Deactivate();
        await _serviceRepository.UpdateAsync(service, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
