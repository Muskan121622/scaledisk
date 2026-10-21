// Teams: card grid of the workspace's support teams, create + delete. /api/teams is real
// tenant-scoped data; teams are what automation rules can assign tickets to.

import { useState } from 'react';
import { motion } from 'framer-motion';
import { Building2, Plus, Trash2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { useCreateTeam, useDeleteTeam, useTeams } from '@/hooks/useWorkspace';
import { useAuth } from '@/lib/auth';
import { relativeTime } from '@/lib/utils';

export function Teams() {
  const { user } = useAuth();
  const canManage = user?.permissions.includes('teams.manage') ?? false;
  const [adding, setAdding] = useState(false);
  const { data: teams, isLoading } = useTeams();
  const del = useDeleteTeam();

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">Teams</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">Routing groups tickets get assigned to</p>
        </div>
        {canManage && (
          <Button onClick={() => setAdding((v) => !v)} className="rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500 shadow-brand">
            {adding ? <X className="mr-1.5 h-4 w-4" /> : <Plus className="mr-1.5 h-4 w-4" />}
            {adding ? 'Cancel' : 'New team'}
          </Button>
        )}
      </div>

      {adding && canManage && <AddTeamForm onDone={() => setAdding(false)} />}

      {isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-36 rounded-2xl" />)}
        </div>
      ) : (teams ?? []).length === 0 ? (
        <div className="surface flex flex-col items-center gap-2 rounded-2xl py-14 text-center">
          <Building2 className="h-8 w-8 text-violet-300" />
          <p className="text-sm font-medium text-slate-700">No teams yet</p>
          <p className="text-xs text-muted-foreground">Create one so tickets can be routed.</p>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {(teams ?? []).map((t) => (
            <motion.div
              key={t.id}
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              className="surface group relative rounded-2xl p-5"
            >
              <div className="flex items-start justify-between gap-2">
                <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-violet-50 text-violet-600">
                  <Building2 className="h-4 w-4" />
                </div>
                {canManage && (
                  <Button
                    size="icon"
                    variant="ghost"
                    onClick={() => del.mutate(t.id)}
                    className="h-8 w-8 text-slate-300 opacity-0 transition-opacity hover:text-destructive group-hover:opacity-100"
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                )}
              </div>
              <h3 className="mt-3 text-sm font-semibold text-slate-900">{t.name}</h3>
              <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">{t.description ?? 'No description'}</p>
              <p className="mt-3 text-[11px] text-slate-400">created {relativeTime(t.createdAt)}</p>
            </motion.div>
          ))}
        </div>
      )}
    </motion.div>
  );
}

function AddTeamForm({ onDone }: { onDone: () => void }) {
  const create = useCreateTeam();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await create.mutateAsync({ name, description: description || undefined });
    onDone();
  };

  return (
    <form onSubmit={submit} className="surface max-w-lg rounded-2xl p-5">
      <Input required value={name} onChange={(e) => setName(e.target.value)} placeholder="Team name (e.g. Billing)" className="rounded-xl" />
      <Textarea
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        placeholder="What does this team handle? (optional)"
        className="mt-3 rounded-xl"
        rows={2}
      />
      {create.isError && <p className="mt-2 text-xs text-destructive">Could not create team.</p>}
      <div className="mt-3 flex justify-end">
        <Button type="submit" size="sm" disabled={create.isPending} className="rounded-xl">
          {create.isPending ? 'Creating…' : 'Create team'}
        </Button>
      </div>
    </form>
  );
}
