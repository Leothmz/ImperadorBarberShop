using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Application.DTOs;

public record AppointmentDto
{
    public Guid Id { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string ClientPhone { get; init; } = string.Empty;
    public Guid BarberId { get; init; }
    public string BarberName { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
    public int TotalDurationMinutes { get; init; }
    public AppointmentStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<ServiceDto> Services { get; init; } = [];
}
