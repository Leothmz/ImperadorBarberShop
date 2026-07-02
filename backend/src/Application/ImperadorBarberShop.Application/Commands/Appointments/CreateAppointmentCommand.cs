using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

public record CreateAppointmentCommand(
    string ClientName,
    string ClientPhone,
    Guid BarberId,
    DateTime ScheduledAt,
    List<Guid> ServiceIds,
    string? Notes) : IRequest<CreateAppointmentResult>;

public record CreateAppointmentResult(Guid Id, string AccessToken);

public class CreateAppointmentCommandValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentCommandValidator()
    {
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ClientPhone).NotEmpty()
            .Matches(@"^\+55\d{11}$")
            .WithMessage("ClientPhone must be in the format +55DDDXXXXXXXXX.");
        RuleFor(x => x.BarberId).NotEmpty();
        RuleFor(x => x.ScheduledAt).GreaterThan(DateTime.UtcNow)
            .WithMessage("ScheduledAt must be in the future.");
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("At least one service is required.");
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
    }
}

public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, CreateAppointmentResult>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppointmentCommandHandler(
        IBarberRepository barberRepository,
        IServiceRepository serviceRepository,
        IAppointmentRepository appointmentRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        _barberRepository      = barberRepository;
        _serviceRepository     = serviceRepository;
        _appointmentRepository = appointmentRepository;
        _notificationService   = notificationService;
        _unitOfWork            = unitOfWork;
    }

    public async Task<CreateAppointmentResult> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        var barber = await _barberRepository.GetByIdAsync(request.BarberId, cancellationToken);
        if (barber is null)
            throw new KeyNotFoundException($"Barber '{request.BarberId}' not found.");

        var services = await _serviceRepository.GetByIdsAsync(request.ServiceIds, cancellationToken);
        if (services.Count != request.ServiceIds.Count)
            throw new KeyNotFoundException("One or more services were not found.");

        // Anti-spam: cap appointment creation per phone number, independent of the
        // per-IP rate limit applied at the HTTP layer (Task 11).
        var recentCount = await _appointmentRepository.CountCreatedByPhoneSinceAsync(
            request.ClientPhone, DateTime.UtcNow.AddHours(-1), cancellationToken);
        if (recentCount >= 3)
            throw new InvalidOperationException("Too many appointment requests from this phone number. Try again later.");

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
            request.ClientName,
            request.ClientPhone,
            request.BarberId,
            request.ScheduledAt,
            totalDuration,
            request.Notes,
            request.ServiceIds);

        await _appointmentRepository.AddAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send notification to barber (best-effort — failure must not roll back the appointment)
        try
        {
            await _notificationService.SendAppointmentCreatedAsync(
                appointment, barber, services, cancellationToken);
        }
        catch { /* best-effort */ }

        return new CreateAppointmentResult(appointment.Id, appointment.AccessToken);
    }
}
