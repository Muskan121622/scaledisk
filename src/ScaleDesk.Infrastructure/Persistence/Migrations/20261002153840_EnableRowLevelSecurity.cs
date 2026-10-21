using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScaleDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Row-Level Security = the second, database-enforced tenant boundary (spec §40). It sits
            // beneath the EF global query filter so that even a forgotten WHERE, a hand-written SQL
            // path, or a compromised app layer cannot read another tenant's rows. Every statement is
            // parameterless so the three clauses can ship as one batch per table.
            //   * FORCE  -> policies apply to the table owner too (our app connects as the owner).
            //   * USING / WITH CHECK -> a row is visible/writable when either the session opted into
            //     the cross-tenant bypass (app.bypass_rls = 'on', set by TenantSessionInterceptor for
            //     background workers / login / migrate / seed), or its TenantId equals the session's
            //     app.tenant_id. When neither holds, the predicate is NULL/false => fail closed.
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql($@"
ALTER TABLE ""{table}"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE ""{table}"" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON ""{table}""
    USING (current_setting('app.bypass_rls', true) = 'on'
           OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (current_setting('app.bypass_rls', true) = 'on'
           OR ""TenantId"" = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
            {
                migrationBuilder.Sql($@"
DROP POLICY IF EXISTS tenant_isolation ON ""{table}"";
ALTER TABLE ""{table}"" DISABLE ROW LEVEL SECURITY;");
            }
        }

        // Every ITenantOwned table (users / tenants / processed_messages are intentionally excluded:
        // they are not tenant-scoped). Column is the literal EF-mapped "TenantId" (PascalCase).
        private static readonly string[] TenantTables =
        [
            "tickets", "ticket_comments", "ticket_events",
            "customers", "teams", "memberships", "invitations",
            "sla_policies", "notifications", "automation_rules",
            "refresh_tokens", "outbox_messages",
        ];
    }
}
