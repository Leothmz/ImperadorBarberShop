using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Infrastructure.Persistence;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

/// <summary>
/// O fluxo Plano pelos endpoints do barbeiro e do admin: concluir já escolhendo o plano,
/// registrar ou trocar depois, e as combinações inválidas recusadas no servidor.
/// Os agendamentos entram direto no banco: o limitador público (5/h por IP) é de outra suíte.
/// </summary>
public class PlanPaymentControllerTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _fixture;
    private readonly HttpClient _admin;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PlanPaymentControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _admin = fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
    }

    private async Task<(HttpClient Client, Guid BarberId)> BarberAsync()
    {
        var email = $"plano-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Barbeiro Plano", email);
        var login = await _fixture.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(_json);
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return (client, body.GetProperty("barberId").GetGuid());
    }

    private static int _slot;

    /// <summary>Corte (R$35) + Barba (R$25), confirmado, cada um num horário próprio.</summary>
    private async Task<Guid> SeedAcceptedAsync(Guid barberId)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var services = await db.Services
            .Where(s => s.Id == ServiceConfiguration.CorteId || s.Id == ServiceConfiguration.BarbaId)
            .ToListAsync();
        var scheduledAt = new DateTime(2026, 3, 10, 9, 0, 0).AddHours(Interlocked.Increment(ref _slot));
        var appointment = Appointment.Create("Cliente Plano", "+5511999990000", barberId, scheduledAt, 50, null, services);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private async Task<JsonElement> FromAgendaAsync(HttpClient barber, Guid appointmentId)
    {
        var agenda = await barber.GetFromJsonAsync<JsonElement>("/api/v1/appointments/barber", _json);
        return agenda.EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == appointmentId);
    }

    private async Task<JsonElement> FromAdminListAsync(Guid barberId, Guid appointmentId)
    {
        var list = await _admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/barbers/{barberId}/appointments", _json);
        return list.EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == appointmentId);
    }

    private static readonly object PlanPayment =
        new { paymentMethod = "Plano", planKind = "Pagamento", planTender = "Pix", chargedAmount = 120m };

    private static readonly object PlanRecurrence = new { paymentMethod = "Plano", planKind = "Recorrencia" };

    [Fact]
    public async Task BarberCompletes_WithPlanPayment_PersistsThePlanAndKeepsTheServices()
    {
        var (barber, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);

        (await barber.PatchAsJsonAsync($"/api/v1/appointments/{id}/complete", PlanPayment))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var appt = await FromAgendaAsync(barber, id);
        appt.GetProperty("status").GetString().Should().Be("Completed");
        appt.GetProperty("paymentMethod").GetString().Should().Be("Plano");
        appt.GetProperty("planKind").GetString().Should().Be("Pagamento");
        appt.GetProperty("planTender").GetString().Should().Be("Pix");
        appt.GetProperty("chargedAmount").GetDecimal().Should().Be(120m);
        appt.GetProperty("effectiveAmount").GetDecimal().Should().Be(120m);
        appt.GetProperty("paidAt").ValueKind.Should().Be(JsonValueKind.String);
        appt.GetProperty("services").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task BarberCompletes_WithPlanRecurrence_IsCompletedAtZeroWithoutAPaymentDate()
    {
        var (barber, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);

        (await barber.PatchAsJsonAsync($"/api/v1/appointments/{id}/complete", PlanRecurrence))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var appt = await FromAgendaAsync(barber, id);
        appt.GetProperty("status").GetString().Should().Be("Completed");
        appt.GetProperty("planKind").GetString().Should().Be("Recorrencia");
        appt.GetProperty("planTender").ValueKind.Should().Be(JsonValueKind.Null);
        appt.GetProperty("chargedAmount").GetDecimal().Should().Be(0m);
        appt.GetProperty("effectiveAmount").GetDecimal().Should().Be(0m);
        appt.GetProperty("paidAt").ValueKind.Should().Be(JsonValueKind.Null);
        appt.GetProperty("services").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task NormalMethod_KeepsTheServiceTotalAndNoPlanFields()
    {
        var (barber, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);

        (await barber.PatchAsJsonAsync($"/api/v1/appointments/{id}/complete", new { paymentMethod = "Cartão" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var appt = await FromAgendaAsync(barber, id);
        appt.GetProperty("paymentMethod").GetString().Should().Be("Cartão");
        appt.GetProperty("planKind").ValueKind.Should().Be(JsonValueKind.Null);
        appt.GetProperty("chargedAmount").ValueKind.Should().Be(JsonValueKind.Null);
        appt.GetProperty("effectiveAmount").GetDecimal().Should().Be(60m);
    }

    [Fact]
    public async Task AdminCompletes_WithPlanPayment_OnAnyBarbersAppointment()
    {
        var (_, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);

        (await _admin.PatchAsJsonAsync($"/api/v1/admin/appointments/{id}/complete",
                new { paymentMethod = "Plano", planKind = "Pagamento", planTender = "Dinheiro", chargedAmount = 0m }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var appt = await FromAdminListAsync(barberId, id);
        appt.GetProperty("status").GetString().Should().Be("Completed");
        appt.GetProperty("planTender").GetString().Should().Be("Dinheiro");
        appt.GetProperty("effectiveAmount").GetDecimal().Should().Be(0m, "zero é um valor válido no pagamento do plano");
        appt.GetProperty("paidAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task BarberRegistersThePlanLater_ThenSwitchesBack_ClearsThePlan()
    {
        var (barber, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);
        (await barber.PatchAsync($"/api/v1/appointments/{id}/complete", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await barber.PatchAsJsonAsync($"/api/v1/appointments/{id}/payment", PlanRecurrence))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var recurrence = await FromAgendaAsync(barber, id);
        recurrence.GetProperty("planKind").GetString().Should().Be("Recorrencia");
        recurrence.GetProperty("effectiveAmount").GetDecimal().Should().Be(0m);

        (await barber.PatchAsJsonAsync($"/api/v1/appointments/{id}/payment", new { paymentMethod = "Pix" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var normal = await FromAgendaAsync(barber, id);
        normal.GetProperty("paymentMethod").GetString().Should().Be("Pix");
        normal.GetProperty("planKind").ValueKind.Should().Be(JsonValueKind.Null);
        normal.GetProperty("planTender").ValueKind.Should().Be(JsonValueKind.Null);
        normal.GetProperty("chargedAmount").ValueKind.Should().Be(JsonValueKind.Null);
        normal.GetProperty("effectiveAmount").GetDecimal().Should().Be(60m);
        normal.GetProperty("paidAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task AdminRegistersAPlanPaymentLater()
    {
        var (_, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);
        (await _admin.PatchAsync($"/api/v1/admin/appointments/{id}/complete", JsonContent.Create(new { })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _admin.PatchAsJsonAsync($"/api/v1/admin/appointments/{id}/payment",
                new { paymentMethod = "Plano", planKind = "Pagamento", planTender = "Cartão", chargedAmount = 89.90m }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var appt = await FromAdminListAsync(barberId, id);
        appt.GetProperty("planKind").GetString().Should().Be("Pagamento");
        appt.GetProperty("planTender").GetString().Should().Be("Cartão");
        appt.GetProperty("chargedAmount").GetDecimal().Should().Be(89.90m);
    }

    public static TheoryData<string, string> InvalidPayloads => new()
    {
        { """{"paymentMethod":"Plano"}""", "PlanKind" },
        { """{"paymentMethod":"Plano","planKind":"Pagamento","chargedAmount":50}""", "PlanTender" },
        { """{"paymentMethod":"Plano","planKind":"Pagamento","planTender":"Pix"}""", "ChargedAmount" },
        { """{"paymentMethod":"Plano","planKind":"Pagamento","planTender":"Pix","chargedAmount":-5}""", "ChargedAmount" },
        { """{"paymentMethod":"Plano","planKind":"Pagamento","planTender":"Plano","chargedAmount":50}""", "PlanTender" },
        { """{"paymentMethod":"Plano","planKind":"Recorrencia","planTender":"Pix"}""", "PlanTender" },
        { """{"paymentMethod":"Plano","planKind":"Recorrencia","chargedAmount":35}""", "ChargedAmount" },
        { """{"paymentMethod":"Pix","planKind":"Pagamento"}""", "PlanKind" },
    };

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public async Task BarberComplete_InvalidPlanPayload_Returns400AndLeavesItAccepted(string payload, string field)
    {
        var (barber, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);

        var resp = await barber.PatchAsync($"/api/v1/appointments/{id}/complete", Json(payload));

        await ShouldFailOnAsync(resp, field);
        (await FromAgendaAsync(barber, id)).GetProperty("status").GetString().Should().Be("Accepted");
    }

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public async Task AdminPayment_InvalidPlanPayload_Returns400AndKeepsThePayment(string payload, string field)
    {
        var (_, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);
        (await _admin.PatchAsJsonAsync($"/api/v1/admin/appointments/{id}/complete", new { paymentMethod = "Pix" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resp = await _admin.PatchAsync($"/api/v1/admin/appointments/{id}/payment", Json(payload));

        await ShouldFailOnAsync(resp, field);
        var appt = await FromAdminListAsync(barberId, id);
        appt.GetProperty("paymentMethod").GetString().Should().Be("Pix");
        appt.GetProperty("planKind").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task BarberPayment_InvalidPlanPayload_Returns400()
    {
        var (barber, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);
        (await barber.PatchAsync($"/api/v1/appointments/{id}/complete", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var resp = await barber.PatchAsync($"/api/v1/appointments/{id}/payment",
            Json("""{"paymentMethod":"Plano","planKind":"Recorrencia","planTender":"Dinheiro"}"""));

        await ShouldFailOnAsync(resp, "PlanTender");
    }

    [Fact]
    public async Task AdminComplete_PlanDataWithoutPlanMethod_Returns400()
    {
        var (_, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);

        var resp = await _admin.PatchAsync($"/api/v1/admin/appointments/{id}/complete",
            Json("""{"planKind":"Recorrencia"}"""));

        await ShouldFailOnAsync(resp, "PaymentMethod");
    }

    [Fact]
    public async Task OtherBarber_CannotCompleteOrPayWithAPlan()
    {
        var (_, ownerId) = await BarberAsync();
        var (intruder, _) = await BarberAsync();
        var accepted = await SeedAcceptedAsync(ownerId);
        var completed = await SeedAcceptedAsync(ownerId);
        (await _admin.PatchAsync($"/api/v1/admin/appointments/{completed}/complete", JsonContent.Create(new { })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await intruder.PatchAsJsonAsync($"/api/v1/appointments/{accepted}/complete", PlanPayment))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await intruder.PatchAsJsonAsync($"/api/v1/appointments/{completed}/payment", PlanRecurrence))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await FromAdminListAsync(ownerId, accepted)).GetProperty("status").GetString().Should().Be("Accepted");
        (await FromAdminListAsync(ownerId, completed)).GetProperty("paymentMethod").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Barber_CannotUseTheAdminPlanEndpoint()
    {
        var (barber, barberId) = await BarberAsync();
        var id = await SeedAcceptedAsync(barberId);

        (await barber.PatchAsJsonAsync($"/api/v1/admin/appointments/{id}/complete", PlanRecurrence))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static StringContent Json(string payload) => new(payload, System.Text.Encoding.UTF8, "application/json");

    private async Task ShouldFailOnAsync(HttpResponseMessage resp, string field)
    {
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        problem.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue(
            $"o erro deve apontar {field}: {problem}");
    }
}
