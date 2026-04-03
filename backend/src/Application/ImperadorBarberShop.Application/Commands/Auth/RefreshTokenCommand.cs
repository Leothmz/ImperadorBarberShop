using FluentValidation;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Auth;

public record RefreshTokenCommand(Guid UserId, string RefreshToken) : IRequest<LoginResult>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IBarberRepository _barberRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtService _jwtService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IBarberRepository barberRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtService jwtService,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _barberRepository = barberRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var storedToken = await _refreshTokenRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked || storedToken.IsExpired)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (!_passwordHasher.Verify(request.RefreshToken, storedToken.TokenHash))
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        Barber? barber = null;
        if (user.Role == Domain.Enums.UserRole.Barber)
            barber = await _barberRepository.GetByUserIdAsync(user.Id, cancellationToken);

        // Revoke old token
        storedToken.Revoke();
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);

        var accessToken = _jwtService.GenerateAccessToken(user, barber?.Id);

        var rawToken = GenerateRawToken();
        var tokenHash = _passwordHasher.Hash(rawToken);
        var newToken = RefreshToken.Create(user.Id, tokenHash, DateTime.UtcNow.AddDays(7));
        await _refreshTokenRepository.AddAsync(newToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResult(accessToken, rawToken, user.Role.ToString(), user.Id, barber?.Id);
    }

    private static string GenerateRawToken()
    {
        var bytes = new byte[64];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
