// Auth context. Owns the current user + tokens, wires the api client's refresh handler,
// and exposes login / register / demoLogin / logout. Tokens live in localStorage so a
// page refresh restores the session; the api module keeps them in-memory for fast reads.

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { api, setRefreshHandler, setTokens, getTokens } from './api';
import { stopHub } from './signalr';
import type { AuthTokens, CurrentUser } from '@/types';

type Session = { tokens: AuthTokens; user: CurrentUser };

type AuthContextValue = {
  status: 'loading' | 'anonymous' | 'authenticated';
  user: CurrentUser | null;
  login: (email: string, password: string, organizationSlug: string) => Promise<void>;
  demoLogin: () => Promise<void>;
  register: (input: {
    organizationName: string; organizationSlug: string;
    email: string; password: string; firstName: string; lastName: string;
  }) => Promise<void>;
  logout: () => Promise<void>;
  hasPermission: (p: string) => boolean;
};

const AuthContext = createContext<AuthContextValue | null>(null);

const LS_TOKENS = 'scaledesk.tokens';
const LS_ORG = 'scaledesk.orgSlug';

function readStored(): AuthTokens | null {
  try {
    const raw = localStorage.getItem(LS_TOKENS);
    return raw ? (JSON.parse(raw) as AuthTokens) : null;
  } catch { return null; }
}
function writeStored(t: AuthTokens | null) {
  if (t) localStorage.setItem(LS_TOKENS, JSON.stringify(t));
  else localStorage.removeItem(LS_TOKENS);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [status, setStatus] = useState<AuthContextValue['status']>('loading');

  // Bootstrap: if a stored token set exists, hydrate and fetch /me; a 401 refreshes once.
  useEffect(() => {
    const stored = readStored();
    if (!stored) { setStatus('anonymous'); return; }
    setTokens({ accessToken: stored.accessToken, refreshToken: stored.refreshToken });
    api.get<CurrentUser>('/auth/me')
      .then((user) => { setSession({ tokens: stored, user }); setStatus('authenticated'); })
      .catch(() => { writeStored(null); setTokens(null); setStatus('anonymous'); });
  }, []);

  // The api client calls this on 401. It must return fresh tokens or null (which triggers a
  // hard logout on the next check).
  useEffect(() => {
    setRefreshHandler(async () => {
      const current = getTokens();
      if (!current) return null;
      try {
        const next = await api.postNoAuth<AuthTokens>('/auth/refresh', { refreshToken: current.refreshToken });
        writeStored(next);
        return { accessToken: next.accessToken, refreshToken: next.refreshToken };
      } catch {
        writeStored(null);
        setSession(null);
        setStatus('anonymous');
        return null;
      }
    });
    return () => setRefreshHandler(null);
  }, []);

  async function adopt(tokens: AuthTokens) {
    writeStored(tokens);
    setTokens({ accessToken: tokens.accessToken, refreshToken: tokens.refreshToken });
    const user = await api.get<CurrentUser>('/auth/me');
    setSession({ tokens, user });
    setStatus('authenticated');
  }

  const value: AuthContextValue = {
    status,
    user: session?.user ?? null,
    async login(email, password, organizationSlug) {
      localStorage.setItem(LS_ORG, organizationSlug);
      const tokens = await api.postNoAuth<AuthTokens>('/auth/login', { email, password, organizationSlug });
      await adopt(tokens);
    },
    async demoLogin() {
      const tokens = await api.postNoAuth<AuthTokens>('/auth/demo', {});
      await adopt(tokens);
    },
    async register(input) {
      const tokens = await api.postNoAuth<AuthTokens>('/auth/register', input);
      await adopt(tokens);
    },
    async logout() {
      const current = getTokens();
      if (current) {
        try { await api.post(`/auth/logout?refreshToken=${encodeURIComponent(current.refreshToken)}`); }
        catch { /* best effort */ }
      }
      writeStored(null);
      setTokens(null);
      setSession(null);
      setStatus('anonymous');
      stopHub();
    },
    hasPermission(p) {
      return !!session?.user.permissions.includes(p);
    },
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const v = useContext(AuthContext);
  if (!v) throw new Error('useAuth must be used inside <AuthProvider>');
  return v;
}
