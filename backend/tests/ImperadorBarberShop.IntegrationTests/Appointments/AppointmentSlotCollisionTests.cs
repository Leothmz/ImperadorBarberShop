using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

/// <summary>
/// A slot someone else just took must come back as 409, which the booking screen turns
/// into "Escolher outro horário". It used to be a 422 carrying an English sentence, so
/// the recovery branch never ran. Own fixture: the per-IP creation limiter is 5/hour.
/// </summary>
public class AppointmentSlotCollisionTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AppointmentSlotCollisionTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task BookingASlotThatWasJustTaken_Returns409WithThePortugueseCollisionMessage()
    {
        var email = $"barber-collision-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Barber", email);
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        var barberId = (await login.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions)).GetProperty("barberId").GetGuid();
        // Horário de parede, sem fuso — o formato que a tela de agendamento envia
        var scheduledAt = DateTime.UtcNow.AddDays(4).Date.AddHours(10).ToString("yyyy-MM-ddTHH:mm:ss");

        var first = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Primeiro Cliente",
            clientPhone = "+5511999991101",
            barberId,
            scheduledAt,
            serviceIds = new[] { ServiceConfiguration.CorteId }
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Segundo Cliente",
            clientPhone = "+5511999991102",
            barberId,
            scheduledAt,
            serviceIds = new[] { ServiceConfiguration.CorteId }
        });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("detail").GetString().Should().Be(Appointment.SlotTakenMessage);
    }
}
