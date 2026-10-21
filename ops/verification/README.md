# ScaleDesk — Stage 5C: Production Failure & E2E Verification

This directory is a **reproducible verification suite** whose purpose is not to make tests pass but
to *try to break the running system* and prove it survives: auth abuse, tenant-boundary attacks, a
hard Kafka outage, a cache outage, duplicate events, and timer-driven SLA enforcement. Every check
observes **final durable state** (Postgres via `psql`, Kafka via broker tooling, the HTTP API) rather
than trusting the request that triggered it.

## How to run

```powershell
# 1. Bring the stack up (idempotent: starts Docker/compose, waits Healthy, provisions Kafka topics,
#    launches the API detached on http://localhost:5102). Safe to re-run any time it dies.
powershell -File ops/verification/up.ps1

# 2. Run the whole suite (brings the stack up first, runs every scenario in an ISOLATED child
#    process, prints a PASS/FAIL table + "N/7 scenarios passed", tees raw output to evidence/).
powershell -File ops/verification/run-all.ps1

# Or a single scenario:
powershell -File ops/verification/kafka-failure.ps1
```

Prereqs: Docker Desktop, host Postgres not bound to `:5432` (the container maps to `:5433` via
`docker-compose.override.yml`). The suite talks to the host-run API (`run-api-5c.ps1`) which mirrors
production config: Kafka + Redis + SLA scanner + OpenTelemetry all enabled, RLS-enforcing app role.

## Results (this environment)

Captured by `run-all.ps1` on the live stack. Raw per-scenario logs: [`evidence/`](evidence/).

```
Scenario                                              Result  Checks  Seconds
--------                                              ------  ------  -------
Auth lifecycle + token-family revocation              PASS       9       64
Ticket journey (DB->Outbox->Kafka->Automation->Notif) PASS      10        7
Tenant isolation (EF filter + Postgres RLS)           PASS      17       33
Duplicate event processed once (idempotency)          PASS       8       19
Redis outage -> degrade to Postgres -> recover        PASS       9       20
SLA breach scanner -> event -> notification           PASS       9       19
FLAGSHIP: Kafka outage -> outbox buffers -> drains    PASS       9       20

7/7 scenarios passed   (71 checks total)
```

Plus the unit/integration suites on the same build: **27/27 unit**, **23/23 integration** green.

## Stage 7 — Super Admin & Customer Portal journey sweeps

Beyond the 5C chaos suite, three `verify-*.ps1` scripts exercise the role journeys against the live
stack (compose, API on `:8080`). `verify-journeys.ps1` folds all five journeys into one integrated
sweep; the two dedicated scripts cover their journey exhaustively:

| Script | Journey | What it proves |
|---|---|---|
| `verify-platform.ps1` | **J1** super admin | platform login, cross-tenant reads, scope matrix both directions, **suspend → staff login 403 → resume → 200** |
| `verify-portal.ps1` | **J3** customer portal | register (link-by-email) → create → staff visibility → **internal note hidden** → reply → close-only-when-resolved → ownership **404** → refresh rotation + reuse detection → logout |
| `verify-journeys.ps1` | **J1–J5** (one sweep) | platform suspend/resume + admin invite/accept + portal↔staff two-way + agent RBAC + fresh-tenant isolation — **36/36 checks** |

All auth-bucket calls route through a 429-aware retry so the ~10 req/min "auth" window doesn't flake
an integrated run. The integrated sweep leaves the `demo` tenant **resumed** at the end, so the later
journeys still log in cleanly.

## What each scenario proves

| Script | Failure / behaviour injected | Durable proof |
|---|---|---|
| `auth-e2e.ps1` | register→login→**refresh rotation**→**replay the old token**; invalid/unknown creds; brute force | Reuse returns 401 **and revokes the whole token family** (the still-valid new token is now dead too); no user enumeration; 5 failures → `423` lockout, correct password while locked still refused. |
| `ticket-e2e.ps1` | Full happy path through every subsystem | create→**Postgres row**; outbox→Kafka→**automation** (High→Urgent, only observable if the async consumer ran); **notification** projected for the assignee; idempotent consumption recorded; final state durable. Trace/correlation id printed for Jaeger. |
| `tenant-isolation.ps1` | TechNova (A) attacks Acme (B) on ticket/customer/team/sla read+write | All cross-tenant accesses `404`; EF global query filter hides B's rows from A; **Postgres RLS** independently: A sees 0 of B's rows, B sees own, no-tenant context sees 0 (default-deny), superuser control sees 1 — defense-in-depth, both layers. |
| `duplicate-event.ps1` | **Re-publish the same logical event at a NEW Kafka offset** | Dedup is keyed on the **stable business event id** (`evt:<outbox-id>`), not the offset — the duplicate is skipped and the notification stays at exactly **1**. |
| `redis-failure.ps1` | `docker stop` Redis | Readiness honestly reports **Degraded**; liveness unaffected; **writes+reads keep working** (SLA snapshot cache-aside falls back to Postgres, `responseDueAt` still set); full recovery on restart. Cache is an accelerator, not a dependency. |
| `sla-breach.ps1` | Overdue a ticket's first-response deadline (fast-forward the clock) | Timer **scanner** flags `SlaBreached=true`, emits `sla.breached` → outbox → Kafka → consumer → **notification** for the assignee; **idempotent** — exactly one breach, never re-escalated on later ticks. |
| `kafka-failure.ps1` | **`docker stop` Kafka (flagship)**, create a ticket mid-outage, then restart | Create still returns **201**, row lands in **Postgres**, event **buffers as Pending** outbox, app keeps serving; after restart the outbox **drains** the buffered event (zero loss) and the never-restarted **consumer processes it**. |

## Hardening landed during 5C

These were genuine gaps surfaced by writing the tests — fixed in product code, not papered over:

1. **Refresh-token family revocation on reuse** (`AuthService.RevokeLineageAsync`). Previously reuse
   was *logged* but the live token survived. On replay we now walk the `ReplacedByTokenHash` rotation
   chain and revoke every descendant, so a stolen replayed token logs the real session out too. No
   schema change (linear chain walk). `auth-e2e.ps1` asserts the new token is dead after reuse.
2. **Stable event-id idempotency** (`OutboxProcessor` → `KafkaEventPublisher` `x-event-id` header →
   `ConsumedIntegrationEvent.DeduplicationKey`). Dedup moved from `(topic,partition,offset)` (which only
   guards redelivery of the *same* record) to the originating **outbox row id**, so the same business
   event is processed exactly once even when re-emitted at a different offset. `duplicate-event.ps1`
   proves it by re-draining an already-published row.

## Real bugs found & fixed while bringing the stack up

- **`/health/ready` returned 500**: `ConnectionMultiplexer.Connect()` was registered as the concrete
  type, so `RedisHealthCheck`'s `IConnectionMultiplexer` was unresolvable. Fixed to
  `AddSingleton<IConnectionMultiplexer>(multiplexer)` in `Infrastructure/DependencyInjection.cs`.
- **Cold-topic stall**: a topic auto-created *after* the consumer subscribed isn't picked up until
  librdkafka's ~5-min metadata refresh. `up.ps1` now **pre-provisions all integration-event topics**
  before the API launches (production-correct: provision, don't rely on auto-create).

## Honest caveats (not glossed over)

- `.NET`'s health middleware returns **HTTP 200 for `Degraded`** (only `Unhealthy` → 503), so the Redis
  outage signal is read from the **response body**, which `redis-failure.ps1` asserts on directly.
- The `"auth"` rate limit is a deliberate **10 req/IP/min** fixed window. When scenarios run
  back-to-back their login bursts can trip `429`; `Invoke-Login` treats that as expected throttling
  and waits for the window to roll rather than weakening the limiter (`auth-e2e.ps1` still exercises
  it, via lockout).
- SLA breach "time travel" is done by moving `ResponseDueAt` into the past — a legitimate way to test
  a timer-driven scanner in bounded time; policy stamping itself is proven in `ticket-e2e.ps1`.

## Observability during failures (ties in Stage 5B)

`ticket-e2e.ps1` prints an `X-Correlation-Id` that equals the W3C trace id, persisted on the outbox
row, injected as Kafka headers, and continued by the consumer — one trace spanning the async hop.
Open the Jaeger UI at **http://localhost:16686** (service `scaledesk-api`) and search that trace id to
see the HTTP request → outbox publish → kafka consume → projection/notification as a single trace; the
same holds for the `sla.breached` and outage-recovery flows.

## Files

- `up.ps1` — idempotent bring-up (Docker → compose → topic provisioning → detached API → readiness).
- `run-api-5c.ps1` — host API launch with production-equivalent env overrides.
- `_common.ps1` — shared helpers: `Invoke-Api` (never throws on non-2xx so 401/403/404 are assertable;
  tolerates non-JSON bodies), `Invoke-Login` (429-aware), `Invoke-Psql` / `Invoke-PsqlTenant` (superuser
  + RLS-enforced app role via stdin), `Check` / `Complete-Scenario`.
- the seven scenario scripts above · `run-all.ps1` — orchestrator + results table.
