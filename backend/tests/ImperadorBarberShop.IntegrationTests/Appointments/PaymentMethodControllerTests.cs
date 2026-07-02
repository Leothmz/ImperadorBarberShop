using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

public class PaymentMethodControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PaymentMethodControllerTests(WebAppFixture fixture) => _fixture = fixture;

    private async Task<(string Token, Guid BarberId)> RegisterBarber()
    {
        var email = $"pm-barber-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("PM Barber", email);
        var loginResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        return (body.GetProperty("accessToken").GetString()!, body.GetProperty("barberId").GetGuid());
    }

    private HttpClient AuthClient(string token)
    {
        var c = _fixture.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<(Guid AppointmentId, string BarberToken, Guid BarberId)> SeedCompletedAppointment()
    {
        var (token, barberId) = await RegisterBarber();
        var barberClient = AuthClient(token);

        // Create appointment via public endpoint
        // Phone must match ^\+55\d{11}$ (+55 then exactly 11 digits)
        var phoneDigits = Random.Shared.Next(100000, 999999).ToString();
        var scheduledAt = DateTime.UtcNow.AddDays(3).Date.AddHours(10);
        var createResp = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Payment Test Client",
            clientPhone = $"+5511999{phoneDigits}",   // +55 + 11 + 999 + 6 digits = +55 + 11 digits
            barberId,
            scheduledAt = scheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.CorteId }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var appointmentId = created.GetProperty("id").GetGuid();

        // Complete the appointment
        var completeResp = await barberClient.PatchAsync($"/api/v1/appointments/{appointmentId}/complete", null);
        completeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        return (appointmentId, token, barberId);
    }

    [Fact]
    public async Task UpdatePayment_AsBarber_Returns204()
    {
        var (apptId, barberToken, _) = await SeedCompletedAppointment();
        var barberClient = AuthClient(barberToken);

        var resp = await barberClient.PatchAsJsonAsync($"/api/v1/appointments/{apptId}/payment",
            new { paymentMethod = "Pix" });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePayment_WrongBarber_Returns403()
    {
        var (apptId, _, _) = await SeedCompletedAppointment();
        var otherBarberClient = _fixture.CreateAuthenticatedClient("Barber", Guid.NewGuid(), Guid.NewGuid());

        var resp = await otherBarberClient.PatchAsJsonAsync($"/api/v1/appointments/{apptId}/payment",
            new { paymentMethod = "Pix" });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePayment_Unauthenticated_Returns401()
    {
        var resp = await _fixture.CreateClient().PatchAsJsonAsync(
            $"/api/v1/appointments/{Guid.NewGuid()}/payment",
            new { paymentMethod = "Pix" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePayment_AsAdmin_Returns204()
    {
        var (apptId, _, _) = await SeedCompletedAppointment();
        var adminClient = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());

        var resp = await adminClient.PatchAsJsonAsync($"/api/v1/admin/appointments/{apptId}/payment",
            new { paymentMethod = "Cartão" });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
