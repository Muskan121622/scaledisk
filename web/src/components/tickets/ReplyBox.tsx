import { useState } from 'react';
import { Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';

type Props = {
  onSubmit: (body: string, isInternal: boolean) => Promise<void> | void;
  disabled?: boolean;
};

export function ReplyBox({ onSubmit, disabled }: Props) {
  const [body, setBody] = useState('');
  const [isInternal, setIsInternal] = useState(false);
  const [busy, setBusy] = useState(false);

  async function send() {
    const trimmed = body.trim();
    if (!trimmed) return;
    setBusy(true);
    try {
      await onSubmit(trimmed, isInternal);
      setBody('');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3 shadow-sm">
      <Textarea
        value={body}
        onChange={(e) => setBody(e.target.value)}
        placeholder={isInternal ? 'Write an internal note (only visible to your team)…' : 'Write a reply to the customer…'}
        rows={3}
        className="min-h-[80px] resize-y border-0 bg-transparent px-1 py-1 focus-visible:ring-0"
        onKeyDown={(e) => {
          if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') { e.preventDefault(); send(); }
        }}
      />
      <div className="mt-2 flex items-center justify-between">
        <div className="inline-flex rounded-lg border border-slate-200 bg-slate-50 p-0.5 text-xs">
          <button
            type="button"
            onClick={() => setIsInternal(false)}
            className={cn('rounded-md px-2 py-1 transition-all', !isInternal ? 'bg-violet-100 text-violet-700' : 'text-slate-500 hover:text-slate-900')}
          >
            Public reply
          </button>
          <button
            type="button"
            onClick={() => setIsInternal(true)}
            className={cn('rounded-md px-2 py-1 transition-all', isInternal ? 'bg-amber-100 text-amber-700' : 'text-slate-500 hover:text-slate-900')}
          >
            Internal note
          </button>
        </div>
        <Button
          size="sm"
          onClick={send}
          disabled={disabled || busy || !body.trim()}
          className="bg-gradient-to-r from-violet-600 to-indigo-500 shadow-brand transition-all hover:scale-[1.02] rounded-xl"
        >
          <Send className="h-3.5 w-3.5" /> {busy ? 'Sending…' : 'Send'}
        </Button>
      </div>
    </div>
  );
}
