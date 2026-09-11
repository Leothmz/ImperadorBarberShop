using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Client?> GetByMatchKeyAsync(string matchKey, CancellationToken ct = default);
    /// <param name="since">Horário de parede da barbearia — o mesmo relógio de LastVisitAt.</param>
    Task<List<Client>> GetLastVisitedSinceAsync(DateTime since, CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
}
