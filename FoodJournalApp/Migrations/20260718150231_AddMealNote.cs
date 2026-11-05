using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodJournalApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMealNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Meals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "Meals");
        }
    }
}
