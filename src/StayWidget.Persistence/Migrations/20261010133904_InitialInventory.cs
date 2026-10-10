using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayWidget.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Issuer = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Contact = table.Column<string>(type: "text", nullable: false),
                    Zone = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Policies = table.Column<string>(type: "text", nullable: false),
                    TermsVersion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Properties_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    OpenFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Turnover = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                    table.UniqueConstraint("AK_Units_PropertyId_Id", x => new { x.PropertyId, x.Id });
                    table.ForeignKey(
                        name: "FK_Units_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    Departure = table.Column<DateOnly>(type: "date", nullable: false),
                    Guests = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    PublicReason = table.Column<string>(type: "text", nullable: false),
                    Policies = table.Column<string>(type: "text", nullable: false),
                    TermsVersion = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    KeyHash = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedReceipt = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.UniqueConstraint("AK_Requests_PropertyId_UnitId_Id", x => new { x.PropertyId, x.UnitId, x.Id });
                    table.ForeignKey(
                        name: "FK_Requests_Units_PropertyId_UnitId",
                        columns: x => new { x.PropertyId, x.UnitId },
                        principalTable: "Units",
                        principalColumns: new[] { "PropertyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    Departure = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    TermsVersion = table.Column<string>(type: "text", nullable: false),
                    Confirmed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.UniqueConstraint("AK_Reservations_PropertyId_UnitId_Id", x => new { x.PropertyId, x.UnitId, x.Id });
                    table.ForeignKey(
                        name: "FK_Reservations_Requests_PropertyId_UnitId_RequestId",
                        columns: x => new { x.PropertyId, x.UnitId, x.RequestId },
                        principalTable: "Requests",
                        principalColumns: new[] { "PropertyId", "UnitId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Start = table.Column<DateOnly>(type: "date", nullable: false),
                    End = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allocations_Reservations_PropertyId_UnitId_ReservationId",
                        columns: x => new { x.PropertyId, x.UnitId, x.ReservationId },
                        principalTable: "Reservations",
                        principalColumns: new[] { "PropertyId", "UnitId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_PropertyId_UnitId_ReservationId",
                table: "Allocations",
                columns: new[] { "PropertyId", "UnitId", "ReservationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Allocations_ReservationId",
                table: "Allocations",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_OwnerId",
                table: "Properties",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_PropertyId_KeyHash",
                table: "Requests",
                columns: new[] { "PropertyId", "KeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_PropertyId_State_Created",
                table: "Requests",
                columns: new[] { "PropertyId", "State", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_TokenHash",
                table: "Requests",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PropertyId_UnitId_RequestId",
                table: "Reservations",
                columns: new[] { "PropertyId", "UnitId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_RequestId",
                table: "Reservations",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_PropertyId_Name",
                table: "Units",
                columns: new[] { "PropertyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Issuer_Subject",
                table: "Users",
                columns: new[] { "Issuer", "Subject" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS btree_gist;
                ALTER TABLE "Allocations" ADD CONSTRAINT "NoUnitOverlap"
                  EXCLUDE USING gist ("UnitId" WITH =, daterange("Start", "End", '[)') WITH &&);
                ALTER TABLE "Allocations" ADD CONSTRAINT "AllocationDates" CHECK ("Start" < "End");
                ALTER TABLE "Requests" ADD CONSTRAINT "RequestDates" CHECK ("Arrival" < "Departure" AND "Guests" > 0);
                ALTER TABLE "Requests" ADD CONSTRAINT "RequestState" CHECK ("State" IN ('Pending','Accepted','Declined','Expired','Withdrawn'));
                ALTER TABLE "Reservations" ADD CONSTRAINT "ReservationDates" CHECK ("Arrival" < "Departure" AND "Amount" >= 0);
                ALTER TABLE "Units" ADD CONSTRAINT "UnitBounds" CHECK ("Capacity" BETWEEN 1 AND 20 AND "OpenFrom" < "OpenTo" AND "Turnover" IN (0,1));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Allocations");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
