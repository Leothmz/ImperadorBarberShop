using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Application.DTOs;

public record AppointmentManageDto
{
    public Guid Id { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string BarberName { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
    public int TotalDurationMinutes { get; init; }
    public AppointmentStatus Status { get; init; }
    public List<ServiceDto> Services { get; init; } = [];
}
