using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayWidget.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WidgetAvailabilityCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowAvailabilityCalendar",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"Properties\" WHERE NOT \"ShowAvailabilityCalendar\") THEN RAISE EXCEPTION 'Calendar preferences require a reviewed rollback; downgrade would discard them'; END IF; END $$;");
            migrationBuilder.DropColumn(
                name: "ShowAvailabilityCalendar",
                table: "Properties");
        }
    }
}
