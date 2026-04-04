namespace ImperadorBarberShop.Application.DTOs;

public record ReviewDto
{
    public Guid Id { get; init; }
    public Guid AppointmentId { get; init; }
    public Guid ClientId { get; init; }
    public Guid BarberId { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
}
