import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/label-badge';
import { TiltCard } from '@/components/ui/TiltCard';
import { GradientText } from '@/components/ui/GradientText';
import { Sparkline } from '@/components/dashboard/Sparkline';
import { useDashboardStats } from '@/hooks/useDashboardStats';
import { useAuth } from '@/lib/auth';
import { api } from '@/lib/api';
import type { Paged, Ticket } from '@/types';
import { PriorityLabel } from '@/types';
import { relativeTime } from '@/lib/utils';
import { Inbox, Clock, CheckCircle2, Shield } from 'lucide-react';

export function Dashboard() {
  const { user } = useAuth();
  // Agents/viewers get a personal queue; admins/managers see the whole workspace feed.
  const isAgentLike = !!user && !user.permissions.includes('teams.manage');
  const { data: stats, isLoading } = useDashboardStats(14);
  const { data: recent } = useQuery({
    queryKey: ['tickets', { page: 1, pageSize: 5, assignee: isAgentLike ? user?.userId : undefined }],
    queryFn: () => api.get<Paged<Ticket>>('/tickets', {
      page: 1, pageSize: 5, ...(isAgentLike ? { assignee: user!.userId } : {}),
    }),
    enabled: !!user,
  });

  if (isLoading || !stats) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <div className="grid grid-cols-2 gap-5 md:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-36 rounded-2xl" />)}
        </div>
        <Skeleton className="h-48 rounded-2xl" />
      </div>
    );
  }

  const totalOpen = stats.open + stats.inProgress + stats.waiting;

  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold tracking-tight">
          <GradientText>Support overview</GradientText>
        </h1>
        <p className="mt-1.5 text-sm text-muted-foreground">
          {isAgentLike
            ? `Signed in as ${user?.role} — showing your assigned queue first.`
            : 'A live view of the workspace, backed by one aggregation endpoint.'}
        </p>
      </div>

      {/* KPI Cards with 3D Tilt */}
      <div className="grid gap-5 md:grid-cols-4 perspective-1000">
        {[
          { label: 'Open Tickets', value: totalOpen, hint: `${stats.inProgress} in progress`, icon: <Inbox className="h-5 w-5" />, color: 'from-violet-100 to-indigo-50', iconColor: 'text-violet-600' },
          { label: 'Waiting', value: stats.waiting, hint: 'customer + internal', icon: <Clock className="h-5 w-5" />, color: 'from-amber-100 to-orange-50', iconColor: 'text-amber-600' },
          { label: 'Resolved', value: stats.resolved + stats.closed, hint: 'last 30 days', icon: <CheckCircle2 className="h-5 w-5" />, color: 'from-emerald-100 to-green-50', iconColor: 'text-emerald-600' },
          { label: 'SLA Success', value: `${(stats.slaSuccessRate * 100).toFixed(1)}%`, hint: 'resolution on time', icon: <Shield className="h-5 w-5" />, color: 'from-cyan-100 to-sky-50', iconColor: 'text-cyan-600' },
        ].map((kpi, i) => (
          <motion.div
            key={i}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.08 }}
          >
            <TiltCard maxTilt={4} glare={false} scale={1.01} className="h-full">
              <div className="flex items-start justify-between">
                <div>
                  <div className="text-[11px] uppercase tracking-wider text-muted-foreground font-medium">{kpi.label}</div>
                  <div className="mt-2 text-3xl font-black text-slate-900">{kpi.value}</div>
                  {kpi.hint && <div className="mt-1 text-xs text-muted-foreground">{kpi.hint}</div>}
                </div>
                <div className={`flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br ${kpi.color} ${kpi.iconColor} border border-slate-200/60`}>
                  {kpi.icon}
                </div>
              </div>
            </TiltCard>
          </motion.div>
        ))}
      </div>

      {/* Chart + Recent */}
      <div className="grid gap-5 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <TiltCard maxTilt={2} glare={false} scale={1} className="h-full">
            <h3 className="mb-4 text-sm font-semibold text-slate-900">Ticket volume · last 14 days</h3>
            <Sparkline points={stats.ticketsPerDay} />
            <div className="mt-3 flex items-center justify-between text-xs text-muted-foreground">
              <span>{stats.ticketsPerDay[0]?.date}</span>
              <span className="font-medium text-slate-700">{totalOpen} currently in queue</span>
              <span>{stats.ticketsPerDay.at(-1)?.date}</span>
            </div>
          </TiltCard>
        </div>

        <TiltCard maxTilt={3} glare={false} scale={1}>
          <h3 className="mb-4 text-sm font-semibold text-slate-900">{isAgentLike ? 'My queue · assigned to you' : 'Recent tickets'}</h3>
          <div className="space-y-2.5">
            {!recent && <Skeleton className="h-16 rounded-xl" />}
            {recent?.items.length === 0 && (
              <p className="text-xs text-muted-foreground py-4">Nothing assigned to you — check the Tickets page for unassigned work.</p>
            )}
            {recent?.items.map((t) => (
              <Link
                key={t.id}
                to={`/app/tickets/${t.id}`}
                className="flex items-center justify-between rounded-xl border border-slate-200 bg-white p-3 text-sm transition-all hover:bg-violet-50/60 hover:border-violet-200 hover:shadow-md hover:shadow-violet-500/10"
              >
                <div className="min-w-0">
                  <div className="truncate font-medium text-slate-900">{t.subject}</div>
                  <div className="mt-0.5 text-xs text-muted-foreground">{relativeTime(t.createdAt)}</div>
                </div>
                <Badge variant={t.priority >= 2 ? 'warn' : 'muted'} className="shrink-0 ml-2">
                  {PriorityLabel[t.priority]}
                </Badge>
              </Link>
            ))}
          </div>
        </TiltCard>
      </div>
    </div>
  );
}
