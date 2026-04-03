using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record RejectAppointmentCommand(Guid AppointmentId, Guid BarberId) : IRequest;

public class RejectAppointmentCommandValidator : AbstractValidator<RejectAppointmentCommand>
{
    public RejectAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BarberId).NotEmpty();
    }
}

public class RejectAppointmentCommandHandler : IRequestHandler<RejectAppointmentCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public RejectAppointmentCommandHandler(
        IAppointmentRepository appointmentRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RejectAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (appointment.BarberId != request.BarberId)
            throw new ForbiddenException("You are not authorized to reject this appointment.");

        appointment.Reject();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var client = await _userRepository.GetByIdAsync(appointment.ClientId, cancellationToken);
        if (client is not null)
        {
            await _emailService.SendAppointmentRejectedAsync(
                client.Email,
                client.Name,
                appointment.ScheduledAt,
                cancellationToken);
        }
    }
}
