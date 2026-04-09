using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farol.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "budget_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_templates_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_template_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_template_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_template_categories_budget_templates_BudgetTemplateId",
                        column: x => x.BudgetTemplateId,
                        principalTable: "budget_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_budget_template_categories_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budget_template_categories_BudgetTemplateId_CategoryId",
                table: "budget_template_categories",
                columns: new[] { "BudgetTemplateId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_template_categories_CategoryId",
                table: "budget_template_categories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_templates_UserId",
                table: "budget_templates",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_template_categories");

            migrationBuilder.DropTable(
                name: "budget_templates");
        }
    }
}
