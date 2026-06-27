using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImperadorBarberShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClientAccountModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new columns as nullable so existing rows don't fail the NOT NULL constraint yet.
            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "Appointments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientPhone",
                table: "Appointments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Appointments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // 2. Backfill from the Users table (still intact at this point in the migration).
            migrationBuilder.Sql(@"
                UPDATE ""Appointments"" a
                SET ""ClientName"" = COALESCE(u.""Name"", 'Cliente'),
                    ""ClientPhone"" = '+550000000000'
                FROM ""Users"" u
                WHERE u.""Id"" = a.""ClientId"";
            ");

            // 3. Generate a unique opaque token per existing row (good enough for backfilled rows;
            // all rows created after this migration get a cryptographically random token from
            // Appointment.Create in application code).
            migrationBuilder.Sql(@"
                UPDATE ""Appointments""
                SET ""AccessToken"" = md5(""Id""::text || random()::text || clock_timestamp()::text)
                                    || md5(random()::text || clock_timestamp()::text);
            ");

            // 4. Remap AppointmentStatus int values to the collapsed enum:
            //    old Pending(0) -> new Accepted(0)
            //    old Accepted(1) -> new Accepted(0)
            //    old Rejected(2) -> new Cancelled(1)
            //    old Cancelled(3) -> new Cancelled(1)
            //    old Completed(4) -> new Completed(2)
            migrationBuilder.Sql(@"
                UPDATE ""Appointments""
                SET ""Status"" = CASE ""Status""
                    WHEN 0 THEN 0
                    WHEN 1 THEN 0
                    WHEN 2 THEN 1
                    WHEN 3 THEN 1
                    WHEN 4 THEN 2
                END;
            ");

            // 5. Now that every row has values, tighten the new columns to NOT NULL.
            migrationBuilder.AlterColumn<string>(
                name: "ClientName",
                table: "Appointments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientPhone",
                table: "Appointments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccessToken",
                table: "Appointments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AccessToken",
                table: "Appointments",
                column: "AccessToken",
                unique: true);

            // 6. Drop the old client FK/column from Appointments and Reviews.
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Users_ClientId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClientId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Reviews");

            // 7. Client accounts no longer exist — remove them (UserRole.Client = 0).
            migrationBuilder.Sql(@"DELETE FROM ""Users"" WHERE ""Role"" = 0;");

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000001"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Haircut", "Corte" });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000002"),
                columns: new[] { "Description", "DurationMinutes", "Name", "Price" },
                values: new object[] { "Fade", 40, "Fade / Disfarçado", 45.00m });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000003"),
                columns: new[] { "Description", "DurationMinutes", "Name", "Price" },
                values: new object[] { "Beard", 20, "Barba", 25.00m });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000004"),
                columns: new[] { "Description", "DurationMinutes", "Name", "Price" },
                values: new object[] { "Eyebrows", 15, "Sobrancelha", 15.00m });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000005"),
                columns: new[] { "Description", "DurationMinutes", "Name", "Price" },
                values: new object[] { "Hydration", 20, "Hidratação", 30.00m });

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000006"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Pigmentation", "Pigmentação" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "This migration deletes client account data and collapses AppointmentStatus values. " +
                "Rolling back would require restoring from a pre-migration backup, not a generated Down().");
        }
    }
}
