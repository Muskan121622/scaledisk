// TanStack Query hooks for the ScaleDesk HQ surface. All go through platformApi
// (its own token space) and share the ['platform', ...] key prefix so a
// suspend/resume invalidates every HQ query at once.

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { platformApi } from '@/lib/platform-api';
import type { PagedPlatformTenants, PlatformStats, PlatformTenant } from '@/types/platform';

export function usePlatformStats() {
  return useQuery({
    queryKey: ['platform', 'stats'],
    queryFn: () => platformApi.get<PlatformStats>('/platform/stats'),
  });
}

export function usePlatformTenants(q: string, page = 1, pageSize = 25) {
  return useQuery({
    queryKey: ['platform', 'tenants', q, page, pageSize],
    queryFn: () =>
      platformApi.get<PagedPlatformTenants>('/platform/tenants', { q: q || undefined, page, pageSize }),
  });
}

export function usePlatformTenant(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'tenant', id],
    queryFn: () => platformApi.get<PlatformTenant>(`/platform/tenants/${id}`),
    enabled: !!id,
  });
}

export function useSetTenantStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, suspend }: { id: string; suspend: boolean }) =>
      platformApi.post<PlatformTenant>(`/platform/tenants/${id}/${suspend ? 'suspend' : 'resume'}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['platform'] }),
  });
}
