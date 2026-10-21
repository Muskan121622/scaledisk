import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { Ticket, TicketActivity, TicketComment, TicketStatus } from '@/types';

export function useTicket(id: string) {
  return useQuery({
    queryKey: ['tickets', id],
    queryFn: () => api.get<Ticket>(`/tickets/${id}`),
    enabled: !!id,
  });
}

export function useTicketComments(id: string) {
  return useQuery({
    queryKey: ['tickets', id, 'comments'],
    queryFn: () => api.get<TicketComment[]>(`/tickets/${id}/comments`),
    enabled: !!id,
  });
}

// Append-only timeline backing the Activity rail. Re-fetched whenever the SignalR
// `ticketChanged` broadcast fires for this ticket — that's what makes new entries animate in live.
export function useTicketEvents(id: string) {
  return useQuery({
    queryKey: ['tickets', id, 'events'],
    queryFn: () => api.get<TicketActivity[]>(`/tickets/${id}/events`),
    enabled: !!id,
  });
}

// Staff status transition (PATCH /tickets/{id}/status). Invalidates this ticket AND the list
// keys so the queue view, dashboard counts and the detail page all agree after a move.
export function useUpdateTicketStatus(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (status: TicketStatus) => api.patch<Ticket>(`/tickets/${id}/status`, { status }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['tickets'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
    },
  });
}
