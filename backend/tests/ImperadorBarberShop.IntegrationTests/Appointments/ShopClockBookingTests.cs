using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ImperadorBarberShop.Application.Commands.Auth;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;
using ImperadorBarberShop.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImperadorBarberShop.IntegrationTests.Appointments;

/// <summary>
/// The report's three live probes, replayed over HTTP against a server whose clock is
/// UTC while the shop is 3 hours behind: 21:58 UTC = 18:58 in São Paulo, Thursday.
/// Each one failed while wall-clock times were compared with DateTime.UtcNow.
/// </summary>
public class ShopClockBookingTests : IClassFixture<ShopClockBookingTests.EveningShopFixture>
{
    private static readonly DateOnly Today = new(2026, 9, 10);

    private readonly EveningShopFixture _fixture;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ShopClockBookingTests(EveningShopFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private async Task<Guid> SeedBarberOpenAllThursdayAsync()
    {
        var email = $"barber-clock-{Guid.NewGuid()}@test.com";
        using (var scope = _fixture.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new RegisterBarberCommand("Barber", email, "Password123!",
                [new AvailabilitySlotInput(DayOfWeek.Thursday, new TimeOnly(8, 0), new TimeOnly(23, 45))]));
        }

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password123!" });
        return (await login.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions)).GetProperty("barberId").GetGuid();
    }

    private Task<HttpResponseMessage> BookAsync(Guid barberId, string scheduledAt, string phone) =>
        _client.PostAsJsonAsync("/api/v1/appointments", new
        {
            clientName = "Cliente Relógio",
            clientPhone = phone,
            barberId,
            scheduledAt,
            serviceIds = new[] { ServiceConfiguration.CorteId }
        });

    private async Task<string> BookTokenAsync(Guid barberId, string scheduledAt, string phone)
    {
        var response = await BookAsync(barberId, scheduledAt, phone);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions)).GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Slots_Today_StartRightAfterShopLocalNow()
    {
        var barberId = await SeedBarberOpenAllThursdayAsync();

        var slots = await _client.GetFromJsonAsync<List<string>>(
            $"/api/v1/barbers/{barberId}/slots?date={Today:yyyy-MM-dd}&serviceIds={ServiceConfiguration.CorteId}",
            _jsonOptions);

        // Antes: o primeiro horário era 22:00 — as três horas seguintes sumiam
        slots.Should().NotBeNull();
        slots!.First().Should().Be("19:00:00");
        slots.Should().Contain("20:00:00");
    }

    [Fact]
    public async Task Booking_OneHourAheadInShopTime_IsAccepted()
    {
        var barberId = await SeedBarberOpenAllThursdayAsync();

        // Antes: 400 "ScheduledAt must be in the future."
        var response = await BookAsync(barberId, "2026-09-10T20:00:00", "+5511999992201");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Cancel_FourHoursAheadInShopTime_IsAllowed()
    {
        var barberId = await SeedBarberOpenAllThursdayAsync();
        var token = await BookTokenAsync(barberId, "2026-09-10T23:00:00", "+5511999992202");

        // Antes: 422 — a janela de "2 horas" era, na prática, de 5
        var response = await _client.PostAsync($"/api/v1/appointments/manage/{token}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Cancel_InsideTheTwoHourWindow_IsRefusedInPortuguese()
    {
        var barberId = await SeedBarberOpenAllThursdayAsync();
        var token = await BookTokenAsync(barberId, "2026-09-10T20:30:00", "+5511999992203");

        var response = await _client.PostAsync($"/api/v1/appointments/manage/{token}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        problem.GetProperty("detail").GetString().Should().Contain("2 horas");
    }

    /// <summary>The real <see cref="ShopTimeProvider"/> zone, frozen at 21:58 UTC.</summary>
    public class EveningShopFixture : WebAppFixture
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(
                    new FrozenShopClock(new DateTimeOffset(2026, 9, 10, 21, 58, 0, TimeSpan.Zero)));
            });
        }
    }

    private sealed class FrozenShopClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
        public override TimeZoneInfo LocalTimeZone => ShopTimeProvider.ShopTimeZone;
    }
}
