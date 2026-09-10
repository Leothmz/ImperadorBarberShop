using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperadorBarberShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotAppointmentServicePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "AppointmentServices",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            // Backfill: agendamentos anteriores a esta migração não guardaram o preço da
            // época, e o catálogo atual é o único registro que existe dele. É o mesmo valor
            // que os relatórios já exibiam, então nenhum número passado muda na virada — a
            // diferença é que, daqui em diante, um reajuste não reescreve mais o histórico.
            // COALESCE: uma linha órfã fica em 0 em vez de derrubar a migração no boot.
            migrationBuilder.Sql(
                """
                UPDATE "AppointmentServices"
                SET "UnitPrice" = COALESCE(
                    (SELECT "Services"."Price" FROM "Services"
                     WHERE "Services"."Id" = "AppointmentServices"."ServiceId"),
                    "UnitPrice");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "AppointmentServices");
        }
    }
}
