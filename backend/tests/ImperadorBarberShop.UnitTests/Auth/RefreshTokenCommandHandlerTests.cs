using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Auth;

public class RefreshTokenCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IBarberRepository _barberRepository = Substitute.For<IBarberRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(
            _userRepository, _barberRepository, _refreshTokenRepository,
            _jwtService, _passwordHasher, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsNewTokenPair()
    {
        var userId = Guid.NewGuid();
        const string rawToken = "raw_token_value";
        const string storedHash = "stored_hash";
        var storedToken = RefreshToken.Create(userId, storedHash, DateTime.UtcNow.AddDays(7));
        var user = User.CreateClient("João", "joao@email.com", "hashed");

        _refreshTokenRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _passwordHasher.Verify(rawToken, storedHash).Returns(true);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _jwtService.GenerateAccessToken(user, null).Returns("new_access_token");
        _passwordHasher.Hash(Arg.Any<string>()).Returns("new_hash");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var result = await _handler.Handle(new RefreshTokenCommand(userId, rawToken), CancellationToken.None);

        result.AccessToken.Should().Be("new_access_token");
        result.Role.Should().Be("Client");
        await _refreshTokenRepository.Received(1).UpdateAsync(storedToken, Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoStoredToken_ThrowsUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();
        _refreshTokenRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var act = () => _handler.Handle(new RefreshTokenCommand(userId, "token"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_RevokedToken_ThrowsUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();
        var storedToken = RefreshToken.Create(userId, "hash", DateTime.UtcNow.AddDays(7));
        storedToken.Revoke();

        _refreshTokenRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(storedToken);

        var act = () => _handler.Handle(new RefreshTokenCommand(userId, "any_token"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();
        var storedToken = RefreshToken.Create(userId, "hash", DateTime.UtcNow.AddDays(-1));

        _refreshTokenRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(storedToken);

        var act = () => _handler.Handle(new RefreshTokenCommand(userId, "token"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WrongRawToken_ThrowsUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();
        var storedToken = RefreshToken.Create(userId, "correct_hash", DateTime.UtcNow.AddDays(7));

        _refreshTokenRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _passwordHasher.Verify("wrong_token", "correct_hash").Returns(false);

        var act = () => _handler.Handle(new RefreshTokenCommand(userId, "wrong_token"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
