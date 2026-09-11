using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Clients;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.ValueObjects;
using ImperadorBarberShop.Infrastructure.Persistence;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ImperadorBarberShop.IntegrationTests.Clients;

/// <summary>
/// Recorrência de ponta a ponta: o agendamento anônimo alimenta o cadastro do cliente, o
/// dashboard lista quem está sumindo e o convite de volta sai pelo WhatsApp uma vez só.
/// Relógio real da barbearia: as datas são relativas ao "agora" dela e ficam longe das bordas
/// da janela, que os testes de unidade cobrem com o relógio parado.
/// </summary>
public class AdminClientsControllerTests : IClassFixture<WebAppFixture>
{
    private const string CandidatesUrl = "/api/v1/admin/clients/reinvite-candidates";

    private readonly WebAppFixture _fixture;
    private readonly HttpClient _admin;
    private readonly HttpClient _public;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public AdminClientsControllerTests(WebAppFixture fixture)
    {
        _fixture = fixture;
        _admin = fixture.CreateAuthenticatedClient("Admin", Guid.NewGuid());
        _public = fixture.CreateClient();
    }

    private static string ReinviteUrl(Guid clientId) => $"/api/v1/admin/clients/{clientId}/reinvite";

    // Celular único por teste: a classe divide o mesmo banco. Os 8 finais começam em 6–9,
    // senão a forma sem o nono dígito seria um fixo e o agendamento a recusaria.
    private static string UniqueSubscriber() => $"9{Random.Shared.Next(60_000_000, 100_000_000)}";

    private async Task<Guid> SeedBarberAsync()
    {
        var email = $"barber-clients-{Guid.NewGuid()}@test.com";
        await _fixture.SeedBarberAsync("Barbeiro Clientes", email);
        var login = await _public.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        return (await login.Content.ReadFromJsonAsync<JsonElement>(_json)).GetProperty("barberId").GetGuid();
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, DateTime, Task<T>> work)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopNow = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetLocalNow().DateTime;
        return await work(db, shopNow);
    }

    private Task<Client> SeedClientAsync(
        string name, int? lastVisitDaysAgo, int? invitedDaysAgo = null, Guid? upcomingWithBarber = null)
        => WithDbAsync(async (db, now) =>
        {
            var client = Client.Create(name, BrazilianPhone.Parse($"11{UniqueSubscriber()}"), DateTime.UtcNow.AddDays(-90));
            if (lastVisitDaysAgo is { } visit) client.RegisterVisit(now.Date.AddDays(-visit).AddHours(10));
            if (invitedDaysAgo is { } invite) client.MarkInvited(now.AddDays(-invite));
            db.Clients.Add(client);
            if (upcomingWithBarber is { } barberId)
                db.Appointments.Add(Appointment.Create(name, client.Phone, barberId,
                    now.Date.AddDays(3).AddHours(Random.Shared.Next(8, 20)), 30, null, [], client.Id));
            await db.SaveChangesAsync();
            return client;
        });

    private async Task<List<JsonElement>> CandidatesAsync()
    {
        var response = await _admin.GetAsync(CandidatesUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<JsonElement>>(_json))!;
    }

    private async Task SetChannelsAsync(params string[] channels)
    {
        var response = await _admin.PutAsJsonAsync("/api/v1/admin/notifications/settings",
            new { channels, reminderMinutesBefore = 60, notificationPhone = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Endpoints_AreAdminOnly()
    {
        (await _public.GetAsync(CandidatesUrl)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var barber = _fixture.CreateAuthenticatedClient("Barber", Guid.NewGuid(), Guid.NewGuid());
        (await barber.GetAsync(CandidatesUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await barber.PostAsync(ReinviteUrl(Guid.NewGuid()), null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReinviteCandidates_ListsOnlyClientsNearChurn()
    {
        var barberId = await SeedBarberAsync();
        var due = await SeedClientAsync("Sumido", lastVisitDaysAgo: 27);
        var booked = await SeedClientAsync("Já Marcou", lastVisitDaysAgo: 27, upcomingWithBarber: barberId);
        var invited = await SeedClientAsync("Convidado", lastVisitDaysAgo: 28, invitedDaysAgo: 3);
        var recent = await SeedClientAsync("Recente", lastVisitDaysAgo: 10);
        var gone = await SeedClientAsync("Perdido", lastVisitDaysAgo: 40);
        var neverCame = await SeedClientAsync("Nunca Veio", lastVisitDaysAgo: null);

        var candidates = await CandidatesAsync();

        var ids = candidates.Select(c => c.GetProperty("clientId").GetGuid()).ToList();
        ids.Should().Contain(due.Id);
        ids.Should().NotContain(new[] { booked.Id, invited.Id, recent.Id, gone.Id, neverCame.Id });

        var row = candidates.Single(c => c.GetProperty("clientId").GetGuid() == due.Id);
        row.GetProperty("name").GetString().Should().Be("Sumido");
        row.GetProperty("phone").GetString().Should().Be(due.Phone);
        row.GetProperty("daysSinceLastVisit").GetInt32().Should().Be(27);
        row.GetProperty("visitCount").GetInt32().Should().Be(1);
        // Horário de parede, sem fuso: o front mostra a data como veio
        row.GetProperty("lastVisitAt").GetString().Should().Be($"{due.LastVisitAt:yyyy-MM-ddTHH:mm:ss}");
    }

    [Fact]
    public async Task Reinvite_RecordsTheInviteAndDropsTheClientFromTheList()
    {
        var client = await SeedClientAsync("Para Convidar", lastVisitDaysAgo: 26);

        // Canal desligado: recusa em vez de marcar um convite que não sairia
        await SetChannelsAsync("email");
        var refused = await _admin.PostAsync(ReinviteUrl(client.Id), null);
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await refused.Content.ReadFromJsonAsync<JsonElement>(_json)).GetProperty("detail").GetString()
            .Should().Be(ReinviteClientCommandHandler.WhatsAppDisabledMessage);

        await SetChannelsAsync("email", "whatsapp");
        var sent = await _admin.PostAsync(ReinviteUrl(client.Id), null);
        sent.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (lastInviteAt, shopNow) = await WithDbAsync(async (db, now) =>
            ((await db.Clients.AsNoTracking().SingleAsync(c => c.Id == client.Id)).LastInviteAt, now));
        lastInviteAt.Should().BeCloseTo(shopNow, TimeSpan.FromMinutes(1));

        (await CandidatesAsync()).Select(c => c.GetProperty("clientId").GetGuid()).Should().NotContain(client.Id);

        // Clique duplo, ou outra aba: o segundo convite não sai
        (await _admin.PostAsync(ReinviteUrl(client.Id), null)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reinvite_UnknownClient_Returns404()
    {
        await SetChannelsAsync("whatsapp");

        (await _admin.PostAsync(ReinviteUrl(Guid.NewGuid()), null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Booking_PhoneTypedTwoWays_IsOneClientAndOnlyCompletionCountsAVisit()
    {
        var barberId = await SeedBarberAsync();
        var subscriber = UniqueSubscriber();
        var day = DateTime.UtcNow.AddDays(4).Date;

        async Task<Guid> BookAsync(string name, string phone, int hour)
        {
            var response = await _public.PostAsJsonAsync("/api/v1/appointments", new
            {
                clientName = name,
                clientPhone = phone,
                barberId,
                scheduledAt = day.AddHours(hour).ToString("yyyy-MM-ddTHH:mm:ss"),
                serviceIds = new[] { ServiceConfiguration.BarbaId }
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<JsonElement>(_json)).GetProperty("id").GetGuid();
        }

        // Primeiro com +55 e nono dígito; depois sem os dois, com máscara
        var first = await BookAsync("Paulo", $"+5521{subscriber}", 10);
        var second = await BookAsync("Paulo Souza", $"(21) {subscriber[1..5]}-{subscriber[5..]}", 14);

        var matchKey = $"21{subscriber[1..]}";
        var (client, appointments) = await WithDbAsync(async (db, _) =>
        {
            var c = await db.Clients.AsNoTracking().SingleAsync(x => x.MatchKey == matchKey);
            var a = await db.Appointments.AsNoTracking().Where(x => x.Id == first || x.Id == second).ToListAsync();
            return (c, a);
        });
        client.Name.Should().Be("Paulo");
        client.Phone.Should().Be($"+5521{subscriber}");
        client.VisitCount.Should().Be(0);
        appointments.Should().HaveCount(2).And.OnlyContain(a => a.ClientId == client.Id && a.ClientPhone == client.Phone);

        (await _admin.PatchAsync($"/api/v1/admin/appointments/{first}/complete", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _admin.PatchAsync($"/api/v1/admin/appointments/{second}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await WithDbAsync((db, _) => db.Clients.AsNoTracking().SingleAsync(x => x.Id == client.Id));
        after.VisitCount.Should().Be(1);
        after.LastVisitAt.Should().Be(day.AddHours(10));
    }
}
