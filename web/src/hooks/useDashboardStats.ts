import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { DashboardStats } from '@/types';

export function useDashboardStats(lookbackDays = 14) {
  return useQuery({
    queryKey: ['dashboard', 'stats', lookbackDays],
    queryFn: () => api.get<DashboardStats>('/dashboard/stats', { lookbackDays }),
    refetchInterval: 30_000,
  });
}
