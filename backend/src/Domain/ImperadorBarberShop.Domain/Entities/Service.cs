namespace ImperadorBarberShop.Domain.Entities;

public class Service
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int DurationMinutes { get; private set; }
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }

    // EF Core constructor
    private Service() { }

    public static Service Create(string name, string description, int durationMinutes, decimal price)
    {
        return new Service
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            DurationMinutes = durationMinutes,
            Price = price,
            IsActive = true
        };
    }

    // Used for seeding with deterministic Ids
    public static Service CreateWithId(Guid id, string name, string description, int durationMinutes, decimal price)
    {
        return new Service
        {
            Id = id,
            Name = name,
            Description = description,
            DurationMinutes = durationMinutes,
            Price = price,
            IsActive = true
        };
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
