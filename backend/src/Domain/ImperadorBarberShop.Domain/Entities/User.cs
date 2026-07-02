using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Barber? BarberProfile { get; private set; }

    // EF Core constructor
    private User() { }

    public static User CreateClient(string name, string email, string passwordHash)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = passwordHash,
            Role = UserRole.Client,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static User CreateBarber(string name, string email, string passwordHash)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = passwordHash,
            Role = UserRole.Barber,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static User CreateAdmin(string name, string email, string passwordHash)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdatePasswordHash(string newHash) => PasswordHash = newHash;
}
