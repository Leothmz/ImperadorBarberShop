using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Application.DTOs;

public record AppointmentDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    Guid BarberId,
    string BarberName,
    DateTime ScheduledAt,
    int TotalDurationMinutes,
    AppointmentStatus Status,
    string? Notes,
    DateTime CreatedAt,
    List<ServiceDto> Services);
