using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Admin;

public record DeleteBarberByAdminCommand(Guid BarberId) : IRequest;

public class DeleteBarberByAdminCommandHandler : IRequestHandler<DeleteBarberByAdminCommand>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteBarberByAdminCommandHandler(
        IBarberRepository barberRepository,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAppointmentRepository appointmentRepository,
        IUnitOfWork unitOfWork)
    {
        _barberRepository = barberRepository;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteBarberByAdminCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken)
            ?? throw new KeyNotFoundException($"Barber {request.BarberId} not found.");

        var hasAppointments = await _appointmentRepository.AnyByBarberIdAsync(request.BarberId, cancellationToken);
        if (hasAppointments)
            throw new InvalidOperationException("Cannot delete a barber that has associated appointments.");

        var userId = barber.UserId;
        await _refreshTokenRepository.DeleteByUserIdAsync(userId, cancellationToken);
        await _barberRepository.DeleteAsync(request.BarberId, cancellationToken);
        await _userRepository.DeleteAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
