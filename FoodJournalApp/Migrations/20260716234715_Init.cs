using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodJournalApp.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MealCount = table.Column<int>(type: "int", nullable: false),
                    TotalProteinG = table.Column<int>(type: "int", nullable: false),
                    TotalCarbsG = table.Column<int>(type: "int", nullable: false),
                    TotalFatG = table.Column<int>(type: "int", nullable: false),
                    TotalFiberG = table.Column<int>(type: "int", nullable: false),
                    TotalCaloriesEst = table.Column<int>(type: "int", nullable: false),
                    FriedMealCount = table.Column<int>(type: "int", nullable: false),
                    LateNightMealCount = table.Column<int>(type: "int", nullable: false),
                    InsightText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MealType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFried = table.Column<bool>(type: "bit", nullable: false),
                    IsHeavilyProcessed = table.Column<bool>(type: "bit", nullable: false),
                    IsSugarHeavy = table.Column<bool>(type: "bit", nullable: false),
                    IsVegetableLight = table.Column<bool>(type: "bit", nullable: false),
                    JunkScore = table.Column<double>(type: "float", nullable: false),
                    ProteinG = table.Column<int>(type: "int", nullable: false),
                    CarbsG = table.Column<int>(type: "int", nullable: false),
                    FatG = table.Column<int>(type: "int", nullable: false),
                    FiberG = table.Column<int>(type: "int", nullable: false),
                    CaloriesEst = table.Column<int>(type: "int", nullable: false),
                    UserConfirmed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PortionGramsEst = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProteinG = table.Column<int>(type: "int", nullable: false),
                    CarbsG = table.Column<int>(type: "int", nullable: false),
                    FatG = table.Column<int>(type: "int", nullable: false),
                    FiberG = table.Column<int>(type: "int", nullable: false),
                    CaloriesEst = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FoodItems_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailySummaries_UserId_Date",
                table: "DailySummaries",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FoodItems_MealId",
                table: "FoodItems",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_Meals_UserId_CapturedAt",
                table: "Meals",
                columns: new[] { "UserId", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailySummaries");

            migrationBuilder.DropTable(
                name: "FoodItems");

            migrationBuilder.DropTable(
                name: "Meals");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
