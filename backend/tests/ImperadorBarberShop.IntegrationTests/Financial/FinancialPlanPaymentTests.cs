using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Infrastructure.Persistence;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImperadorBarberShop.IntegrationTests.Financial;

/// <summary>
/// Todo relatório soma o valor efetivo (ChargedAmount ?? soma dos preços guardados): o plano
/// entra pelo que foi cobrado, a recorrência por R$ 0, e nenhum atendimento é contado duas vezes.
/// O ticket médio olha só os atendimentos avulsos.
/// </summary>
public class FinancialPlanPaymentTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _admin;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FinancialPlanPaymentTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _admin = fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
    }

    private async Task<(HttpClient Client, Guid BarberId)> BarberAsync(string name)
    {
        var email = $"fin-plano-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync(name, email);
        var login = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(_json);
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return (client, body.GetProperty("barberId").GetGuid());
    }

    private async Task<Guid> SeedAcceptedAsync(Guid barberId, DateTime scheduledAt, params Guid[] serviceIds)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var services = await db.Services.Where(s => serviceIds.Contains(s.Id)).ToListAsync();
        var appointment = Appointment.Create("Cliente Financeiro", "+5511999990000", barberId, scheduledAt,
            services.Sum(s => s.DurationMinutes), null, services);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    /// <summary>O admin semeado: despesa guarda o usuário que a criou.</summary>
    private async Task<HttpClient> SeededAdminAsync()
    {
        var login = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@test.com", password = "AdminTest123!" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(_json)).GetProperty("accessToken").GetString();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task CompleteAsync(HttpClient client, string url, object body)
        => (await client.PatchAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    [Fact]
    public async Task EveryReport_UsesTheEffectiveAmount_AndTheAverageTicketIgnoresPlans()
    {
        var day1 = new DateTime(2025, 5, 5);
        var day2 = day1.AddDays(1);
        var (barberA, barberAId) = await BarberAsync("Barbeiro A");
        var (_, barberBId) = await BarberAsync("Barbeiro B");

        // A, dia 1: um avulso no Pix (35 + 25) e um pagamento de plano de R$ 150 no cartão
        var normalPix = await SeedAcceptedAsync(barberAId, day1.AddHours(10), ServiceConfiguration.CorteId, ServiceConfiguration.BarbaId);
        var planPayment = await SeedAcceptedAsync(barberAId, day1.AddHours(11), ServiceConfiguration.CorteId, ServiceConfiguration.BarbaId);
        await CompleteAsync(barberA, $"/api/v1/appointments/{normalPix}/complete", new { paymentMethod = "Pix" });
        await CompleteAsync(barberA, $"/api/v1/appointments/{planPayment}/complete",
            new { paymentMethod = "Plano", planKind = "Pagamento", planTender = "Cartão", chargedAmount = 150m });

        // B, dia 2, concluídos pelo admin: uma recorrência de plano e um avulso sem pagamento registrado
        var recurrence = await SeedAcceptedAsync(barberBId, day2.AddHours(10), ServiceConfiguration.CorteId);
        var unpaid = await SeedAcceptedAsync(barberBId, day2.AddHours(11), ServiceConfiguration.CorteId);
        await CompleteAsync(_admin, $"/api/v1/admin/appointments/{recurrence}/complete",
            new { paymentMethod = "Plano", planKind = "Recorrencia" });
        await CompleteAsync(_admin, $"/api/v1/admin/appointments/{unpaid}/complete", new { });

        // Fora do período: não entra em nada
        var outside = await SeedAcceptedAsync(barberAId, day2.AddDays(1).AddHours(10), ServiceConfiguration.CorteId);
        await CompleteAsync(barberA, $"/api/v1/appointments/{outside}/complete",
            new { paymentMethod = "Plano", planKind = "Pagamento", planTender = "Pix", chargedAmount = 999m });

        var admin = await SeededAdminAsync();
        (await admin.PostAsJsonAsync("/api/v1/admin/financial/expenses",
                new { amount = 45m, description = "Produto", date = $"{day1:yyyy-MM-dd}" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var period = $"from={day1:yyyy-MM-dd}&to={day2:yyyy-MM-dd}";

        var summary = await _admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/summary?{period}", _json);
        summary.GetProperty("totalRevenue").GetDecimal().Should().Be(245m, "60 + 150 + 0 + 35");
        summary.GetProperty("totalAppointments").GetInt32().Should().Be(4, "a recorrência também é atendimento");
        summary.GetProperty("averageTicket").GetDecimal().Should().Be(47.50m, "só os avulsos: (60 + 35) / 2");
        summary.GetProperty("totalExpenses").GetDecimal().Should().Be(45m);
        summary.GetProperty("netRevenue").GetDecimal().Should().Be(200m);

        var byBarber = await _admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/by-barber?{period}", _json);
        byBarber.EnumerateArray()
            .Select(r => (r.GetProperty("barberId").GetGuid(), r.GetProperty("appointments").GetInt32(), r.GetProperty("revenue").GetDecimal()))
            .Should().Equal((barberAId, 2, 210m), (barberBId, 2, 35m));

        var timeline = await _admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/timeline?{period}", _json);
        timeline.EnumerateArray()
            .Select(r => (r.GetProperty("period").GetString(), r.GetProperty("revenue").GetDecimal(), r.GetProperty("appointments").GetInt32()))
            .Should().Equal(($"{day1:yyyy-MM-dd}", 210m, 2), ($"{day2:yyyy-MM-dd}", 35m, 2));

        // Plano numa linha só; os serviços dos atendimentos de plano não entram nas linhas reais
        var byService = await _admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/by-service?{period}", _json);
        byService.EnumerateArray()
            .Select(r => (r.GetProperty("serviceId").GetGuid(), r.GetProperty("serviceName").GetString(),
                r.GetProperty("count").GetInt32(), r.GetProperty("revenue").GetDecimal()))
            .Should().Equal(
                (Guid.Empty, "Plano", 2, 150m),
                (ServiceConfiguration.CorteId, "Corte", 2, 70m),
                (ServiceConfiguration.BarbaId, "Barba", 1, 25m));
        byService.EnumerateArray().Sum(r => r.GetProperty("revenue").GetDecimal())
            .Should().Be(245m, "a quebra por serviço fecha com a receita total");

        var csvLines = (await _admin.GetStringAsync($"/api/v1/admin/financial/export?{period}"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        csvLines[0].Should().Be("Data,Barbeiro,Cliente,Telefone,Serviço,Preço,AgendamentoId");
        var rows = csvLines.Skip(1).ToList();
        rows.Should().HaveCount(5);
        rows.Where(r => r.EndsWith($",{normalPix}")).Should().HaveCount(2, "avulso mantém uma linha por serviço");
        rows.Should().ContainSingle(r => r.EndsWith($",{planPayment}"))
            .Which.Should().Contain($",Barbeiro A,").And.Contain($",Plano,{150m:F2},");
        rows.Should().ContainSingle(r => r.EndsWith($",{recurrence}"))
            .Which.Should().StartWith($"{day2:yyyy-MM-dd},Barbeiro B,Cliente Financeiro,").And.Contain($",Plano,{0m:F2},");
        rows.Should().ContainSingle(r => r.EndsWith($",{unpaid}")).Which.Should().Contain($",Corte,{35m:F2},");
        rows.Should().NotContain(r => r.EndsWith($",{outside}"));
    }

    [Fact]
    public async Task OnlyPlanAppointments_AverageTicketIsZeroButRevenueAndVisitsCount()
    {
        var day = new DateTime(2025, 6, 2);
        var (barber, barberId) = await BarberAsync("Barbeiro Só Plano");
        var planPayment = await SeedAcceptedAsync(barberId, day.AddHours(10), ServiceConfiguration.CorteId);
        var recurrence = await SeedAcceptedAsync(barberId, day.AddHours(11), ServiceConfiguration.CorteId);
        await CompleteAsync(barber, $"/api/v1/appointments/{planPayment}/complete",
            new { paymentMethod = "Plano", planKind = "Pagamento", planTender = "Pix", chargedAmount = 200m });
        await CompleteAsync(barber, $"/api/v1/appointments/{recurrence}/complete",
            new { paymentMethod = "Plano", planKind = "Recorrencia" });

        var summary = await _admin.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/financial/summary?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}", _json);

        summary.GetProperty("totalRevenue").GetDecimal().Should().Be(200m);
        summary.GetProperty("totalAppointments").GetInt32().Should().Be(2);
        summary.GetProperty("averageTicket").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task ChangingAPlanPaymentLater_MovesTheReportsWithIt()
    {
        var day = new DateTime(2025, 7, 7);
        var (barber, barberId) = await BarberAsync("Barbeiro Troca");
        var id = await SeedAcceptedAsync(barberId, day.AddHours(10), ServiceConfiguration.CorteId, ServiceConfiguration.BarbaId);
        await CompleteAsync(barber, $"/api/v1/appointments/{id}/complete", new { paymentMethod = "Dinheiro" });
        var period = $"from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}";
        (await RevenueAsync(period)).Should().Be(60m);

        await CompleteAsync(barber, $"/api/v1/appointments/{id}/payment",
            new { paymentMethod = "Plano", planKind = "Pagamento", planTender = "Pix", chargedAmount = 130m });
        (await RevenueAsync(period)).Should().Be(130m);

        await CompleteAsync(_admin, $"/api/v1/admin/appointments/{id}/payment", new { paymentMethod = "Plano", planKind = "Recorrencia" });
        (await RevenueAsync(period)).Should().Be(0m);

        await CompleteAsync(_admin, $"/api/v1/admin/appointments/{id}/payment", new { paymentMethod = "Cartão" });
        (await RevenueAsync(period)).Should().Be(60m, "de volta ao método normal, vale a soma dos serviços");
    }

    private async Task<decimal> RevenueAsync(string period)
        => (await _admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/financial/summary?{period}", _json))
            .GetProperty("totalRevenue").GetDecimal();
}
