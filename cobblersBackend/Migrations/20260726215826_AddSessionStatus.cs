using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cobblersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "session",
                type: "text",
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_status",
                table: "session",
                sql: "status IN ('active', 'ended')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_session_status",
                table: "session");

            migrationBuilder.DropColumn(
                name: "status",
                table: "session");
        }
    }
}
