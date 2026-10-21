# Failure testing — chaos suite

Seven scenarios in `ops/verification/`. Run them all against a live stack:

```powershell
powershell -File ops/verification/run-all.ps1
```

They execute in order and print pass/fail. As of Stage 5F.1 the whole suite is
**7/7 green** with **71 individual assertions** on a clean seeded stack. Every
check is a real HTTP or `docker exec` probe, not a mock.

## Precondition — do not run this on the load-test DB

The `sla-breach` scenario needs the SLA scanner to reach *its* ticket inside a
40-second window. The scanner processes `.OrderBy(CreatedAt).Take(BatchSize)`
on a fixed interval. After Stage 5D seeds the 1M-ticket corpus, there are
~800k backlogged tickets with `ResponseDueAt < now()`; the scanner is still
working through September-dated rows 36 hours later, and the freshly-created
chaos ticket never gets picked up. Symptom: `sla-breach` fails with
`SLA scanner marked the overdue ticket SlaBreached=true` as the only failing
assertion while the other 6 scenarios pass.

The chaos suite is designed to run against a clean seeded stack (`technova`,
`acme`, `demo` only). If you just finished a load run, reset first:

```powershell
docker compose down -v   # removes postgres-data + kafka-data volumes
powershell -File ops/verification/run-all.ps1
```

The DbSeeder re-creates the curated tenants in ~12 seconds. This does not
lose anything the load harness cannot re-seed with `dotnet run --project
ops/load/seed -c Release`.

## The scenarios

### 1. Auth lifecycle + token-family revocation (`auth-e2e.ps1`)
Register a fresh tenant. Login. Refresh once. Attempt to reuse the *old*
refresh token. Assert: the reuse is rejected, AND every active descendant of
that token family is now revoked, forcing the legitimate holder to re-login.
This is the theft-detection path: a stolen token replayed after rotation kills
the whole session lineage, not just the stolen branch.

### 2. Full ticket journey (`ticket-e2e.ps1`)
Create ticket → assign → status transition → add internal comment → add public
comment → resolve. Assert the aggregate state at each step matches the
commands sent, that the internal comment stays flagged as internal, and that
the resolution stamps `resolvedAt` correctly.

### 3. Cross-tenant isolation (`tenant-isolation.ps1`)
Two tenants, each with a ticket. `GET /api/tickets/{id}` with the *other*
tenant's JWT returns 404 (not 403 — the filter doesn't reveal existence).
Direct Postgres query with `app.tenant_id` set to one tenant confirms RLS
returns zero rows for the other's data even at the raw SQL layer.

### 4. Duplicate Kafka event, processed once (`duplicate-event.ps1`)
Manually inject the same Kafka message twice. Assert: the second delivery
hits `processed_messages` unique index and is dropped as a no-op — no
duplicate notification, no duplicate automation action.

### 5. Redis outage / recovery (`redis-failure.ps1`)
`docker stop scaledesk-redis`. Fire a burst of read-heavy ticket-list requests.
Assert: still 200s from Postgres (cache miss falls through). Restart Redis.
Assert: cache starts warming up again without any application restart.

### 6. SLA breach → event → notification (`sla-breach.ps1`)
Create a ticket with a 15-minute SLA. Bypass real waiting by directly updating
`response_due_at` to a past timestamp via `psql`. Trigger the breach scanner
(or wait for the timer). Assert: `SlaBreached` domain event lands, the
consumer writes a notification, and the client's `/notifications/unread-count`
reflects the increment.

### 7. Kafka outage → outbox buffering → recovery (`kafka-failure.ps1`)
`docker stop scaledesk-kafka`. Create 5 tickets via HTTP — all writes succeed
because the outbox row is in the same transaction. `SELECT COUNT(*) FROM
outbox_messages` shows 5 pending events. Restart Kafka. Wait for the
dispatcher. Assert: outbox drains to 0 pending, and consumers process each
of the 5 events exactly once (no duplicates from at-least-once semantics).

## What this proves

The suite is not "unit tests that hit HTTP." Each scenario intentionally
mutates the environment (kills a broker, replays a token, corrupts a due-date)
and asserts the system's **contract** still holds:

- Writes never fail when the async path is down (outbox absorbs).
- Consumers never double-apply (idempotency table absorbs).
- Cross-tenant access is not possible via any path we tried (two independent
  guards, EF + RLS).
- Stolen refresh tokens can't extend a session; the theft *hurts the
  attacker's target*, not just the attacker.
- Cache absence degrades performance, not correctness.

These are the properties an "event-driven SaaS backend" needs to be
production-grade, and they're what makes the code reviewable as a real
system rather than a CRUD scaffold.

## Reading order

1. `ops/verification/run-all.ps1` for the orchestration.
2. Individual scenario scripts for the concrete assertions.
3. `docs/event-driven-architecture.md` for how the pieces compose.
4. `ops/load/FINDINGS.md` for the performance side of the same stack.
