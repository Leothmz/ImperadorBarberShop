namespace ImperadorBarberShop.Application.DTOs;

public record ServiceDto(
    Guid Id,
    string Name,
    string Description,
    int DurationMinutes,
    decimal Price,
    bool IsActive);
