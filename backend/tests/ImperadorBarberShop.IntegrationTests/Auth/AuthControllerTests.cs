using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Auth;

public class AuthControllerTests : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client;
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AuthControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task RegisterClient_Returns404_RouteNoLongerExists()
    {
        var payload = new { name = "João Teste", email = $"joao-{Guid.NewGuid()}@test.com", password = "Password123!" };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/client", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegisterBarber_Returns404_RouteNoLongerExists()
    {
        var payload = new
        {
            name = "Carlos Barbeiro",
            email = $"carlos-{Guid.NewGuid()}@test.com",
            password = "Password123!",
            availability = new[]
            {
                new { dayOfWeek = 1, startTime = "09:00:00", endTime = "18:00:00" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/barber", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var email = $"login-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Test Barber", email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("role").GetString().Should().Be("Barber");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = $"wrong-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Test", email);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPassword!" });

        // LoginCommandHandler throws UnauthorizedAccessException for invalid credentials,
        // which ExceptionHandlingMiddleware maps to 401 (see LoginCommandHandlerTests unit tests).
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "unknown@nowhere.com", password = "anything" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
