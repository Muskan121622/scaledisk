// Portal layout: light top bar with the customer's email + sign-out. Deliberately NOT the staff
// AppShell (no workspace switcher, no notifications, no SignalR) — customers see only their own
// tickets. The J3 demo pairs this against an /app tab in a second browser window.

import { Link, NavLink, Outlet } from 'react-router-dom';
import { LifeBuoy, LogOut, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { usePortalAuth } from '@/lib/portal-auth';

const nav = [
  { to: '/portal/tickets', label: 'My tickets', end: false },
];

export function PortalShell() {
  const { email, slug, logout } = usePortalAuth();

  return (
    <div className="min-h-screen bg-slate-50 text-foreground">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-4xl items-center gap-6 px-6 py-3">
          <Link to="/portal/tickets" className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-violet-600 to-indigo-500">
              <LifeBuoy className="h-5 w-5 text-white" />
            </div>
            <div className="leading-tight">
              <div className="text-sm font-semibold text-slate-900">Support Portal</div>
              <div className="text-[10px] uppercase tracking-widest text-violet-500">{slug ?? 'workspace'}</div>
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
                    isActive ? 'bg-violet-50 text-violet-700' : 'text-slate-500 hover:bg-slate-50 hover:text-slate-800'
                  }`
                }
              >
                {n.label}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-3">
            <Button asChild size="sm" variant="outline" className="rounded-xl border-violet-200 text-violet-700 hover:bg-violet-50">
              <Link to="/portal/tickets/new"><Plus className="mr-1.5 h-3.5 w-3.5" /> New request</Link>
            </Button>
            <span className="hidden text-xs text-slate-400 sm:inline">{email}</span>
            <Button size="sm" variant="ghost" onClick={() => void logout()} className="rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-800">
              <LogOut className="mr-1.5 h-3.5 w-3.5" /> Sign out
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-4xl px-6 py-8">
        <Outlet />
      </main>
    </div>
  );
}
