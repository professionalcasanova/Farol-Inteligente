using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farol.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityBudgetModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "community_budgets",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.Sql("""
                UPDATE community_budgets
                SET "Status" = CASE
                    WHEN "IsPublic" THEN 'Published'
                    ELSE 'Draft'
                END;
                """);

            migrationBuilder.CreateTable(
                name: "community_budget_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityBudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_budget_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_budget_reports_community_budgets_CommunityBudgetId",
                        column: x => x.CommunityBudgetId,
                        principalTable: "community_budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_budget_reports_users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_budgets_Status_CreatedAtUtc",
                table: "community_budgets",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_community_budget_reports_CommunityBudgetId",
                table: "community_budget_reports",
                column: "CommunityBudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_community_budget_reports_CommunityBudgetId_ReporterUserId",
                table: "community_budget_reports",
                columns: new[] { "CommunityBudgetId", "ReporterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_budget_reports_ReporterUserId",
                table: "community_budget_reports",
                column: "ReporterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_budget_reports");

            migrationBuilder.DropIndex(
                name: "IX_community_budgets_Status_CreatedAtUtc",
                table: "community_budgets");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "community_budgets");
        }
    }
}
