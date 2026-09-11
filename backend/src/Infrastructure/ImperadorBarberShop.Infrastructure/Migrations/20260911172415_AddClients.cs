using System;
using ImperadorBarberShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperadorBarberShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MatchKey = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastVisitAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VisitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastInviteAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            // A FK do modelo (ON DELETE SET NULL) numa coluna nova, com REFERENCES em linha. O
            // AddForeignKey gerado faria o SQLite reconstruir Appointments: copiar a tabela inteira
            // com PRAGMA foreign_keys fora de transação, e um boot interrompido no meio deixaria a
            // migração pela metade. Assim ela roda inteira numa transação só.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Appointments" ADD "ClientId" TEXT NULL
                    CONSTRAINT "FK_Appointments_Clients_ClientId" REFERENCES "Clients" ("Id") ON DELETE SET NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClientId",
                table: "Appointments",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_LastVisitAt",
                table: "Clients",
                column: "LastVisitAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_MatchKey",
                table: "Clients",
                column: "MatchKey",
                unique: true);

            // Backfill: um cliente por telefone normalizado. As funções imperador_phone_* são o
            // próprio BrazilianPhone registrado na conexão (PhoneSqlFunctions), então os formatos
            // legados caem na mesma chave que o agendamento usaria hoje. Telefone ilegível devolve
            // NULL: o agendamento fica sem cliente em vez de derrubar a migração no boot.
            // Nome e telefone vêm do agendamento mais antigo; visitas, só dos concluídos (Status 2).
            // Id: GUID em TEXT maiúsculo, o formato em que o EF grava chaves no SQLite.
            migrationBuilder.Sql(
                $"""
                WITH "Keyed" AS (
                    SELECT "Id", "ClientName", "CreatedAt", "ScheduledAt", "Status",
                           {PhoneSqlFunctions.MatchKeyFunction}("ClientPhone") AS "MatchKey",
                           {PhoneSqlFunctions.CanonicalFunction}("ClientPhone") AS "Phone"
                    FROM "Appointments"
                ),
                "Ranked" AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY "MatchKey" ORDER BY "CreatedAt", "Id") AS "Position"
                    FROM "Keyed"
                    WHERE "MatchKey" IS NOT NULL
                ),
                "Totals" AS (
                    SELECT "MatchKey",
                           MIN("CreatedAt") AS "FirstSeenAt",
                           MAX(CASE WHEN "Status" = 2 THEN "ScheduledAt" END) AS "LastVisitAt",
                           COUNT(CASE WHEN "Status" = 2 THEN 1 END) AS "VisitCount"
                    FROM "Ranked"
                    GROUP BY "MatchKey"
                ),
                "NewClients" AS (
                    SELECT hex(randomblob(16)) AS "Hex", "First"."Phone", "First"."MatchKey", "First"."ClientName",
                           "Totals"."FirstSeenAt", "Totals"."LastVisitAt", "Totals"."VisitCount"
                    FROM "Ranked" AS "First"
                    JOIN "Totals" ON "Totals"."MatchKey" = "First"."MatchKey"
                    WHERE "First"."Position" = 1
                )
                INSERT INTO "Clients" ("Id", "Phone", "MatchKey", "Name", "FirstSeenAt", "LastVisitAt", "VisitCount")
                SELECT substr("Hex", 1, 8) || '-' || substr("Hex", 9, 4) || '-' || substr("Hex", 13, 4) || '-'
                           || substr("Hex", 17, 4) || '-' || substr("Hex", 21, 12),
                       "Phone", "MatchKey", "ClientName", "FirstSeenAt", "LastVisitAt", "VisitCount"
                FROM "NewClients";

                UPDATE "Appointments"
                SET "ClientId" = (
                    SELECT "Clients"."Id" FROM "Clients"
                    WHERE "Clients"."MatchKey" = {PhoneSqlFunctions.MatchKeyFunction}("Appointments"."ClientPhone"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Clients_ClientId",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClientId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Appointments");
        }
    }
}
