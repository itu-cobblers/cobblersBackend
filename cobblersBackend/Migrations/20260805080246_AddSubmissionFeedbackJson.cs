using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cobblersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionFeedbackJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "feedback_json",
                table: "submission",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "feedback_json",
                table: "submission");
        }
    }
}
