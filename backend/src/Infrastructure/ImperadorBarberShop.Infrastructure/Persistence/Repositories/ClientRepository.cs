using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly AppDbContext _context;
    public ClientRepository(AppDbContext context) => _context = context;

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Clients.FindAsync([id], ct);

    public Task<Client?> GetByMatchKeyAsync(string matchKey, CancellationToken ct = default)
        => _context.Clients.FirstOrDefaultAsync(c => c.MatchKey == matchKey, ct);

    public Task<List<Client>> GetLastVisitedSinceAsync(DateTime since, CancellationToken ct = default)
        => _context.Clients
            .Where(c => c.LastVisitAt >= since)
            .ToListAsync(ct);

    public async Task AddAsync(Client client, CancellationToken ct = default)
        => await _context.Clients.AddAsync(client, ct);

    public Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        _context.Clients.Update(client);
        return Task.CompletedTask;
    }
}
