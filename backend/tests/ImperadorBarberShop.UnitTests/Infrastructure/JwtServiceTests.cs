using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Infrastructure.Services;
using ImperadorBarberShop.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace ImperadorBarberShop.UnitTests.Infrastructure;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private const string Secret = "super-secret-key-at-least-256-bits-long-for-hmac-sha256-algorithm";

    public JwtServiceTests()
    {
        var settings = new JwtSettings
        {
            Secret = Secret,
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationMinutes = 15
        };
        _jwtService = new JwtService(Options.Create(settings));
    }

    [Fact]
    public void GenerateAccessToken_ForClient_ReturnsValidJwtWithCorrectClaims()
    {
        var user = User.CreateClient("João", "joao@email.com", "hash");

        var token = _jwtService.GenerateAccessToken(user);

        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value.Should().Be(user.Id.ToString());
        jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value.Should().Be("joao@email.com");
        jwt.Claims.FirstOrDefault(c => c.Type == "role")?.Value.Should().Be("Client");
        jwt.Claims.FirstOrDefault(c => c.Type == "barberId").Should().BeNull();
    }

    [Fact]
    public void GenerateAccessToken_ForBarber_IncludesBarberIdClaim()
    {
        var user = User.CreateBarber("Carlos", "carlos@email.com", "hash");
        var barberId = Guid.NewGuid();

        var token = _jwtService.GenerateAccessToken(user, barberId);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.FirstOrDefault(c => c.Type == "role")?.Value.Should().Be("Barber");
        jwt.Claims.FirstOrDefault(c => c.Type == "barberId")?.Value.Should().Be(barberId.ToString());
    }

    [Fact]
    public void GenerateAccessToken_HasCorrectExpiry()
    {
        var user = User.CreateClient("João", "joao@email.com", "hash");

        var token = _jwtService.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GenerateAccessToken_HasCorrectIssuerAndAudience()
    {
        var user = User.CreateClient("João", "joao@email.com", "hash");

        var token = _jwtService.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("TestIssuer");
        jwt.Audiences.Should().Contain("TestAudience");
    }
}
