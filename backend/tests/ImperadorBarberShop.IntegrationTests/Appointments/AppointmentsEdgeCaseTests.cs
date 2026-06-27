using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

/// <summary>
/// Edge-case HTTP coverage for appointment management/review that supplements
/// AppointmentsControllerTests (the brief's literal Step 1 test set). Kept in a
/// separate class — and thus its own WebAppFixture/TestServer/Postgres
/// container — because the "appointment-creation" rate limiter (Program.cs) is a
/// single global fixed window (5 requests/hour, no per-IP partitioning) shared by
/// every caller against one fixture instance. AppointmentsControllerTests already
/// uses 4 of the 5 permitted creations; adding these 3 more to the same class would
/// intermittently 429 whichever facts happened to run last (xUnit does not
/// guarantee execution order within a class).
/// </summary>
public class AppointmentsEdgeCaseTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AppointmentsEdgeCaseTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private async Task<(string Token, Guid BarberId)> RegisterBarber()
    {
        var email = $"barber-appt-edge-{Guid.NewGuid()}@test.com";
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
    public async Task CancelByToken_LessThan2HoursBeforeSchedule_Returns422()
    {
        var (_, barberId) = await RegisterBarber();
        // Within the 2-hour cancellation cutoff — must still be far enough in the
        // future to pass slot-availability validation, but inside the no-cancel window.
        var scheduledAt = DateTime.UtcNow.AddHours(1);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990009",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = created.GetProperty("accessToken").GetString();

        var response = await _client.PostAsync($"/api/v1/appointments/manage/{token}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CancelByBarber_WrongBarber_Returns403()
    {
        var (_, barberId) = await RegisterBarber();
        var (otherBarberToken, _) = await RegisterBarber();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(14);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990004",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var appointmentId = created.GetProperty("id").GetGuid();

        var response = await AuthClient(otherBarberToken)
            .PatchAsync($"/api/v1/appointments/{appointmentId}/cancel-by-barber", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReviewByToken_AppointmentNotCompleted_Returns422()
    {
        var (_, barberId) = await RegisterBarber();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(15);
        var createResp = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Teste",
            clientPhone = "+5511999990005",
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = created.GetProperty("accessToken").GetString();

        // Appointment is auto-confirmed (Accepted), never marked Completed.
        var response = await _client.PostAsJsonAsync($"/api/v1/appointments/manage/{token}/review",
            new { rating = 5, comment = "Great!" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
