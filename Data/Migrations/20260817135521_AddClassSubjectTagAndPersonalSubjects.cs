using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSubjectTagAndPersonalSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SchoolId",
                table: "Subjects",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "Classes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClassId",
                table: "ChatSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classes_SubjectId",
                table: "Classes",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ClassId",
                table: "ChatSessions",
                column: "ClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_Classes_ClassId",
                table: "ChatSessions",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Classes_Subjects_SubjectId",
                table: "Classes",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_Classes_ClassId",
                table: "ChatSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Classes_Subjects_SubjectId",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_Classes_SubjectId",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_ClassId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "ChatSessions");

            migrationBuilder.AlterColumn<int>(
                name: "SchoolId",
                table: "Subjects",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
