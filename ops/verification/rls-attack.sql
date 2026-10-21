-- ScaleDesk — PostgreSQL Row-Level Security attack test (spec §40 / Stage 5A).
--
-- WHY THIS EXISTS: the application connects as the least-privilege role `scaledesk_app`
-- (NOSUPERUSER, NOBYPASSRLS) — NOT as the superuser owner. A superuser would bypass RLS
-- entirely, so the DB-level tenant boundary only bites when runtime uses this confined role.
--
-- Run inside the postgres container (local socket auth as scaledesk_app):
--   Get-Content ops/verification/rls-attack.sql -Raw | docker exec -i scaledesk-postgres psql -U scaledesk_app -d scaledesk -f -
--
-- Every statement uses parameterised session GUCs (app.tenant_id / app.bypass_rls) exactly as the
-- TenantSessionInterceptor does at runtime. No ticket ids are hardcoded — tenants are resolved by slug.

\pset pager off

\echo '===== 0) we are NOT a superuser, so RLS is enforced ====='
SELECT current_user AS whoami, current_setting('is_superuser') AS is_superuser;

\echo ''
\echo '===== A) DEFAULT-DENY: a raw session with no tenant bound sees ZERO tenant rows ====='
SELECT count(*) AS visible_tickets_with_no_context FROM tickets;

\echo ''
\echo '===== B) bind to TechNova => sees ONLY TechNova tickets ====='
SELECT set_config('app.tenant_id', (SELECT "Id"::text FROM tenants WHERE "Slug"='technova'), false);
SELECT t."Slug" AS tenant, tk."Subject"
FROM tickets tk JOIN tenants t ON t."Id" = tk."TenantId" ORDER BY tk."Subject";

\echo ''
\echo '===== C) CROSS-TENANT READ: Acme''s ticket is invisible to a TechNova session ====='
SELECT count(*) AS acme_rows_visible_to_technova
FROM tickets
WHERE "TenantId" = (SELECT "Id" FROM tenants WHERE "Slug"='acme');

\echo ''
\echo '===== D) bind to Acme => sees ONLY the Acme ticket (mirror of B) ====='
SELECT set_config('app.tenant_id', (SELECT "Id"::text FROM tenants WHERE "Slug"='acme'), false);
SELECT t."Slug" AS tenant, tk."Subject"
FROM tickets tk JOIN tenants t ON t."Id" = tk."TenantId" ORDER BY tk."Subject";

\echo ''
\echo '===== E) CROSS-TENANT WRITE (WITH CHECK): relocating a row into another tenant is rejected ====='
SELECT set_config('app.tenant_id', (SELECT "Id"::text FROM tenants WHERE "Slug"='technova'), false);
UPDATE tickets
SET "TenantId" = (SELECT "Id" FROM tenants WHERE "Slug"='acme')
WHERE "TenantId" = (SELECT "Id" FROM tenants WHERE "Slug"='technova');

\echo ''
\echo '===== F) PLATFORM / WORKER path: explicit bypass (app.bypass_rls=on) sees BOTH tenants ====='
SELECT set_config('app.bypass_rls', 'on', false);
SELECT count(*) AS visible_tickets_with_bypass FROM tickets;
