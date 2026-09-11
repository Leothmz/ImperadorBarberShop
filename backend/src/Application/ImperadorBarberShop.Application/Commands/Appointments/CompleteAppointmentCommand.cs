using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CompleteAppointmentCommand(
    Guid AppointmentId,
    Guid? RequesterBarberId,   // null = admin, bypasses IDOR
    PaymentMethod? PaymentMethod = null)
    : IRequest;

public class CompleteAppointmentCommandValidator : AbstractValidator<CompleteAppointmentCommand>
{
    public CompleteAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.RequesterBarberId).NotEmpty().When(x => x.RequesterBarberId.HasValue);
        RuleFor(x => x.PaymentMethod).IsInEnum().When(x => x.PaymentMethod.HasValue);
    }
}

public class CompleteAppointmentCommandHandler : IRequestHandler<CompleteAppointmentCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IClientRepository _clientRepository;
    private readonly INotificationQueue _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteAppointmentCommandHandler(
        IAppointmentRepository appointmentRepository,
        IClientRepository clientRepository,
        INotificationQueue notifications,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _clientRepository = clientRepository;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CompleteAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (request.RequesterBarberId.HasValue && appointment.BarberId != request.RequesterBarberId)
            throw new ForbiddenException("You are not authorized to complete this appointment.");

        appointment.Complete(request.PaymentMethod);
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);

        // Só atendimento concluído conta visita: é o que a recorrência mede
        if (appointment.ClientId is { } clientId
            && await _clientRepository.GetByIdAsync(clientId, cancellationToken) is { } client)
        {
            client.RegisterVisit(appointment.ScheduledAt);
            await _clientRepository.UpdateAsync(client, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fora do ciclo da requisição: SMTP/WhatsApp lento não segura a resposta
        _notifications.Enqueue((n, ct) => n.SendAppointmentCompletedAsync(appointment, ct));
    }
}
