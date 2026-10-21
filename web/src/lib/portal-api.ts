// Customer portal HTTP client. Third isolated token space (staff: ./api.ts, HQ: ./platform-api.ts):
// `scaledesk.portal.*` sessions live in their own module state + localStorage namespace, so the
// same browser can show an agent tab and a customer tab side by side — the J3 money shot.
//
// Unlike HQ tokens, portal access tokens DO rotate: a 401 triggers the provider-registered
// refresh handler once, then the call is retried; only a failed rotation drops the session.

import { ApiError, type ApiErrorBody } from './api';

export type PortalTokens = { accessToken: string; refreshToken: string } | null;

let tokens: PortalTokens = null;
let refreshHandler: (() => Promise<PortalTokens>) | null = null;

export function setPortalTokens(t: PortalTokens) { tokens = t; }
export function getPortalTokens(): PortalTokens { return tokens; }
export function setPortalRefreshHandler(fn: (() => Promise<PortalTokens>) | null) { refreshHandler = fn; }

type Init = RequestInit & { withAuth?: boolean };

async function doFetch(path: string, init: Init): Promise<Response> {
  const headers = new Headers(init.headers ?? {});
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (init.withAuth !== false && tokens?.accessToken) headers.set('Authorization', `Bearer ${tokens.accessToken}`);
  return fetch(`/api${path}`, { ...init, headers, credentials: 'omit' });
}

async function request<T>(path: string, init: Init): Promise<T> {
  let res = await doFetch(path, init);

  if (res.status === 401 && refreshHandler && init.withAuth !== false) {
    const next = await refreshHandler();
    if (next?.accessToken) {
      setPortalTokens(next);
      res = await doFetch(path, init);
    }
  }

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  let body: ApiErrorBody | null = null;
  try { body = text ? (JSON.parse(text) as ApiErrorBody) : null; } catch { body = null; }

  if (!res.ok) throw new ApiError(res.status, body);
  return (body ? (body as unknown as T) : undefined) as T;
}

function withQuery(path: string, params?: Record<string, string | number | undefined | null>): string {
  if (!params) return path;
  const usp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '') continue;
    usp.set(k, String(v));
  }
  const s = usp.toString();
  return s ? `${path}?${s}` : path;
}

export const portalApi = {
  get: <T>(path: string, params?: Record<string, string | number | undefined | null>) =>
    request<T>(withQuery(path, params), { method: 'GET' }),

  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),

  postNoAuth: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: JSON.stringify(body ?? {}), withAuth: false }),
};
