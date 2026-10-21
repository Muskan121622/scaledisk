// Customer portal auth context. Same isolation idea as lib/platform-auth.tsx (own localStorage
// namespace so staff / HQ / customer sessions coexist in one browser), but closer to lib/auth.tsx
// in behavior: portal sessions have a refresh lineage, so the 401-rotate-retry loop lives here.
//
// There is no /me endpoint for the portal — the display email is whatever the user typed at
// login/signup, persisted alongside the tokens. Ticket identity always comes from the cid claim.

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import {
  portalApi, setPortalTokens, getPortalTokens, setPortalRefreshHandler,
} from './portal-api';
import type { AuthTokens } from '@/types';

type PortalAuthValue = {
  status: 'loading' | 'anonymous' | 'authenticated';
  email: string | null;
  slug: string | null;
  login: (email: string, password: string, organizationSlug: string) => Promise<void>;
  register: (input: {
    organizationSlug: string; email: string; password: string; firstName: string; lastName: string;
  }) => Promise<void>;
  logout: () => Promise<void>;
};

const PortalAuthContext = createContext<PortalAuthValue | null>(null);

const LS_SESSION = 'scaledesk.portal.session';

type StoredSession = {
  email: string;
  slug: string;
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
};

function readStored(): StoredSession | null {
  try {
    const raw = localStorage.getItem(LS_SESSION);
    return raw ? (JSON.parse(raw) as StoredSession) : null;
  } catch {
    return null;
  }
}
function writeStored(s: StoredSession | null) {
  if (s) localStorage.setItem(LS_SESSION, JSON.stringify(s));
  else localStorage.removeItem(LS_SESSION);
}

export function PortalAuthProvider({ children }: { children: ReactNode }) {
  const [email, setEmail] = useState<string | null>(null);
  const [slug, setSlug] = useState<string | null>(null);
  const [status, setStatus] = useState<PortalAuthValue['status']>('loading');

  const clear = () => {
    writeStored(null);
    setPortalTokens(null);
    setEmail(null);
    setSlug(null);
    setStatus('anonymous');
  };

  // Bootstrap: hydrate stored tokens and validate with a cheap call. An expired access token is
  // fine here — the request below transparently rotates once before it can fail.
  useEffect(() => {
    const stored = readStored();
    if (!stored) { setStatus('anonymous'); return; }
    setPortalTokens({ accessToken: stored.accessToken, refreshToken: stored.refreshToken });
    portalApi.get('/portal/tickets', { page: 1, pageSize: 1 })
      .then(() => {
        setEmail(stored.email);
        setSlug(stored.slug);
        setStatus('authenticated');
      })
      .catch(clear);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Registered with portal-api: called on any 401 with an active session (one retry per call).
  useEffect(() => {
    setPortalRefreshHandler(async () => {
      const current = getPortalTokens();
      if (!current) return null;
      try {
        const next = await portalApi.postNoAuth<AuthTokens>('/portal/refresh', {
          refreshToken: current.refreshToken,
        });
        persist(next);
        return { accessToken: next.accessToken, refreshToken: next.refreshToken };
      } catch {
        clear();
        return null;
      }
    });
    return () => setPortalRefreshHandler(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function persist(t: AuthTokens) {
    writeStored({ email: email ?? readStored()?.email ?? '', slug: slug ?? readStored()?.slug ?? '',
      accessToken: t.accessToken, refreshToken: t.refreshToken, expiresAtUtc: t.accessTokenExpiresAtUtc });
    setPortalTokens({ accessToken: t.accessToken, refreshToken: t.refreshToken });
  }

  const value: PortalAuthValue = {
    status,
    email,
    slug,
    async login(loginEmail, password, organizationSlug) {
      const t = await portalApi.postNoAuth<AuthTokens>('/portal/login', {
        organizationSlug, email: loginEmail, password,
      });
      setEmail(loginEmail);
      setSlug(organizationSlug);
      writeStored({ email: loginEmail, slug: organizationSlug, accessToken: t.accessToken,
        refreshToken: t.refreshToken, expiresAtUtc: t.accessTokenExpiresAtUtc });
      setPortalTokens({ accessToken: t.accessToken, refreshToken: t.refreshToken });
      setStatus('authenticated');
    },
    async register(input) {
      const t = await portalApi.postNoAuth<AuthTokens>('/portal/register', input);
      setEmail(input.email);
      setSlug(input.organizationSlug);
      writeStored({ email: input.email, slug: input.organizationSlug, accessToken: t.accessToken,
        refreshToken: t.refreshToken, expiresAtUtc: t.accessTokenExpiresAtUtc });
      setPortalTokens({ accessToken: t.accessToken, refreshToken: t.refreshToken });
      setStatus('authenticated');
    },
    async logout() {
      const current = getPortalTokens();
      if (current) {
        try { await portalApi.post(`/portal/logout?refreshToken=${encodeURIComponent(current.refreshToken)}`); }
        catch { /* best effort — clearing locally is what matters */ }
      }
      clear();
    },
  };

  return <PortalAuthContext.Provider value={value}>{children}</PortalAuthContext.Provider>;
}

export function usePortalAuth(): PortalAuthValue {
  const v = useContext(PortalAuthContext);
  if (!v) throw new Error('usePortalAuth must be used inside <PortalAuthProvider>');
  return v;
}
