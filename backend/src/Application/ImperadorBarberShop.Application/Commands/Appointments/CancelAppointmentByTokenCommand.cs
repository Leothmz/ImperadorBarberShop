using FluentValidation;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CancelAppointmentByTokenCommand(string AccessToken) : IRequest;

public class CancelAppointmentByTokenCommandValidator : AbstractValidator<CancelAppointmentByTokenCommand>
{
    public CancelAppointmentByTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
    }
}

public class CancelAppointmentByTokenCommandHandler : IRequestHandler<CancelAppointmentByTokenCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAppointmentByTokenCommandHandler(
        IAppointmentRepository appointmentRepository,
        IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CancelAppointmentByTokenCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByAccessTokenAsync(request.AccessToken, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException("Appointment not found for the given token.");

        // Rule: ScheduledAt must be MORE THAN 2 hours away; "exactly 2h" is not enough — use <=
        if (appointment.ScheduledAt - DateTime.UtcNow <= TimeSpan.FromHours(2))
            throw new InvalidOperationException("Cannot cancel an appointment within 2 hours of the scheduled time.");

        appointment.Cancel();
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
