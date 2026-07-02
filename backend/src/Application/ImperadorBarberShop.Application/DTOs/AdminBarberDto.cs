namespace ImperadorBarberShop.Application.DTOs;

public record AdminBarberDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public decimal AverageRating { get; init; }
    public bool IsActive { get; init; }
    public List<BarberAvailabilityDto> Availability { get; init; } = [];
}
