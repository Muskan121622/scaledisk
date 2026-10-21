// Stage 5D — shared k6 helpers: auth pool + one tagged function per operation.
// Run inside the grafana/k6 container joined to the compose network, e.g.:
//   docker run --rm --network scaledesk_default -v <repo>/ops/load:/scripts \
//     --env BASE=http://api:8080 --env CTX=/scripts/ctx.json grafana/k6 run /scripts/k6/mixed.js
//
// Enums go on the wire as NUMBERS (the API serializes enums numerically — verified by the 5C suite):
//   priority 2=High, category 1=Billing, status 1=InProgress.
import http from 'k6/http';
import { check } from 'k6';

export const BASE = __ENV.BASE || 'http://localhost:8080';
// Strip a leading UTF-8 BOM (Windows PowerShell's Set-Content -Encoding UTF8 adds one) before parse.
const pool = JSON.parse(open(__ENV.CTX || './ctx.json').replace(/^\uFEFF/, ''));

export function size() { return pool.length; }

// One stable virtual user per k6 VU, so a given VU always acts as the same tenant/agent.
export function userFor(vuId) { return pool[(vuId - 1) % pool.length]; }

function bearer(u) { return { headers: { Authorization: `Bearer ${u.token}` }, tags: {} }; }
function jsonOpts(u, op) {
  return { headers: { Authorization: `Bearer ${u.token}`, 'Content-Type': 'application/json' }, tags: { op } };
}
function getOpts(u, op) { return { headers: { Authorization: `Bearer ${u.token}` }, tags: { op } }; }

function randomTicket(u) {
  const ids = u.ticketIds;
  if (!ids || ids.length === 0) return null;
  return ids[Math.floor(Math.random() * ids.length)];
}

export function opList(u) {
  return http.get(`${BASE}/api/tickets?pageSize=20&page=1`, getOpts(u, 'ticket_list'));
}
export function opGet(u) {
  const id = randomTicket(u); if (!id) return null;
  return http.get(`${BASE}/api/tickets/${id}`, getOpts(u, 'ticket_get'));
}
export function opCreate(u) {
  const body = JSON.stringify({
    customerId: u.customerId,
    subject: `k5d ${Date.now()} ${Math.floor(Math.random() * 1e6)}`,
    description: 'load-test ticket', priority: 2, category: 1, teamId: null, assigneeUserId: null,
  });
  return http.post(`${BASE}/api/tickets`, body, jsonOpts(u, 'ticket_create'));
}
export function opUpdate(u) {
  const id = randomTicket(u); if (!id) return null;
  return http.patch(`${BASE}/api/tickets/${id}/status`, JSON.stringify({ status: 1 }), jsonOpts(u, 'ticket_update'));
}
export function opComment(u) {
  const id = randomTicket(u); if (!id) return null;
  return http.post(`${BASE}/api/tickets/${id}/comments`, JSON.stringify({ body: 'k6 load note', isInternal: false }), jsonOpts(u, 'ticket_comment'));
}
export function opSearch(u) {
  return http.get(`${BASE}/api/customers?search=cust&page=1&pageSize=20`, getOpts(u, 'customer_search'));
}
// "Dashboard" has no single endpoint; model it as the read-composite a real dashboard would fire.
export function opDashboard(u) {
  http.get(`${BASE}/api/tickets?status=Open&pageSize=10`, getOpts(u, 'dashboard'));
  http.get(`${BASE}/api/notifications/unread-count`, getOpts(u, 'dashboard'));
  return http.get(`${BASE}/api/customers?pageSize=10`, getOpts(u, 'dashboard'));
}
export function opLogin(u) {
  return http.post(`${BASE}/api/auth/login`, JSON.stringify({ email: u.email, password: 'Passw0rd!', organizationSlug: u.slug }),
    { headers: { 'Content-Type': 'application/json' }, tags: { op: 'auth_login' } });
}

const OK = { 'status<400': (r) => r.status < 400 };
export function assert(r) { if (r) check(r, OK); }

// Weighted op mix exactly per the Stage 5D spec:
// 40% list, 25% create, 15% update, 10% comment, 5% customer search, 5% dashboard composite.
export function runMixed(u) {
  const r = Math.random();
  if (r < 0.40) return assert(opList(u));
  if (r < 0.65) return assert(opCreate(u));
  if (r < 0.80) return assert(opUpdate(u));
  if (r < 0.90) return assert(opComment(u));
  if (r < 0.95) return assert(opSearch(u));
  return assert(opDashboard(u));
}

// Shared SLO / stop lines. A breach makes k6 exit non-zero, which run-load.ps1 uses to halt the ramp.
export const SLO = {
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  thresholds: {
    http_req_failed: ['rate<0.01'],          // error rate under 1%
    http_req_duration: ['p(95)<1000'],        // overall p95 under 1s
    'http_req_duration{op:ticket_list}': ['p(95)<1000'],
    'http_req_duration{op:ticket_create}': ['p(95)<1500'],
    'http_req_duration{op:ticket_update}': ['p(95)<1500'],
    'http_req_duration{op:ticket_comment}': ['p(95)<1500'],
    'http_req_duration{op:customer_search}': ['p(95)<1500'],
    'http_req_duration{op:dashboard}': ['p(95)<2000'],
    'http_req_duration{op:auth_login}': ['p(95)<3000'],
  },
};
