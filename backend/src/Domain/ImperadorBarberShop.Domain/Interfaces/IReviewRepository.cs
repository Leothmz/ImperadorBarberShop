using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.Domain.Interfaces;

public interface IReviewRepository
{
    Task<Review?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task<List<Review>> GetByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
    Task AddAsync(Review review, CancellationToken cancellationToken = default);
    Task<decimal> GetAverageRatingByBarberIdAsync(Guid barberId, CancellationToken cancellationToken = default);
}
