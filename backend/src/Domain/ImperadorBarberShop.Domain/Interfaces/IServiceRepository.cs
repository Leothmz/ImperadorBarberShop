using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IServiceRepository
{
    Task<List<Service>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<Service>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Service>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Service service, CancellationToken cancellationToken = default);
    Task UpdateAsync(Service service, CancellationToken cancellationToken = default);
}
