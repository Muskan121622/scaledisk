// Stage 5D.1 — Baseline. One VU, sequential ops, no contention. Establishes the latency floor for
// each operation (cold-ish cache on the first pass). Percentiles here are the no-load reference the
// ramp is measured against.
import { userFor, opLogin, opCreate, opList, opGet, opUpdate, opComment, opSearch, opDashboard, assert, SLO } from './common.js';

const thresholds = Object.assign({}, SLO.thresholds, {
  'http_req_duration{op:ticket_get}': ['p(95)<1000'],
});

export const options = {
  vus: 1,
  iterations: 12,
  summaryTrendStats: SLO.summaryTrendStats,
  thresholds,
};

export default function () {
  const u = userFor(__VU);
  assert(opLogin(u));
  assert(opCreate(u));
  assert(opList(u));
  assert(opGet(u));
  assert(opUpdate(u));
  assert(opComment(u));
  assert(opSearch(u));
  assert(opDashboard(u));
}
