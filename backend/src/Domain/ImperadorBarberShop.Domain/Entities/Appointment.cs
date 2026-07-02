using System.Security.Cryptography;
using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Domain.Entities;

public class Appointment
{
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
    public DateTime? PaidAt { get; private set; }
    public Barber Barber { get; private set; } = null!;
    public IReadOnlyCollection<AppointmentService> AppointmentServices => _appointmentServices.AsReadOnly();

    // EF Core constructor
    private Appointment() { }

    public static Appointment Create(
        string clientName,
        string clientPhone,
        Guid barberId,
        DateTime scheduledAt,
        int totalDurationMinutes,
        string? notes,
        IEnumerable<Guid> serviceIds)
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
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var serviceId in serviceIds)
            appointment._appointmentServices.Add(AppointmentService.Create(appointment.Id, serviceId));

        return appointment;
    }

    public void Cancel()
    {
        if (Status != AppointmentStatus.Accepted)
            throw new InvalidOperationException($"Cannot cancel appointment in status {Status}.");
        Status = AppointmentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(PaymentMethod? paymentMethod = null)
    {
        if (Status != AppointmentStatus.Accepted)
            throw new InvalidOperationException($"Cannot complete appointment in status {Status}.");
        Status = AppointmentStatus.Completed;
        if (paymentMethod.HasValue)
        {
            PaymentMethod = paymentMethod;
            PaidAt = DateTime.UtcNow;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPaymentMethod(PaymentMethod paymentMethod)
    {
        if (Status != AppointmentStatus.Completed)
            throw new InvalidOperationException("Cannot set payment method on a non-completed appointment.");
        PaymentMethod = paymentMethod;
        if (!PaidAt.HasValue)
            PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
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
