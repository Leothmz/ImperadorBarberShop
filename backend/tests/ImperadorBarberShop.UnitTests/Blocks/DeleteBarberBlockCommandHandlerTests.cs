using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Blocks;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Blocks;

public class DeleteBarberBlockCommandHandlerTests
{
    private readonly IBarberBlockRepository _repo = Substitute.For<IBarberBlockRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly DeleteBarberBlockCommandHandler _handler;

    public DeleteBarberBlockCommandHandlerTests()
    {
        _handler = new DeleteBarberBlockCommandHandler(_repo, _uow);
    }

    [Fact]
    public async Task Handle_OwnBlock_DeletesAndSaves()
    {
        var barberId = Guid.NewGuid();
        var block = BarberBlock.Create(barberId,
            DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3),
            null, false, null, null);

        _repo.GetByIdAsync(block.Id, Arg.Any<CancellationToken>()).Returns(block);

        await _handler.Handle(new DeleteBarberBlockCommand(block.Id, barberId), CancellationToken.None);

        await _repo.Received(1).DeleteAsync(block, Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DifferentBarber_ThrowsForbidden()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var block = BarberBlock.Create(ownerId,
            DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3),
            null, false, null, null);

        _repo.GetByIdAsync(block.Id, Arg.Any<CancellationToken>()).Returns(block);

        var act = () => _handler.Handle(new DeleteBarberBlockCommand(block.Id, attackerId), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
