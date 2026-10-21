// HQ layout: a slim navy top bar that keeps this area unmistakably separate from the
// tenant workspace (no SignalR, no workspace chrome) + the routed content below.

import { Link, NavLink, Outlet } from 'react-router-dom';
import { ShieldCheck, LogOut } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { usePlatformAuth } from '@/lib/platform-auth';

const nav = [
  { to: '/platform', label: 'Overview', end: true },
  { to: '/platform/tenants', label: 'Tenants', end: false },
];

export function PlatformShell() {
  const { email, logout } = usePlatformAuth();

  return (
    <div className="min-h-screen bg-slate-50 text-foreground">
      <header className="border-b border-white/10 bg-[#0a0d22]">
        <div className="mx-auto flex max-w-6xl items-center gap-6 px-6 py-3">
          <Link to="/platform" className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-violet-500 to-indigo-600">
              <ShieldCheck className="h-5 w-5 text-white" />
            </div>
            <div className="leading-tight">
              <div className="text-sm font-semibold text-white">ScaleDesk HQ</div>
              <div className="text-[10px] uppercase tracking-widest text-violet-300">Platform admin</div>
            </div>
          </Link>

          <nav className="flex items-center gap-1">
            {nav.map((n) => (
              <NavLink
                key={n.to}
                to={n.to}
                end={n.end}
                className={({ isActive }) =>
                  `rounded-lg px-3 py-1.5 text-sm transition-colors ${
                    isActive ? 'bg-white/10 text-white' : 'text-slate-300 hover:bg-white/5 hover:text-white'
                  }`
                }
              >
                {n.label}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-3">
            <span className="hidden text-xs text-slate-400 sm:inline">{email}</span>
            <Button
              size="sm"
              variant="ghost"
              onClick={logout}
              className="rounded-lg text-slate-300 hover:bg-white/10 hover:text-white"
            >
              <LogOut className="mr-1.5 h-3.5 w-3.5" /> Leave HQ
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 py-8">
        <Outlet />
      </main>
    </div>
  );
}
