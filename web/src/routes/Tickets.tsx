import { useState } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Plus, ChevronLeft, ChevronRight, Inbox, User } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { GradientText } from '@/components/ui/GradientText';
import { TicketRow } from '@/components/tickets/TicketRow';
import { useTickets } from '@/hooks/useTickets';
import { useAuth } from '@/lib/auth';
import { PriorityLabel, StatusLabel, TicketPriority, TicketStatus } from '@/types';
import { cn } from '@/lib/utils';

const STATUS_OPTIONS: Array<{ value?: TicketStatus; label: string }> = [
  { label: 'All' },
  { value: TicketStatus.Open, label: StatusLabel[TicketStatus.Open] },
  { value: TicketStatus.InProgress, label: StatusLabel[TicketStatus.InProgress] },
  { value: TicketStatus.WaitingForCustomer, label: StatusLabel[TicketStatus.WaitingForCustomer] },
  { value: TicketStatus.Resolved, label: StatusLabel[TicketStatus.Resolved] },
  { value: TicketStatus.Closed, label: StatusLabel[TicketStatus.Closed] },
];

const PRIORITY_OPTIONS: Array<{ value?: TicketPriority; label: string }> = [
  { label: 'Any priority' },
  { value: TicketPriority.Low, label: PriorityLabel[TicketPriority.Low] },
  { value: TicketPriority.Medium, label: PriorityLabel[TicketPriority.Medium] },
  { value: TicketPriority.High, label: PriorityLabel[TicketPriority.High] },
  { value: TicketPriority.Urgent, label: PriorityLabel[TicketPriority.Urgent] },
];

export function Tickets() {
  const { user } = useAuth();
  const [status, setStatus] = useState<TicketStatus | undefined>();
  const [priority, setPriority] = useState<TicketPriority | undefined>();
  const [mine, setMine] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data, isLoading, isFetching } = useTickets({
    status, priority, page, pageSize,
    assignee: mine && user ? user.userId : undefined,
  });

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">
            <GradientText>Tickets</GradientText>
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {data ? `${data.totalCount} total · newest first` : 'Loading workspace queue…'}
          </p>
        </div>
        <Button size="sm" asChild className="bg-gradient-to-r from-violet-600 to-indigo-500 shadow-brand hover:shadow-lg transition-all hover:scale-[1.02] rounded-xl">
          <Link to="/app/tickets/new"><Plus className="h-4 w-4" /> New ticket</Link>
        </Button>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-2">
        <FilterChips
          options={STATUS_OPTIONS.map((o) => ({ ...o, active: o.value === status }))}
          onPick={(v) => { setStatus(v); setPage(1); }}
        />
        <span className="mx-2 h-5 w-px bg-slate-200" />
        <FilterChips
          options={PRIORITY_OPTIONS.map((o) => ({ ...o, active: o.value === priority }))}
          onPick={(v) => { setPriority(v); setPage(1); }}
        />
        {user && (
          <>
            <span className="mx-2 h-5 w-px bg-slate-200" />
            <button
              onClick={() => { setMine((v) => !v); setPage(1); }}
              className={cn(
                'inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition-all duration-200 border',
                mine
                  ? 'border-violet-300 bg-violet-600 text-white shadow-brand'
                  : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50 hover:text-slate-900'
              )}
            >
              <User className="h-3 w-3" /> My tickets
            </button>
          </>
        )}
      </div>

      {/* Table */}
      <div className={cn('surface rounded-2xl overflow-hidden transition-opacity', isFetching && !isLoading && 'opacity-60')}>
        <div className="grid grid-cols-12 items-center gap-3 border-b border-slate-200 bg-slate-50/60 px-5 py-3 text-xs uppercase tracking-wider text-slate-500 font-medium">
          <div className="col-span-5">Subject</div>
          <div className="col-span-2">Priority</div>
          <div className="col-span-2">Status</div>
          <div className="col-span-2">Created</div>
          <div className="col-span-1 text-right">SLA</div>
        </div>

        {isLoading && (
          <div className="divide-y divide-slate-100">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="flex items-center gap-3 px-5 py-4">
                <Skeleton className="h-4 flex-1" />
                <Skeleton className="h-4 w-16" />
                <Skeleton className="h-4 w-20" />
                <Skeleton className="h-4 w-14" />
              </div>
            ))}
          </div>
        )}

        {data && data.items.length === 0 && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="flex flex-col items-center gap-3 py-20 text-center"
          >
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-violet-50 border border-violet-200">
              <Inbox className="h-8 w-8 text-violet-600" />
            </div>
            <p className="text-sm text-muted-foreground">No tickets match the current filters.</p>
          </motion.div>
        )}

        {data?.items.map((t, i) => (
          <motion.div
            key={t.id}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.03 }}
          >
            <TicketRow ticket={t} />
          </motion.div>
        ))}
      </div>

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-end gap-3 text-sm">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="rounded-xl">
            <ChevronLeft className="h-3.5 w-3.5" /> Prev
          </Button>
          <span className="text-muted-foreground text-xs">Page {data.page} / {data.totalPages}</span>
          <Button variant="outline" size="sm" disabled={!data.hasNextPage} onClick={() => setPage((p) => p + 1)} className="rounded-xl">
            Next <ChevronRight className="h-3.5 w-3.5" />
          </Button>
        </div>
      )}
    </div>
  );
}

function FilterChips<T extends number>({
  options, onPick,
}: { options: Array<{ value?: T; label: string; active: boolean }>; onPick: (v: T | undefined) => void }) {
  return (
    <div className="inline-flex flex-wrap gap-1.5">
      {options.map((o) => (
        <button
          key={o.label}
          onClick={() => onPick(o.value)}
          className={cn(
            'rounded-lg px-3 py-1.5 text-xs font-medium transition-all duration-200 border',
            o.active
              ? 'border-violet-200 bg-violet-50 text-violet-700'
              : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50 hover:text-slate-900'
          )}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}
