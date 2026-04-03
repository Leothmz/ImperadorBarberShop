using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.Infrastructure.Persistence.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly AppDbContext _context;

    public ReviewRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Review?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default)
        => await _context.Reviews
            .FirstOrDefaultAsync(r => r.AppointmentId == appointmentId, cancellationToken);

    public async Task<List<Review>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default)
        => await _context.Reviews
            .Where(r => r.BarberId == barberId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Review review, CancellationToken cancellationToken = default)
        => await _context.Reviews.AddAsync(review, cancellationToken);

    public async Task<decimal> GetAverageRatingByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default)
    {
        var reviews = await _context.Reviews
            .Where(r => r.BarberId == barberId)
            .Select(r => (double)r.Rating)
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
            return 0m;

        return (decimal)reviews.Average();
    }
}
