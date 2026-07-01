using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class ServiceAddonRepository : IServiceAddonRepository
{
    private readonly AppDbContext _context;
    public ServiceAddonRepository(AppDbContext context) => _context = context;

    public async Task<List<ServiceAddon>> GetByParentIdsAsync(IEnumerable<Guid> parentIds, CancellationToken ct = default)
        => await _context.ServiceAddons
            .Include(a => a.AddonService)
            .Where(a => parentIds.Contains(a.ParentServiceId))
            .ToListAsync(ct);

    public async Task<ServiceAddon?> GetAsync(Guid parentId, Guid addonId, CancellationToken ct = default)
        => await _context.ServiceAddons
            .FirstOrDefaultAsync(a => a.ParentServiceId == parentId && a.AddonServiceId == addonId, ct);

    public async Task AddAsync(ServiceAddon addon, CancellationToken ct = default)
        => await _context.ServiceAddons.AddAsync(addon, ct);

    public void Remove(ServiceAddon addon) => _context.ServiceAddons.Remove(addon);
}
