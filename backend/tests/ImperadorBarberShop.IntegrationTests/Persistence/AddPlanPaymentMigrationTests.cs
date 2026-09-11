using FluentAssertions;
using ImperadorBarberShop.Application.Queries.Financial;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.ValueObjects;
using ImperadorBarberShop.Infrastructure.Persistence;
using ImperadorBarberShop.Infrastructure.Persistence.Configurations;
using ImperadorBarberShop.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ImperadorBarberShop.IntegrationTests.Persistence;

/// <summary>
/// Um banco já em produção, com clientes, pagamentos e preços guardados, sobe para o esquema
/// com plano sem que nenhum agendamento vire plano e sem que um número do financeiro mude.
/// Arquivo com connection string, como no boot do app — o EF abre a própria conexão.
/// </summary>
public sealed class AddPlanPaymentMigrationTests : IDisposable
{
    private const string BeforePlan = "20260911172415_AddClients";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"imperador-plano-{Guid.NewGuid():N}.db");

    private AppDbContext NewContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task Migration_KeepsExistingAppointmentsOutOfThePlanAndTheirFinancialsUnchanged()
    {
        // GUIDs como TEXT maiúsculo, o formato em que o EF grava as chaves no SQLite.
        var userId = Guid.NewGuid();
        var barberId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var paidPix = Guid.NewGuid();
        var unpaid = Guid.NewGuid();
        var cash = Guid.NewGuid();
        var accepted = Guid.NewGuid();

        await using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(BeforePlan);

            // Preços guardados diferentes do catálogo atual: o financeiro lê o que foi guardado
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Users" ("Id", "Name", "Email", "PasswordHash", "Role", "CreatedAt")
                VALUES ({0}, 'Barbeiro', 'antigo@test.com', 'hash', 1, '2026-06-01 12:00:00');
                INSERT INTO "Barbers" ("Id", "UserId", "AverageRating", "IsActive")
                VALUES ({1}, {0}, '0.0', 1);
                INSERT INTO "Clients" ("Id", "Phone", "MatchKey", "Name", "FirstSeenAt", "LastVisitAt", "VisitCount")
                VALUES ({2}, '+5511999990000', '1199990000', 'João', '2026-07-01 12:00:00', '2026-07-12 10:00:00', 3);
                INSERT INTO "Appointments" ("Id", "ClientName", "ClientPhone", "AccessToken", "BarberId",
                    "ScheduledAt", "TotalDurationMinutes", "Status", "CreatedAt", "UpdatedAt", "PaymentMethod", "PaidAt", "ClientId")
                VALUES
                    ({3}, 'João', '+5511999990000', 'token-1', {1},
                     '2026-07-10 10:00:00', 50, 2, '2026-07-01 12:00:00', '2026-07-10 11:00:00', 2, '2026-07-10 13:50:00', {2}),
                    ({4}, 'João', '+5511999990000', 'token-2', {1},
                     '2026-07-11 10:00:00', 30, 2, '2026-07-02 12:00:00', '2026-07-11 11:00:00', NULL, NULL, {2}),
                    ({5}, 'João', '+5511999990000', 'token-3', {1},
                     '2026-07-12 10:00:00', 30, 2, '2026-07-03 12:00:00', '2026-07-12 11:00:00', 0, '2026-07-12 13:40:00', {2}),
                    ({6}, 'João', '+5511999990000', 'token-4', {1},
                     '2026-09-30 10:00:00', 30, 0, '2026-09-01 12:00:00', '2026-09-01 12:00:00', NULL, NULL, {2});
                INSERT INTO "AppointmentServices" ("AppointmentId", "ServiceId", "UnitPrice")
                VALUES ({3}, {7}, '30.0'), ({3}, {8}, '20.0'), ({4}, {7}, '30.0'), ({5}, {7}, '32.5'), ({6}, {7}, '35.0');
                """,
                Upper(userId), Upper(barberId), Upper(clientId), Upper(paidPix), Upper(unpaid), Upper(cash), Upper(accepted),
                Upper(ServiceConfiguration.CorteId), Upper(ServiceConfiguration.BarbaId));
        }

        // Contexto e conexão novos, como no próximo boot
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();

            var appointments = await db.Appointments.Include(a => a.AppointmentServices).ToDictionaryAsync(a => a.Id);
            appointments.Values.Should().OnlyContain(a => a.PlanKind == null && a.PlanTender == null && a.ChargedAmount == null);
            appointments.Values.Should().OnlyContain(a => a.ClientId == clientId, "o vínculo com o cliente sobrevive");
            appointments[paidPix].PaymentMethod.Should().Be(PaymentMethod.Pix);
            appointments[paidPix].PaidAt.Should().Be(new DateTime(2026, 7, 10, 13, 50, 0));
            appointments[cash].PaymentMethod.Should().Be(PaymentMethod.Dinheiro);
            appointments[unpaid].PaymentMethod.Should().BeNull();
            appointments[paidPix].EffectiveAmount.Should().Be(50m);
            appointments[cash].EffectiveAmount.Should().Be(32.5m);

            var summary = await new GetFinancialSummaryQueryHandler(new AppointmentRepository(db), new ExpenseRepository(db))
                .Handle(new GetFinancialSummaryQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)), CancellationToken.None);
            summary.TotalRevenue.Should().Be(112.5m, "50 + 30 + 32,50: os mesmos preços guardados de antes");
            summary.TotalAppointments.Should().Be(3);
            summary.AverageTicket.Should().Be(37.5m);

            (await db.Clients.SingleAsync()).VisitCount.Should().Be(3);

            var columns = await PragmaRowsAsync(db, "PRAGMA table_info('Appointments');");
            foreach (var column in new[] { "PlanKind", "PlanTender", "ChargedAmount" })
                columns.Should().ContainSingle(c => c["name"] == column).Which["notnull"].Should().Be("0");
            (await PragmaRowsAsync(db, "PRAGMA foreign_key_check;")).Should().BeEmpty();
        }

        // O esquema novo grava e relê o plano (decimal em TEXT, enums em INTEGER)
        await using (var db = NewContext())
        {
            var appointment = await db.Appointments.Include(a => a.AppointmentServices).SingleAsync(a => a.Id == unpaid);
            appointment.SetPayment(AppointmentPayment.PlanPayment(99.90m, PaymentMethod.Cartão));
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var appointment = await db.Appointments.Include(a => a.AppointmentServices).SingleAsync(a => a.Id == unpaid);
            appointment.PaymentMethod.Should().Be(PaymentMethod.Plano);
            appointment.PlanKind.Should().Be(PlanKind.Pagamento);
            appointment.PlanTender.Should().Be(PaymentMethod.Cartão);
            appointment.ChargedAmount.Should().Be(99.90m);
            appointment.EffectiveAmount.Should().Be(99.90m);
            appointment.AppointmentServices.Single().UnitPrice.Should().Be(30m, "o plano não reescreve o preço guardado");
        }
    }

    private static string Upper(Guid id) => id.ToString().ToUpperInvariant();

    private static async Task<List<Dictionary<string, string?>>> PragmaRowsAsync(AppDbContext db, string pragma)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = pragma;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, string?>>();
        while (await reader.ReadAsync())
            rows.Add(Enumerable.Range(0, reader.FieldCount)
                .ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i).ToString()));
        return rows;
    }
}
