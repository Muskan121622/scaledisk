// Ticket conversation view. One thread, two sides: agent replies (left) vs the customer's own
// messages (right). Internal staff notes never reach this endpoint (filtered server-side), and
// "Close ticket" appears only when the agent marked it Resolved — the customer confirms closure.

import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowLeft, CheckCircle2, LifeBuoy, Send, X } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { ApiError } from '@/lib/api';
import { relativeTime } from '@/lib/utils';
import { useCloseTicket, useMyTicket, useMyTicketComments, useReplyTicket } from '@/hooks/usePortal';
import { CategoryLabel, PriorityLabel, StatusLabel, TicketStatus, type TicketComment } from '@/types';

function CommentRow({ c }: { c: TicketComment }) {
  const mine = c.customerId !== null; // cid-attributed => the visitor's own message
  return (
    <div className={`flex ${mine ? 'justify-end' : 'justify-start'}`}>
      <div
        className={`max-w-[80%] rounded-2xl px-4 py-2.5 text-sm shadow-sm ${
          mine
            ? 'rounded-br-md bg-gradient-to-r from-violet-600 to-indigo-500 text-white'
            : 'rounded-bl-md border border-slate-200 bg-white text-slate-700'
        }`}
      >
        <div className={`mb-1 text-[10px] font-medium uppercase tracking-wider ${mine ? 'text-violet-200' : 'text-slate-400'}`}>
          {mine ? 'You' : 'Support team'} · {relativeTime(c.createdAt)}
        </div>
        <p className="whitespace-pre-wrap leading-relaxed">{c.body}</p>
      </div>
    </div>
  );
}

export function PortalTicketDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: ticket, isLoading, error } = useMyTicket(id);
  const { data: comments, isLoading: commentsLoading } = useMyTicketComments(id);
  const reply = useReplyTicket(id!);
  const close = useCloseTicket(id!);
  const [body, setBody] = useState('');
  const [note, setNote] = useState<string | null>(null);

  // A 404 here means "not yours / not here" — show the same answer the API gave, no leak.
  if (error) {
    const status = error instanceof ApiError ? error.status : 0;
    return (
      <div className="surface mx-auto max-w-md rounded-2xl p-8 text-center">
        <X className="mx-auto h-8 w-8 text-rose-300" />
        <p className="mt-2 text-sm font-medium text-slate-700">
          {status === 404 ? 'This ticket does not exist in your workspace.' : 'Could not load the ticket.'}
        </p>
        <Button variant="outline" className="mt-4 rounded-xl" onClick={() => navigate('/portal/tickets')}>
          Back to my tickets
        </Button>
      </div>
    );
  }

  if (isLoading || !ticket) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-64 rounded-lg" />
        <Skeleton className="h-24 rounded-2xl" />
        <Skeleton className="h-64 rounded-2xl" />
      </div>
    );
  }

  const resolved = ticket.status === TicketStatus.Resolved;
  const closed = ticket.status === TicketStatus.Closed;

  async function send(e: React.FormEvent) {
    e.preventDefault();
    setNote(null);
    if (!body.trim()) return;
    try {
      await reply.mutateAsync(body.trim());
      setBody('');
    } catch {
      setNote('Could not send the reply — is your session still active?');
    }
  }

  async function confirmClose() {
    setNote(null);
    try {
      await close.mutateAsync();
    } catch (err) {
      setNote(err instanceof ApiError ? (err.body?.title ?? 'Could not close the ticket.') : 'Could not close the ticket.');
    }
  }

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-5">
      <button onClick={() => navigate('/portal/tickets')} className="inline-flex items-center gap-1 text-xs text-slate-400 hover:text-slate-600">
        <ArrowLeft className="h-3 w-3" /> Back to my tickets
      </button>

      <div className="surface rounded-2xl p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-xl font-bold tracking-tight text-slate-900">{ticket.subject}</h1>
            <p className="mt-1 text-xs text-muted-foreground">
              #{ticket.id.slice(0, 8)} · opened {relativeTime(ticket.createdAt)} · {CategoryLabel[ticket.category]}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Badge variant={ticket.priority >= 2 ? (ticket.priority === 3 ? 'danger' : 'warn') : 'muted'}>
              {PriorityLabel[ticket.priority]}
            </Badge>
            <Badge
              variant={
                closed ? 'muted'
                : resolved ? 'success'
                : ticket.status === TicketStatus.WaitingForCustomer ? 'warn'
                : ticket.status === TicketStatus.Open ? 'info'
                : 'default'
              }
            >
              {StatusLabel[ticket.status]}
            </Badge>
          </div>
        </div>
        <p className="mt-4 whitespace-pre-wrap border-t border-slate-100 pt-4 text-sm leading-relaxed text-slate-600">
          {ticket.description}
        </p>
      </div>

      {resolved && (
        <div className="flex items-center justify-between gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 px-5 py-4">
          <div className="flex items-center gap-3 text-sm text-emerald-800">
            <CheckCircle2 className="h-5 w-5" />
            <span>The team marked this resolved. If that fixes it, close the ticket.</span>
          </div>
          <Button size="sm" onClick={() => void confirmClose()} disabled={close.isPending}
            className="shrink-0 rounded-xl bg-emerald-600 text-white hover:bg-emerald-700">
            {close.isPending ? 'Closing…' : 'Close ticket'}
          </Button>
        </div>
      )}
      {closed && (
        <div className="flex items-center gap-2 rounded-2xl bg-slate-100 px-5 py-3 text-sm text-slate-500">
          <LifeBuoy className="h-4 w-4" /> This ticket is closed. File a new request if it comes back.
        </div>
      )}

      <div className="space-y-3">
        <h2 className="text-sm font-semibold text-slate-700">Conversation</h2>
        {commentsLoading ? (
          <Skeleton className="h-40 rounded-2xl" />
        ) : (comments ?? []).length === 0 ? (
          <p className="rounded-2xl border border-dashed border-slate-200 p-6 text-center text-xs text-slate-400">
            No replies yet — drop one below.
          </p>
        ) : (
          (comments ?? []).map((c) => <CommentRow key={c.id} c={c} />)
        )}
      </div>

      {!closed && (
        <form onSubmit={send} className="surface flex items-end gap-2 rounded-2xl p-3">
          <textarea
            value={body}
            onChange={(e) => setBody(e.target.value)}
            rows={2}
            placeholder={resolved ? 'Reply or confirm the fix…' : 'Write a reply…'}
            className="flex-1 resize-none rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none ring-violet-200 focus:ring-2"
          />
          <Button type="submit" size="sm" disabled={reply.isPending || !body.trim()}
            className="mb-0.5 h-9 rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500 px-4">
            <Send className="mr-1.5 h-3.5 w-3.5" /> {reply.isPending ? 'Sending…' : 'Send'}
          </Button>
        </form>
      )}

      {note && <p className="text-center text-xs text-rose-600">{note}</p>}
    </motion.div>
  );
}
