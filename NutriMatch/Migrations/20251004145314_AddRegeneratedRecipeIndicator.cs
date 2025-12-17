using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutriMatch.Migrations
{
    /// <inheritdoc />
    public partial class AddRegeneratedRecipeIndicator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRegenerated",
                table: "MealSlots",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRegenerated",
                table: "MealSlots");
        }
    }
}
