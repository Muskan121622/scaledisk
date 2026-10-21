// Automation rules: what the consumer-side engine evaluates for every incoming event (§10).
// Trigger event + match conditions (nulls are wildcards) -> ordered actions. The engine runs
// off Kafka, completely outside the HTTP request path — this page just manages its config.

import { useState } from 'react';
import { motion } from 'framer-motion';
import { ArrowRight, Plus, Trash2, Zap, X } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { useAutomationRules, useCreateAutomationRule, useDeleteAutomationRule, useTeams, useUpdateAutomationRule } from '@/hooks/useWorkspace';
import { useAuth } from '@/lib/auth';
import {
  ActionTypeLabel, AutomationActionType, CategoryLabel, PriorityLabel, StatusLabel,
  TriggerEvents, TicketPriority, TicketStatus,
  type AutomationActionType as AAT, type AutomationRule, type TicketCategory as TC, type TicketPriority as TP, type TicketStatus as TS,
} from '@/types';

export function Automation() {
  const { user } = useAuth();
  const canManage = user?.permissions.includes('automation.manage') ?? false;
  const [adding, setAdding] = useState(false);
  const { data: rules, isLoading } = useAutomationRules();
  const update = useUpdateAutomationRule();
  const del = useDeleteAutomationRule();

  const sorted = [...(rules ?? [])].sort((a, b) => a.priority - b.priority);

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">Automation</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">Rules evaluated by the event consumer when tickets change</p>
        </div>
        {canManage && (
          <Button onClick={() => setAdding((v) => !v)} className="rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500 shadow-brand">
            {adding ? <X className="mr-1.5 h-4 w-4" /> : <Plus className="mr-1.5 h-4 w-4" />}
            {adding ? 'Cancel' : 'New rule'}
          </Button>
        )}
      </div>

      {adding && canManage && <AddRuleForm onDone={() => setAdding(false)} />}

      {isLoading ? (
        <div className="space-y-4">
          {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-28 rounded-2xl" />)}
        </div>
      ) : sorted.length === 0 ? (
        <div className="surface flex flex-col items-center gap-2 rounded-2xl py-14 text-center">
          <Zap className="h-8 w-8 text-violet-300" />
          <p className="text-sm font-medium text-slate-700">No rules yet</p>
          <p className="text-xs text-muted-foreground">Create one — e.g. "Urgent billing tickets go to the Billing team".</p>
        </div>
      ) : (
        <div className="space-y-4">
          {sorted.map((r) => (
            <RuleCard
              key={r.id}
              rule={r}
              canManage={canManage}
              busy={update.isPending || del.isPending}
              onToggle={() => update.mutate({ ...r, isEnabled: !r.isEnabled })}
              onDelete={() => del.mutate(r.id)}
            />
          ))}
        </div>
      )}
    </motion.div>
  );
}

function RuleCard({ rule, canManage, busy, onToggle, onDelete }: {
  rule: AutomationRule;
  canManage: boolean;
  busy: boolean;
  onToggle: () => void;
  onDelete: () => void;
}) {
  const c = rule.conditions;
  const conditions = [
    c.priority !== null && `priority is ${PriorityLabel[c.priority]}`,
    c.category !== null && `category is ${CategoryLabel[c.category]}`,
    c.status !== null && `status is ${StatusLabel[c.status]}`,
    c.subjectContains && `subject contains "${c.subjectContains}"`,
  ].filter(Boolean) as string[];

  return (
    <div className={`surface rounded-2xl p-5 transition-opacity ${rule.isEnabled ? '' : 'opacity-60'}`}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-semibold text-slate-900">{rule.name}</h3>
            <Badge variant="info">{rule.triggerEvent}</Badge>
            {!rule.isEnabled && <Badge variant="muted">disabled</Badge>}
          </div>
          {rule.description && <p className="mt-1 text-xs text-muted-foreground">{rule.description}</p>}
        </div>
        {canManage && (
          <div className="flex items-center gap-1">
            {/* enable/disable toggle */}
            <button
              onClick={onToggle}
              disabled={busy}
              aria-label={rule.isEnabled ? 'Disable rule' : 'Enable rule'}
              className={`relative h-6 w-11 rounded-full transition-colors ${rule.isEnabled ? 'bg-violet-600' : 'bg-slate-200'}`}
            >
              <span className={`absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-all ${rule.isEnabled ? 'left-[22px]' : 'left-0.5'}`} />
            </button>
            <Button size="icon" variant="ghost" onClick={onDelete} disabled={busy} className="h-8 w-8 text-slate-400 hover:text-destructive">
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        )}
      </div>

      {/* WHEN matches THEN do... strip */}
      <div className="mt-4 flex flex-wrap items-center gap-2 text-xs">
        <span className="rounded-lg bg-violet-50 px-2.5 py-1 font-medium text-violet-700">
          WHEN {rule.triggerEvent}
        </span>
        {conditions.length > 0 && (
          <span className="rounded-lg bg-slate-50 px-2.5 py-1 text-slate-600">
            if {conditions.join(' · ')}
          </span>
        )}
        <ArrowRight className="h-3.5 w-3.5 text-slate-300" />
        {rule.actions.map((a, i) => (
          <span key={i} className="rounded-lg bg-cyan-50 px-2.5 py-1 font-medium text-cyan-700">
            {ActionTypeLabel[a.type]}
            {a.priority !== null && ` → ${PriorityLabel[a.priority]}`}
            {a.status !== null && ` → ${StatusLabel[a.status]}`}
          </span>
        ))}
        {rule.actions.length === 0 && <span className="text-muted-foreground">no actions</span>}
      </div>
      <p className="mt-3 text-[11px] text-slate-400">execution priority {rule.priority}</p>
    </div>
  );
}

function AddRuleForm({ onDone }: { onDone: () => void }) {
  const create = useCreateAutomationRule();
  const { data: teams } = useTeams();
  const [name, setName] = useState('');
  const [trigger, setTrigger] = useState<string>(TriggerEvents[0]);
  const [priority, setPriority] = useState(100);
  // conditions (empty = wildcard)
  const [condPriority, setCondPriority] = useState<TP | ''>('');
  const [condCategory, setCondCategory] = useState('');
  const [condSubject, setCondSubject] = useState('');
  // single action
  const [actionType, setActionType] = useState<AAT>(AutomationActionType.SetPriority);
  const [actionPriority, setActionPriority] = useState<TP>(TicketPriority.High);
  const [actionStatus, setActionStatus] = useState<TS>(TicketStatus.InProgress);
  const [actionTeam, setActionTeam] = useState('');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await create.mutateAsync({
      name,
      description: null,
      triggerEvent: trigger,
      isEnabled: true,
      priority,
      conditions: {
        priority: condPriority === '' ? null : Number(condPriority) as TP,
        category: condCategory === '' ? null : Number(condCategory) as TC,
        status: null,
        subjectContains: condSubject || null,
      },
      actions: [{
        type: actionType,
        priority: actionType === AutomationActionType.SetPriority ? actionPriority : null,
        status: actionType === AutomationActionType.SetStatus ? actionStatus : null,
        teamId: actionType === AutomationActionType.AssignTeam ? (actionTeam || null) : null,
        assigneeUserId: null,
      }],
    });
    onDone();
  };

  return (
    <form onSubmit={submit} className="surface max-w-3xl space-y-4 rounded-2xl p-5">
      <div className="grid gap-3 md:grid-cols-3">
        <Input required value={name} onChange={(e) => setName(e.target.value)} placeholder="Rule name" className="rounded-xl" />
        <select value={trigger} onChange={(e) => setTrigger(e.target.value)} className="h-9 rounded-xl border border-input bg-background px-3 text-sm">
          {TriggerEvents.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
        <Input type="number" value={priority} onChange={(e) => setPriority(Number(e.target.value))} className="rounded-xl" placeholder="Exec priority" />
      </div>

      <fieldset className="rounded-xl border border-slate-100 p-3">
        <legend className="px-1 text-[11px] font-medium uppercase tracking-wider text-violet-600">IF (conditions — blank = any)</legend>
        <div className="grid gap-3 md:grid-cols-3">
          <select value={condPriority} onChange={(e) => setCondPriority(e.target.value === '' ? '' : Number(e.target.value) as TP)} className="h-9 rounded-xl border border-input bg-background px-2 text-sm">
            <option value="">Any priority</option>
            {Object.entries(PriorityLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
          <select value={condCategory} onChange={(e) => setCondCategory(e.target.value)} className="h-9 rounded-xl border border-input bg-background px-2 text-sm">
            <option value="">Any category</option>
            {Object.entries(CategoryLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
          <Input value={condSubject} onChange={(e) => setCondSubject(e.target.value)} placeholder="Subject contains…" className="rounded-xl" />
        </div>
      </fieldset>

      <fieldset className="rounded-xl border border-slate-100 p-3">
        <legend className="px-1 text-[11px] font-medium uppercase tracking-wider text-cyan-600">THEN (action)</legend>
        <div className="grid gap-3 md:grid-cols-2">
          <select value={actionType} onChange={(e) => setActionType(Number(e.target.value) as AAT)} className="h-9 rounded-xl border border-input bg-background px-2 text-sm">
            {Object.entries(ActionTypeLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
          {actionType === AutomationActionType.SetPriority && (
            <select value={actionPriority} onChange={(e) => setActionPriority(Number(e.target.value) as TP)} className="h-9 rounded-xl border border-input bg-background px-2 text-sm">
              {Object.entries(PriorityLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
          )}
          {actionType === AutomationActionType.SetStatus && (
            <select value={actionStatus} onChange={(e) => setActionStatus(Number(e.target.value) as TS)} className="h-9 rounded-xl border border-input bg-background px-2 text-sm">
              {Object.entries(StatusLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
          )}
          {actionType === AutomationActionType.AssignTeam && (
            <select value={actionTeam} onChange={(e) => setActionTeam(e.target.value)} className="h-9 rounded-xl border border-input bg-background px-2 text-sm">
              <option value="">Pick a team…</option>
              {(teams ?? []).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          )}
        </div>
      </fieldset>

      {create.isError && <p className="text-xs text-destructive">Could not create rule.</p>}
      <div className="flex justify-end">
        <Button type="submit" size="sm" disabled={create.isPending} className="rounded-xl">
          {create.isPending ? 'Saving…' : 'Create rule'}
        </Button>
      </div>
    </form>
  );
}
