using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace cobblersBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    hint = table.Column<string>(type: "text", nullable: true),
                    content_json = table.Column<string>(type: "jsonb", nullable: false),
                    sample_solution_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task", x => x.id);
                    table.CheckConstraint("ck_task_kind", "kind IN ('code', 'predict', 'project')");
                });

            migrationBuilder.CreateTable(
                name: "task_set",
                columns: table => new
                {
                    task_set_id = table.Column<string>(type: "text", nullable: false),
                    display_title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_set", x => x.task_set_id);
                });

            migrationBuilder.CreateTable(
                name: "session",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    task_set_id = table.Column<string>(type: "text", nullable: false),
                    create_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session", x => x.session_id);
                    table.ForeignKey(
                        name: "fk_session_task_set_task_set_id",
                        column: x => x.task_set_id,
                        principalTable: "task_set",
                        principalColumn: "task_set_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_set_task",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_set_id = table.Column<string>(type: "text", nullable: false),
                    task_id = table.Column<int>(type: "integer", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_set_task", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_set_task_task_set_task_set_id",
                        column: x => x.task_set_id,
                        principalTable: "task_set",
                        principalColumn: "task_set_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_set_task_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendance",
                columns: table => new
                {
                    student_id = table.Column<string>(type: "text", nullable: false),
                    session_id = table.Column<string>(type: "text", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance", x => new { x.student_id, x.session_id });
                    table.ForeignKey(
                        name: "fk_attendance_session_session_id",
                        column: x => x.session_id,
                        principalTable: "session",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attendance_student_student_id",
                        column: x => x.student_id,
                        principalTable: "student",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission",
                columns: table => new
                {
                    sub_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<string>(type: "text", nullable: false),
                    task_id = table.Column<int>(type: "integer", nullable: false),
                    session_id = table.Column<string>(type: "text", nullable: true),
                    content_json = table.Column<string>(type: "jsonb", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: true),
                    passed = table.Column<bool>(type: "boolean", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission", x => x.sub_id);
                    table.ForeignKey(
                        name: "fk_submission_session_session_id",
                        column: x => x.session_id,
                        principalTable: "session",
                        principalColumn: "session_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_submission_student_student_id",
                        column: x => x.student_id,
                        principalTable: "student",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_task_task_id",
                        column: x => x.task_id,
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_session_id",
                table: "attendance",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_code",
                table: "session",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_task_set_id",
                table: "session",
                column: "task_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_session_id",
                table: "submission",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_student_id",
                table: "submission",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_submission_task_id",
                table: "submission",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_set_task_task_id",
                table: "task_set_task",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_set_task_task_set_id_order_index",
                table: "task_set_task",
                columns: new[] { "task_set_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_set_task_task_set_id_task_id",
                table: "task_set_task",
                columns: new[] { "task_set_id", "task_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance");

            migrationBuilder.DropTable(
                name: "submission");

            migrationBuilder.DropTable(
                name: "task_set_task");

            migrationBuilder.DropTable(
                name: "session");

            migrationBuilder.DropTable(
                name: "student");

            migrationBuilder.DropTable(
                name: "task");

            migrationBuilder.DropTable(
                name: "task_set");
        }
    }
}
