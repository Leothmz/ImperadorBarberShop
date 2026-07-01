using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Admin;

/// <summary>
/// Verifies that admin seeding works end-to-end and that admin credentials
/// produce a valid JWT with role=Admin.
/// </summary>
public class AdminAuthTests : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AdminAuthTests(WebAppFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Login_WithAdminCredentials_Returns200WithAdminRole()
    {
        // Admin is seeded on startup via AdminSeedService with credentials from
        // WebAppFixture.TestEnvironmentVariables (Admin__Email / Admin__Password).
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@test.com", password = "AdminTest123!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("role").GetString().Should().Be("Admin");
    }

    [Fact]
    public async Task RegisterBarber_Returns404_EndpointRemoved()
    {
        var payload = new
        {
            name = "Attempted Barber",
            email = $"attempt-{Guid.NewGuid()}@test.com",
            password = "Password123!",
            availability = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register/barber", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
