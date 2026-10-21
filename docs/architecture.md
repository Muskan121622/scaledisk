# ScaleDesk Architecture

Short version of what's actually running. Every claim here is verifiable by
`docker compose up` + looking at `src/`.

## One request, end to end

```
Browser
  ↓ (HTTPS)
API (ASP.NET Core 9)
  ↓ JwtBearer (RS256, 15min TTL)
  ↓ Tenant resolution (slug → TenantId, bound to HttpContext)
  ↓ RBAC policy → Controller action
  ↓ Application Service (permission check + tenant guard)
  ↓ EF Core DbContext (global tenant query filter)
  ↓ Npgsql session (app.tenant_id GUC set per request)
  ↓ PostgreSQL, with RLS as the second layer beneath the EF filter
```

Writes to the ticket aggregate atomically append a row to the `outbox_messages`
table inside the same transaction (the **transactional outbox**). A hosted
service drains the outbox to Kafka with at-least-once delivery. Consumers
dedupe by message id in a `processed_messages` table before mutating state.

## Layers

```
ScaleDesk.Domain          entities, enums, domain events — no dependencies
ScaleDesk.Application     use cases, contracts, ports (repositories, cache, notifier)
ScaleDesk.Infrastructure  EF + Npgsql, Redis, Kafka, SignalR, JWT, password hash, clock
ScaleDesk.Api             controllers, hub, health checks, DI wiring, Program.cs
```

Dependency direction is inward-only. Application never references Infrastructure.
Api references both, but only through interfaces registered in DI.

## Multi-tenancy — two independent layers

1. **EF Core global query filter.** Every `ITenantOwned` entity is filtered by
   the ambient `TenantId` resolved from the JWT + workspace slug. Missing
   WHERE clauses become structurally impossible at the ORM level.
2. **PostgreSQL Row-Level Security.** A policy on every tenant-owned table
   compares `current_setting('app.tenant_id')::uuid` with the row's
   `TenantId` column. The Npgsql session sets this GUC on connection open
   via `TenantSessionInterceptor`. RLS catches any query that bypasses EF
   (e.g. raw SQL, migrations, admin tooling).

Neither layer alone is sufficient: EF's filter can be `Ignore()`d, and RLS
only fires on queries that hit the DB. Together they close both holes.

The application role `scaledesk_app` is deliberately the least privilege
principal — no DDL, no cross-tenant SELECT without going through RLS. Migrations
and the DbSeeder use a separate `scaledesk` superuser role.

## Access scopes (Stage 7)

Three JWT scopes — `staff`, `platform`, `portal` — share one token pipeline but are confined by a
single choke-point guard (`TenantScopeGuardMiddleware`): each scope may only hit its own URL space
(`/api/*` staff, `/api/platform/*` HQ, `/api/portal/*` customer), so a staff token on a portal or
platform route is rejected `403` and vice-versa. Platform tokens carry **no** `tid` and run on the
cross-tenant bypass session (read-only except tenant status); portal tokens carry a `cid` (customer
id) and are pinned to `Ticket.CustomerId`, with the tenant still bound from the workspace slug. A
missing `scope` claim defaults to `staff`, so pre-Stage-7 tokens keep working unchanged.

## Reliability primitives

- **Transactional outbox.** Every state change publishes its domain events
  in the same DB transaction. Kafka outage → the outbox grows, the write
  still succeeds, delivery resumes when the broker returns.
- **Idempotent consumer.** `processed_messages` records each Kafka message
  id in a unique index before the handler mutates state. Duplicate delivery
  is a no-op, not a bug.
- **Cache-aside + graceful degrade.** Redis is used for SLA policy lookups
  on ticket create. If Redis is down, we fall through to Postgres; the
  readiness probe reports degraded but not failing.
- **SLA breach scanner.** A `BackgroundService` polls for tickets whose
  `resolution_due_at` has passed and fires `SlaBreached` domain events —
  catching lapses that happen while nobody is writing to the ticket.

## Observability

- OpenTelemetry traces every HTTP + EF + Kafka hop, exported to Jaeger when
  `Otel:Enabled=true`. Correlation-Id middleware assigns one per request and
  propagates it into every emitted log scope.
- `/health/live` = process up. `/health/ready` = full dependency check
  (Postgres, Redis, Kafka) with per-component detail.

## What lives where (repo layout)

```
src/ScaleDesk.Domain/                 entities, enums, events
src/ScaleDesk.Application/            use cases, contracts, ports
src/ScaleDesk.Infrastructure/         EF + Npgsql, Redis, Kafka, SignalR, JWT
src/ScaleDesk.Api/                    controllers, hubs, Program.cs
web/                                  Vite + React + TS + shadcn/ui
tests/ScaleDesk.UnitTests/            27 tests (permission + tenant guards + platform/portal)
tests/ScaleDesk.IntegrationTests/     23 tests (WebApplicationFactory end-to-end)
ops/verification/                     5C chaos suite — 7 scenarios, all pass
ops/load/                             5D/5E k6 harness + findings
docs/                                 this folder
```

Read next: [multi-tenancy.md](multi-tenancy.md) ·
[event-driven-architecture.md](event-driven-architecture.md) ·
[performance.md](performance.md) ·
[failure-testing.md](failure-testing.md)
