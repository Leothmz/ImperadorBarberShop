using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record ActivateBarberCommand(Guid BarberId) : IRequest;

public class ActivateBarberCommandHandler : IRequestHandler<ActivateBarberCommand>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateBarberCommandHandler(IBarberRepository barberRepository, IUnitOfWork unitOfWork)
    {
        _barberRepository = barberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ActivateBarberCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken)
            ?? throw new KeyNotFoundException($"Barber {request.BarberId} not found.");
        barber.Activate();
        await _barberRepository.UpdateAsync(barber, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
