using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cobblersBackend.Migrations
{
    /// <inheritdoc />
    public partial class RenameTaskSetTablesToAssignmentSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_task_set_task_task_set_task_set_id",
                table: "task_set_task");

            migrationBuilder.DropForeignKey(
                name: "fk_task_set_task_task_task_id",
                table: "task_set_task");

            // session's FK into task_set also depends on pk_task_set and must be
            // dropped before that PK can be dropped/recreated under the new name
            // — re-added below once "assignment_set" exists.
            migrationBuilder.DropForeignKey(
                name: "fk_session_assignment_set_assignment_set_id",
                table: "session");

            migrationBuilder.DropPrimaryKey(
                name: "pk_task_set_task",
                table: "task_set_task");

            migrationBuilder.DropPrimaryKey(
                name: "pk_task_set",
                table: "task_set");

            migrationBuilder.RenameTable(
                name: "task_set_task",
                newName: "assignment_set_assignment");

            migrationBuilder.RenameTable(
                name: "task_set",
                newName: "assignment_set");

            migrationBuilder.RenameColumn(
                name: "task_set_id",
                table: "assignment_set_assignment",
                newName: "assignment_set_id");

            migrationBuilder.RenameColumn(
                name: "task_id",
                table: "assignment_set_assignment",
                newName: "assignment_id");

            migrationBuilder.RenameIndex(
                name: "ix_task_set_task_task_set_id_task_id",
                table: "assignment_set_assignment",
                newName: "ix_assignment_set_assignment_assignment_set_id_assignment_id");

            migrationBuilder.RenameIndex(
                name: "ix_task_set_task_task_set_id_order_index",
                table: "assignment_set_assignment",
                newName: "ix_assignment_set_assignment_assignment_set_id_order_index");

            migrationBuilder.RenameIndex(
                name: "ix_task_set_task_assignment_id",
                table: "assignment_set_assignment",
                newName: "ix_assignment_set_assignment_assignment_id");

            migrationBuilder.RenameColumn(
                name: "task_set_id",
                table: "assignment_set",
                newName: "assignment_set_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_assignment_set_assignment",
                table: "assignment_set_assignment",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_assignment_set",
                table: "assignment_set",
                column: "assignment_set_id");

            migrationBuilder.AddForeignKey(
                name: "fk_assignment_set_assignment_assignment_assignment_id",
                table: "assignment_set_assignment",
                column: "assignment_id",
                principalTable: "assignment",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_assignment_set_assignment_assignment_set_assignment_set_id",
                table: "assignment_set_assignment",
                column: "assignment_set_id",
                principalTable: "assignment_set",
                principalColumn: "assignment_set_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_session_assignment_set_assignment_set_id",
                table: "session",
                column: "assignment_set_id",
                principalTable: "assignment_set",
                principalColumn: "assignment_set_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assignment_set_assignment_assignment_assignment_id",
                table: "assignment_set_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_session_assignment_set_assignment_set_id",
                table: "session");

            migrationBuilder.DropForeignKey(
                name: "fk_assignment_set_assignment_assignment_set_assignment_set_id",
                table: "assignment_set_assignment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_assignment_set_assignment",
                table: "assignment_set_assignment");

            migrationBuilder.DropPrimaryKey(
                name: "pk_assignment_set",
                table: "assignment_set");

            migrationBuilder.RenameTable(
                name: "assignment_set_assignment",
                newName: "task_set_task");

            migrationBuilder.RenameTable(
                name: "assignment_set",
                newName: "task_set");

            migrationBuilder.RenameColumn(
                name: "assignment_set_id",
                table: "task_set_task",
                newName: "task_set_id");

            migrationBuilder.RenameColumn(
                name: "assignment_id",
                table: "task_set_task",
                newName: "task_id");

            migrationBuilder.RenameIndex(
                name: "ix_assignment_set_assignment_assignment_set_id_order_index",
                table: "task_set_task",
                newName: "ix_task_set_task_task_set_id_order_index");

            migrationBuilder.RenameIndex(
                name: "ix_assignment_set_assignment_assignment_set_id_assignment_id",
                table: "task_set_task",
                newName: "ix_task_set_task_task_set_id_task_id");

            migrationBuilder.RenameIndex(
                name: "ix_assignment_set_assignment_assignment_id",
                table: "task_set_task",
                newName: "ix_task_set_task_assignment_id");

            migrationBuilder.RenameColumn(
                name: "assignment_set_id",
                table: "task_set",
                newName: "task_set_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_task_set_task",
                table: "task_set_task",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_task_set",
                table: "task_set",
                column: "task_set_id");

            migrationBuilder.AddForeignKey(
                name: "fk_task_set_task_task_set_task_set_id",
                table: "task_set_task",
                column: "task_set_id",
                principalTable: "task_set",
                principalColumn: "task_set_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_task_set_task_task_task_id",
                table: "task_set_task",
                column: "task_id",
                principalTable: "assignment",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_session_task_set_task_set_id",
                table: "session",
                column: "task_set_id",
                principalTable: "task_set",
                principalColumn: "task_set_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
