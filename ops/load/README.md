# Stage 5D — Load & Performance Baseline

A **measurement-driven** load investigation, not a feature sprint. The goal is honest numbers:
where the system saturates, which subsystem bottlenecks first, and evidence for the fix — the fix
itself and the BEFORE/AFTER re-run are **5E**.

> Read [`FINDINGS.md`](./FINDINGS.md) for the results and diagnosis. This file is the *how*.

---

## Principles

- **Real numbers only.** Every figure comes from an actual run on this machine; raw artifacts are
  kept under `results/` and are re-generable.
- **Single machine = machine-bound.** k6, the API, Postgres, Redis and Kafka share one laptop. We
  report the *shape* (throughput knee, which subsystem saturates) and the *relative* utilization —
  **not** production throughput. Absolute `req/s` here is not a scaling claim.
- **Measure now, optimize later.** 5D ends at a bottleneck diagnosis + prioritized proposal.

## Locked decisions

| Aspect | Choice | Why |
|--------|--------|-----|
| Infra metrics | **lightweight snapshot collector** (`docker stats` + `psql` + `redis-cli` + Kafka tooling) → CSV | no monitoring stack competes for the CPU under test |
| Topology | **containerized API, Release build** (`docker-compose.load.yml`) | matches how the app ships; keeps the 5C host process from double-consuming |
| Dataset | **~20 tenants / ~1M tickets** seeded via server-side bulk SQL | read paths run against a realistic working set, not demo rows |
| Rate limits | **lifted via the LoadTesting profile** | the prod limits (300/min global, 10/min auth) would throttle the run to a standstill and we'd measure the limiter, not the backend |
| OTel | **disabled for load** (`Otel__Enabled=false`) | tracing overhead would distort p50/p95/p99 |

The production rate-limit defaults are unchanged in `appsettings.json`; the limit numbers simply
became **configuration-driven** (see `RateLimiting` in `Program.cs`) so a dedicated profile can
raise them. Prod behavior is byte-for-byte identical and stays proven by the 5C suite.

---

## Layout

```
ops/load/
  README.md              this file
  FINDINGS.md            results + diagnosis (read this)
  up-for-load.ps1        idempotent bring-up: stop host API -> Release container API (LoadTesting) -> wait ready -> seed if needed -> verify scale
  run-load.ps1           orchestrator: up -> token pool -> baseline -> per-level ramp (10..500) with a parallel infra sampler -> results/
  docker-compose.load.yml (at repo root)  load override: lifts limits, JWT TTL=600m, OTel off
  seed/load-dataset.sql  20 tenants / ~1M tickets (idempotent; clones the demo password hash so k6 can log in)
  auth/tokenpool.ps1     logs in 40 seeded agents -> ctx.json (tokens + sample tenant/customer/ticket ids)
  metrics/collect.ps1    per-sample infra snapshot -> results/metrics.csv
  k6/common.js           shared op engine + per-op tagging + SLO thresholds (error>1%, p95>1s)
  k6/baseline.js         1 VU, sequential, no-contention floor
  k6/mixed.js            weighted 40/25/15/10/5/5 workload on a constant VU count (env VUS/DUR)
  k6/ramp.js             continuous 10->500 ramp (alternative view to run-load.ps1's discrete levels)
  results/               generated: ramp.csv, metrics.csv, *.k6.log, report.md
  ctx.json               generated token pool (consumed by k6)
```

k6 runs as a **container** (`grafana/k6`) joined to the compose network and targets
`http://api:8080` directly — no host install, reproducible, and off the Windows host port-proxy.

## The mixed workload (realistic read/write mix)

| weight | operation |
|-------:|-----------|
| 40% | ticket list (`GET /api/tickets`) |
| 25% | ticket create (`POST /api/tickets`) |
| 15% | ticket update (`PATCH .../status`) |
| 10% | ticket comment (`POST .../comments`) |
|  5% | customer search (`GET /api/customers?search=`) |
|  5% | dashboard (read-composite: filtered list + unread-count + customers list) |

No "dashboard" endpoint exists; it is modeled as a read-composite rather than building an
aggregate endpoint (that would be feature work, out of scope for 5D). No think-time: VUs loop as
fast as the backend answers, so we push to the saturation knee on purpose — the goal is to find
where it breaks.

## Pre-agreed SLO / stop lines (encoded in k6 `thresholds`)

- error rate > **1%**
- p95 > **1000 ms**
- Postgres active connections sustained at the pool ceiling
- Kafka consumer LAG growing monotonically across two samples

The ramp **flags** which levels breach rather than halting at the first breach (a curve needs all
levels); it hard-stops only on a catastrophic result (no throughput, or error ≥ 25%).

---

## How to reproduce

```powershell
# 1) Bring up the load topology (Release container API + data plane) and seed if needed.
powershell -NoProfile -ExecutionPolicy Bypass -File ops/load/up-for-load.ps1

# 2) Provision the authenticated token pool (writes ops/load/ctx.json).
powershell -NoProfile -ExecutionPolicy Bypass -File ops/load/auth/tokenpool.ps1

# 3) Run the whole measurement (baseline + 10..500 ramp + infra sampling) -> results/.
powershell -NoProfile -ExecutionPolicy Bypass -File ops/load/run-load.ps1
```

`run-load.ps1` is the one-shot entry point and calls the other two itself. Bring-up is idempotent
(skips the seed once tickets ≥ 1M). Docker Desktop on this machine is flaky under sustained load;
the orchestrator writes each stage's log + the metrics CSV incrementally, so a mid-run crash still
leaves salvageable artifacts for the levels that completed.

## Exit criteria (met)

1. Seeder verified: ≥1M tickets across ≥20 tenants (asserted via psql). ✅
2. Baseline + full ramp produce p50/p95/p99, req/s, error% per stage. ✅
3. `metrics.csv` has per-stage API/Postgres/Redis/Kafka/Outbox samples correlated to the ramp. ✅
4. `FINDINGS.md` names ONE evidence-backed bottleneck (PostgreSQL: unindexed ticket-list sort) with
   the throughput knee and a prioritized 5E fix. ✅
5. All numbers reproducible by re-running `run-load.ps1`. ✅
