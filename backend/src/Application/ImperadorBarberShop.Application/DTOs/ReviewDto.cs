namespace ImperadorBarberShop.Application.DTOs;

public record ReviewDto
{
    public Guid Id { get; init; }
    public Guid AppointmentId { get; init; }
    public Guid BarberId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
}
