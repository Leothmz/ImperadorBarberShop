namespace ImperadorBarberShop.Domain.Entities;

public class Expense
{
    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private Expense() { }

    public static Expense Create(decimal amount, string description, DateOnly date, Guid createdByUserId)
        => new()
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Description = description,
            Date = date,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
}
