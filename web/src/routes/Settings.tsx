// Settings: workspace identity (from the JWT-backed /auth/me the app already holds) plus
// member invitations — invite by email + role, see pending, revoke. Accept happens
// out-of-band via the emailed token (anonymous POST /invitations/accept), which is why no
// link is shown here.

import { useState } from 'react';
import { motion } from 'framer-motion';
import { Building2, Mail, ShieldCheck, UserPlus, X } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { useInviteMember, usePendingInvitations, useRevokeInvitation } from '@/hooks/useWorkspace';
import { useAuth } from '@/lib/auth';
import { RoleLabel, UserRole, type UserRole as UR } from '@/types';
import { shortDateTime } from '@/lib/utils';

export function Settings() {
  const { user } = useAuth();
  const canInvite = user?.permissions.includes('members.invite') ?? false;
  const [adding, setAdding] = useState(false);
  const { data: invitations, isLoading } = usePendingInvitations();
  const revoke = useRevokeInvitation();

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="max-w-3xl space-y-5">
      <h1 className="text-2xl font-bold tracking-tight text-slate-900">Settings</h1>

      {/* Workspace identity */}
      <div className="surface rounded-2xl p-6">
        <div className="mb-4 flex items-center gap-2">
          <Building2 className="h-4 w-4 text-violet-600" />
          <h2 className="text-sm font-semibold text-slate-900">Workspace</h2>
        </div>
        <dl className="grid gap-4 text-sm sm:grid-cols-3">
          <div>
            <dt className="text-xs text-muted-foreground">Name</dt>
            <dd className="mt-1 font-medium text-slate-800">{user?.tenantName ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">Your email</dt>
            <dd className="mt-1 font-medium text-slate-800">{user?.email ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">Your role</dt>
            <dd className="mt-1">
              <Badge variant="secondary">{user?.role ?? '—'}</Badge>
            </dd>
          </div>
        </dl>
        <div className="mt-5 flex items-start gap-2 rounded-xl border border-slate-100 bg-slate-50/70 p-3 text-xs text-slate-600">
          <ShieldCheck className="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-600" />
          <p>
            Tenant id <span className="font-mono">{user?.tenantId?.slice(0, 8)}…</span> is carried inside your JWT and
            enforced by EF query filters <em>and</em> PostgreSQL row-level security — every screen above only ever sees this workspace's rows.
          </p>
        </div>
      </div>

      {/* Permissions you hold */}
      <div className="surface rounded-2xl p-6">
        <div className="mb-3 flex items-center gap-2">
          <ShieldCheck className="h-4 w-4 text-violet-600" />
          <h2 className="text-sm font-semibold text-slate-900">Your permissions ({user?.permissions.length ?? 0})</h2>
        </div>
        <div className="flex flex-wrap gap-1.5">
          {(user?.permissions ?? []).map((p) => <Badge key={p} variant="muted">{p}</Badge>)}
        </div>
      </div>

      {/* Invitations */}
      <div className="surface rounded-2xl p-6">
        <div className="mb-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Mail className="h-4 w-4 text-violet-600" />
            <h2 className="text-sm font-semibold text-slate-900">Pending invitations</h2>
          </div>
          {canInvite && (
            <Button size="sm" variant={adding ? 'outline' : 'default'} onClick={() => setAdding((v) => !v)} className="rounded-xl">
              {adding ? <X className="mr-1.5 h-3.5 w-3.5" /> : <UserPlus className="mr-1.5 h-3.5 w-3.5" />}
              {adding ? 'Cancel' : 'Invite'}
            </Button>
          )}
        </div>

        {adding && canInvite && <InviteForm onDone={() => setAdding(false)} />}

        {isLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 2 }).map((_, i) => <Skeleton key={i} className="h-10 rounded-xl" />)}
          </div>
        ) : (invitations ?? []).length === 0 ? (
          <p className="py-4 text-center text-xs text-muted-foreground">No pending invitations.</p>
        ) : (
          <ul className="divide-y divide-slate-100">
            {(invitations ?? []).map((inv) => (
              <li key={inv.id} className="flex items-center justify-between gap-3 py-2.5">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-slate-800">{inv.email}</p>
                  <p className="text-[11px] text-muted-foreground">expires {shortDateTime(inv.expiresAt)}</p>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="secondary">{RoleLabel[inv.role as UR]}</Badge>
                  {canInvite && (
                    <Button size="sm" variant="ghost" onClick={() => revoke.mutate(inv.id)} className="text-slate-400 hover:text-destructive">
                      Revoke
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </motion.div>
  );
}

function InviteForm({ onDone }: { onDone: () => void }) {
  const invite = useInviteMember();
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<UR>(UserRole.Agent);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await invite.mutateAsync({ email, role });
    onDone();
  };

  return (
    <form onSubmit={submit} className="mb-4 flex flex-wrap gap-2 rounded-xl border border-violet-100 bg-violet-50/50 p-3">
      <Input required type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="teammate@company.com" className="min-w-[200px] flex-1 rounded-xl" />
      <select value={role} onChange={(e) => setRole(Number(e.target.value) as UR)} className="h-9 rounded-xl border border-input bg-background px-3 text-sm">
        {[UserRole.Admin, UserRole.Manager, UserRole.Agent, UserRole.Viewer].map((r) => (
          <option key={r} value={r}>{RoleLabel[r]}</option>
        ))}
      </select>
      <Button type="submit" size="sm" disabled={invite.isPending} className="rounded-xl">
        {invite.isPending ? 'Sending…' : 'Send invite'}
      </Button>
    </form>
  );
}
