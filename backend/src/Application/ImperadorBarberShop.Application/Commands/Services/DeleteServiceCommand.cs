using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Services;

public record DeleteServiceCommand(Guid Id) : IRequest;

public class DeleteServiceCommandHandler : IRequestHandler<DeleteServiceCommand>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteServiceCommandHandler(
        IServiceRepository serviceRepository,
        IAppointmentRepository appointmentRepository,
        IUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Service {request.Id} not found.");

        var hasAppointments = await _appointmentRepository.AnyByServiceIdAsync(request.Id, cancellationToken);
        if (hasAppointments)
            throw new InvalidOperationException("Cannot delete a service that has associated appointments.");

        await _serviceRepository.DeleteAsync(request.Id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
