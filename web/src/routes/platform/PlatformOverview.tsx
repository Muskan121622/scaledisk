// HQ overview: platform-wide counters straight from /api/platform/stats —
// the "how big is the fleet" screen. No mocks; skeleton while loading.

import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Building2, Users, Ticket, CheckCircle2, PauseCircle, Inbox } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { usePlatformStats } from '@/hooks/usePlatform';

function StatCard({ icon, label, value, tone }: {
  icon: React.ReactNode; label: string; value: number | undefined; tone: string;
}) {
  return (
    <div className="surface rounded-2xl p-5">
      <div className="flex items-center gap-3">
        <div className={`flex h-10 w-10 items-center justify-center rounded-xl ${tone}`}>{icon}</div>
        <div>
          <div className="text-2xl font-bold tracking-tight text-slate-900">
            {value === undefined ? <Skeleton className="h-7 w-12" /> : value.toLocaleString()}
          </div>
          <div className="text-xs uppercase tracking-wider text-muted-foreground">{label}</div>
        </div>
      </div>
    </div>
  );
}

export function PlatformOverview() {
  const { data } = usePlatformStats();

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">Platform overview</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Counters across every tenant. <Link to="/platform/tenants" className="text-primary hover:underline">Manage tenants →</Link>
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard icon={<Building2 className="h-5 w-5 text-indigo-600" />} label="Total tenants" value={data?.totalTenants} tone="bg-indigo-50" />
        <StatCard icon={<CheckCircle2 className="h-5 w-5 text-emerald-600" />} label="Active" value={data?.activeTenants} tone="bg-emerald-50" />
        <StatCard icon={<PauseCircle className="h-5 w-5 text-rose-600" />} label="Suspended" value={data?.suspendedTenants} tone="bg-rose-50" />
        <StatCard icon={<Users className="h-5 w-5 text-violet-600" />} label="Users (all tenants)" value={data?.totalUsers} tone="bg-violet-50" />
        <StatCard icon={<Ticket className="h-5 w-5 text-sky-600" />} label="Tickets (all time)" value={data?.totalTickets} tone="bg-sky-50" />
        <StatCard icon={<Inbox className="h-5 w-5 text-amber-600" />} label="Open tickets" value={data?.openTickets} tone="bg-amber-50" />
      </div>
    </motion.div>
  );
}
