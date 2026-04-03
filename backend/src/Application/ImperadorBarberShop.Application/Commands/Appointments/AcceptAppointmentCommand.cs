using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record AcceptAppointmentCommand(Guid AppointmentId, Guid BarberId) : IRequest;

public class AcceptAppointmentCommandValidator : AbstractValidator<AcceptAppointmentCommand>
{
    public AcceptAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BarberId).NotEmpty();
    }
}

public class AcceptAppointmentCommandHandler : IRequestHandler<AcceptAppointmentCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public AcceptAppointmentCommandHandler(
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

    public async Task Handle(AcceptAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (appointment.BarberId != request.BarberId)
            throw new UnauthorizedAccessException("You are not authorized to accept this appointment.");

        appointment.Accept();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var client = await _userRepository.GetByIdAsync(appointment.ClientId, cancellationToken);
        if (client is not null)
        {
            await _emailService.SendAppointmentAcceptedAsync(
                client.Email,
                client.Name,
                appointment.ScheduledAt,
                cancellationToken);
        }
    }
}
