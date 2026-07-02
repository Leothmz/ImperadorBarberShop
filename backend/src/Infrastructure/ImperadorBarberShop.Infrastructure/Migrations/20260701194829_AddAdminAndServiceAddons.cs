using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperadorBarberShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAndServiceAddons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Services",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Barbers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Barbers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceAddons",
                columns: table => new
                {
                    ParentServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddonServiceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAddons", x => new { x.ParentServiceId, x.AddonServiceId });
                    table.CheckConstraint("CK_ServiceAddons_NoCycles", "\"ParentServiceId\" <> \"AddonServiceId\"");
                    table.ForeignKey(
                        name: "FK_ServiceAddons_Services_AddonServiceId",
                        column: x => x.AddonServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceAddons_Services_ParentServiceId",
                        column: x => x.ParentServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000001"),
                column: "PhotoUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000002"),
                column: "PhotoUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000003"),
                column: "PhotoUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000004"),
                column: "PhotoUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000005"),
                column: "PhotoUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000006"),
                column: "PhotoUrl",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAddons_AddonServiceId",
                table: "ServiceAddons",
                column: "AddonServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceAddons");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Barbers");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Barbers");
        }
    }
}
