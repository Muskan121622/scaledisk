import { useEffect } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { motion } from 'framer-motion';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/label-badge';
import { GradientText } from '@/components/ui/GradientText';
import { CommentThread } from '@/components/tickets/CommentThread';
import { ActivityTimeline } from '@/components/tickets/ActivityTimeline';
import { ReplyBox } from '@/components/tickets/ReplyBox';
import { SlaCountdown } from '@/components/tickets/SlaCountdown';
import { useTicket, useTicketComments, useTicketEvents, useUpdateTicketStatus } from '@/hooks/useTicket';
import { useAddComment } from '@/hooks/useAddComment';
import { useCustomers } from '@/hooks/useWorkspace';
import { on } from '@/lib/signalr';
import { useAuth } from '@/lib/auth';
import { CategoryLabel, PriorityLabel, StatusLabel, TicketStatus, type Ticket } from '@/types';
import { shortDateTime } from '@/lib/utils';

export function TicketDetail() {
  const { id = '' } = useParams();
  const qc = useQueryClient();
  const { user, hasPermission } = useAuth();
  const { data: ticket, isLoading } = useTicket(id);
  const { data: comments, isLoading: commentsLoading } = useTicketComments(id);
  const { data: events, isLoading: eventsLoading } = useTicketEvents(id);
  const addComment = useAddComment(id);
  const updateStatus = useUpdateTicketStatus(id);
  // Name lookup for the meta panel (tenant's customer directory; falls back to the short id).
  const { data: customers } = useCustomers('');

  // Live updates. The backend broadcasts a compact `ticketChanged` signal to every connection
  // in `tenant:{id}` when any ticket mutates — status, assignment, or a new comment. When the
  // signal matches the ticket we're viewing we invalidate both queries; the second tab therefore
  // sees the reply within a couple of hundred milliseconds without a manual refresh.
  useEffect(() => {
    if (!id) return;
    const off = on('ticketChanged', (...args: unknown[]) => {
      const payload = args[0] as { ticketId?: string } | undefined;
      if (payload?.ticketId === id) {
        qc.invalidateQueries({ queryKey: ['tickets', id] });
        qc.invalidateQueries({ queryKey: ['tickets', id, 'comments'] });
        qc.invalidateQueries({ queryKey: ['tickets', id, 'events'] });
      }
    });
    return off;
  }, [id, qc]);

  if (isLoading || !ticket) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-64 rounded-xl" />
        <Skeleton className="h-32 rounded-2xl" />
        <Skeleton className="h-64 rounded-2xl" />
      </div>
    );
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      className="space-y-5"
    >
      <Link
        to="/app/tickets"
        className="inline-flex items-center gap-1 text-xs text-muted-foreground transition-colors hover:text-violet-700"
      >
        <ArrowLeft className="h-3 w-3" /> All tickets
      </Link>

      {/* Header panel */}
      <div className="surface rounded-2xl p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="text-xl font-bold tracking-tight">
              <GradientText>{ticket.subject}</GradientText>
            </h1>
            <div className="mt-1 text-xs text-muted-foreground">
              #{ticket.id.slice(0, 8)} · opened {shortDateTime(ticket.createdAt)}
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <Badge variant={ticket.priority >= 2 ? 'warn' : 'muted'}>{PriorityLabel[ticket.priority]}</Badge>
            <Badge variant={ticket.status <= 1 ? 'info' : ticket.status >= 4 ? 'success' : 'muted'}>
              {StatusLabel[ticket.status]}
            </Badge>
            {/* Same PATCH the J3 close-handshake relies on: agents move the ticket, the customer confirms. */}
            {hasPermission('tickets.update') && (
              <select
                value={ticket.status}
                disabled={updateStatus.isPending}
                onChange={(e) => updateStatus.mutate(Number(e.target.value) as Ticket['status'])}
                className="h-7 cursor-pointer rounded-lg border border-slate-200 bg-white px-2 text-xs font-medium text-slate-700 outline-none transition-colors hover:border-violet-300 focus:ring-2 focus:ring-violet-200 disabled:opacity-50"
                title="Change status"
              >
                {Object.entries(StatusLabel).map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            )}
            <SlaCountdown dueAt={ticket.responseDueAt} breached={ticket.slaBreached} />
          </div>
        </div>

        {updateStatus.isError && (
          <p className="mt-2 text-xs text-destructive">Could not change the status — try again.</p>
        )}

        <div className="mt-5 grid gap-3 text-xs md:grid-cols-4">
          <Meta label="Category" value={CategoryLabel[ticket.category]} />
          <Meta label="Assignee" value={ticket.assigneeUserId ? ticket.assigneeUserId.slice(0, 8) : 'Unassigned'} />
          <Meta label="Customer" value={customers?.items.find((c) => c.id === ticket.customerId)?.name ?? ticket.customerId.slice(0, 8)} />
          <Meta label="Resolution SLA" value={ticket.resolutionDueAt ? shortDateTime(ticket.resolutionDueAt) : '—'} />
        </div>

        <p className="mt-5 whitespace-pre-wrap rounded-xl border border-slate-200 bg-slate-50/70 p-4 text-sm leading-relaxed text-slate-700">
          {ticket.description}
        </p>
      </div>

      {/* Conversation + live activity rail */}
      <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,1fr)_320px]">
        <div className="surface rounded-2xl p-6">
          <h2 className="mb-4 text-sm font-semibold text-slate-900">Conversation</h2>
          <div className="space-y-4">
            {commentsLoading ? (
              <Skeleton className="h-24 rounded-xl" />
            ) : (
              <CommentThread comments={comments ?? []} currentUserId={user?.userId} />
            )}
            <ReplyBox onSubmit={async (body, isInternal) => { await addComment.mutateAsync({ body, isInternal }); }} />
            <div className="flex items-center justify-end gap-2 text-xs text-muted-foreground">
              {addComment.isError && <span className="text-destructive">Could not post reply.</span>}
              <Button
                size="sm"
                variant="outline"
                onClick={() => qc.invalidateQueries({ queryKey: ['tickets', id] })}
                className="rounded-xl"
              >
                Refresh
              </Button>
            </div>
          </div>
        </div>

        {/* Activity rail — the backend's append-only event stream, pushed in live over SignalR. */}
        <aside className="surface rounded-2xl p-5 xl:sticky xl:top-6">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-sm font-semibold text-slate-900">Activity</h2>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[10px] font-medium text-emerald-700">
              <span className="relative flex h-1.5 w-1.5">
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
                <span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-emerald-500" />
              </span>
              Live
            </span>
          </div>
          {eventsLoading ? (
            <div className="space-y-4">
              <Skeleton className="h-10 rounded-xl" />
              <Skeleton className="h-10 rounded-xl" />
              <Skeleton className="h-10 rounded-xl" />
            </div>
          ) : (
            <ActivityTimeline events={events ?? []} />
          )}
        </aside>
      </div>
    </motion.div>
  );
}

function Meta({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3">
      <div className="text-muted-foreground">{label}</div>
      <div className="mt-0.5 truncate text-foreground">{value}</div>
      {hint && <div className="text-[10px] text-muted-foreground">{hint}</div>}
    </div>
  );
}
