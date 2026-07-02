using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IExpenseRepository
{
    Task<List<Expense>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<decimal> GetTotalByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Expense expense, CancellationToken ct = default);
    Task DeleteAsync(Expense expense, CancellationToken ct = default);
}
