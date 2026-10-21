// Portal intake form. Mirrors CreatePortalTicketRequest: subject/description/priority/category
// ONLY — no customer picker (cid claim), no team/assignee (tenant automation routes it).

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label-badge';
import { ApiError } from '@/lib/api';
import { useCreateTicket } from '@/hooks/usePortal';
import { CategoryLabel, PriorityLabel, TicketCategory, TicketPriority } from '@/types';

export function PortalNewTicket() {
  const navigate = useNavigate();
  const create = useCreateTicket();
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<number>(TicketPriority.Medium);
  const [category, setCategory] = useState<number>(TicketCategory.General);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      const t = await create.mutateAsync({ subject, description, priority, category });
      navigate(`/portal/tickets/${t.id}`, { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? (err.body?.title ?? 'Could not file the request.') : 'Could not file the request.');
    }
  }

  const field = 'mt-1.5 rounded-xl border-slate-200 bg-white';

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="mx-auto max-w-xl space-y-5">
      <div>
        <button onClick={() => navigate(-1)} className="mb-2 inline-flex items-center gap-1 text-xs text-slate-400 hover:text-slate-600">
          <ArrowLeft className="h-3 w-3" /> Back to my tickets
        </button>
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">New support request</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">The team is notified the moment you file it.</p>
      </div>

      <form onSubmit={submit} className="surface space-y-4 rounded-2xl p-6">
        <div>
          <Label htmlFor="subject" className="text-slate-600">Subject</Label>
          <Input
            id="subject" value={subject} onChange={(e) => setSubject(e.target.value)}
            placeholder="Short summary of the problem" required maxLength={200} className={field}
          />
        </div>
        <div>
          <Label htmlFor="description" className="text-slate-600">What's happening?</Label>
          <textarea
            id="description" value={description} onChange={(e) => setDescription(e.target.value)}
            required rows={6} maxLength={4000}
            placeholder="Steps, expected vs actual, order numbers — anything that helps…"
            className={`w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none ring-violet-200 focus:ring-2 ${field}`}
          />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <Label htmlFor="priority" className="text-slate-600">Urgency</Label>
            <select id="priority" value={priority} onChange={(e) => setPriority(Number(e.target.value))} className={field + ' h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm'}>
              {Object.entries(PriorityLabel).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </div>
          <div>
            <Label htmlFor="category" className="text-slate-600">Category</Label>
            <select id="category" value={category} onChange={(e) => setCategory(Number(e.target.value))} className={field + ' h-9 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm'}>
              {Object.entries(CategoryLabel).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </div>
        </div>

        {error && (
          <div className="rounded-md border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">{error}</div>
        )}

        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" className="rounded-xl" onClick={() => navigate('/portal/tickets')}>Cancel</Button>
          <Button type="submit" disabled={create.isPending || !subject.trim() || !description.trim()}
            className="rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500">
            {create.isPending ? 'Filing…' : 'File request'}
          </Button>
        </div>
      </form>
    </motion.div>
  );
}
