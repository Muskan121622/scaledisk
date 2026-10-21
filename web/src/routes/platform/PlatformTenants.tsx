// Tenant fleet management: search + suspend/resume. Suspension is enforced at login by the
// backend (403 tenant_suspended); live sessions keep their current token until it expires —
// which is exactly what the J1 journey script asserts.

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Building2, PauseCircle, PlayCircle, Search } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { usePlatformTenants, useSetTenantStatus } from '@/hooks/usePlatform';
import { relativeTime } from '@/lib/utils';
import type { PlatformTenant } from '@/types/platform';

// TenantStatus wire values: 0 Active, 1 Suspended, 2 Cancelled.
const statusBadge = (status: number) =>
  status === 0 ? <Badge variant="success">Active</Badge>
  : status === 1 ? <Badge variant="danger">Suspended</Badge>
  : <Badge variant="muted">Cancelled</Badge>;

function TenantRow({ tenant }: { tenant: PlatformTenant }) {
  const setStatus = useSetTenantStatus();
  const suspended = tenant.status !== 0;

  return (
    <tr className="border-b border-slate-50 transition-colors last:border-0 hover:bg-violet-50/40">
      <td className="px-5 py-3">
        <Link to={`/platform/tenants/${tenant.id}`} className="font-medium text-slate-800 hover:text-primary">
          {tenant.name}
        </Link>
        <div className="text-xs text-muted-foreground">{tenant.slug} · plan {tenant.planTier ?? '—'}</div>
      </td>
      <td className="px-5 py-3">{statusBadge(tenant.status)}</td>
      <td className="px-5 py-3 text-slate-600">{tenant.userCount}</td>
      <td className="hidden px-5 py-3 text-slate-600 sm:table-cell">
        {tenant.openTicketCount} <span className="text-xs text-muted-foreground">/ {tenant.ticketCount}</span>
      </td>
      <td className="hidden px-5 py-3 text-xs text-muted-foreground md:table-cell">{relativeTime(tenant.createdAt)}</td>
      <td className="px-5 py-3 text-right">
        <Button
          size="sm"
          variant={suspended ? 'outline' : 'ghost'}
          disabled={setStatus.isPending}
          onClick={() => setStatus.mutate({ id: tenant.id, suspend: !suspended })}
          className={suspended ? 'rounded-xl text-emerald-700' : 'rounded-xl text-rose-600 hover:bg-rose-50'}
        >
          {suspended ? <PlayCircle className="mr-1.5 h-4 w-4" /> : <PauseCircle className="mr-1.5 h-4 w-4" />}
          {suspended ? 'Resume' : 'Suspend'}
        </Button>
      </td>
    </tr>
  );
}

export function PlatformTenants() {
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const { data, isLoading } = usePlatformTenants(q, page);

  const tenants = data?.items ?? [];
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">Tenants</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          {data ? `${data.totalCount} organizations on the platform` : 'Loading…'} · suspending blocks new logins instantly
        </p>
      </div>

      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          value={q}
          onChange={(e) => { setQ(e.target.value); setPage(1); }}
          placeholder="Search name or slug…"
          className="rounded-xl pl-9"
        />
      </div>

      <div className="surface overflow-hidden rounded-2xl">
        {isLoading ? (
          <div className="space-y-3 p-5">
            {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-12 rounded-xl" />)}
          </div>
        ) : tenants.length === 0 ? (
          <div className="flex flex-col items-center gap-2 py-14 text-center">
            <Building2 className="h-8 w-8 text-violet-300" />
            <p className="text-sm font-medium text-slate-700">No tenants match</p>
            <p className="text-xs text-muted-foreground">Try a different search.</p>
          </div>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-100 text-[11px] uppercase tracking-wider text-muted-foreground">
              <tr>
                <th className="px-5 py-3 font-medium">Tenant</th>
                <th className="px-5 py-3 font-medium">Status</th>
                <th className="px-5 py-3 font-medium">Users</th>
                <th className="hidden px-5 py-3 font-medium sm:table-cell">Open / total tickets</th>
                <th className="hidden px-5 py-3 font-medium md:table-cell">Created</th>
                <th className="px-5 py-3 text-right" />
              </tr>
            </thead>
            <tbody>
              {tenants.map((t) => <TenantRow key={t.id} tenant={t} />)}
            </tbody>
          </table>
        )}
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 text-sm">
          <Button size="sm" variant="outline" className="rounded-xl" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Previous
          </Button>
          <span className="text-muted-foreground">Page {page} / {totalPages}</span>
          <Button size="sm" variant="outline" className="rounded-xl" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
            Next
          </Button>
        </div>
      )}
    </motion.div>
  );
}
