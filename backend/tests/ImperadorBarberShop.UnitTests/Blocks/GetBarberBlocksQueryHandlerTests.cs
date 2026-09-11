using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Blocks;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Blocks;

public class GetBarberBlocksQueryHandlerTests
{
    private readonly IBarberBlockRepository _repo = Substitute.For<IBarberBlockRepository>();
    private readonly Guid _barberId = Guid.NewGuid();

    [Fact]
    public async Task Handle_KeepsABlockStillInProgressInShopTime()
    {
        // 18:58 na barbearia (21:58 UTC). O bloqueio das 17:00 às 20:00 ainda está valendo,
        // mas contra DateTime.UtcNow já tinha "acabado" e sumia da lista.
        var inProgress = BarberBlock.Create(_barberId,
            new DateTime(2026, 9, 10, 17, 0, 0), new DateTime(2026, 9, 10, 20, 0, 0), "Curso", false, null, null);
        var finished = BarberBlock.Create(_barberId,
            new DateTime(2026, 9, 10, 12, 0, 0), new DateTime(2026, 9, 10, 13, 0, 0), "Almoço", false, null, null);
        _repo.GetByBarberIdAsync(_barberId, Arg.Any<CancellationToken>())
            .Returns(new List<BarberBlock> { inProgress, finished });
        var handler = new GetBarberBlocksQueryHandler(_repo, new FixedShopClock(FixedShopClock.EveningUtc));

        var result = await handler.Handle(new GetBarberBlocksQuery(_barberId), CancellationToken.None);

        result.Select(b => b.Id).Should().Equal(inProgress.Id);
    }
}
