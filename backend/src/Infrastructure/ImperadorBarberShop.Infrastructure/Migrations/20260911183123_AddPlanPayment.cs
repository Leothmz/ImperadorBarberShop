using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperadorBarberShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Só colunas nulas, sem backfill: todo agendamento existente fica fora do plano, com
            // ChargedAmount nulo, e o financeiro dele segue somando os preços guardados. Plano é o
            // valor 3 na coluna PaymentMethod que já existe; Dinheiro/Cartão/Pix mantêm 0/1/2.
            migrationBuilder.AddColumn<decimal>(
                name: "ChargedAmount",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanKind",
                table: "Appointments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanTender",
                table: "Appointments",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargedAmount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PlanKind",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PlanTender",
                table: "Appointments");
        }
    }
}
