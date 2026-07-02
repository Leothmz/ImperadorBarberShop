using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Admin;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Admin;

public class CreateBarberByAdminCommandHandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IBarberRepository _barberRepo = Substitute.For<IBarberRepository>();
    private readonly IBarberAvailabilityRepository _availRepo = Substitute.For<IBarberAvailabilityRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateBarberByAdminCommandHandler _handler;

    public CreateBarberByAdminCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");
        _handler = new CreateBarberByAdminCommandHandler(
            _userRepo, _barberRepo, _availRepo, _hasher, _uow);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesBarber()
    {
        _userRepo.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new CreateBarberByAdminCommand(
            "João Barbeiro", "joao@test.com", "senha123",
            new List<AvailabilitySlotInput>(), PhotoUrl: null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _barberRepo.Received(1).AddAsync(Arg.Any<Barber>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_Throws()
    {
        var existing = User.CreateBarber("Existing", "joao@test.com", "hash");
        _userRepo.GetByEmailAsync("joao@test.com", Arg.Any<CancellationToken>()).Returns(existing);

        var command = new CreateBarberByAdminCommand(
            "João", "joao@test.com", "senha123", new List<AvailabilitySlotInput>(), null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already registered*");
    }
}
