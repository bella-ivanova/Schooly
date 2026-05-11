using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "School",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "School",
                table: "AspNetUsers");
        }
    }
}
