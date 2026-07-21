using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cobblersBackend.Migrations
{
    /// <inheritdoc />
    public partial class RenameAssignmentPhysicalNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_session_task_set_task_set_id",
                table: "session");

            migrationBuilder.DropForeignKey(
                name: "fk_submission_task_task_id",
                table: "submission");

            // task_set_task's FK into task also depends on pk_task and must be
            // dropped before the PK can be dropped/recreated under the new name;
            // EF didn't scaffold this because task_set_task's own configuration
            // is untouched in this migration (see AssignmentSetAssignmentConfiguration's
            // temporary pins) — re-added below once "assignment" exists.
            migrationBuilder.DropForeignKey(
                name: "fk_task_set_task_task_task_id",
                table: "task_set_task");

            migrationBuilder.DropPrimaryKey(
                name: "pk_task",
                table: "task");

            migrationBuilder.DropCheckConstraint(
                name: "ck_task_kind",
                table: "task");

            migrationBuilder.RenameTable(
                name: "task",
                newName: "assignment");

            migrationBuilder.RenameIndex(
                name: "ix_task_set_task_task_id",
                table: "task_set_task",
                newName: "ix_task_set_task_assignment_id");

            migrationBuilder.RenameColumn(
                name: "task_id",
                table: "submission",
                newName: "assignment_id");

            migrationBuilder.RenameIndex(
                name: "ix_submission_task_id",
                table: "submission",
                newName: "ix_submission_assignment_id");

            migrationBuilder.RenameColumn(
                name: "task_set_id",
                table: "session",
                newName: "assignment_set_id");

            migrationBuilder.RenameIndex(
                name: "ix_session_task_set_id",
                table: "session",
                newName: "ix_session_assignment_set_id");

            migrationBuilder.RenameIndex(
                name: "ix_task_slug",
                table: "assignment",
                newName: "ix_assignment_slug");

            migrationBuilder.AddPrimaryKey(
                name: "pk_assignment",
                table: "assignment",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assignment_kind",
                table: "assignment",
                sql: "kind IN ('code', 'predict', 'project')");

            migrationBuilder.AddForeignKey(
                name: "fk_task_set_task_task_task_id",
                table: "task_set_task",
                column: "task_id",
                principalTable: "assignment",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_session_assignment_set_assignment_set_id",
                table: "session",
                column: "assignment_set_id",
                principalTable: "task_set",
                principalColumn: "task_set_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_submission_assignment_assignment_id",
                table: "submission",
                column: "assignment_id",
                principalTable: "assignment",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_session_assignment_set_assignment_set_id",
                table: "session");

            migrationBuilder.DropForeignKey(
                name: "fk_submission_assignment_assignment_id",
                table: "submission");

            migrationBuilder.DropForeignKey(
                name: "fk_task_set_task_task_task_id",
                table: "task_set_task");

            migrationBuilder.DropPrimaryKey(
                name: "pk_assignment",
                table: "assignment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignment_kind",
                table: "assignment");

            migrationBuilder.RenameTable(
                name: "assignment",
                newName: "task");

            migrationBuilder.RenameIndex(
                name: "ix_task_set_task_assignment_id",
                table: "task_set_task",
                newName: "ix_task_set_task_task_id");

            migrationBuilder.RenameColumn(
                name: "assignment_id",
                table: "submission",
                newName: "task_id");

            migrationBuilder.RenameIndex(
                name: "ix_submission_assignment_id",
                table: "submission",
                newName: "ix_submission_task_id");

            migrationBuilder.RenameColumn(
                name: "assignment_set_id",
                table: "session",
                newName: "task_set_id");

            migrationBuilder.RenameIndex(
                name: "ix_session_assignment_set_id",
                table: "session",
                newName: "ix_session_task_set_id");

            migrationBuilder.RenameIndex(
                name: "ix_assignment_slug",
                table: "task",
                newName: "ix_task_slug");

            migrationBuilder.AddPrimaryKey(
                name: "pk_task",
                table: "task",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_task_kind",
                table: "task",
                sql: "kind IN ('code', 'predict', 'project')");

            migrationBuilder.AddForeignKey(
                name: "fk_task_set_task_task_task_id",
                table: "task_set_task",
                column: "task_id",
                principalTable: "task",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_session_task_set_task_set_id",
                table: "session",
                column: "task_set_id",
                principalTable: "task_set",
                principalColumn: "task_set_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_submission_task_task_id",
                table: "submission",
                column: "task_id",
                principalTable: "task",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
