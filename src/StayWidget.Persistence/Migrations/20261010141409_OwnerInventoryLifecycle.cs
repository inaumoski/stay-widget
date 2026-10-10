using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayWidget.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnerInventoryLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxNights",
                table: "Units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinNights",
                table: "Units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestId",
                table: "Reservations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "CancellationRequested",
                table: "Reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Cancelled",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommandHash",
                table: "Reservations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Guests",
                table: "Reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Reservations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Reservations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Redacted",
                table: "Reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Reservations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Closed",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Redacted",
                table: "Requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MaintenanceAt",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Properties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReservationId",
                table: "Allocations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "Allocations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CommandHash",
                table: "Allocations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Created",
                table: "Allocations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Allocations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Allocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Activity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activity_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_State_Expires",
                table: "Requests",
                columns: new[] { "State", "Expires" });

            migrationBuilder.CreateIndex(
                name: "IX_Activity_PropertyId_At",
                table: "Activity",
                columns: new[] { "PropertyId", "At" });

            migrationBuilder.AddForeignKey(
                name: "FK_Allocations_Units_PropertyId_UnitId",
                table: "Allocations",
                columns: new[] { "PropertyId", "UnitId" },
                principalTable: "Units",
                principalColumns: new[] { "PropertyId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Units_PropertyId_UnitId",
                table: "Reservations",
                columns: new[] { "PropertyId", "UnitId" },
                principalTable: "Units",
                principalColumns: new[] { "PropertyId", "Id" },
                onDelete: ReferentialAction.Restrict);
            // Preserve M1 inventory and contact snapshots before strengthening the lifecycle rules.
            migrationBuilder.Sql("""
                UPDATE "Units" SET "MinNights"=1, "MaxNights"=28;
                UPDATE "Properties" SET "Published"=TRUE, "MaintenanceAt"='1970-01-01T00:00:00Z';
                UPDATE "Reservations" r SET "State"='Confirmed', "Guests"=q."Guests", "Name"=q."Name", "Phone"=q."Phone"
                  FROM "Requests" q WHERE r."RequestId"=q."Id";
                UPDATE "Allocations" a SET "Active"=TRUE, "Created"=r."Confirmed"
                  FROM "Reservations" r WHERE a."ReservationId"=r."Id";
                ALTER TABLE "Allocations" DROP CONSTRAINT "NoUnitOverlap";
                ALTER TABLE "Allocations" ADD CONSTRAINT "NoUnitOverlap"
                  EXCLUDE USING gist ("UnitId" WITH =, daterange("Start", "End", '[)') WITH &&) WHERE ("Active");
                ALTER TABLE "Units" ADD CONSTRAINT "StayBounds" CHECK ("MinNights" BETWEEN 1 AND 28 AND "MaxNights" BETWEEN "MinNights" AND 28);
                ALTER TABLE "Reservations" ADD CONSTRAINT "ReservationState" CHECK ("State" IN ('Confirmed','Cancelled') AND "Guests" BETWEEN 1 AND 20);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Reservations" WHERE "RequestId" IS NULL OR "State"='Cancelled')
                     OR EXISTS (SELECT 1 FROM "Allocations" WHERE "ReservationId" IS NULL OR NOT "Active") THEN
                    RAISE EXCEPTION 'M2 data cannot be downgraded safely. Restore a reviewed pre-M2 backup instead.';
                  END IF;
                END $$;
                ALTER TABLE "Units" DROP CONSTRAINT "StayBounds";
                ALTER TABLE "Reservations" DROP CONSTRAINT "ReservationState";
                ALTER TABLE "Allocations" DROP CONSTRAINT "NoUnitOverlap";
                ALTER TABLE "Allocations" ADD CONSTRAINT "NoUnitOverlap"
                  EXCLUDE USING gist ("UnitId" WITH =, daterange("Start", "End", '[)') WITH &&);
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Allocations_Units_PropertyId_UnitId",
                table: "Allocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Units_PropertyId_UnitId",
                table: "Reservations");

            migrationBuilder.DropTable(
                name: "Activity");

            migrationBuilder.DropIndex(
                name: "IX_Requests_State_Expires",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "MaxNights",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "MinNights",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "CancellationRequested",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Cancelled",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "CommandHash",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Guests",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Redacted",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Closed",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Redacted",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "MaintenanceAt",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "CommandHash",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Allocations");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Allocations");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestId",
                table: "Reservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReservationId",
                table: "Allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
