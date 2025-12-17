using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutriMatch.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyMealMatchesTags",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyMealPlanUpdated",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyNewRestaurant",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyRecipeAccepted",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyRecipeDeclined",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyRecipeMatchesTags",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyRecipeRated",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyRestaurantNewMeal",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyMealMatchesTags",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyMealPlanUpdated",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyNewRestaurant",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyRecipeAccepted",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyRecipeDeclined",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyRecipeMatchesTags",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyRecipeRated",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyRestaurantNewMeal",
                table: "AspNetUsers");
        }
    }
}
