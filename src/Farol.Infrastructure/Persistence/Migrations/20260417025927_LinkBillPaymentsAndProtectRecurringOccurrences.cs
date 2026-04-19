using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farol.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkBillPaymentsAndProtectRecurringOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bills_BillSeriesId_DueOn",
                table: "bills");

            migrationBuilder.AddColumn<Guid>(
                name: "PaidTransactionId",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH ranked_duplicates AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY "BillSeriesId", "DueOn"
                            ORDER BY "IsPaid" DESC, "PaidAtUtc" DESC NULLS LAST, "CreatedAtUtc", "Id"
                        ) AS row_number
                    FROM bills
                    WHERE "BillSeriesId" IS NOT NULL
                )
                DELETE FROM bills AS duplicate_bill
                USING ranked_duplicates
                WHERE duplicate_bill."Id" = ranked_duplicates."Id"
                  AND ranked_duplicates.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_bills_BillSeriesId_DueOn",
                table: "bills",
                columns: new[] { "BillSeriesId", "DueOn" },
                unique: true,
                filter: "\"BillSeriesId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bills_PaidTransactionId",
                table: "bills",
                column: "PaidTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_bills_transactions_PaidTransactionId",
                table: "bills",
                column: "PaidTransactionId",
                principalTable: "transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bills_transactions_PaidTransactionId",
                table: "bills");

            migrationBuilder.DropIndex(
                name: "IX_bills_BillSeriesId_DueOn",
                table: "bills");

            migrationBuilder.DropIndex(
                name: "IX_bills_PaidTransactionId",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "PaidTransactionId",
                table: "bills");

            migrationBuilder.CreateIndex(
                name: "IX_bills_BillSeriesId_DueOn",
                table: "bills",
                columns: new[] { "BillSeriesId", "DueOn" });
        }
    }
}
