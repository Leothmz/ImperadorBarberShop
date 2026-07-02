namespace ImperadorBarberShop.Application.DTOs;

public record BarberDto
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

public record BarberAvailabilityDto
{
    public DayOfWeek DayOfWeek { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
}
