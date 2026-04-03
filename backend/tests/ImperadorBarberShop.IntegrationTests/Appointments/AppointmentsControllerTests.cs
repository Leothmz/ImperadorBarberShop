using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

public class AppointmentsControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AppointmentsControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private async Task<(string Token, Guid BarberId)> RegisterBarber()
    {
        var email = $"barber-appt-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/barber", new
        {
            name = "Barber",
            email,
            password = "Password123!",
            availability = new[] { new { dayOfWeek = (int)DateTime.UtcNow.AddDays(3).DayOfWeek, startTime = "08:00:00", endTime = "20:00:00" } }
        });
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return (body.GetProperty("accessToken").GetString()!, body.GetProperty("barberId").GetGuid());
    }

    private async Task<string> RegisterClient()
    {
        var email = $"client-appt-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register/client",
            new { name = "Client", email, password = "Password123!" });
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return body.GetProperty("accessToken").GetString()!;
    }

    private HttpClient AuthClient(string token)
    {
        var c = _fixture.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task GetMine_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/appointments/mine");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMine_AsClient_Returns200()
    {
        var clientToken = await RegisterClient();
        var response = await AuthClient(clientToken).GetAsync("/api/v1/appointments/mine");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBarber_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/appointments/barber");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAppointment_AsClient_Returns201()
    {
        var (barberToken, barberId) = await RegisterBarber();
        var clientToken = await RegisterClient();

        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(10);
        var payload = new
        {
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.HaircutId },
            notes = (string?)null
        };

        var response = await AuthClient(clientToken).PostAsJsonAsync("/api/v1/appointments", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AcceptAppointment_AsBarber_Returns204()
    {
        var (barberToken, barberId) = await RegisterBarber();
        var clientToken = await RegisterClient();

        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(11);
        var payload = new
        {
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BeardId }
        };

        var createResp = await AuthClient(clientToken).PostAsJsonAsync("/api/v1/appointments", payload);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var appointmentId = created.GetProperty("id").GetGuid();

        var response = await AuthClient(barberToken)
            .PatchAsync($"/api/v1/appointments/{appointmentId}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
