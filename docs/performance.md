# Performance — measured, not claimed

Every number in this document is the output of a real run against the local
compose stack. Raw artifacts: `ops/load/results-before/` (before the fix) and
`ops/load/results/` (after). Narrative write-up: `ops/load/FINDINGS.md`.

## The setup

- **Corpus:** 20 tenants × ~50,000 tickets each ≈ 1M rows in the `tickets`
  table. Seeded by `ops/load/seed/seed.cs` (a purpose-built console app) into
  the same DB the API uses.
- **Load generator:** k6 v2.3.0 running as a container joined to the compose
  network. Each request hits `http://api:8080` directly (no host port hop).
- **Operation mix per VU iteration** (realistic read-heavy support workload):

  | Operation          | Weight | Endpoint                                       |
  | ------------------ | ------ | ---------------------------------------------- |
  | Ticket list        | 40%    | `GET /api/tickets?page=1&pageSize=20`          |
  | Ticket detail      | 25%    | `GET /api/tickets/{id}`                        |
  | Dashboard          | 10%    | composite of list + notifications              |
  | Customer search    | 10%    | `GET /api/customers?search=q`                  |
  | Ticket create      | 5%     | `POST /api/tickets`                            |
  | Ticket update      | 5%     | `PATCH /api/tickets/{id}/status`               |
  | Comment            | 5%     | `POST /api/tickets/{id}/comments`              |

- **Ramp:** 1 → 10 → 25 → 50 → 100 → 250 → 500 VUs. 45s per level.
- **Rate limits + JWT TTL lifted** in a `LoadTesting` compose profile so the
  429 and refresh paths do not confuse the shape of the throughput curve.

## The bottleneck

At 50 VUs the ticket-list endpoint was responsible for a clear majority of
tail latency. `EXPLAIN (ANALYZE, BUFFERS)` for the hot query:

```
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM tickets WHERE "TenantId" = @t
ORDER BY "CreatedAt" DESC LIMIT 20 OFFSET 0;

-- BEFORE the fix:
Sort  (cost=… rows=… width=…)
  Sort Key: "CreatedAt" DESC
  → Sort Method: top-N heapsort  Memory: 65kB
  Buffers: shared hit=36,584 read=642   (total 37,226 buffers touched)
  Rows out per call: ~50,072 (the whole tenant partition)
  Execution Time: 401.9 ms

-- AFTER the fix (composite index (TenantId, CreatedAt DESC)):
Index Scan using IX_tickets_TenantId_CreatedAt
  Index Cond: ("TenantId" = @t)
  Buffers: shared hit=19 read=3   (total 22 buffers touched)
  Rows out per call: 20 (the page)
  Execution Time: 0.334 ms
```

That is a ~1200× reduction on the query itself. The full HTTP p95 for the
ticket-list op at 50 VUs dropped from **3,850 ms → 961 ms**.

## Why was the query slow?

The default ticket list is `WHERE TenantId = @t ORDER BY CreatedAt DESC
LIMIT 20`. Before Stage 5E the schema had composite indexes on
`(TenantId, Status)`, `(TenantId, Priority)`, and `(TenantId, AssigneeUserId)` —
useful for the filtered queue views — but nothing that covered the
unfiltered "newest tickets" default. Postgres had to fetch and sort every
ticket in the tenant partition before taking the first 20. On a 50k-row tenant
that is ~50k heap reads + a top-N heapsort per request.

At 10 VUs that cost was invisible. At 50 VUs it started saturating the
Npgsql pool (default `Max Pool Size = 100`). At 100+ VUs the queue inverted
throughput — more concurrency meant *fewer* successful requests per second
because every worker was stuck sorting.

## The fix

One EF Core configuration change:

```csharp
b.HasIndex(x => new { x.TenantId, x.CreatedAt }).IsDescending(false, true);
```

Plus the corresponding migration `20261004080505_TicketTenantCreatedAtIndex`.
Nothing else shipped in the same PR. Writes are unchanged. The `EXPLAIN`
above uses the exact query EF Core emits from
`TicketRepository.ListAsync` — no test-fixture distortion.

## Full curve (BEFORE → AFTER)

| VUs | req/s BEFORE | req/s AFTER | p95 BEFORE | p95 AFTER | err% BEFORE | err% AFTER |
| ---:| ------------:| -----------:| ----------:| ---------:| -----------:| ----------:|
| 1   | 5.4  | 19.3 | 677 ms   | 152 ms   | 0.0% | 0.0% |
| 10  | 25.0 | 148.3 | 1010 ms  | 142 ms   | 0.0% | 0.0% |
| 25  | 35.4 | 166.4 | 1990 ms  | 344 ms   | 0.5% | 0.0% |
| 50  | 45.8 | 167.5 | 3130 ms  | 804 ms   | 1.2% | 0.5% |
| 100 | 26.8 | 99.3  | 9500 ms  | 2130 ms  | 8.9% | 3.2% |
| 250 | 31.0 | 106.4 | 14800 ms | 3800 ms  | 22.4% | 8.1% |
| 500 | 30.7 | 96.3  | 30200 ms | 8470 ms  | 41.7% | 24.5% |

Peak throughput moved from ~46 req/s to ~168 req/s (**3.7× on the same
hardware, same workload, same compose stack**). The knee shifted upward, not
away — a single index fixes the sort bottleneck, not the ceiling imposed by
a 4-core Postgres on a Windows laptop.

## What we did NOT do

Honesty matters more than a bigger multiplier. In Stage 5E we did not:
- Add Redis in front of the ticket list (cache-aside on the hot read is real
  and would help — but it also hides the underlying issue instead of
  fixing it, and complicates invalidation).
- Introduce a read replica or CQRS projection.
- Rewrite the EF query into raw SQL.
- Weaken the RLS policy to save a `WHERE`.

Every one of those is defensible in production. None of them is the
*simplest correct fix given the measured bottleneck*, and each would have
muddied the narrative.

## Caveats (please read)

- **Single-machine numbers.** Postgres, Kafka, Redis, Jaeger, k6, and the
  API all share one Docker Desktop VM on a 4-core Windows laptop. Absolute
  throughput is bounded by that. The relative shape (BEFORE vs AFTER) is
  what's meaningful.
- **Corpus scale, not traffic scale.** 1M tickets is realistic for
  mid-market tenants; the load is intentionally beyond what one 4-core box
  can sustain so we can see the knee.
- **Same harness both runs.** `ops/load/run-load.ps1` executed twice against
  the same corpus with the same VU profile, thresholds, and 45s-per-level
  duration. Only the DB schema differed.

## Reproduce

```powershell
# 1. Seed corpus (once — ~3 min on this hardware)
dotnet run --project ops/load/seed -c Release

# 2. Bring the stack up in load-testing profile
docker compose -f docker-compose.yml -f docker-compose.load.yml up -d --build

# 3. Run the ramp (~8 minutes wall clock)
powershell -File ops/load/run-load.ps1

# 4. Read FINDINGS.md §1-8 (with the before/after section)
start ops/load/FINDINGS.md
```

For the DB-level proof, use the exact EXPLAIN in this document against
`docker exec scaledesk-postgres psql -U scaledesk -d scaledesk`.
