using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayWidget.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuestConfirmationEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Requests",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ConfirmationEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedMessage = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Failure = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttempt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Finished = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttempt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmationEmails", x => x.Id);
                    table.CheckConstraint("ConfirmationEmailState", "\"State\" IN ('Queued','Accepted','LocalPreview','Failed','Unknown','Cancelled') AND \"Attempts\" BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_ConfirmationEmails_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmationEmails_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationEmails_Created",
                table: "ConfirmationEmails",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationEmails_PropertyId",
                table: "ConfirmationEmails",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationEmails_RequestId",
                table: "ConfirmationEmails",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationEmails_State_NextAttempt",
                table: "ConfirmationEmails",
                columns: new[] { "State", "NextAttempt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"Requests\" WHERE \"Email\" <> '') OR EXISTS (SELECT 1 FROM \"ConfirmationEmails\") THEN RAISE EXCEPTION 'Email records require a reviewed rollback'; END IF; END $$;");
            migrationBuilder.DropTable(
                name: "ConfirmationEmails");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Requests");
        }
    }
}
