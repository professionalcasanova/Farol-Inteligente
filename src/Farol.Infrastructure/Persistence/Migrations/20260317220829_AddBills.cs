using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farol.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bills_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bills_UserId_DueOn",
                table: "bills",
                columns: new[] { "UserId", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_bills_UserId_IsPaid_DueOn",
                table: "bills",
                columns: new[] { "UserId", "IsPaid", "DueOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bills");
        }
    }
}
