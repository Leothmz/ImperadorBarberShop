using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Auth;

public class RegisterBarberCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IBarberAvailabilityRepository _availabilityRepository = Substitute.For<IBarberAvailabilityRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RegisterBarberCommandHandler _handler;

    public RegisterBarberCommandHandlerTests()
    {
        _handler = new RegisterBarberCommandHandler(
            _userRepository, _barberRepository, _availabilityRepository, _passwordHasher, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesBarberWithAvailability()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var availability = new List<AvailabilitySlotInput>
        {
            new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0))
        };
        var command = new RegisterBarberCommand("Carlos Barbeiro", "carlos@email.com", "password123", availability);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _barberRepository.Received(1).AddAsync(Arg.Any<Barber>(), Arg.Any<CancellationToken>());
        await _availabilityRepository.Received(1).AddRangeAsync(
            Arg.Any<IEnumerable<BarberAvailability>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var existingUser = User.CreateBarber("Existing", "carlos@email.com", "hash");
        _userRepository.GetByEmailAsync("carlos@email.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var command = new RegisterBarberCommand(
            "Carlos", "carlos@email.com", "password123",
            new List<AvailabilitySlotInput>());

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public async Task Handle_EmptyAvailability_CreatesBarberWithoutAvailability()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var command = new RegisterBarberCommand(
            "Carlos", "carlos@email.com", "password123",
            new List<AvailabilitySlotInput>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _availabilityRepository.DidNotReceive().AddRangeAsync(
            Arg.Any<IEnumerable<BarberAvailability>>(), Arg.Any<CancellationToken>());
    }
}
