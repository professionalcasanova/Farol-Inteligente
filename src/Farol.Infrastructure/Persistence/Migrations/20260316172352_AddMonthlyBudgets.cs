using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farol.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monthly_budgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monthly_budgets_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "monthly_budget_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlyBudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_budget_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_monthly_budget_categories_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monthly_budget_categories_monthly_budgets_MonthlyBudgetId",
                        column: x => x.MonthlyBudgetId,
                        principalTable: "monthly_budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budget_categories_CategoryId",
                table: "monthly_budget_categories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budget_categories_MonthlyBudgetId_CategoryId",
                table: "monthly_budget_categories",
                columns: new[] { "MonthlyBudgetId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_budgets_UserId_Month_Year",
                table: "monthly_budgets",
                columns: new[] { "UserId", "Month", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monthly_budget_categories");

            migrationBuilder.DropTable(
                name: "monthly_budgets");
        }
    }
}
