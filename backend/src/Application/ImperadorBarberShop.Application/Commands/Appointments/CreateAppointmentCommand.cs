using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CreateAppointmentCommand(
    Guid ClientId,
    Guid BarberId,
    DateTime ScheduledAt,
    List<Guid> ServiceIds,
    string? Notes) : IRequest<Guid>;

public class CreateAppointmentCommandValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.BarberId).NotEmpty();
        RuleFor(x => x.ScheduledAt).GreaterThan(DateTime.UtcNow)
            .WithMessage("ScheduledAt must be in the future.");
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("At least one service is required.");
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
    }
}

public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, Guid>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppointmentCommandHandler(
        IBarberRepository barberRepository,
        IServiceRepository serviceRepository,
        IAppointmentRepository appointmentRepository,
        IEmailService emailService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _barberRepository = barberRepository;
        _serviceRepository = serviceRepository;
        _appointmentRepository = appointmentRepository;
        _emailService = emailService;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken);
        if (barber is null)
            throw new KeyNotFoundException($"Barber '{request.BarberId}' not found.");

        var services = await _serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        if (services.Count != request.ServiceIds.Count)
            throw new KeyNotFoundException("One or more services were not found.");

        // Check slot availability — ensure no overlap with existing appointments
        var date = DateOnly.FromDateTime(request.ScheduledAt);
        var activeAppointments = await _appointmentRepository.GetActiveByBarberIdAndDateAsync(
            request.BarberId, date, cancellationToken);

        var totalDuration = services.Sum(s => s.DurationMinutes);
        var requestEnd = request.ScheduledAt.AddMinutes(totalDuration);

        foreach (var existing in activeAppointments)
        {
            var existingEnd = existing.ScheduledAt.AddMinutes(existing.TotalDurationMinutes);
            if (request.ScheduledAt < existingEnd && requestEnd > existing.ScheduledAt)
                throw new InvalidOperationException("The requested time slot is not available.");
        }

        var appointment = Appointment.Create(
            request.ClientId,
            request.BarberId,
            request.ScheduledAt,
            totalDuration,
            request.Notes,
            request.ServiceIds);

        await _appointmentRepository.AddAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send notification to barber (best-effort — email failure must not roll back the appointment)
        var client = await _userRepository.GetByIdAsync(request.ClientId, cancellationToken);
        if (client is not null && barber.User is not null)
        {
            try
            {
                await _emailService.SendAppointmentCreatedAsync(
                    barber.User.Email,
                    barber.User.Name,
                    client.Name,
                    request.ScheduledAt,
                    cancellationToken);
            }
            catch
            {
                // Notification failure is non-critical; appointment is already persisted
            }
        }

        return appointment.Id;
    }
}
