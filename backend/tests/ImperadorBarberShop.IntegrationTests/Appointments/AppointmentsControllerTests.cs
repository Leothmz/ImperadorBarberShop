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

    private HttpClient AuthClient(string token)
    {
        var c = _fixture.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task GetBarber_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/appointments/barber");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ManageByToken_UnknownToken_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/appointments/manage/this-token-does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAppointment_Anonymous_Returns201WithAccessToken()
    {
        var (_, barberId) = await RegisterBarber();

        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(10);
        var payload = new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990000",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.CorteId },
            notes = (string?)null
        };

        var response = await _client.PostAsJsonAsync("/api/v1/appointments", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ManageByToken_AfterCreate_ReturnsAcceptedStatus()
    {
        var (_, barberId) = await RegisterBarber();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(11);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990001",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = created.GetProperty("accessToken").GetString();

        var response = await _client.GetAsync($"/api/v1/appointments/manage/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("status").GetString().Should().Be("Accepted");
    }

    [Fact]
    public async Task CancelByToken_MoreThan2HoursBeforeSchedule_Returns204()
    {
        var (_, barberId) = await RegisterBarber();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(12);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990002",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = created.GetProperty("accessToken").GetString();

        var response = await _client.PostAsync($"/api/v1/appointments/manage/{token}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CancelByBarber_AsBarber_Returns204()
    {
        var (barberToken, barberId) = await RegisterBarber();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(13);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990003",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var appointmentId = created.GetProperty("id").GetGuid();

        var response = await AuthClient(barberToken)
            .PatchAsync($"/api/v1/appointments/{appointmentId}/cancel-by-barber", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
