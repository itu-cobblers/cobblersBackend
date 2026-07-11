using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cobblersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskSlugAndGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grading_json",
                table: "task",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "task",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_task_slug",
                table: "task",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_task_slug",
                table: "task");

            migrationBuilder.DropColumn(
                name: "grading_json",
                table: "task");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "task");
        }
    }
}
