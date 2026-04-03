using FluentValidation;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CancelAppointmentCommand(Guid AppointmentId, Guid ClientId) : IRequest;

public class CancelAppointmentCommandValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty();
    }
}

public class CancelAppointmentCommandHandler : IRequestHandler<CancelAppointmentCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAppointmentCommandHandler(
        IAppointmentRepository appointmentRepository,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        // Security: only the client who created the appointment can cancel it
        if (appointment.ClientId != request.ClientId)
            throw new UnauthorizedAccessException("You are not authorized to cancel this appointment.");

        // Business rule: cannot cancel within 2 hours of scheduled time
        if (appointment.ScheduledAt - DateTime.UtcNow < TimeSpan.FromHours(2))
            throw new InvalidOperationException("Cannot cancel an appointment within 2 hours of the scheduled time.");

        appointment.Cancel();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
