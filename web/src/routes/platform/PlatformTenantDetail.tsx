// Single tenant from HQ: identity + fleet metrics + the suspend/resume switch.

import { Link, useParams } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowLeft, PauseCircle, PlayCircle } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { usePlatformTenant, useSetTenantStatus } from '@/hooks/usePlatform';

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <div className="text-[11px] uppercase tracking-wider text-muted-foreground">{label}</div>
      <div className="mt-0.5 text-sm font-medium text-slate-800">{value}</div>
    </div>
  );
}

export function PlatformTenantDetail() {
  const { id } = useParams<{ id: string }>();
  const { data: tenant, isLoading } = usePlatformTenant(id);
  const setStatus = useSetTenantStatus();

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48 rounded-xl" />
        <Skeleton className="h-40 rounded-2xl" />
      </div>
    );
  }

  if (!tenant) {
    return (
      <div className="py-14 text-center">
        <p className="text-sm font-medium text-slate-700">Tenant not found</p>
        <Link to="/platform/tenants" className="text-sm text-primary hover:underline">Back to tenants</Link>
      </div>
    );
  }

  const suspended = tenant.status !== 0;

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link to="/platform/tenants" className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground">
            <ArrowLeft className="h-3 w-3" /> All tenants
          </Link>
          <h1 className="mt-1 text-2xl font-bold tracking-tight text-slate-900">{tenant.name}</h1>
        </div>
        <Button
          variant={suspended ? 'outline' : 'ghost'}
          disabled={setStatus.isPending}
          onClick={() => setStatus.mutate({ id: tenant.id, suspend: !suspended })}
          className={suspended ? 'rounded-xl text-emerald-700' : 'rounded-xl text-rose-600 hover:bg-rose-50'}
        >
          {suspended ? <PlayCircle className="mr-1.5 h-4 w-4" /> : <PauseCircle className="mr-1.5 h-4 w-4" />}
          {suspended ? 'Resume tenant' : 'Suspend tenant'}
        </Button>
      </div>

      <div className="surface rounded-2xl p-6">
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="Status" value={
            tenant.status === 0 ? <Badge variant="success">Active</Badge>
            : tenant.status === 1 ? <Badge variant="danger">Suspended</Badge>
            : <Badge variant="muted">Cancelled</Badge>
          } />
          <Field label="Slug" value={tenant.slug} />
          <Field label="Plan tier" value={tenant.planTier ?? '—'} />
          <Field label="Created" value={new Date(tenant.createdAt).toLocaleDateString()} />
        </div>

        <div className="mt-6 grid gap-6 border-t border-slate-100 pt-6 sm:grid-cols-3">
          <Field label="Active users" value={tenant.userCount} />
          <Field label="Tickets (all time)" value={tenant.ticketCount} />
          <Field label="Open tickets" value={tenant.openTicketCount} />
        </div>
      </div>

      <p className="text-xs text-muted-foreground">
        Suspension blocks new logins for every member of this tenant (HTTP 403 tenant_suspended).
        Already-issued access tokens stay valid until they expire — by design, short by default (15 min).
      </p>
    </motion.div>
  );
}
