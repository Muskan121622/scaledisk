// ScaleDesk HQ HTTP client. Deliberately separate from lib/api.ts: platform sessions keep
// their token in its own module-scoped variable + localStorage namespace, so an HQ tab and a
// staff tab can be demoed in the same browser without clobbering each other.
//
// There is no refresh flow by design — platform tokens are access-only; on a 401 the session
// handler clears it and the guard sends the user back to /platform/login.

import { ApiError, type ApiErrorBody } from './api';

let token: string | null = null;
let expiredHandler: (() => void) | null = null;

export function setPlatformToken(t: string | null) { token = t; }
export function setPlatformSessionExpiredHandler(fn: (() => void) | null) { expiredHandler = fn; }

type Init = RequestInit & { withAuth?: boolean };

async function request<T>(path: string, init: Init = {}): Promise<T> {
  const headers = new Headers(init.headers ?? {});
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (init.withAuth !== false && token) headers.set('Authorization', `Bearer ${token}`);

  const res = await fetch(`/api${path}`, { ...init, headers, credentials: 'omit' });

  // Expired/tampered HQ token: notify once so the provider can drop the session.
  if (res.status === 401 && init.withAuth !== false && token) expiredHandler?.();

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

export const platformApi = {
  get: <T>(path: string, params?: Record<string, string | number | undefined | null>) =>
    request<T>(withQuery(path, params), { method: 'GET' }),

  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),

  postNoAuth: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: JSON.stringify(body ?? {}), withAuth: false }),
};
