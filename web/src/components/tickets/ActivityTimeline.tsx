// Activity rail for the ticket workspace: renders the append-only `ticket_events` timeline the
// backend writes on every mutation (created / assigned / status / comment / SLA breach). Entries
// are keyed by id inside AnimatePresence, so when the SignalR `ticketChanged` broadcast refetches
// the list, only genuinely-new events mount — and those are exactly the ones that animate in.

import { useEffect, useRef } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import {
  AlertTriangle,
  ArrowRightLeft,
  CheckCircle2,
  FilePlus2,
  MessageSquare,
  Sparkles,
  UserCheck,
} from 'lucide-react';
import type { TicketActivity } from '@/types';

type EventStyle = {
  icon: typeof Sparkles;
  dot: string;
  label: string;
};

const styles: Record<string, EventStyle> = {
  'ticket.created': { icon: FilePlus2, dot: 'border-violet-200 bg-violet-50 text-violet-600', label: 'Created' },
  'ticket.assigned': { icon: UserCheck, dot: 'border-indigo-200 bg-indigo-50 text-indigo-600', label: 'Assignment' },
  'ticket.status_changed': { icon: ArrowRightLeft, dot: 'border-cyan-200 bg-cyan-50 text-cyan-600', label: 'Status' },
  'ticket.commented': { icon: MessageSquare, dot: 'border-purple-200 bg-purple-50 text-purple-600', label: 'Reply' },
  'ticket.resolved': { icon: CheckCircle2, dot: 'border-emerald-200 bg-emerald-50 text-emerald-600', label: 'Resolved' },
  'sla.breached': { icon: AlertTriangle, dot: 'border-rose-200 bg-rose-50 text-rose-600', label: 'SLA breach' },
};

const fallback: EventStyle = { icon: Sparkles, dot: 'border-slate-200 bg-slate-50 text-slate-500', label: 'Event' };

// Summaries embed raw user ids until the 5F.2 users slice gives them names — shorten in place.
const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;
const humanize = (summary: string) => summary.replace(GUID, (m) => m.slice(0, 8));

const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
function timeAgo(iso: string): string {
  const diff = (Date.now() - new Date(iso).getTime()) / 1000;
  if (diff < 60) return rtf.format(-Math.round(diff), 'second');
  if (diff < 3600) return rtf.format(-Math.round(diff / 60), 'minute');
  if (diff < 86400) return rtf.format(-Math.round(diff / 3600), 'hour');
  return rtf.format(-Math.round(diff / 86400), 'day');
}

export function ActivityTimeline({ events }: { events: TicketActivity[] }) {
  // Stagger only on the first paint; live-appended entries after that slide in on their own.
  const firstPaint = useRef(true);
  useEffect(() => {
    firstPaint.current = false;
  }, []);

  if (events.length === 0) {
    return <p className="text-xs text-muted-foreground">No activity yet.</p>;
  }

  const lastIndex = events.length - 1;

  return (
    <div className="relative">
      {/* spine */}
      <div className="absolute bottom-4 left-4 top-4 w-px bg-gradient-to-b from-violet-300 via-indigo-200 to-transparent" />
      <AnimatePresence initial={false}>
        {events.map((e, i) => {
          const s = styles[e.type] ?? fallback;
          const Icon = s.icon;
          const latest = i === lastIndex;
          const delay = firstPaint.current ? Math.min(i * 0.06, 0.5) : 0;
          return (
            <motion.div
              key={e.id}
              initial={{ opacity: 0, x: 14 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.35, delay }}
              className="relative flex gap-3 pb-5 last:pb-1"
            >
              <span
                className={`z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full border ${s.dot} ${
                  latest ? 'ring-2 ring-violet-200' : ''
                }`}
              >
                <Icon className="h-3.5 w-3.5" />
              </span>
              <div className="min-w-0 pt-0.5">
                <p className={`text-xs font-medium ${latest ? 'text-violet-800' : 'text-slate-700'}`}>
                  {humanize(e.summary)}
                </p>
                <p className="mt-0.5 text-[11px] text-muted-foreground">
                  {s.label} · {timeAgo(e.createdAt)}
                </p>
              </div>
            </motion.div>
          );
        })}
      </AnimatePresence>
    </div>
  );
}
