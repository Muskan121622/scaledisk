import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useUnreadNotifications() {
  return useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: () => api.get<number>('/notifications/unread-count'),
    refetchInterval: 30_000,
  });
}
