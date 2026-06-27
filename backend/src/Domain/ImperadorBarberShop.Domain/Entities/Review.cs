namespace ImperadorBarberShop.Domain.Entities;

public class Review
{
    public Guid Id { get; private set; }
    public Guid AppointmentId { get; private set; }
    public Guid BarberId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Appointment Appointment { get; private set; } = null!;

    // EF Core constructor
    private Review() { }

    public static Review Create(Guid appointmentId, Guid barberId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        return new Review
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointmentId,
            BarberId = barberId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTime.UtcNow
        };
    }
}
