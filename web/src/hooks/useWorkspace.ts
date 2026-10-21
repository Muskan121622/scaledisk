// Data hooks for the workspace-admin screens (Customers / Teams / Automation / SLA / Settings).
// Same pattern as useTickets/useTicket: thin wrappers over the api client + TanStack Query,
// with prefix-based invalidation so a mutation refetches exactly its own list.

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type {
  AutomationRule, Customer, Invitation, Paged, SlaPolicy, Team, UserRole,
} from '@/types';

const asKey = (...parts: (string | number)[]) => parts;

// ---- Customers ------------------------------------------------------------------------------

export function useCustomers(search: string) {
  return useQuery({
    queryKey: asKey('customers', search),
    queryFn: () => api.get<Paged<Customer>>('/customers', { search, page: 1, pageSize: 50 }),
  });
}

export function useCreateCustomer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; email: string; company?: string; tags?: string[] }) =>
      api.post<Customer>('/customers', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['customers'] }),
  });
}

export function useDeleteCustomer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del(`/customers/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['customers'] }),
  });
}

// ---- Teams ----------------------------------------------------------------------------------

export function useTeams() {
  return useQuery({ queryKey: ['teams'], queryFn: () => api.get<Team[]>('/teams') });
}

export function useCreateTeam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; description?: string }) => api.post<Team>('/teams', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['teams'] }),
  });
}

export function useDeleteTeam() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del(`/teams/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['teams'] }),
  });
}

// ---- Automation rules -----------------------------------------------------------------------

export function useAutomationRules() {
  return useQuery({ queryKey: ['automation'], queryFn: () => api.get<AutomationRule[]>('/automation-rules') });
}

export function useCreateAutomationRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Omit<AutomationRule, 'id'>) => api.post<AutomationRule>('/automation-rules', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['automation'] }),
  });
}

// PUT is a full replacement — send the whole rule back with isEnabled flipped, etc.
export function useUpdateAutomationRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (rule: AutomationRule) => api.put<AutomationRule>(`/automation-rules/${rule.id}`, rule),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['automation'] }),
  });
}

export function useDeleteAutomationRule() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del(`/automation-rules/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['automation'] }),
  });
}

// ---- SLA policies ---------------------------------------------------------------------------

export function useSlaPolicies() {
  return useQuery({ queryKey: ['sla-policies'], queryFn: () => api.get<SlaPolicy[]>('/sla-policies') });
}

export function useCreateSlaPolicy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Omit<SlaPolicy, 'id'>) => api.post<SlaPolicy>('/sla-policies', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sla-policies'] }),
  });
}

export function useDeleteSlaPolicy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del(`/sla-policies/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sla-policies'] }),
  });
}

// ---- Invitations (Settings) -----------------------------------------------------------------

export function usePendingInvitations() {
  return useQuery({
    queryKey: ['invitations', 'pending'],
    queryFn: () => api.get<Invitation[]>('/invitations/pending'),
  });
}

export function useInviteMember() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { email: string; role: UserRole }) => api.post<Invitation>('/invitations', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['invitations'] }),
  });
}

export function useRevokeInvitation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post(`/invitations/${id}/revoke`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['invitations'] }),
  });
}
