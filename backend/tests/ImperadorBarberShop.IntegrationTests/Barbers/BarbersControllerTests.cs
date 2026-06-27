using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace ImperadorBarberShop.IntegrationTests.Barbers;

public class BarbersControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public BarbersControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/barbers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/barbers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetReviews_ValidBarberId_Returns200()
    {
        // Register a barber first
        var email = $"rev-barber-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/barber", new
        {
            name = "Review Barber",
            email,
            password = "Password123!",
            availability = Array.Empty<object>()
        });
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var barberId = body.GetProperty("barberId").GetGuid();

        var response = await _client.GetAsync($"/api/v1/barbers/{barberId}/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSlots_PublicAccess_Returns200()
    {
        var response = await _client.GetAsync(
            $"/api/v1/barbers/{Guid.NewGuid()}/slots?date=2026-04-06&serviceIds={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateAvailability_AsBarber_Returns204()
    {
        // Register barber and get token
        var email = $"avail-barber-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/barber", new
        {
            name = "Avail Barber",
            email,
            password = "Password123!",
            availability = Array.Empty<object>()
        });
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = body.GetProperty("accessToken").GetString()!;

        var authClient = _fixture.CreateClient();
        authClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // PUT /barbers/me/availability binds to UpdateAvailabilityRequest(List<AvailabilitySlotInput> Availability)
        // — the body must be wrapped under "availability", not a bare array.
        var payload = new
        {
            availability = new[]
            {
                new { dayOfWeek = 2, startTime = "08:00:00", endTime = "17:00:00" }
            }
        };
        var response = await authClient.PutAsJsonAsync("/api/v1/barbers/me/availability", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
