import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { Paged, Ticket, TicketPriority, TicketStatus } from '@/types';

export type TicketListFilters = {
  status?: TicketStatus;
  priority?: TicketPriority;
  /** User id — binds to the controller's [FromQuery] Guid? assignee. */
  assignee?: string;
  page: number;
  pageSize: number;
};

export function useTickets(f: TicketListFilters) {
  return useQuery({
    queryKey: ['tickets', f],
    queryFn: () => api.get<Paged<Ticket>>('/tickets', {
      status: f.status, priority: f.priority, assignee: f.assignee, page: f.page, pageSize: f.pageSize,
    }),
  });
}
