// Analytics: reads the same /api/dashboard/stats projection as the Overview, with a
// selectable lookback window — status distribution, SLA health and the daily volume trend.

import { useState } from 'react';
import { motion } from 'framer-motion';
import { BarChart3, TrendingUp } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Sparkline } from '@/components/dashboard/Sparkline';
import { GradientText } from '@/components/ui/GradientText';
import { useDashboardStats } from '@/hooks/useDashboardStats';

const WINDOWS = [7, 14, 30] as const;

export function Analytics() {
  const [days, setDays] = useState<(typeof WINDOWS)[number]>(14);
  const { data: stats, isLoading } = useDashboardStats(days);

  const total = stats ? stats.open + stats.inProgress + stats.waiting + stats.resolved + stats.closed : 0;

  const distribution = stats
    ? [
        { label: 'Open', value: stats.open, cls: 'bg-violet-500' },
        { label: 'In progress', value: stats.inProgress, cls: 'bg-indigo-500' },
        { label: 'Waiting', value: stats.waiting, cls: 'bg-cyan-500' },
        { label: 'Resolved', value: stats.resolved, cls: 'bg-emerald-500' },
        { label: 'Closed', value: stats.closed, cls: 'bg-slate-400' },
      ]
    : [];

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">Analytics</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">Aggregated live from the ticket read-model</p>
        </div>
        <div className="flex gap-1 rounded-xl border border-slate-200 bg-white p-1">
          {WINDOWS.map((w) => (
            <button
              key={w}
              onClick={() => setDays(w)}
              className={`rounded-lg px-3 py-1.5 text-xs font-medium transition-colors ${
                days === w ? 'bg-violet-600 text-white shadow-sm' : 'text-slate-500 hover:bg-violet-50'
              }`}
            >
              {w}d
            </button>
          ))}
        </div>
      </div>

      {isLoading || !stats ? (
        <div className="grid gap-5 md:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-32 rounded-2xl" />)}
        </div>
      ) : (
        <>
          <div className="grid gap-5 md:grid-cols-3">
            <div className="surface rounded-2xl p-5">
              <p className="text-xs uppercase tracking-wider text-muted-foreground">Tickets in window</p>
              <p className="mt-2 text-3xl font-bold"><GradientText>{total}</GradientText></p>
              <p className="mt-1 text-xs text-muted-foreground">all statuses, this workspace</p>
            </div>
            <div className="surface rounded-2xl p-5">
              <p className="text-xs uppercase tracking-wider text-muted-foreground">SLA success rate</p>
              <p className="mt-2 text-3xl font-bold text-emerald-600">{(stats.slaSuccessRate * 100).toFixed(1)}%</p>
              <p className="mt-1 text-xs text-muted-foreground">resolved within response deadline</p>
            </div>
            <div className="surface rounded-2xl p-5">
              <p className="text-xs uppercase tracking-wider text-muted-foreground">Unresolved</p>
              <p className="mt-2 text-3xl font-bold text-slate-800">{stats.open + stats.inProgress + stats.waiting}</p>
              <p className="mt-1 text-xs text-muted-foreground">{stats.unreadNotifications} unread notifications</p>
            </div>
          </div>

          <div className="surface rounded-2xl p-6">
            <div className="mb-4 flex items-center gap-2">
              <TrendingUp className="h-4 w-4 text-violet-600" />
              <h2 className="text-sm font-semibold text-slate-900">Daily new tickets · last {days} days</h2>
            </div>
            <Sparkline points={stats.ticketsPerDay} />
          </div>

          <div className="surface rounded-2xl p-6">
            <div className="mb-4 flex items-center gap-2">
              <BarChart3 className="h-4 w-4 text-violet-600" />
              <h2 className="text-sm font-semibold text-slate-900">Status distribution</h2>
            </div>
            <div className="flex h-3 w-full overflow-hidden rounded-full bg-slate-100">
              {distribution.map((d) => (
                <div key={d.label} className={d.cls} style={{ width: total ? `${(d.value / total) * 100}%` : 0 }} />
              ))}
            </div>
            <div className="mt-4 flex flex-wrap gap-2">
              {distribution.map((d) => (
                <Badge key={d.label} variant="muted">
                  <span className={`mr-1.5 inline-block h-2 w-2 rounded-full ${d.cls}`} />
                  {d.label} · {d.value}
                </Badge>
              ))}
            </div>
          </div>
        </>
      )}
    </motion.div>
  );
}
