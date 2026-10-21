using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScaleDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeastPrivilegeAppRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RLS is only meaningful if the application does NOT connect as a superuser: superusers
            // (and the table owner) bypass row-level security, so the Docker `scaledesk` account can't
            // be used for runtime traffic. This creates a least-privilege LOGIN role — explicitly
            // NOSUPERUSER + NOBYPASSRLS — granted only DML on the data. The app connects as this role,
            // so the tenant_isolation policies (and the app.tenant_id session GUC set by the
            // TenantSessionInterceptor) genuinely constrain every query, not just EF-generated ones.
            // Migrations/DDL still run as the owner via the separate PostgresMigrator connection.
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'scaledesk_app') THEN
    CREATE ROLE scaledesk_app LOGIN PASSWORD 'scaledesk_app_pw'
      NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
  END IF;
END $$;

GRANT CONNECT ON DATABASE scaledesk TO scaledesk_app;
GRANT USAGE ON SCHEMA public TO scaledesk_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO scaledesk_app;

-- Any table a FUTURE migration creates (owned by the `scaledesk` migrator role) is auto-granted, so
-- the app role never silently loses access to new tables after a schema change.
ALTER DEFAULT PRIVILEGES FOR ROLE scaledesk IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO scaledesk_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER DEFAULT PRIVILEGES FOR ROLE scaledesk IN SCHEMA public
  REVOKE ALL ON TABLES FROM scaledesk_app;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM scaledesk_app;
REVOKE USAGE ON SCHEMA public FROM scaledesk_app;
REVOKE CONNECT ON DATABASE scaledesk FROM scaledesk_app;
DROP ROLE IF EXISTS scaledesk_app;
");
        }
    }
}
