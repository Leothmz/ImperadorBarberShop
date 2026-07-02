using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

/// <summary>
/// The "appointment-creation" rate limiter (Program.cs) is partitioned per caller
/// IP (5 requests/hour/IP). WebApplicationFactory's in-process TestServer has no
/// real network layer, so every request in a test run reports the same fixed
/// RemoteIpAddress — meaning all requests against a given WebAppFixture/TestServer
/// share a single partition/bucket, just like a global limiter would. So
/// exhausting it here must still not share a WebAppFixture/TestServer with any
/// other test that creates appointments. IClassFixture gives this class its own
/// fixture instance (and thus its own limiter bucket), isolating the other
/// Appointments tests from 429s caused by this test exhausting the quota.
/// </summary>
public class AppointmentCreationRateLimitTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AppointmentCreationRateLimitTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private async Task<Guid> RegisterBarber()
    {
        var email = $"barber-ratelimit-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Barber", email);
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Password123!" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return body.GetProperty("barberId").GetGuid();
    }

    [Fact]
    public async Task CreateAppointment_SixthRequestWithinWindow_Returns429()
    {
        for (var i = 0; i < 5; i++)
        {
            var barberId = await RegisterBarber();
            var scheduledAt = DateTime.UtcNow.AddDays(5).Date.AddHours(9 + i);
            var resp = await _client.PostAsJsonAsync("/api/v1/appointments", new
            {
                clientName = "Cliente Rate Limit",
                clientPhone = $"+551199999{1000 + i}",
                barberId,
                scheduledAt = scheduledAt.ToString("o"),
                serviceIds = new[] { ServiceConfiguration.BarbaId }
            });
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var lastBarberId = await RegisterBarber();
        var sixthScheduledAt = DateTime.UtcNow.AddDays(5).Date.AddHours(20);
        var sixthResponse = await _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Rate Limit",
            clientPhone = "+5511999991999",
            barberId = lastBarberId,
            scheduledAt = sixthScheduledAt.ToString("o"),
            serviceIds = new[] { ServiceConfiguration.BarbaId }
        });

        sixthResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
