namespace ImperadorBarberShop.Application.DTOs;

public record ReviewDto(
    Guid Id,
    Guid AppointmentId,
    Guid ClientId,
    Guid BarberId,
    int Rating,
    string? Comment,
    DateTime CreatedAt);
