using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeToClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Grade",
                table: "Classes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Classes");
        }
    }
}
