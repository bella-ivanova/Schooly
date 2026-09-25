using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSceneToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SceneJson",
                table: "ChatMessages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SceneJson",
                table: "ChatMessages");
        }
    }
}
