using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record DeactivateBarberCommand(Guid BarberId) : IRequest;

public class DeactivateBarberCommandHandler : IRequestHandler<DeactivateBarberCommand>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateBarberCommandHandler(IBarberRepository barberRepository, IUnitOfWork unitOfWork)
    {
        _barberRepository = barberRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeactivateBarberCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken)
            ?? throw new KeyNotFoundException($"Barber {request.BarberId} not found.");
        barber.Deactivate();
        await _barberRepository.UpdateAsync(barber, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
