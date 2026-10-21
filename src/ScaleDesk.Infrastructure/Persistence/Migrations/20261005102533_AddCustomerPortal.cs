using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScaleDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_users_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_users_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_CustomerId",
                table: "customer_users",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_TenantId_CustomerId",
                table: "customer_users",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_TenantId_UserId",
                table: "customer_users",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_users_UserId",
                table: "customer_users",
                column: "UserId");

            // Same database-enforced tenant boundary as EnableRowLevelSecurity (spec §40) — new
            // tenant-owned tables must not ship without it. FORCE + bypass/tenant_id GUC semantics
            // are copied verbatim so the policy behaves identically to the existing ones.
            migrationBuilder.Sql(@"
ALTER TABLE ""customer_users"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""customer_users"" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON ""customer_users""
    USING (current_setting('app.bypass_rls', true) = 'on'
           OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (current_setting('app.bypass_rls', true) = 'on'
           OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON \"customer_users\";\nALTER TABLE \"customer_users\" DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.DropTable(
                name: "customer_users");
        }
    }
}
