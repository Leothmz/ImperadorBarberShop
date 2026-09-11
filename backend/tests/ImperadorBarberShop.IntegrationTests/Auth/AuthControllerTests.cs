using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;

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
        body.GetProperty("role").GetString().Should().Be("Barber");
    }

    // ── Refresh token in a backend-issued HttpOnly cookie ─────────────────────────
    // O middleware do Next libera /admin e /barber pela presença deste cookie. Ele só
    // vale como guarda porque quem o emite é a API e o JavaScript da página nunca o vê.

    private HttpClient ClientWithoutCookieJar() =>
        _fixture.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static string? SetCookieFor(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(name + "=", StringComparison.Ordinal))
            : null;

    private static string CookieValue(string setCookie) =>
        setCookie.Split(';')[0].Split('=', 2)[1];

    private async Task<(HttpResponseMessage Response, JsonElement Body)> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (response, await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions));
    }

    private static HttpRequestMessage RefreshRequest(Guid userId, string? cookieValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { userId })
        };
        if (cookieValue is not null)
            request.Headers.Add("Cookie", $"{AuthController.RefreshCookieName}={cookieValue}");
        return request;
    }

    [Fact]
    public async Task Login_SetsRefreshTokenAsHttpOnlyCookie_AndKeepsItOutOfTheBody()
    {
        var email = $"cookie-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Test Barber", email);

        var (response, body) = await LoginAsync(ClientWithoutCookieJar(), email);

        body.TryGetProperty("refreshToken", out _).Should().BeFalse("JavaScript must never see the refresh token");
        var setCookie = SetCookieFor(response, AuthController.RefreshCookieName);
        setCookie.Should().NotBeNull();
        CookieValue(setCookie!).Should().NotBeNullOrEmpty();
        setCookie.Should().ContainEquivalentOf("httponly");
        setCookie.Should().ContainEquivalentOf("samesite=lax");
        setCookie.Should().ContainEquivalentOf("path=/");
        setCookie.Should().ContainEquivalentOf("secure", "outside Development the cookie is HTTPS-only");
    }

    [Fact]
    public async Task Refresh_WithTheCookie_ReturnsANewAccessTokenAndRotatesTheCookie()
    {
        var email = $"refresh-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Test Barber", email);
        var client = ClientWithoutCookieJar();
        var (login, loginBody) = await LoginAsync(client, email);
        var userId = loginBody.GetProperty("userId").GetGuid();
        var firstCookie = CookieValue(SetCookieFor(login, AuthController.RefreshCookieName)!);

        var response = await client.SendAsync(RefreshRequest(userId, firstCookie));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("role").GetString().Should().Be("Barber");
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
        var rotated = SetCookieFor(response, AuthController.RefreshCookieName);
        rotated.Should().NotBeNull();
        CookieValue(rotated!).Should().NotBe(firstCookie);
    }

    [Fact]
    public async Task Refresh_WithoutTheCookie_Returns401()
    {
        var email = $"nocookie-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Test Barber", email);
        var client = ClientWithoutCookieJar();
        var (_, loginBody) = await LoginAsync(client, email);

        // Um refresh token no corpo, como o front antigo mandava, não serve mais
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { userId = loginBody.GetProperty("userId").GetGuid(), refreshToken = "from-js" })
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithARotatedOutCookie_Returns401AndClearsIt()
    {
        var email = $"stale-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Test Barber", email);
        var client = ClientWithoutCookieJar();
        var (login, loginBody) = await LoginAsync(client, email);
        var userId = loginBody.GetProperty("userId").GetGuid();
        var staleCookie = CookieValue(SetCookieFor(login, AuthController.RefreshCookieName)!);
        (await client.SendAsync(RefreshRequest(userId, staleCookie))).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.SendAsync(RefreshRequest(userId, staleCookie));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // Um cookie morto não pode continuar abrindo a porta do middleware do Next
        var cleared = SetCookieFor(response, AuthController.RefreshCookieName);
        cleared.Should().NotBeNull();
        cleared.Should().ContainEquivalentOf("expires=Thu, 01 Jan 1970");
    }

    [Fact]
    public async Task Logout_ClearsTheRefreshCookie()
    {
        var response = await ClientWithoutCookieJar().PostAsync("/api/v1/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cleared = SetCookieFor(response, AuthController.RefreshCookieName);
        cleared.Should().NotBeNull();
        cleared.Should().ContainEquivalentOf("expires=Thu, 01 Jan 1970");
        cleared.Should().ContainEquivalentOf("path=/");
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
