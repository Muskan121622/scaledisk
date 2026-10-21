// Customers directory: server-side search + create + delete. Data comes straight from
// /api/customers (tenant-scoped by EF filter + RLS), nothing is mocked.

import { useState } from 'react';
import { motion } from 'framer-motion';
import { Plus, Search, Trash2, Users, X } from 'lucide-react';
import { Badge } from '@/components/ui/label-badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { useCreateCustomer, useCustomers, useDeleteCustomer } from '@/hooks/useWorkspace';
import { useAuth } from '@/lib/auth';
import { relativeTime } from '@/lib/utils';

export function Customers() {
  const { user } = useAuth();
  const canManage = user?.permissions.includes('customers.update') ?? false;
  const [search, setSearch] = useState('');
  const [adding, setAdding] = useState(false);
  const { data, isLoading } = useCustomers(search);
  const del = useDeleteCustomer();

  const customers = data?.items ?? [];

  return (
    <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">Customers</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {data ? `${data.totalCount} total` : 'Loading…'} · directory scoped to your workspace
          </p>
        </div>
        {canManage && (
          <Button onClick={() => setAdding((v) => !v)} className="rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500 shadow-brand">
            {adding ? <X className="mr-1.5 h-4 w-4" /> : <Plus className="mr-1.5 h-4 w-4" />}
            {adding ? 'Cancel' : 'Add customer'}
          </Button>
        )}
      </div>

      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search name or email…"
          className="rounded-xl pl-9"
        />
      </div>

      {adding && canManage && <AddCustomerForm onDone={() => setAdding(false)} />}

      <div className="surface overflow-hidden rounded-2xl">
        {isLoading ? (
          <div className="space-y-3 p-5">
            {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-12 rounded-xl" />)}
          </div>
        ) : customers.length === 0 ? (
          <div className="flex flex-col items-center gap-2 py-14 text-center">
            <Users className="h-8 w-8 text-violet-300" />
            <p className="text-sm font-medium text-slate-700">No customers found</p>
            <p className="text-xs text-muted-foreground">Add one, or clear the search.</p>
          </div>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-100 text-[11px] uppercase tracking-wider text-muted-foreground">
              <tr>
                <th className="px-5 py-3 font-medium">Name</th>
                <th className="px-5 py-3 font-medium">Company</th>
                <th className="hidden px-5 py-3 font-medium md:table-cell">Tags</th>
                <th className="hidden px-5 py-3 font-medium sm:table-cell">Added</th>
                <th className="px-5 py-3" />
              </tr>
            </thead>
            <tbody>
              {customers.map((c) => (
                <tr key={c.id} className="border-b border-slate-50 transition-colors last:border-0 hover:bg-violet-50/40">
                  <td className="px-5 py-3">
                    <div className="font-medium text-slate-800">{c.name}</div>
                    <div className="text-xs text-muted-foreground">{c.email}</div>
                  </td>
                  <td className="px-5 py-3 text-slate-600">{c.company ?? '—'}</td>
                  <td className="hidden px-5 py-3 md:table-cell">
                    <div className="flex flex-wrap gap-1">
                      {c.tags.slice(0, 3).map((t) => <Badge key={t} variant="muted">{t}</Badge>)}
                    </div>
                  </td>
                  <td className="hidden px-5 py-3 text-xs text-muted-foreground sm:table-cell">{relativeTime(c.createdAt)}</td>
                  <td className="px-5 py-3 text-right">
                    {canManage && (
                      <Button
                        size="icon"
                        variant="ghost"
                        onClick={() => del.mutate(c.id)}
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
        )}
      </div>
    </motion.div>
  );
}

function AddCustomerForm({ onDone }: { onDone: () => void }) {
  const create = useCreateCustomer();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [company, setCompany] = useState('');
  const [tags, setTags] = useState('');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    await create.mutateAsync({
      name,
      email,
      company: company || undefined,
      tags: tags ? tags.split(',').map((t) => t.trim()).filter(Boolean) : undefined,
    });
    onDone();
  };

  return (
    <form onSubmit={submit} className="surface rounded-2xl p-5">
      <div className="grid gap-3 md:grid-cols-4">
        <Input required value={name} onChange={(e) => setName(e.target.value)} placeholder="Full name" className="rounded-xl" />
        <Input required type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="Email" className="rounded-xl" />
        <Input value={company} onChange={(e) => setCompany(e.target.value)} placeholder="Company (optional)" className="rounded-xl" />
        <Input value={tags} onChange={(e) => setTags(e.target.value)} placeholder="Tags, comma separated" className="rounded-xl" />
      </div>
      {create.isError && <p className="mt-2 text-xs text-destructive">Could not add customer — check the values.</p>}
      <div className="mt-3 flex justify-end">
        <Button type="submit" size="sm" disabled={create.isPending} className="rounded-xl">
          {create.isPending ? 'Saving…' : 'Save customer'}
        </Button>
      </div>
    </form>
  );
}
