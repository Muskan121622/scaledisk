using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScaleDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketTenantCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_tickets_TenantId_CreatedAt",
                table: "tickets",
                columns: new[] { "TenantId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tickets_TenantId_CreatedAt",
                table: "tickets");
        }
    }
}
