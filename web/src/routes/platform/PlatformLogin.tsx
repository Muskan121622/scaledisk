// ScaleDesk HQ sign-in. Visually split from the staff login (navy panel, shield icon):
// same product, different world. Credentials prefilled for the demo, same as /login.

import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { ArrowLeft, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label-badge';
import { ApiError } from '@/lib/api';
import { usePlatformAuth } from '@/lib/platform-auth';

export function PlatformLogin() {
  const { status, login } = usePlatformAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('platform@scaledesk.app');
  const [password, setPassword] = useState('DemoPass2026!');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (status === 'authenticated') return <Navigate to="/platform" replace />;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await login(email, password);
      navigate('/platform', { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? (err.body?.title ?? 'Invalid credentials.') : 'Unable to reach the server.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-[#0a0d22] px-4">
      <div className="w-full max-w-md">
        <Link to="/" className="mb-6 inline-flex items-center gap-1 text-xs text-slate-400 hover:text-white">
          <ArrowLeft className="h-3 w-3" /> Back to home
        </Link>

        <div className="rounded-2xl border border-white/10 bg-white/[0.04] p-8 shadow-2xl backdrop-blur">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-indigo-600">
              <ShieldCheck className="h-6 w-6 text-white" />
            </div>
            <div>
              <h1 className="text-xl font-semibold tracking-tight text-white">ScaleDesk HQ</h1>
              <p className="text-xs text-slate-400">Platform administration — authorized staff only</p>
            </div>
          </div>

          <form className="mt-7 space-y-4" onSubmit={submit}>
            <div>
              <Label htmlFor="email" className="text-slate-300">Email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="mt-1.5 rounded-xl border-white/15 bg-white/5 text-white placeholder:text-slate-500"
              />
            </div>
            <div>
              <Label htmlFor="password" className="text-slate-300">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="mt-1.5 rounded-xl border-white/15 bg-white/5 text-white placeholder:text-slate-500"
              />
            </div>

            {error && (
              <div className="rounded-md border border-rose-400/40 bg-rose-500/10 px-3 py-2 text-sm text-rose-200">
                {error}
              </div>
            )}

            <Button type="submit" className="w-full rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500" disabled={busy}>
              {busy ? 'Verifying…' : 'Enter HQ'}
            </Button>
          </form>

          <div className="mt-6 text-center text-xs text-slate-500">
            Workspace member? <Link to="/login" className="text-violet-300 hover:underline">Sign in to your tenant</Link>
          </div>
        </div>
      </div>
    </div>
  );
}
