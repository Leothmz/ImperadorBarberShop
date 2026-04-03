namespace ImperadorBarberShop.Domain.Entities;

public class BarberAvailability
{
    public Guid Id { get; private set; }
    public Guid BarberId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    // EF Core constructor
    private BarberAvailability() { }

    public static BarberAvailability Create(Guid barberId, DayOfWeek dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        if (endTime <= startTime)
            throw new ArgumentException("EndTime must be after StartTime.");

        return new BarberAvailability
        {
            Id = Guid.NewGuid(),
            BarberId = barberId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            EndTime = endTime
        };
    }
}
