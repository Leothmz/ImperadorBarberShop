using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _context;
    public ExpenseRepository(AppDbContext context) => _context = context;

    public async Task<List<Expense>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _context.Expenses
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderByDescending(e => e.Date)
            .ToListAsync(ct);

    public async Task<decimal> GetTotalByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _context.Expenses
            .Where(e => e.Date >= from && e.Date <= to)
            .SumAsync(e => e.Amount, ct);

    public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Expenses.FindAsync([id], ct);

    public async Task AddAsync(Expense expense, CancellationToken ct = default)
        => await _context.Expenses.AddAsync(expense, ct);

    public Task DeleteAsync(Expense expense, CancellationToken ct = default)
    {
        _context.Expenses.Remove(expense);
        return Task.CompletedTask;
    }
}
