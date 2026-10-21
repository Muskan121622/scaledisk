import { Link } from 'react-router-dom';
import { Badge } from '@/components/ui/label-badge';
import { SlaCountdown } from './SlaCountdown';
import { PriorityLabel, StatusLabel, TicketPriority, TicketStatus, type Ticket } from '@/types';
import { relativeTime } from '@/lib/utils';
import { cn } from '@/lib/utils';

const priorityVariant: Record<TicketPriority, 'default' | 'warn' | 'danger' | 'muted'> = {
  [TicketPriority.Low]: 'muted',
  [TicketPriority.Medium]: 'info',
  [TicketPriority.High]: 'warn',
  [TicketPriority.Urgent]: 'danger',
} as never;

const statusVariant: Record<TicketStatus, 'success' | 'info' | 'warn' | 'muted'> = {
  [TicketStatus.Open]: 'info',
  [TicketStatus.InProgress]: 'warn',
  [TicketStatus.WaitingForCustomer]: 'muted',
  [TicketStatus.WaitingForInternal]: 'muted',
  [TicketStatus.Resolved]: 'success',
  [TicketStatus.Closed]: 'muted',
} as never;

export function TicketRow({ ticket }: { ticket: Ticket }) {
  return (
    <Link
      to={`/app/tickets/${ticket.id}`}
      className={cn(
        'group grid grid-cols-12 items-center gap-3 border-b border-slate-100 px-5 py-3.5 text-sm transition-all duration-200',
        'hover:bg-violet-50/50'
      )}
    >
      <div className="col-span-5 min-w-0">
        <div className="truncate font-medium text-slate-900 group-hover:text-violet-700 transition-colors">{ticket.subject}</div>
        <div className="mt-0.5 truncate text-xs text-muted-foreground">
          {ticket.description}
        </div>
      </div>
      <div className="col-span-2">
        <Badge variant={priorityVariant[ticket.priority]}>{PriorityLabel[ticket.priority]}</Badge>
      </div>
      <div className="col-span-2">
        <Badge variant={statusVariant[ticket.status]}>{StatusLabel[ticket.status]}</Badge>
      </div>
      <div className="col-span-2 text-xs text-muted-foreground">{relativeTime(ticket.createdAt)}</div>
      <div className="col-span-1 flex justify-end">
        <SlaCountdown dueAt={ticket.responseDueAt} breached={ticket.slaBreached} />
      </div>
    </Link>
  );
}
