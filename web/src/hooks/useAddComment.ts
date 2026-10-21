import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { TicketComment } from '@/types';

export function useAddComment(ticketId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { body: string; isInternal: boolean }) =>
      api.post<TicketComment>(`/tickets/${ticketId}/comments`, input),
    onSuccess: (created) => {
      // Optimistic merge avoids a full refetch round-trip for the poster. Other tabs see the
      // same comment via the SignalR push (see useTicketCommentStream).
      qc.setQueryData<TicketComment[]>(['tickets', ticketId, 'comments'], (prev) =>
        prev ? [...prev, created] : [created]
      );
      qc.invalidateQueries({ queryKey: ['tickets', ticketId] });
    },
  });
}
