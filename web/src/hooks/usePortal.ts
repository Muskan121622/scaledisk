// TanStack Query hooks for the customer portal. Everything goes through portalApi (its own
// token space) under the ['portal', ...] key prefix. The backend pins every read/write to the
// caller's cid claim, so no customer id ever appears in these calls — the token IS the identity.

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { portalApi } from '@/lib/portal-api';
import type { Paged, Ticket, TicketComment } from '@/types';

export function useMyTickets(status: number | null, page = 1, pageSize = 15) {
  return useQuery({
    queryKey: ['portal', 'tickets', status, page, pageSize],
    queryFn: () =>
      portalApi.get<Paged<Ticket>>('/portal/tickets', {
        status: status ?? undefined, page, pageSize,
      }),
  });
}

export function useMyTicket(id: string | undefined) {
  return useQuery({
    queryKey: ['portal', 'ticket', id],
    queryFn: () => portalApi.get<Ticket>(`/portal/tickets/${id}`),
    enabled: !!id,
  });
}

export function useMyTicketComments(id: string | undefined) {
  return useQuery({
    queryKey: ['portal', 'ticket', id, 'comments'],
    queryFn: () => portalApi.get<TicketComment[]>(`/portal/tickets/${id}/comments`),
    enabled: !!id,
  });
}

export function useCreateTicket() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { subject: string; description: string; priority: number; category: number }) =>
      portalApi.post<Ticket>('/portal/tickets', input),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['portal'] }),
  });
}

export function useReplyTicket(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: string) =>
      portalApi.post<TicketComment>(`/portal/tickets/${id}/comments`, { body }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['portal', 'ticket', id] }),
  });
}

export function useCloseTicket(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => portalApi.post<Ticket>(`/portal/tickets/${id}/close`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['portal', 'ticket', id] }),
  });
}
