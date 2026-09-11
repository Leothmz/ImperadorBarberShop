using FluentAssertions;
using ImperadorBarberShop.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ImperadorBarberShop.IntegrationTests.Persistence;

/// <summary>
/// Um banco já em produção sobe para o esquema com Clients: cada telefone vira um cliente,
/// mesmo quando a mesma pessoa digitou o número de jeitos diferentes ao longo do tempo.
/// Arquivo com connection string, como no boot do app — o EF abre a própria conexão.
/// </summary>
public sealed class AddClientsMigrationTests : IDisposable
{
    private const string BeforeClients = "20260910232010_SnapshotAppointmentServicePrice";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"imperador-clients-{Guid.NewGuid():N}.db");

    private AppDbContext NewContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task Migration_BackfillsOneClientPerNormalizedPhoneAndLinksTheAppointments()
    {
        // GUIDs como TEXT maiúsculo, o formato em que o EF grava as chaves no SQLite.
        var userId = Guid.NewGuid();
        var barberId = Guid.NewGuid();
        var joaoFirst = Guid.NewGuid();
        var joaoSecond = Guid.NewGuid();
        var joaoCancelled = Guid.NewGuid();
        var maria = Guid.NewGuid();
        var unreadable = Guid.NewGuid();

        await using (var db = NewContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(BeforeClients);

            // O mesmo João em três formatos; o cancelado é o mais antigo e é dele o nome
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Users" ("Id", "Name", "Email", "PasswordHash", "Role", "CreatedAt")
                VALUES ({0}, 'Barbeiro', 'antigo@test.com', 'hash', 1, '2026-06-01 12:00:00');
                INSERT INTO "Barbers" ("Id", "UserId", "AverageRating", "IsActive")
                VALUES ({1}, {0}, '0.0', 1);
                INSERT INTO "Appointments" ("Id", "ClientName", "ClientPhone", "AccessToken", "BarberId",
                    "ScheduledAt", "TotalDurationMinutes", "Status", "CreatedAt", "UpdatedAt")
                VALUES
                    ({2}, 'João', '+5511999990000', 'token-1', {1},
                     '2026-07-10 10:00:00', 30, 2, '2026-07-01 12:00:00', '2026-07-10 11:00:00'),
                    ({3}, 'João Silva', '(11) 9999-0000', 'token-2', {1},
                     '2026-07-20 10:00:00', 30, 2, '2026-07-15 09:00:00', '2026-07-20 11:00:00'),
                    ({4}, 'Joãozinho', '11 9 9999-0000', 'token-3', {1},
                     '2026-06-25 10:00:00', 30, 1, '2026-06-20 08:00:00', '2026-06-21 08:00:00'),
                    ({5}, 'Maria', '5521988887777', 'token-4', {1},
                     '2026-09-30 10:00:00', 30, 0, '2026-09-01 10:00:00', '2026-09-01 10:00:00'),
                    ({6}, 'Sem Telefone', 'não informado', 'token-5', {1},
                     '2026-07-11 10:00:00', 30, 2, '2026-07-02 10:00:00', '2026-07-11 11:00:00');
                """,
                Upper(userId), Upper(barberId), Upper(joaoFirst), Upper(joaoSecond), Upper(joaoCancelled),
                Upper(maria), Upper(unreadable));
        }

        // Contexto e conexão novos, como no próximo boot
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();

            var clients = await db.Clients.OrderBy(c => c.MatchKey).ToListAsync();
            clients.Should().HaveCount(2);

            var joao = clients[0];
            joao.MatchKey.Should().Be("1199990000");
            joao.Phone.Should().Be("+5511999990000");
            joao.Name.Should().Be("Joãozinho");
            joao.FirstSeenAt.Should().Be(new DateTime(2026, 6, 20, 8, 0, 0));
            joao.LastVisitAt.Should().Be(new DateTime(2026, 7, 20, 10, 0, 0));
            joao.VisitCount.Should().Be(2, "o cancelado não conta visita");
            joao.LastInviteAt.Should().BeNull();

            var mariaClient = clients[1];
            mariaClient.MatchKey.Should().Be("2188887777");
            mariaClient.Phone.Should().Be("+5521988887777");
            mariaClient.Name.Should().Be("Maria");
            mariaClient.FirstSeenAt.Should().Be(new DateTime(2026, 9, 1, 10, 0, 0));
            mariaClient.LastVisitAt.Should().BeNull();
            mariaClient.VisitCount.Should().Be(0);

            var links = await db.Appointments.ToDictionaryAsync(a => a.Id, a => a.ClientId);
            links[joaoFirst].Should().Be(joao.Id);
            links[joaoSecond].Should().Be(joao.Id);
            links[joaoCancelled].Should().Be(joao.Id);
            links[maria].Should().Be(mariaClient.Id);
            links[unreadable].Should().BeNull("telefone ilegível fica sem cliente em vez de derrubar o boot");

            // O histórico não é reescrito: o agendamento guarda o que foi digitado na época
            (await db.Appointments.SingleAsync(a => a.Id == joaoSecond)).ClientPhone.Should().Be("(11) 9999-0000");

            // A FK do modelo existe no banco, sem a reconstrução da tabela
            (await PragmaRowsAsync(db, "PRAGMA foreign_key_list('Appointments');"))
                .Should().ContainSingle(fk => fk["table"] == "Clients" && fk["from"] == "ClientId")
                .Which["on_delete"].Should().Be("SET NULL");
            (await PragmaRowsAsync(db, "PRAGMA foreign_key_check;")).Should().BeEmpty();
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
