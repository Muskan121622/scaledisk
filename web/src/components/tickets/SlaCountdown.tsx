import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge-exports';
import { countdown } from '@/lib/utils';

type Props = { dueAt: string | null; breached?: boolean; className?: string };

/**
 * Small live countdown badge — the SLA "whoosh" that makes the workspace feel real-time.
 * Recomputes once a minute; the parent typically provides a fresh dueAt on any ticket change.
 */
export function SlaCountdown({ dueAt, breached, className }: Props) {
  const [, tick] = useState(0);
  useEffect(() => {
    const id = setInterval(() => tick((t) => t + 1), 60_000);
    return () => clearInterval(id);
  }, []);

  if (breached) return <Badge variant="danger" className={className}>SLA breached</Badge>;
  if (!dueAt) return <Badge variant="muted" className={className}>No SLA</Badge>;

  const remaining = new Date(dueAt).getTime() - Date.now();
  const { text, tone } = countdown(remaining);
  const variant = tone === 'danger' ? 'danger' : tone === 'warn' ? 'warn' : 'success';
  return <Badge variant={variant}>{text}</Badge>;
}
