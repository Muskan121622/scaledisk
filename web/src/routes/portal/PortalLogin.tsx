// Customer sign-in / sign-up. One screen with a mode toggle: "claim your existing account"
// (register links the seeded/known customer record by exact email) vs plain login. Light theme
// with the violet brand — clearly the customer-facing side, not the navy HQ or the staff app.

import { useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { LifeBuoy } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label-badge';
import { ApiError } from '@/lib/api';
import { usePortalAuth } from '@/lib/portal-auth';

type Mode = 'login' | 'register';

export function PortalLogin() {
  const { status, login, register } = usePortalAuth();
  const [mode, setMode] = useState<Mode>('login');
  const [slug, setSlug] = useState('demo');
  const [email, setEmail] = useState('priya@acme-retail.example');
  const [password, setPassword] = useState('PortalPass2026!');
  const [firstName, setFirstName] = useState('Priya');
  const [lastName, setLastName] = useState('Nair');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (status === 'authenticated') return <Navigate to="/portal/tickets" replace />;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (mode === 'login') await login(email, password, slug);
      else await register({ organizationSlug: slug, email, password, firstName, lastName });
    } catch (err) {
      setError(
        err instanceof ApiError
          ? (err.body?.title ?? err.message)
          : 'Unable to reach the server.',
      );
      setBusy(false);
    }
    // On success the Navigate above takes over; keep the spinner otherwise.
  }

  const field = 'mt-1.5 rounded-xl border-slate-200 bg-white';

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-slate-50 via-violet-50 to-sky-50 px-4">
      <div className="w-full max-w-md">
        <Link to="/" className="mb-6 inline-flex items-center gap-1 text-xs text-slate-400 hover:text-slate-600">
          ← Back to home
        </Link>

        <div className="surface rounded-2xl p-8 shadow-xl">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-gradient-to-br from-violet-600 to-indigo-500">
              <LifeBuoy className="h-6 w-6 text-white" />
            </div>
            <div>
              <h1 className="text-xl font-semibold tracking-tight text-slate-900">Support Portal</h1>
              <p className="text-xs text-slate-500">Track your tickets and talk to the support team</p>
            </div>
          </div>

          {/* mode toggle */}
          <div className="mt-6 grid grid-cols-2 rounded-xl bg-slate-100 p-1 text-sm font-medium">
            {(['login', 'register'] as const).map((m) => (
              <button
                key={m}
                type="button"
                onClick={() => { setMode(m); setError(null); }}
                className={`rounded-lg py-1.5 transition-colors ${
                  mode === m ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700'
                }`}
              >
                {m === 'login' ? 'Sign in' : 'Create account'}
              </button>
            ))}
          </div>

          <form className="mt-5 space-y-4" onSubmit={submit}>
            <div>
              <Label htmlFor="slug" className="text-slate-600">Workspace</Label>
              <Input id="slug" value={slug} onChange={(e) => setSlug(e.target.value)}
                placeholder="e.g. demo" className={field} />
            </div>
            <div>
              <Label htmlFor="pemail" className="text-slate-600">Email</Label>
              <Input id="pemail" type="email" autoComplete="username" value={email}
                onChange={(e) => setEmail(e.target.value)} className={field} />
            </div>
            {mode === 'register' && (
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label htmlFor="first" className="text-slate-600">First name</Label>
                  <Input id="first" value={firstName} onChange={(e) => setFirstName(e.target.value)} className={field} />
                </div>
                <div>
                  <Label htmlFor="last" className="text-slate-600">Last name</Label>
                  <Input id="last" value={lastName} onChange={(e) => setLastName(e.target.value)} className={field} />
                </div>
              </div>
            )}
            <div>
              <Label htmlFor="ppwd" className="text-slate-600">Password</Label>
              <Input id="ppwd" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                value={password} onChange={(e) => setPassword(e.target.value)} className={field} />
            </div>

            {error && (
              <div className="rounded-md border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
                {error}
              </div>
            )}

            <Button type="submit" className="w-full rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500" disabled={busy}>
              {busy ? 'Working…' : mode === 'login' ? 'Sign in' : 'Create my account'}
            </Button>
          </form>

          <p className="mt-5 text-center text-xs text-slate-400">
            {mode === 'register'
              ? 'Use the email the company already knows you by — it links your tickets to this account.'
              : 'First time here? Switch to “Create account” — same email as your invoices.'}
          </p>
        </div>

        <div className="mt-4 text-center text-xs text-slate-400">
          Staff member? <Link to="/login" className="text-violet-600 hover:underline">Agent sign in</Link>
        </div>
      </div>
    </div>
  );
}
