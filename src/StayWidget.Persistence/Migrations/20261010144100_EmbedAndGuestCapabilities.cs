using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayWidget.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmbedAndGuestCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionHash",
                table: "Requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CapabilitiesRevoked",
                table: "Requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "#12616A");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmbedAttestedAt",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "EmbedOrigins",
                table: "Properties",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<bool>(
                name: "Suspended",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_ActionHash",
                table: "Requests",
                column: "ActionHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_PropertyId_Created",
                table: "Requests",
                columns: new[] { "PropertyId", "Created" });
            migrationBuilder.Sql("""
                ALTER TABLE "Properties" ADD CONSTRAINT "EmbedBounds" CHECK (cardinality("EmbedOrigins") <= 5 AND "AccentColor" ~ '^#[0-9A-Fa-f]{6}$');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Requests" WHERE "ActionHash" IS NOT NULL OR "CapabilitiesRevoked") THEN
                    RAISE EXCEPTION 'M3 guest capabilities cannot be downgraded safely. Restore a reviewed pre-M3 backup instead.';
                  END IF;
                END $$;
                ALTER TABLE "Properties" DROP CONSTRAINT "EmbedBounds";
                """);
            migrationBuilder.DropIndex(
                name: "IX_Requests_ActionHash",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_PropertyId_Created",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ActionHash",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "CapabilitiesRevoked",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "EmbedAttestedAt",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "EmbedOrigins",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Suspended",
                table: "Properties");
        }
    }
}
