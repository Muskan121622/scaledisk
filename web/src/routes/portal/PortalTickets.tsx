// "My tickets" — the customer's whole world. Backend pins every row to the caller's cid claim,
// so this list can't show anyone else's ticket even if the query params are tampered with.

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Inbox, Plus } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useMyTickets } from '@/hooks/usePortal';
import { relativeTime } from '@/lib/utils';
import { PriorityLabel, StatusLabel, TicketStatus, type Ticket } from '@/types';

// Customer-facing status colors: waiting-on-customer glows amber (it needs THEIR reply),
// resolved glows green with the "confirm close" hint living on the detail page.
function StatusBadge({ status }: { status: Ticket['status'] }) {
  const variant =
    status === TicketStatus.Resolved ? 'success'
    : status === TicketStatus.Closed ? 'muted'
    : status === TicketStatus.WaitingForCustomer ? 'warn'
    : status === TicketStatus.Open ? 'info'
    : 'default';
  return <Badge variant={variant}>{StatusLabel[status]}</Badge>;
}

function PriorityBadge({ priority }: { priority: Ticket['priority'] }) {
  return priority >= 2
    ? <Badge variant={priority === 3 ? 'danger' : 'warn'}>{PriorityLabel[priority]}</Badge>
    : <Badge variant="muted">{PriorityLabel[priority]}</Badge>;
}

export function PortalTickets() {
  const [status, setStatus] = useState<number | null>(null);
  const [page, setPage] = useState(1);
  const { data, isLoading } = useMyTickets(status, page);

  const tickets = data?.items ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  const filters: { label: string; value: number | null }[] = [
    { label: 'All', value: null },
    { label: 'Open', value: TicketStatus.Open },
    { label: 'In progress', value: TicketStatus.InProgress },
    { label: 'Waiting on you', value: TicketStatus.WaitingForCustomer },
    { label: 'Resolved', value: TicketStatus.Resolved },
    { label: 'Closed', value: TicketStatus.Closed },
  ];

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-5">
      <div className="flex items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">My tickets</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {data ? `${data.totalCount} request${data.totalCount === 1 ? '' : 's'}` : 'Loading…'}
          </p>
        </div>
        <Button asChild className="rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500">
          <Link to="/portal/tickets/new"><Plus className="mr-1.5 h-4 w-4" /> New request</Link>
        </Button>
      </div>

      <div className="flex flex-wrap gap-1.5">
        {filters.map((f) => (
          <button
            key={f.label}
            onClick={() => { setStatus(f.value); setPage(1); }}
            className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
              status === f.value ? 'bg-violet-600 text-white' : 'bg-white text-slate-500 ring-1 ring-slate-200 hover:bg-slate-50'
            }`}
          >
            {f.label}
          </button>
        ))}
      </div>

      <div className="surface overflow-hidden rounded-2xl">
        {isLoading ? (
          <div className="space-y-3 p-5">
            {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-16 rounded-xl" />)}
          </div>
        ) : tickets.length === 0 ? (
          <div className="flex flex-col items-center gap-2 py-14 text-center">
            <Inbox className="h-8 w-8 text-violet-300" />
            <p className="text-sm font-medium text-slate-700">Nothing here yet</p>
            <p className="text-xs text-muted-foreground">File a request and the support team will pick it up.</p>
          </div>
        ) : (
          <ul className="divide-y divide-slate-100">
            {tickets.map((t) => (
              <li key={t.id}>
                <Link
                  to={`/portal/tickets/${t.id}`}
                  className="flex items-center gap-4 px-5 py-4 transition-colors hover:bg-violet-50/40"
                >
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-medium text-slate-800">{t.subject}</div>
                    <div className="mt-0.5 text-xs text-muted-foreground">
                      opened {relativeTime(t.createdAt)} · {t.id.slice(0, 8)}
                    </div>
                  </div>
                  <PriorityBadge priority={t.priority} />
                  <StatusBadge status={t.status} />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 text-sm">
          <Button size="sm" variant="outline" className="rounded-xl" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Previous
          </Button>
          <span className="text-muted-foreground">Page {page} / {totalPages}</span>
          <Button size="sm" variant="outline" className="rounded-xl" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
            Next
          </Button>
        </div>
      )}
    </motion.div>
  );
}
