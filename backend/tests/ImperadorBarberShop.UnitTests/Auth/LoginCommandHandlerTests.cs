using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Auth;

public class LoginCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _userRepository, _barberRepository, _passwordHasher, _jwtService,
            _refreshTokenRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidClientCredentials_ReturnsLoginResult()
    {
        var user = User.CreateClient("João", "joao@email.com", "hashed");
        _userRepository.GetByEmailAsync("joao@email.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("password123", "hashed").Returns(true);
        _jwtService.GenerateAccessToken(user, null).Returns("access_token");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var result = await _handler.Handle(new LoginCommand("joao@email.com", "password123"), CancellationToken.None);

        result.AccessToken.Should().Be("access_token");
        result.Role.Should().Be("Client");
        result.BarberId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidBarberCredentials_ReturnsBarberId()
    {
        var user = User.CreateBarber("Carlos", "carlos@email.com", "hashed");
        var barber = Barber.Create(user.Id);
        _userRepository.GetByEmailAsync("carlos@email.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("password123", "hashed").Returns(true);
        _barberRepository.GetByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(barber);
        _jwtService.GenerateAccessToken(user, barber.Id).Returns("barber_token");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var result = await _handler.Handle(new LoginCommand("carlos@email.com", "password123"), CancellationToken.None);

        result.Role.Should().Be("Barber");
        result.BarberId.Should().Be(barber.Id);
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedAccessException()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var act = () => _handler.Handle(new LoginCommand("unknown@email.com", "pass"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        var user = User.CreateClient("João", "joao@email.com", "hashed");
        _userRepository.GetByEmailAsync("joao@email.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrongpass", "hashed").Returns(false);

        var act = () => _handler.Handle(new LoginCommand("joao@email.com", "wrongpass"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
