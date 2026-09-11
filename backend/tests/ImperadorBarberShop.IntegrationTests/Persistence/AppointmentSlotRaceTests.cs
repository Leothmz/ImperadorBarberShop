using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.ValueObjects;
using ImperadorBarberShop.Infrastructure.Persistence;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImperadorBarberShop.IntegrationTests.Persistence;

/// <summary>
/// Two requests for the same slot can both pass the overlap check before either one
/// commits. The (BarberId, ScheduledAt) unique index stops the second — and it must
/// reach the client as the same 409 as the overlap check, not as a 500.
/// </summary>
public sealed class AppointmentSlotRaceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<AppDbContext> _options = null!;
    private Guid _barberId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        await using var db = new AppDbContext(_options);
        await db.Database.MigrateAsync();
        var user = User.CreateBarber("Barbeiro", $"race-{Guid.NewGuid()}@test.com", "hash");
        var barber = Barber.Create(user.Id);
        db.Users.Add(user);
        db.Barbers.Add(barber);
        await db.SaveChangesAsync();
        _barberId = barber.Id;
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    private async Task<Appointment> NewAppointmentAsync(AppDbContext db, string phone)
    {
        var corte = await db.Services.SingleAsync(s => s.Id == ServiceConfiguration.CorteId);
        return Appointment.Create("Cliente", phone, _barberId, new DateTime(2026, 9, 20, 10, 0, 0), 30, null, [corte]);
    }

    [Fact]
    public async Task SecondInsertForTheSameBarberAndTime_ThrowsConflictWithTheSlotTakenMessage()
    {
        await using (var first = new AppDbContext(_options))
        {
            first.Appointments.Add(await NewAppointmentAsync(first, "+5511999994401"));
            await first.SaveChangesAsync();
        }

        await using var second = new AppDbContext(_options);
        second.Appointments.Add(await NewAppointmentAsync(second, "+5511999994402"));

        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<ConflictException>().WithMessage(Appointment.SlotTakenMessage);
    }

    // Os dois primeiros agendamentos de um telefone novo passam juntos pelo "cliente não existe"
    // e tentam criá-lo. O índice único de MatchKey barra o segundo — como 409, e sem culpar o horário.
    [Fact]
    public async Task FirstBookingsFromTheSamePhoneAtOnce_SecondThrowsConflictWithTheDuplicateClientMessage()
    {
        await using (var first = new AppDbContext(_options))
        {
            var client = Client.Create("Ana", BrazilianPhone.Parse("11 98877-6655"), DateTime.UtcNow);
            first.Clients.Add(client);
            first.Appointments.Add(Appointment.Create("Ana", client.Phone, _barberId,
                new DateTime(2026, 9, 21, 10, 0, 0), 30, null, [], client.Id));
            await first.SaveChangesAsync();
        }

        await using var second = new AppDbContext(_options);
        // Mesma pessoa, digitado sem o nono dígito, noutro horário
        var duplicate = Client.Create("Ana Paula", BrazilianPhone.Parse("11 8877-6655"), DateTime.UtcNow);
        second.Clients.Add(duplicate);
        second.Appointments.Add(Appointment.Create("Ana Paula", duplicate.Phone, _barberId,
            new DateTime(2026, 9, 21, 15, 0, 0), 30, null, [], duplicate.Id));

        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<ConflictException>().WithMessage(Client.DuplicateMessage);
    }

    [Fact]
    public async Task UniqueViolationOutsideAppointments_IsNotReportedAsASlotConflict()
    {
        var email = $"dup-{Guid.NewGuid()}@test.com";
        await using (var first = new AppDbContext(_options))
        {
            first.Users.Add(User.CreateBarber("Um", email, "hash"));
            await first.SaveChangesAsync();
        }

        await using var second = new AppDbContext(_options);
        second.Users.Add(User.CreateBarber("Dois", email, "hash"));

        var act = () => second.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.Should().NotBeOfType<ConflictException>();
    }
}
