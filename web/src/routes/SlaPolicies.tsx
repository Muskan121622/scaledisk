// SLA policies: the deadlines stamped onto tickets at creation (response + resolution minutes,
// per priority, one workspace default). The consumer-side breach scanner acts on these.
// Note: the whole /api/sla-policies surface requires the sla.manage permission.

import { useState } from 'react';
import { motion } from 'framer-motion';
import { Plus, ShieldCheck, Trash2, X } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { useCreateSlaPolicy, useDeleteSlaPolicy, useSlaPolicies } from '@/hooks/useWorkspace';
import { useAuth } from '@/lib/auth';
import { PriorityLabel, TicketPriority, type TicketPriority as TP } from '@/types';

export function SlaPolicies() {
  const { user } = useAuth();
  const canManage = user?.permissions.includes('sla.manage') ?? false;
  const [adding, setAdding] = useState(false);
  const { data: policies, isLoading, isError } = useSlaPolicies();
  const del = useDeleteSlaPolicy();

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">SLA Policies</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">First-response and resolution deadlines per priority</p>
        </div>
        {canManage && (
          <Button onClick={() => setAdding((v) => !v)} className="rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500 shadow-brand">
            {adding ? <X className="mr-1.5 h-4 w-4" /> : <Plus className="mr-1.5 h-4 w-4" />}
            {adding ? 'Cancel' : 'New policy'}
          </Button>
        )}
      </div>

      {adding && canManage && <AddPolicyForm onDone={() => setAdding(false)} />}

      {isLoading ? (
        <div className="surface space-y-3 rounded-2xl p-5">
          {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-12 rounded-xl" />)}
        </div>
      ) : isError ? (
        <div className="surface flex flex-col items-center gap-2 rounded-2xl py-14 text-center">
          <ShieldCheck className="h-8 w-8 text-rose-300" />
          <p className="text-sm font-medium text-slate-700">Not visible to your role</p>
          <p className="text-xs text-muted-foreground">Managing SLA policies needs the <code>sla.manage</code> permission.</p>
        </div>
      ) : (
        <div className="surface overflow-hidden rounded-2xl">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-100 text-[11px] uppercase tracking-wider text-muted-foreground">
              <tr>
                <th className="px-5 py-3 font-medium">Policy</th>
                <th className="px-5 py-3 font-medium">Priority</th>
                <th className="px-5 py-3 font-medium">Response</th>
                <th className="px-5 py-3 font-medium">Resolution</th>
                <th className="px-5 py-3" />
              </tr>
            </thead>
            <tbody>
              {(policies ?? []).map((p) => (
                <tr key={p.id} className="border-b border-slate-50 transition-colors last:border-0 hover:bg-violet-50/40">
                  <td className="px-5 py-3">
                    <span className="font-medium text-slate-800">{p.name}</span>
                    {p.isDefault && <Badge variant="info" className="ml-2">default</Badge>}
                  </td>
                  <td className="px-5 py-3">
                    <Badge variant={p.priority >= 2 ? 'warn' : 'muted'}>{PriorityLabel[p.priority]}</Badge>
                  </td>
                  <td className="px-5 py-3 text-slate-600">{formatMinutes(p.responseDueInMinutes)}</td>
                  <td className="px-5 py-3 text-slate-600">{formatMinutes(p.resolutionDueInMinutes)}</td>
                  <td className="px-5 py-3 text-right">
                    {canManage && (
                      <Button
                        size="icon"
                        variant="ghost"
                        onClick={() => del.mutate(p.id)}
                        className="h-8 w-8 text-slate-400 hover:text-destructive"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </motion.div>
  );
}

function formatMinutes(m: number): string {
  if (m < 60) return `${m} min`;
  const h = Math.floor(m / 60);
  const rest = m % 60;
  return rest ? `${h}h ${rest}m` : `${h}h`;
}

function AddPolicyForm({ onDone }: { onDone: () => void }) {
  const create = useCreateSlaPolicy();
  const [name, setName] = useState('');
  const [priority, setPriority] = useState<TP>(TicketPriority.Medium);
  const [response, setResponse] = useState(30);
  const [resolution, setResolution] = useState(240);
  const [isDefault, setIsDefault] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await create.mutateAsync({
      name,
      priority,
      responseDueInMinutes: response,
      resolutionDueInMinutes: resolution,
      isDefault,
    });
    onDone();
  };

  return (
    <form onSubmit={submit} className="surface max-w-2xl rounded-2xl p-5">
      <div className="grid gap-3 md:grid-cols-2">
        <Input required value={name} onChange={(e) => setName(e.target.value)} placeholder="Policy name (e.g. High — 30m / 4h)" className="rounded-xl" />
        <select
          value={priority}
          onChange={(e) => setPriority(Number(e.target.value) as TP)}
          className="h-9 rounded-xl border border-input bg-background px-3 text-sm"
        >
          {Object.entries(PriorityLabel).map(([v, label]) => (
            <option key={v} value={v}>{label}</option>
          ))}
        </select>
        <Input
          required type="number" min={1} value={response}
          onChange={(e) => setResponse(Number(e.target.value))}
          placeholder="Response due in minutes" className="rounded-xl"
        />
        <Input
          required type="number" min={1} value={resolution}
          onChange={(e) => setResolution(Number(e.target.value))}
          placeholder="Resolution due in minutes" className="rounded-xl"
        />
      </div>
      <label className="mt-3 flex items-center gap-2 text-xs text-slate-600">
        <input type="checkbox" checked={isDefault} onChange={(e) => setIsDefault(e.target.checked)} className="accent-[hsl(262_83%_58%)]" />
        Workspace default (used when no priority-specific policy matches)
      </label>
      {create.isError && <p className="mt-2 text-xs text-destructive">Could not create policy — a default may already exist.</p>}
      <div className="mt-3 flex justify-end">
        <Button type="submit" size="sm" disabled={create.isPending} className="rounded-xl">
          {create.isPending ? 'Saving…' : 'Save policy'}
        </Button>
      </div>
    </form>
  );
}
