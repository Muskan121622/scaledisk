-- Stage 5D load dataset seeder.
-- Creates a realistic corpus so read paths (ticket list / get, customer search) and the SLA scanner
-- exercise real working sets instead of a handful of demo rows. Target: 20 tenants, ~1M tickets.
--
-- Runs as the PostgreSQL SUPERUSER (scaledesk) via `docker exec -i ... psql -U scaledesk`, which
-- intentionally BYPASSES RLS so we can write rows for every tenant in one pass. The application later
-- reads them back as the least-privilege role scaledesk_app, where the RLS policy
-- (TenantId = current_setting('app.tenant_id')) restricts each tenant to its own rows.
--
-- Enum columns are stored by NAME (EF HasConversion<string>()): Status/Priority/Category/Role/Status
-- must be the literal enum member strings, which is what the queries below insert.
--
-- IDEMPOTENCY: up-for-load.ps1 guards this (skips when tickets >= 1M or a 'load-tenant-%' slug exists).
-- The UNIQUE index on tenants.Slug is a second backstop against a double run creating a second corpus.

SET client_min_messages = warning;

BEGIN;

-- 1) Tenants (20).
CREATE TEMP TABLE t_tenants ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, ('load-tenant-' || g) AS slug
FROM generate_series(1, 20) AS g;

INSERT INTO tenants ("Id", "Name", "Slug", "Status", "PlanTier", "CreatedAt")
SELECT id, 'Load ' || slug, slug, 'Active', 'pro', now() FROM t_tenants;

-- Clone the demo user's PasswordHash so every load user authenticates with the known password.
CREATE TEMP TABLE t_pw ON COMMIT DROP AS
SELECT "PasswordHash" AS pw FROM users WHERE "Email" = 'rahul@technova.com' LIMIT 1;

-- 2) Users: 2 Agent members per tenant (40 total). Agent grants tickets.read/create/update/assign/
--    comment + customers.read — exactly the operations the load suite drives.
CREATE TEMP TABLE t_users ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, tt.id AS tenant_id, ('agent' || s.n || '@' || tt.slug || '.test') AS email
FROM t_tenants tt
JOIN LATERAL generate_series(1, 2) AS s(n) ON true;

INSERT INTO users ("Id", "Email", "PasswordHash", "FirstName", "LastName",
                   "EmailConfirmed", "AccessFailedCount", "CreatedAt")
SELECT id, email, (SELECT pw FROM t_pw), 'Load', 'Agent', true, 0, now() FROM t_users;

INSERT INTO memberships ("Id", "TenantId", "UserId", "Role", "Status", "CreatedAt")
SELECT gen_random_uuid(), tenant_id, id, 'Agent', 'Active', now() FROM t_users;

-- 3) One team + one default SLA policy per tenant.
CREATE TEMP TABLE t_teams ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, tt.id AS tenant_id, tt.slug AS slug FROM t_tenants tt;

INSERT INTO teams ("Id", "TenantId", "Name", "CreatedAt")
SELECT id, tenant_id, 'Team-' || slug, now() FROM t_teams;

CREATE TEMP TABLE t_sla ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, tt.id AS tenant_id FROM t_tenants tt;

INSERT INTO sla_policies ("Id", "TenantId", "Name", "Priority",
                          "ResponseDueInMinutes", "ResolutionDueInMinutes", "IsDefault", "CreatedAt")
SELECT id, tenant_id, 'Default', 'Medium', 240, 2880, true, now() FROM t_sla;

-- 4) Customers: 5000 per tenant (100k total) so customer search has a real working set.
CREATE TEMP TABLE t_customers ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, tt.id AS tenant_id
FROM t_tenants tt
JOIN LATERAL generate_series(1, 5000) AS c(n) ON true;

INSERT INTO customers ("Id", "TenantId", "Name", "Email", "Phone", "Company", "Tags", "CreatedAt")
SELECT id, tenant_id,
       'Customer-' || left(md5(id::text), 8),
       'cust-' || left(md5(id::text), 12) || '@example.test',
       NULL,
       'Company-' || left(md5(tenant_id::text), 6),
       ARRAY['load', 'seed']::text[],
       now()
FROM t_customers;

-- 5) Per-tenant id pools (arrays) so each ticket can pick a same-tenant customer/agent/team in O(1).
CREATE TEMP TABLE t_pool ON COMMIT DROP AS
SELECT tt.id AS tenant_id, tt.slug AS slug,
       (SELECT array_agg(c.id) FROM t_customers c WHERE c.tenant_id = tt.id) AS cust_ids,
       (SELECT array_agg(u.id) FROM t_users u WHERE u.tenant_id = tt.id)      AS agent_ids,
       (SELECT array_agg(tm.id) FROM t_teams tm WHERE tm.tenant_id = tt.id)   AS team_ids,
       (SELECT sl.id FROM t_sla sl WHERE sl.tenant_id = tt.id LIMIT 1)        AS sla_id
FROM t_tenants tt;

-- 6) Tickets: 50000 per tenant (1,000,000 total), with realistic status/priority/category
--    distributions and CreatedAt spread over the past 60 days (some overdue for SLA realism).
INSERT INTO tickets (
    "Id", "TenantId", "CustomerId", "Subject", "Description", "Status", "Priority", "Category",
    "AssigneeUserId", "TeamId", "SlaPolicyId", "FirstResponseAt", "ResolvedAt",
    "ResponseDueAt", "ResolutionDueAt", "SlaBreached", "CreatedAt")
SELECT
    gen_random_uuid(),
    p.tenant_id,
    (p.cust_ids)[1 + (floor(random() * array_length(p.cust_ids, 1)))::int],
    'Ticket ' || n || ' :: ' || p.slug,
    'Seeded load-test body ' || n || '. Reproduction steps and customer context live here.',
    x.status, x.priority, x.category,
    (p.agent_ids)[1 + (floor(random() * array_length(p.agent_ids, 1)))::int],
    (p.team_ids)[1],
    p.sla_id,
    CASE WHEN x.status IN ('InProgress', 'Resolved', 'Closed', 'WaitingForInternal')
         THEN x.created + interval '45 minutes' ELSE NULL END,
    CASE WHEN x.status IN ('Resolved', 'Closed') THEN x.created + interval '2 days' ELSE NULL END,
    x.created + interval '4 hours',
    x.created + interval '2 days',
    (random() < 0.05),
    x.created
FROM t_pool p
JOIN LATERAL generate_series(1, 50000) AS n ON true
JOIN LATERAL (
    SELECT
        (CASE WHEN s.rr < 0.35 THEN 'Open'
              WHEN s.rr < 0.55 THEN 'InProgress'
              WHEN s.rr < 0.65 THEN 'WaitingForCustomer'
              WHEN s.rr < 0.70 THEN 'WaitingForInternal'
              WHEN s.rr < 0.88 THEN 'Resolved'
              ELSE 'Closed' END) AS status,
        (CASE WHEN s.pp < 0.40 THEN 'Low'
              WHEN s.pp < 0.70 THEN 'Medium'
              WHEN s.pp < 0.92 THEN 'High'
              ELSE 'Urgent' END) AS priority,
        (ARRAY['General','Billing','Technical','Account','FeatureRequest','Bug'])[1 + (floor(s.cate * 6))::int] AS category,
        s.created
    FROM (SELECT random() AS rr, random() AS pp, random() AS cate,
                 (now() - random() * interval '60 days') AS created) s
) x ON true;

COMMIT;

-- Give the planner accurate statistics so the baseline reflects a tuned planner (a fair starting point).
ANALYZE tenants;
ANALYZE users;
ANALYZE memberships;
ANALYZE customers;
ANALYZE tickets;
