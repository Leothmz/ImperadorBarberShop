using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Auth;

public class RegisterClientCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RegisterClientCommandHandler _handler;

    public RegisterClientCommandHandlerTests()
    {
        _handler = new RegisterClientCommandHandler(_userRepository, _passwordHasher, _unitOfWork);
    }

    [Fact]
    public async Task Handle_NewEmail_ReturnsNewUserId()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed_password");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var command = new RegisterClientCommand("João Silva", "joao@email.com", "password123");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsInvalidOperationException()
    {
        var existingUser = User.CreateClient("Existing", "joao@email.com", "hash");
        _userRepository.GetByEmailAsync("joao@email.com", Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var command = new RegisterClientCommand("João Silva", "joao@email.com", "password123");
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }
}
