using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IServiceAddonRepository
{
    Task<List<ServiceAddon>> GetByParentIdsAsync(IEnumerable<Guid> parentIds, CancellationToken ct = default);
    Task<ServiceAddon?> GetAsync(Guid parentId, Guid addonId, CancellationToken ct = default);
    Task AddAsync(ServiceAddon addon, CancellationToken ct = default);
    void Remove(ServiceAddon addon);
}
