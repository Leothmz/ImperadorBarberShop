using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Users.FindAsync(new object[] { id }, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<User?> GetAdminAsync(CancellationToken cancellationToken = default)
        => await _context.Users
            .FirstOrDefaultAsync(u => u.Role == UserRole.Admin, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await _context.Users.AddAsync(user, cancellationToken);
}
