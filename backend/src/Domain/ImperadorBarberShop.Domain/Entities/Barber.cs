namespace ImperadorBarberShop.Domain.Entities;

public class Barber
{
    private readonly List<BarberAvailability> _availability = new();
    private readonly List<Appointment> _appointments = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public decimal AverageRating { get; private set; }
    public IReadOnlyCollection<BarberAvailability> Availability => _availability.AsReadOnly();
    public IReadOnlyCollection<Appointment> Appointments => _appointments.AsReadOnly();

    // EF Core constructor
    private Barber() { }

    public static Barber Create(Guid userId)
    {
        return new Barber
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AverageRating = 0m
        };
    }

    public void UpdateAverageRating(decimal newAverage)
    {
        AverageRating = newAverage;
    }
}
