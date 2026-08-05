using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CancelAppointmentByBarberCommand(
    Guid AppointmentId,
    Guid? RequesterBarberId)   // null = admin, bypasses IDOR
    : IRequest;

public class CancelAppointmentByBarberCommandValidator : AbstractValidator<CancelAppointmentByBarberCommand>
{
    public CancelAppointmentByBarberCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.RequesterBarberId).NotEmpty().When(x => x.RequesterBarberId.HasValue);
    }
}

public class CancelAppointmentByBarberCommandHandler : IRequestHandler<CancelAppointmentByBarberCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationQueue _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAppointmentByBarberCommandHandler(
        IAppointmentRepository appointmentRepository,
        INotificationQueue notifications,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _notifications         = notifications;
        _unitOfWork            = unitOfWork;
    }

    public async Task Handle(CancelAppointmentByBarberCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (request.RequesterBarberId.HasValue && appointment.BarberId != request.RequesterBarberId)
            throw new ForbiddenException("You are not authorized to cancel this appointment.");

        appointment.Cancel();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fora do ciclo da requisição: SMTP/WhatsApp lento não segura a resposta
        _notifications.Enqueue((n, ct) => n.SendAppointmentCancelledAsync(appointment, ct));
    }
}
