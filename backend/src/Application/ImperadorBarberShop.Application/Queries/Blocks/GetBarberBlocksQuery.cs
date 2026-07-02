using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Blocks;

public record BarberBlockDto(
    Guid Id,
    DateTime StartsAt,
    DateTime EndsAt,
    string? Description,
    bool IsRecurring,
    int? RecurrenceDays,
    DateTime? RecurrenceEndsAt,
    DateTime CreatedAt);

public record GetBarberBlocksQuery(Guid BarberId) : IRequest<List<BarberBlockDto>>;

public class GetBarberBlocksQueryHandler : IRequestHandler<GetBarberBlocksQuery, List<BarberBlockDto>>
{
    private readonly IBarberBlockRepository _repo;

    public GetBarberBlocksQueryHandler(IBarberBlockRepository repo) => _repo = repo;

    public async Task<List<BarberBlockDto>> Handle(GetBarberBlocksQuery request, CancellationToken cancellationToken)
    {
        var blocks = await _repo.GetByBarberIdAsync(request.BarberId, cancellationToken);
        var now = DateTime.UtcNow;
        var activeBlocks = blocks.Where(b => b.IsRecurring || b.EndsAt >= now).ToList();
        return activeBlocks.Select(b => new BarberBlockDto(
            b.Id, b.StartsAt, b.EndsAt, b.Description,
            b.IsRecurring, b.RecurrenceDays, b.RecurrenceEndsAt, b.CreatedAt))
            .ToList();
    }
}
