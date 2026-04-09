using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farol.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringBillSeriesAndInstallments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BillSeriesId",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OccurrenceNumber",
                table: "bills",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalOccurrences",
                table: "bills",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bill_series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    FirstDueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EndMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UntilDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bill_series_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bills_BillSeriesId_DueOn",
                table: "bills",
                columns: new[] { "BillSeriesId", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_bill_series_UserId_IsActive_FirstDueOn",
                table: "bill_series",
                columns: new[] { "UserId", "IsActive", "FirstDueOn" });

            migrationBuilder.AddForeignKey(
                name: "FK_bills_bill_series_BillSeriesId",
                table: "bills",
                column: "BillSeriesId",
                principalTable: "bill_series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bills_bill_series_BillSeriesId",
                table: "bills");

            migrationBuilder.DropTable(
                name: "bill_series");

            migrationBuilder.DropIndex(
                name: "IX_bills_BillSeriesId_DueOn",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "BillSeriesId",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "OccurrenceNumber",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "TotalOccurrences",
                table: "bills");
        }
    }
}
