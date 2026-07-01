using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IBarberRepository
{
    Task<Barber?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Barber?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Barber>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Barber>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Barber barber, CancellationToken cancellationToken = default);
    Task UpdateAsync(Barber barber, CancellationToken cancellationToken = default);
}
