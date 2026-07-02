using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Services;

public record ActivateServiceCommand(Guid Id) : IRequest;

public class ActivateServiceCommandHandler : IRequestHandler<ActivateServiceCommand>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateServiceCommandHandler(IServiceRepository serviceRepository, IUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ActivateServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service {request.Id} not found.");
        service.Activate();
        await _serviceRepository.UpdateAsync(service, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
