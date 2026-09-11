using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Persistence;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ImperadorBarberShop.IntegrationTests.Persistence;

/// <summary>
/// Appointments booked before the price snapshot existed get the catalog price as of
/// the migration — the same number the reports already showed, so no past figure moves
/// on upgrade day. From then on, a price change no longer reaches them.
/// </summary>
public sealed class SnapshotAppointmentServicePriceMigrationTests : IAsyncLifetime
{
    private const string BeforeSnapshot = "20260703154933_InitialCreate";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public Task InitializeAsync() => _connection.OpenAsync();

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    [Fact]
    public async Task Migration_BackfillsExistingAppointmentLinesWithTheCatalogPrice()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeSnapshot);

        // Um agendamento de antes da migração, gravado no esquema antigo (sem UnitPrice).
        // GUIDs como TEXT maiúsculo, o formato em que o EF grava as chaves no SQLite.
        var userId = Guid.NewGuid().ToString().ToUpperInvariant();
        var barberId = Guid.NewGuid().ToString().ToUpperInvariant();
        var appointmentId = Guid.NewGuid().ToString().ToUpperInvariant();
        var fadeId = ServiceConfiguration.FadeId.ToString().ToUpperInvariant();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Users" ("Id", "Name", "Email", "PasswordHash", "Role", "CreatedAt")
            VALUES ({0}, 'Barbeiro', 'antigo@test.com', 'hash', 1, '2026-07-01 12:00:00');
            INSERT INTO "Barbers" ("Id", "UserId", "AverageRating", "IsActive")
            VALUES ({1}, {0}, '0.0', 1);
            INSERT INTO "Appointments" ("Id", "ClientName", "ClientPhone", "AccessToken", "BarberId",
                "ScheduledAt", "TotalDurationMinutes", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({2}, 'Cliente', '+5511999995501', 'token-antigo', {1},
                '2026-07-10 10:00:00', 40, 2, '2026-07-01 12:00:00', '2026-07-10 11:00:00');
            INSERT INTO "AppointmentServices" ("AppointmentId", "ServiceId")
            VALUES ({2}, {3});
            """,
            userId, barberId, appointmentId, fadeId);

        await migrator.MigrateAsync();

        var line = await db.AppointmentServices.SingleAsync();
        line.ServiceId.Should().Be(ServiceConfiguration.FadeId);
        line.UnitPrice.Should().Be(45.00m); // preço do Fade no catálogo semeado
    }
}
