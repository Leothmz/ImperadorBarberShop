using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Domain.Entities;

public class Appointment
{
    private readonly List<AppointmentService> _appointmentServices = new();

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid BarberId { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public int TotalDurationMinutes { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public User Client { get; private set; } = null!;
    public Barber Barber { get; private set; } = null!;
    public IReadOnlyCollection<AppointmentService> AppointmentServices => _appointmentServices.AsReadOnly();

    // EF Core constructor
    private Appointment() { }

    public static Appointment Create(
        Guid clientId,
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
            ClientId = clientId,
            BarberId = barberId,
            ScheduledAt = scheduledAt,
            TotalDurationMinutes = totalDurationMinutes,
            Status = AppointmentStatus.Pending,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var serviceId in serviceIds)
            appointment._appointmentServices.Add(AppointmentService.Create(appointment.Id, serviceId));

        return appointment;
    }

    public void Accept()
    {
        if (Status != AppointmentStatus.Pending)
            throw new InvalidOperationException($"Cannot accept appointment in status {Status}.");
        Status = AppointmentStatus.Accepted;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (Status != AppointmentStatus.Pending)
            throw new InvalidOperationException($"Cannot reject appointment in status {Status}.");
        Status = AppointmentStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is not (AppointmentStatus.Pending or AppointmentStatus.Accepted))
            throw new InvalidOperationException($"Cannot cancel appointment in status {Status}.");
        Status = AppointmentStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status != AppointmentStatus.Accepted)
            throw new InvalidOperationException($"Cannot complete appointment in status {Status}.");
        Status = AppointmentStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }
}
