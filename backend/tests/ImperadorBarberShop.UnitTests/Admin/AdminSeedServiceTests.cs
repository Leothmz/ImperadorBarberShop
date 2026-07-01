using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Admin;

public class AdminSeedServiceTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private AdminSeedService BuildService(string? email = "admin@test.com", string? password = "pass123")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Email"] = email,
                ["Admin:Password"] = password
            })
            .Build();
        return new AdminSeedService(_userRepo, _hasher, _uow, config);
    }

    [Fact]
    public async Task SeedAsync_NoAdminExists_CreatesAdmin()
    {
        _userRepo.GetAdminAsync(Arg.Any<CancellationToken>()).Returns((User?)null);
        _hasher.Hash("pass123").Returns("hashed");

        await BuildService().SeedAsync(CancellationToken.None);

        await _userRepo.Received(1).AddAsync(
            Arg.Is<User>(u => u.Role == UserRole.Admin && u.Email == "admin@test.com"),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_AdminAlreadyExists_DoesNothing()
    {
        var existing = User.CreateAdmin("Admin", "admin@test.com", "hash");
        _userRepo.GetAdminAsync(Arg.Any<CancellationToken>()).Returns(existing);

        await BuildService().SeedAsync(CancellationToken.None);

        await _userRepo.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_MissingEnvVar_Throws()
    {
        _userRepo.GetAdminAsync(Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => BuildService(email: null).SeedAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ADMIN__EMAIL*");
    }
}
