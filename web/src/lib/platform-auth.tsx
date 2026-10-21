// Platform (ScaleDesk HQ) auth context. Mirrors lib/auth.tsx but far simpler:
// access-only tokens (no refresh lineage), no /me round-trip, and its own localStorage
// namespace so staff and HQ sessions can live in the same browser for demos.

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { platformApi, setPlatformToken, setPlatformSessionExpiredHandler } from './platform-api';
import type { PlatformSession } from '@/types/platform';

type PlatformAuthValue = {
  status: 'loading' | 'anonymous' | 'authenticated';
  email: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
};

const PlatformAuthContext = createContext<PlatformAuthValue | null>(null);

const LS_SESSION = 'scaledesk.platform.session';

type StoredSession = { email: string; accessToken: string; expiresAtUtc: string };

function readStored(): StoredSession | null {
  try {
    const raw = localStorage.getItem(LS_SESSION);
    if (!raw) return null;
    const s = JSON.parse(raw) as StoredSession;
    // Expired on read: treat as absent (tokens are access-only, there is nothing to refresh).
    if (new Date(s.expiresAtUtc).getTime() <= Date.now()) return null;
    return s;
  } catch {
    return null;
  }
}
function writeStored(s: StoredSession | null) {
  if (s) localStorage.setItem(LS_SESSION, JSON.stringify(s));
  else localStorage.removeItem(LS_SESSION);
}

export function PlatformAuthProvider({ children }: { children: ReactNode }) {
  const [email, setEmail] = useState<string | null>(null);
  const [status, setStatus] = useState<PlatformAuthValue['status']>('loading');

  useEffect(() => {
    const stored = readStored();
    if (!stored) { setStatus('anonymous'); return; }
    setPlatformToken(stored.accessToken);
    setEmail(stored.email);
    setStatus('authenticated');
    return () => setPlatformToken(null);
  }, []);

  const clear = () => {
    writeStored(null);
    setPlatformToken(null);
    setEmail(null);
    setStatus('anonymous');
  };

  // A 401 from any HQ call means the access token died mid-session: drop it, guard redirects.
  useEffect(() => {
    setPlatformSessionExpiredHandler(clear);
    return () => setPlatformSessionExpiredHandler(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const value: PlatformAuthValue = {
    status,
    email,
    async login(loginEmail, password) {
      const session = await platformApi.postNoAuth<PlatformSession>('/auth/platform/login', {
        email: loginEmail,
        password,
      });
      writeStored({
        email: session.email,
        accessToken: session.accessToken,
        expiresAtUtc: session.accessTokenExpiresAtUtc,
      });
      setPlatformToken(session.accessToken);
      setEmail(session.email);
      setStatus('authenticated');
    },
    logout: clear,
  };

  return <PlatformAuthContext.Provider value={value}>{children}</PlatformAuthContext.Provider>;
}

export function usePlatformAuth(): PlatformAuthValue {
  const v = useContext(PlatformAuthContext);
  if (!v) throw new Error('usePlatformAuth must be used inside <PlatformAuthProvider>');
  return v;
}
