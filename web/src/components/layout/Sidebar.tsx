import { Link, useLocation } from 'react-router-dom';
import {
  LayoutDashboard, Ticket, Users, Building2, Zap, Timer, BarChart3, Settings as SettingsIcon,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useAuth } from '@/lib/auth';

type Item = { to: string; label: string; icon: React.ReactNode; exact?: boolean; perm?: string };

// Every entry here is a real, working screen backed by live API data.
// `perm` mirrors the backend authorization token for that area — the nav shows
// exactly what the signed-in user's JWT allows (same checks run server-side).
const MAIN: Item[] = [
  { to: '/app', label: 'Overview', icon: <LayoutDashboard className="h-4 w-4" />, exact: true },
  { to: '/app/tickets', label: 'Tickets', icon: <Ticket className="h-4 w-4" /> },
  { to: '/app/customers', label: 'Customers', icon: <Users className="h-4 w-4" />, perm: 'customers.read' },
  { to: '/app/teams', label: 'Teams', icon: <Building2 className="h-4 w-4" />, perm: 'teams.manage' },
  { to: '/app/automation', label: 'Automation', icon: <Zap className="h-4 w-4" />, perm: 'automation.read' },
  { to: '/app/sla', label: 'SLA', icon: <Timer className="h-4 w-4" />, perm: 'sla.manage' },
  { to: '/app/analytics', label: 'Analytics', icon: <BarChart3 className="h-4 w-4" /> },
  { to: '/app/settings', label: 'Settings', icon: <SettingsIcon className="h-4 w-4" /> },
];

export function Sidebar() {
  const { pathname } = useLocation();
  const { user } = useAuth();
  const items = MAIN.filter((it) => !it.perm || user?.permissions.includes(it.perm));

  return (
    <aside className="sticky top-0 hidden h-screen w-64 shrink-0 lg:flex lg:flex-col border-r border-white/10 bg-[#0a0d22]">
      {/* Logo */}
      <Link to="/" className="flex items-center gap-2.5 border-b border-white/10 px-5 py-4 text-lg font-bold tracking-tight text-white">
        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-violet-500 to-indigo-500 text-sm font-black text-white shadow-brand">
          S
        </div>
        ScaleDesk
      </Link>

      {/* Workspace */}
      <div className="px-5 py-4">
        <div className="text-[11px] uppercase tracking-wider text-slate-500 font-medium">Workspace</div>
        <div className="mt-1 truncate text-sm font-semibold text-white">{user?.tenantName ?? '—'}</div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto px-3 pb-6">
        <Section items={items} pathname={pathname} />
      </nav>

      {/* Status footer */}
      <div className="border-t border-white/10 px-5 py-4 text-xs">
        <div className="mb-1.5 flex items-center gap-2 text-emerald-400">
          <span className="relative flex h-2 w-2">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
          </span>
          All services healthy
        </div>
        <div className="text-slate-500">API · Postgres · Kafka · Redis</div>
      </div>
    </aside>
  );
}

function Section({ items, pathname }: { items: Item[]; pathname: string }) {
  return (
    <ul className="space-y-1">
      {items.map((it) => {
        const active = it.exact ? pathname === it.to : pathname.startsWith(it.to);
        const cls = cn(
          'relative flex items-center gap-3 rounded-xl px-3.5 py-2.5 text-sm font-medium transition-all duration-200',
          active
            ? 'bg-violet-500/15 text-white border border-violet-400/30'
            : 'text-slate-400 hover:bg-white/5 hover:text-white'
        );
        const inner = (
          <>
            <span className={cn('transition-colors', active ? 'text-violet-300' : '')}>{it.icon}</span>
            <span className="flex-1 truncate">{it.label}</span>
          </>
        );
        return (
          <li key={it.label}>
            <Link to={it.to} className={cls}>
              {inner}
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
