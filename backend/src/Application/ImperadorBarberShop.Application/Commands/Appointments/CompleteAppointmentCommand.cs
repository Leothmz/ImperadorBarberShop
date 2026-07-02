using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CompleteAppointmentCommand(Guid AppointmentId, Guid BarberId, PaymentMethod? PaymentMethod = null) : IRequest;

public class CompleteAppointmentCommandValidator : AbstractValidator<CompleteAppointmentCommand>
{
    public CompleteAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BarberId).NotEmpty();
    }
}

public class CompleteAppointmentCommandHandler : IRequestHandler<CompleteAppointmentCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteAppointmentCommandHandler(
        IAppointmentRepository appointmentRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _notificationService   = notificationService;
        _unitOfWork            = unitOfWork;
    }

    public async Task Handle(CompleteAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (appointment.BarberId != request.BarberId)
            throw new ForbiddenException("You are not authorized to complete this appointment.");

        appointment.Complete(request.PaymentMethod);
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _notificationService.SendAppointmentCompletedAsync(appointment, cancellationToken);
        }
        catch { /* best-effort */ }
    }
}
