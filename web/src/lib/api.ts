// Central HTTP client. Adds bearer token, transparently refreshes once on 401,
// and throws a typed ApiError with the backend's problem-details shape.
//
// Token storage is intentionally kept in a module-scoped variable so the api client
// can be used outside React (in query functions, in the SignalR factory, etc.). The AuthProvider
// in ./auth.tsx is what hydrates and clears it in response to login/logout.

export type ApiErrorBody = {
  title?: string;
  status?: number;
  detail?: string;
  errorCode?: string;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  constructor(public status: number, public body: ApiErrorBody | null, message?: string) {
    super(message ?? body?.detail ?? body?.title ?? `Request failed (${status})`);
    this.name = 'ApiError';
  }
}

type Tokens = { accessToken: string; refreshToken: string } | null;

let tokens: Tokens = null;
let refreshHandler: (() => Promise<Tokens>) | null = null;
const listeners = new Set<(t: Tokens) => void>();

export function setTokens(t: Tokens) {
  tokens = t;
  listeners.forEach((l) => l(t));
}
export function getTokens(): Tokens { return tokens; }
export function onTokenChange(l: (t: Tokens) => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}
export function setRefreshHandler(fn: (() => Promise<Tokens>) | null) { refreshHandler = fn; }

export const API_BASE = '/api';

type FetchInit = RequestInit & { withAuth?: boolean };

async function doFetch(path: string, init: FetchInit): Promise<Response> {
  const headers = new Headers(init.headers ?? {});
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (init.withAuth !== false && tokens?.accessToken) headers.set('Authorization', `Bearer ${tokens.accessToken}`);
  return fetch(path, { ...init, headers, credentials: 'omit' });
}

async function request<T>(path: string, init: FetchInit): Promise<T> {
  let res = await doFetch(path, init);

  // 401 with an active refresh handler: rotate once and retry. The backend rotates refresh
  // tokens and revokes the whole family if it sees a reused token, so we only retry once per call.
  if (res.status === 401 && refreshHandler && init.withAuth !== false) {
    const next = await refreshHandler();
    if (next?.accessToken) {
      setTokens(next);
      res = await doFetch(path, init);
    }
  }

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const parsed: ApiErrorBody | null = text ? safeJson(text) : null;

  if (!res.ok) throw new ApiError(res.status, parsed);
  return parsed ? (parsed as unknown as T) : (undefined as T);
}

function safeJson(text: string): ApiErrorBody | null {
  try { return JSON.parse(text) as ApiErrorBody; } catch { return null; }
}

function qs(params?: Record<string, string | number | undefined | null>): string {
  if (!params) return '';
  const usp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '') continue;
    usp.set(k, String(v));
  }
  const s = usp.toString();
  return s ? `?${s}` : '';
}

export const api = {
  get: <T>(path: string, params?: Record<string, unknown>) =>
    request<T>(`${API_BASE}${path}${qs(params as never)}`, { method: 'GET' }),

  post: <T>(path: string, body?: unknown) =>
    request<T>(`${API_BASE}${path}`, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),

  postNoAuth: <T>(path: string, body?: unknown) =>
    request<T>(`${API_BASE}${path}`, { method: 'POST', body: JSON.stringify(body ?? {}), withAuth: false }),

  patch: <T>(path: string, body?: unknown) =>
    request<T>(`${API_BASE}${path}`, { method: 'PATCH', body: JSON.stringify(body ?? {}) }),

  put: <T>(path: string, body?: unknown) =>
    request<T>(`${API_BASE}${path}`, { method: 'PUT', body: JSON.stringify(body ?? {}) }),

  del: <T>(path: string) => request<T>(`${API_BASE}${path}`, { method: 'DELETE' }),
};
