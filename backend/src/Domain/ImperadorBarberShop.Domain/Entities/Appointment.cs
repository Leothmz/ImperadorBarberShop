using System.Security.Cryptography;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.ValueObjects;

namespace ImperadorBarberShop.Domain.Entities;

public class Appointment
{
    /// <summary>Um barbeiro, um horário: a mesma recusa vale para a checagem e para o índice único.</summary>
    public const string SlotTakenMessage =
        "Esse horário acabou de ser reservado por outra pessoa. Escolha outro horário.";

    private readonly List<AppointmentService> _appointmentServices = new();

    public Guid Id { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public string ClientPhone { get; private set; } = string.Empty;
    public string AccessToken { get; private set; } = string.Empty;
    public Guid BarberId { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public int TotalDurationMinutes { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ReminderSentAt { get; private set; }
    public PaymentMethod? PaymentMethod { get; private set; }

    /// <summary>Instante UTC do pagamento. Nulo sem pagamento e na recorrência de plano, que nada cobra na visita.</summary>
    public DateTime? PaidAt { get; private set; }

    /// <summary>Só com <see cref="Enums.PaymentMethod.Plano"/>. As regras estão em <see cref="AppointmentPayment"/>.</summary>
    public PlanKind? PlanKind { get; private set; }

    /// <summary>Como o plano foi pago (Pix, Dinheiro ou Cartão). Só em <see cref="Enums.PlanKind.Pagamento"/>.</summary>
    public PaymentMethod? PlanTender { get; private set; }

    /// <summary>Valor cobrado, só no plano (0 na recorrência). Nulo: vale a soma dos serviços.</summary>
    public decimal? ChargedAmount { get; private set; }

    /// <summary>Cliente reconhecido pelo telefone. Nulo só em agendamento legado com telefone ilegível.</summary>
    public Guid? ClientId { get; private set; }
    public Barber Barber { get; private set; } = null!;
    public IReadOnlyCollection<AppointmentService> AppointmentServices => _appointmentServices.AsReadOnly();

    /// <summary>
    /// Quanto o atendimento rendeu: o valor cobrado no plano ou, fora dele, a soma dos preços
    /// guardados no agendamento. Todo número do financeiro sai daqui. Os preços dos serviços
    /// nunca mudam: a recorrência de plano rende 0 sem apagar o que foi feito.
    /// </summary>
    public decimal EffectiveAmount => ChargedAmount ?? _appointmentServices.Sum(s => s.UnitPrice);

    public bool IsPlan => PaymentMethod == Enums.PaymentMethod.Plano;

    // EF Core constructor
    private Appointment() { }

    public static Appointment Create(
        string clientName,
        string clientPhone,
        Guid barberId,
        DateTime scheduledAt,
        int totalDurationMinutes,
        string? notes,
        IEnumerable<Service> services,
        Guid? clientId = null)
    {
        var now = DateTime.UtcNow;
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ClientName = clientName,
            ClientPhone = clientPhone,
            AccessToken = GenerateAccessToken(),
            BarberId = barberId,
            ScheduledAt = scheduledAt,
            TotalDurationMinutes = totalDurationMinutes,
            Status = AppointmentStatus.Accepted,
            Notes = notes,
            ClientId = clientId,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var service in services)
            appointment._appointmentServices.Add(AppointmentService.Create(appointment.Id, service));

        return appointment;
    }

    public void Cancel()
    {
        if (Status != AppointmentStatus.Accepted)
            throw new InvalidOperationException($"Cannot cancel appointment in status {Status}.");
        Status = AppointmentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(AppointmentPayment? payment = null)
    {
        if (Status != AppointmentStatus.Accepted)
            throw new InvalidOperationException($"Cannot complete appointment in status {Status}.");
        Status = AppointmentStatus.Completed;
        if (payment is not null)
            ApplyPayment(payment);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPayment(AppointmentPayment payment)
    {
        if (Status != AppointmentStatus.Completed)
            throw new InvalidOperationException("Cannot set payment method on a non-completed appointment.");
        ApplyPayment(payment);
        UpdatedAt = DateTime.UtcNow;
    }

    // Substitui o pagamento inteiro: voltar a um método normal apaga os dados do plano
    private void ApplyPayment(AppointmentPayment payment)
    {
        PaymentMethod = payment.Method;
        PlanKind = payment.PlanKind;
        PlanTender = payment.PlanTender;
        ChargedAmount = payment.ChargedAmount;
        // Recorrência não recebe nada na visita: não há pagamento para datar. Nos demais,
        // trocar de método mantém a primeira data registrada.
        PaidAt = payment.PlanKind == Enums.PlanKind.Recorrencia ? null : PaidAt ?? DateTime.UtcNow;
    }

    public void MarkReminderSent()
    {
        ReminderSentAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string GenerateAccessToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
