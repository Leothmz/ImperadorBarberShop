using FluentValidation;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
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
    public CreateAppointmentCommandValidator(TimeProvider clock)
    {
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(100);
        // Aceita o número do jeito que o cliente digitou; o handler guarda a forma canônica
        RuleFor(x => x.ClientPhone)
            .Must(phone => BrazilianPhone.TryParse(phone, out _))
            .WithMessage(BrazilianPhone.InvalidMessage);
        RuleFor(x => x.BarberId).NotEmpty();
        // ScheduledAt é horário de parede da barbearia: compara com o "agora" dela, não com UTC
        RuleFor(x => x.ScheduledAt).Must(scheduledAt => scheduledAt > clock.GetLocalNow().DateTime)
            .WithMessage("O horário escolhido já passou. Escolha um horário futuro.");
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("At least one service is required.");
        RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
    }
}

public class CreateAppointmentCommandHandler : IRequestHandler<CreateAppointmentCommand, CreateAppointmentResult>
{
    private readonly IBarberRepository _barberRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IClientRepository _clientRepository;
    private readonly INotificationQueue _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppointmentCommandHandler(
        IBarberRepository barberRepository,
        IServiceRepository serviceRepository,
        IAppointmentRepository appointmentRepository,
        IClientRepository clientRepository,
        INotificationQueue notifications,
        IUnitOfWork unitOfWork)
    {
        _barberRepository      = barberRepository;
        _serviceRepository     = serviceRepository;
        _appointmentRepository = appointmentRepository;
        _clientRepository      = clientRepository;
        _notifications         = notifications;
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

        var phone = BrazilianPhone.Parse(request.ClientPhone);
        var client = await _clientRepository.GetByMatchKeyAsync(phone.MatchKey, cancellationToken);

        // Anti-spam: cap appointment creation per phone number, independent of the
        // per-IP rate limit applied at the HTTP layer (Task 11). Counts the person, not
        // the typing: "11 9999-0000" and "+5511999990000" share one cap.
        if (client is not null)
        {
            var recentCount = await _appointmentRepository.CountCreatedByClientSinceAsync(
                client.Id, DateTime.UtcNow.AddHours(-1), cancellationToken);
            if (recentCount >= 3)
                throw new InvalidOperationException(
                    "Este WhatsApp já fez vários agendamentos na última hora. Aguarde um pouco ou fale com a barbearia.");
        }

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
                throw new ConflictException(Appointment.SlotTakenMessage);
        }

        // Primeiro agendamento deste telefone: nasce o cliente, com o nome digitado agora.
        // Nos seguintes o nome do cliente fica — o agendamento guarda o que veio desta vez.
        if (client is null)
        {
            client = Client.Create(request.ClientName, phone, DateTime.UtcNow);
            await _clientRepository.AddAsync(client, cancellationToken);
        }

        var appointment = Appointment.Create(
            request.ClientName,
            phone.Canonical,
            request.BarberId,
            request.ScheduledAt,
            totalDuration,
            request.Notes,
            services,
            client.Id);

        await _appointmentRepository.AddAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Enfileira o aviso ao barbeiro: o cliente recebe o 201 sem esperar SMTP/WhatsApp
        _notifications.Enqueue((n, ct) =>
            n.SendAppointmentCreatedAsync(appointment, barber, services, ct));

        return new CreateAppointmentResult(appointment.Id, appointment.AccessToken);
    }
}
