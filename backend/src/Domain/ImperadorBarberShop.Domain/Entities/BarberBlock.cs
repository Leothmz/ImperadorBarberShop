namespace ImperadorBarberShop.Domain.Entities;

public class BarberBlock
{
    public Guid Id { get; private set; }
    public Guid BarberId { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string? Description { get; private set; }
    public bool IsRecurring { get; private set; }
    public int? RecurrenceDays { get; private set; }
    public DateTime? RecurrenceEndsAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BarberBlock() { }

    public static BarberBlock Create(
        Guid barberId,
        DateTime startsAt,
        DateTime endsAt,
        string? description,
        bool isRecurring,
        int? recurrenceDays,
        DateTime? recurrenceEndsAt)
    {
        return new BarberBlock
        {
            Id = Guid.NewGuid(),
            BarberId = barberId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Description = description,
            IsRecurring = isRecurring,
            RecurrenceDays = isRecurring ? recurrenceDays : null,
            RecurrenceEndsAt = isRecurring ? recurrenceEndsAt : null,
            CreatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>Returns true if this block is active on the given UTC date.</summary>
    public bool IsActiveOn(DateOnly date)
    {
        if (!IsRecurring)
            return DateOnly.FromDateTime(StartsAt) == date;

        var dayBit = 1 << (int)date.DayOfWeek; // Sun=1,Mon=2,...,Sat=64
        if ((RecurrenceDays & dayBit) == 0)
            return false;

        if (RecurrenceEndsAt.HasValue && date > DateOnly.FromDateTime(RecurrenceEndsAt.Value))
            return false;

        return true;
    }
}
