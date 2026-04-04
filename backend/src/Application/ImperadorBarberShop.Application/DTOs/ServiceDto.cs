namespace ImperadorBarberShop.Application.DTOs;

public record ServiceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int DurationMinutes { get; init; }
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
}
