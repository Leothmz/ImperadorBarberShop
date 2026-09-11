using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;

namespace ImperadorBarberShop.IntegrationTests.Financial;

/// <summary>
/// Revenue is what an appointment cost when it was booked. Every financial figure used
/// to join the live Service.Price, so raising "Corte" from R$35 to R$50 rewrote every
/// past period's revenue, average ticket and CSV export.
/// </summary>
public class FinancialPriceSnapshotTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FinancialPriceSnapshotTests(WebAppFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RaisingAServicePrice_DoesNotChangeAPastPeriodsReportedRevenue()
    {
        var admin = _fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
        var publicClient = _fixture.CreateClient();
        var email = $"barber-snapshot-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Barbeiro Snapshot", email);
        var login = await publicClient.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        var barberId = (await login.Content.ReadFromJsonAsync<JsonElement>(_json)).GetProperty("barberId").GetGuid();

        var day = DateTime.UtcNow.AddDays(3).Date;
        var created = await publicClient.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Snapshot",
            clientPhone = "+5511999993301",
            barberId,
            scheduledAt = day.AddHours(10).ToString("yyyy-MM-ddTHH:mm:ss"),
            serviceIds = new[] { ServiceConfiguration.CorteId } // R$35 no catálogo semeado
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var appointmentId = (await created.Content.ReadFromJsonAsync<JsonElement>(_json)).GetProperty("id").GetGuid();
        (await admin.PatchAsync($"/api/v1/admin/appointments/{appointmentId}/complete", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var period = $"from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}";
        (await RevenueAsync(admin, period)).Should().Be(35m);

        // Reajuste de preço depois do período fechado
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Corte"), "name" },
            { new StringContent("Haircut"), "description" },
            { new StringContent("50"), "price" },
            { new StringContent("30"), "durationMinutes" },
        };
        (await admin.PutAsync($"/api/v1/services/{ServiceConfiguration.CorteId}", form))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RevenueAsync(admin, period)).Should().Be(35m);

        var byService = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/by-service?{period}", _json);
        byService.EnumerateArray().Single().GetProperty("revenue").GetDecimal().Should().Be(35m);

        var byBarber = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/by-barber?{period}", _json);
        byBarber.EnumerateArray().Single().GetProperty("revenue").GetDecimal().Should().Be(35m);

        var timeline = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/timeline?{period}", _json);
        timeline.EnumerateArray().Single().GetProperty("revenue").GetDecimal().Should().Be(35m);

        var csv = await admin.GetStringAsync($"/api/v1/admin/financial/export?{period}");
        csv.Should().Contain($",{35m:F2},").And.NotContain($",{50m:F2},");
    }

    private async Task<decimal> RevenueAsync(HttpClient admin, string period)
    {
        var summary = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/summary?{period}", _json);
        return summary.GetProperty("totalRevenue").GetDecimal();
    }
}
