using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Blocks;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Blocks;

public class CreateBarberBlockCommandHandlerTests
{
    private readonly IBarberBlockRepository _repo = Substitute.For<IBarberBlockRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateBarberBlockCommandHandler _handler;

    public CreateBarberBlockCommandHandlerTests()
    {
        _handler = new CreateBarberBlockCommandHandler(_repo, _uow);
    }

    [Fact]
    public async Task Handle_PontualBlock_AddsAndSaves()
    {
        var barberId = Guid.NewGuid();
        var cmd = new CreateBarberBlockCommand(
            barberId,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3),
            "Almoço",
            false, null, null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _repo.Received(1).AddAsync(Arg.Any<BarberBlock>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RecurringBlock_SetsRecurrenceDays()
    {
        var barberId = Guid.NewGuid();
        var cmd = new CreateBarberBlockCommand(
            barberId,
            new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 7, 13, 0, 0, DateTimeKind.Utc),
            null,
            true, 42, null);

        await _handler.Handle(cmd, CancellationToken.None);

        await _repo.Received(1).AddAsync(
            Arg.Is<BarberBlock>(b => b.IsRecurring && b.RecurrenceDays == 42),
            Arg.Any<CancellationToken>());
    }
}
