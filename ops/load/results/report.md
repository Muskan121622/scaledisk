# Stage 5D/5E — Load Test Report (correlated)

**5D** measured the system and named the bottleneck (PostgreSQL: unindexed ticket-list sort).
**5E** applied the one fix — a composite `(TenantId, CreatedAt DESC)` index on `tickets` — and
re-ran the **identical** ramp. This report shows the **AFTER** curve with the BEFORE alongside.
Raw BEFORE artifacts are snapshotted in [`../results-before/`](../results-before/); AFTER in the
files here. Full reasoning: [`../FINDINGS.md`](../FINDINGS.md) (§1–7 diagnosis, §8 the fix).

> **Honesty:** single machine — k6, API, Postgres, Redis, Kafka share one laptop. Absolute `req/s`
> is machine-bound, **not** production throughput. The transferable result is the shape + the
> BEFORE/AFTER *delta* of the single change.

## HTTP curve — BEFORE vs AFTER (from `ramp.csv` vs `results-before/ramp.csv`)

| VUs | req/s BEF | req/s AFT | p95 BEF | p95 AFT | err% BEF | err% AFT |
|----:|----------:|----------:|--------:|--------:|---------:|---------:|
|   1 |       5.4 |      19.3 |  677 ms |  152 ms |  0.00 |  0.00 |
|  10 |      25.0 | **148.3** |  1010 ms | **142 ms** |  0.08 |  0.05 |
|  25 |      35.4 |     166.4 |  1990 ms |   344 ms |  0.05 |  0.03 |
|  50 |      45.8 | **167.5** |  3130 ms |   804 ms |  0.17 |  0.05 |
| 100 |      26.8 |      99.3 |  9500 ms |  2130 ms |  0.40 |  0.17 |
| 250 |      31.0 |     106.4 | 14800 ms |  3800 ms |  2.05 |  0.57 |
| 500 |      30.7 |      96.3 | 30200 ms |  8470 ms |  2.35 |  0.98 |

**Headline:** throughput at 50 VUs **45.8 → 167.5 req/s (3.7×)**; p95 at 50 VUs **3130 → 804 ms
(3.9×)**; the SLO-compliant ceiling (p95 < 1 s) moved from **~1 VU → 50 VUs**.

## Per-operation at 50 VUs (p95, BEFORE → AFTER)

| op (weight) | BEFORE | AFTER | Δ |
|-------------|-------:|------:|:-:|
| **ticket_list (40%)** | 3850 ms | **961 ms** | **4.0×** ← the fix target |
| dashboard (5%)        | 3000 ms |  795 ms | 3.8× |
| customer_search (5%)  | 1150 ms |  642 ms | 1.8× (secondary anti-pattern, no matching index yet) |
| ticket_create (25%)   |  259 ms |  259 ms | — writes untouched, correctly flat |
| ticket_update (15%)   |  221 ms |  240 ms | ~ |
| ticket_comment (10%)  |  358 ms |  233 ms | 1.5× |

Reads improved 2–4×; writes stayed flat — the exact fingerprint of removing sort cost from the
list query without touching the write path.

## Query plan (same `EXPLAIN`, BEFORE → AFTER)

| | BEFORE | AFTER |
|---|---:|---:|
| Plan | Bitmap Heap Scan + top-N Sort | Index Scan on `IX_tickets_TenantId_CreatedAt`, stops at 20 |
| Execution time | **401.9 ms** | **0.334 ms** (~1200×) |
| Buffers | **37,226** | **22** |
| Rows read | ~50,072 | 20 |

## Infrastructure — AFTER (peak-in-window, `metrics.csv`)

| VUs | api CPU % | pg CPU % | pg active/total conns | pool vs 100 | kafka lag | outbox pending |
|----:|----------:|---------:|:---------------------:|:-----------:|----------:|---------------:|
|  10 |     197   |    978   |       11 / 15         |    15%      |    706    |     679        |
|  25 |     181   |   1506   |       21 / 31         |    31%      |   1671    |     966        |
|  50 |     201   |   1759   |       47 / 56         |    56%      |   1918    |    1122        |
| 100 |     228   |   1702   |       66 / 69         |  **69%** (was 94%) | 1965 | 688       |
| 250 |     211   |   1366   |       35 / 87         |    87%      |    796    |    1243        |
| 500 |     246   |   1431   |       43 / 92         |    92%      |    291    |    1827        |

Reading honestly: pg active connections at 100 VUs dropped **94 → 66** (the sort fix released the
pool). pg CPU stays high because **throughput is 3–4× larger**, not because any query is slow —
that's a healthy utilization, not a regression. Kafka LAG / outbox pending grew (3–4× more events/s)
but still drain between levels — the async pipeline tracks the higher load. The new ceiling is the
**shared laptop CPU**, not one query shape.

## Next (5F candidates — deliberately not done in 5E)

1. `(TenantId, CreatedAt DESC)` on `customers` (customer_search still 642 ms, same anti-pattern).
2. Keyset/estimated paging to drop the second `COUNT(*)` full-tenant scan.

One fix → one clean measured delta was the goal; that's what this table shows.
