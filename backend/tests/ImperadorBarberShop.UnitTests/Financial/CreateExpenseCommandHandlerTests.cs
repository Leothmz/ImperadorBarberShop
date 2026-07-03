using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Financial;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Financial;

public class CreateExpenseCommandHandlerTests
{
    private readonly IExpenseRepository _repo = Substitute.For<IExpenseRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateExpenseCommandHandler _handler;

    public CreateExpenseCommandHandlerTests()
        => _handler = new CreateExpenseCommandHandler(_repo, _uow);

    [Fact]
    public async Task Handle_ValidExpense_ReturnsId()
    {
        var userId = Guid.NewGuid();
        var cmd = new CreateExpenseCommand(150m, "Produto para cabelo", new DateOnly(2026, 7, 1), userId);
        var id = await _handler.Handle(cmd, CancellationToken.None);
        id.Should().NotBeEmpty();
        await _repo.Received(1).AddAsync(Arg.Is<Expense>(e => e.Amount == 150m), Arg.Any<CancellationToken>());
    }
}
