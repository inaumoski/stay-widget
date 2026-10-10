using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayWidget.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductPlansFeedbackMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HideBranding",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InstallationAcknowledgedAt",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "Pro");

            migrationBuilder.CreateTable(
                name: "Feedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmitterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    NoticeVersion = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Response = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CloseReason = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reviewed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Closed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedback", x => x.Id);
                    table.CheckConstraint("FeedbackState", "\"Status\" IN ('New','Reviewed','Planned','InProgress','Closed') AND ((\"Status\"='Closed' AND \"Closed\" IS NOT NULL AND \"CloseReason\" IS NOT NULL AND \"CloseReason\" IN ('Delivered','Duplicate','NotPlanned','CannotReproduce','Spam')) OR (\"Status\"<>'Closed' AND \"Closed\" IS NULL AND \"CloseReason\" IS NULL)) AND ((\"Status\"='New' AND \"Reviewed\" IS NULL) OR (\"Status\"<>'New' AND \"Reviewed\" IS NOT NULL)) AND \"Kind\" IN ('Feedback','Custom')");
                    table.ForeignKey(
                        name: "FK_Feedback_Users_SubmitterUserId",
                        column: x => x.SubmitterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Metrics",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Event = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrics", x => new { x.Day, x.Event });
                });

            migrationBuilder.AddCheckConstraint(
                name: "PlanValid",
                table: "Properties",
                sql: "\"Plan\" IN ('Free','Pro')");

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_Status_Closed",
                table: "Feedback",
                columns: new[] { "Status", "Closed" });

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_SubmitterUserId_Created",
                table: "Feedback",
                columns: new[] { "SubmitterUserId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_SubmitterUserId_SubmissionKey",
                table: "Feedback",
                columns: new[] { "SubmitterUserId", "SubmissionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"Feedback\") OR EXISTS (SELECT 1 FROM \"Properties\" WHERE \"Plan\"<>'Pro' OR \"HideBranding\" OR \"InstallationAcknowledgedAt\" IS NOT NULL) THEN RAISE EXCEPTION 'M4 data requires a reviewed restore or forward recovery, not destructive downgrade'; END IF; END $$;");
            migrationBuilder.DropTable(
                name: "Feedback");

            migrationBuilder.DropTable(
                name: "Metrics");

            migrationBuilder.DropCheckConstraint(
                name: "PlanValid",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "HideBranding",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "InstallationAcknowledgedAt",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Properties");
        }
    }
}
