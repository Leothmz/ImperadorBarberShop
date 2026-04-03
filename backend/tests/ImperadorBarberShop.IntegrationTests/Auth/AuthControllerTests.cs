using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Auth;

public class AuthControllerTests : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AuthControllerTests(WebAppFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task RegisterClient_ValidPayload_Returns201()
    {
        var payload = new
        {
            name = "João Teste",
            email = $"joao-{Guid.NewGuid()}@test.com",
            password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/client", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RegisterClient_DuplicateEmail_Returns422()
    {
        var email = $"dup-{Guid.NewGuid()}@test.com";
        var payload = new { name = "João", email, password = "Password123!" };

        await _client.PostAsJsonAsync("/api/v1/auth/register/client", payload);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/client", payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RegisterBarber_ValidPayload_Returns201()
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

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var email = $"login-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/client",
            new { name = "Test User", email, password = "Password123!" });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("role").GetString().Should().Be("Client");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns403()
    {
        var email = $"wrong-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/client",
            new { name = "Test", email, password = "Password123!" });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns403()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "unknown@nowhere.com", password = "anything" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
