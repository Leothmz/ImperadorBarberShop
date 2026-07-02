using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ImperadorBarberShop.Infrastructure.Services;

public class AdminSeedService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;

    public AdminSeedService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _userRepository.GetAdminAsync(cancellationToken);
        if (existing is not null) return;

        var email = _configuration["Admin:Email"];
        var password = _configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Admin credentials not configured. Set ADMIN__EMAIL environment variable.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Admin credentials not configured. Set ADMIN__PASSWORD environment variable.");

        var hash = _passwordHasher.Hash(password);
        var admin = User.CreateAdmin("Administrador", email, hash);

        await _userRepository.AddAsync(admin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
