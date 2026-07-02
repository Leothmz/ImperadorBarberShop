namespace ImperadorBarberShop.Domain.Entities;

public class Barber
{
    private readonly List<BarberAvailability> _availability = new();
    private readonly List<Appointment> _appointments = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public decimal AverageRating { get; private set; }
    public bool IsActive { get; private set; }
    public string? PhotoUrl { get; private set; }
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
            AverageRating = 0m,
            IsActive = true
        };
    }

    public void UpdateAverageRating(decimal newAverage)
    {
        AverageRating = newAverage;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    public void UpdatePhoto(string photoUrl) => PhotoUrl = photoUrl;
}
