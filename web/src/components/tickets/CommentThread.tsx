import { motion } from 'framer-motion';
import { shortDateTime, cn } from '@/lib/utils';
import { Badge } from '@/components/ui/label-badge';
import type { TicketComment } from '@/types';

type Props = { comments: TicketComment[]; currentUserId?: string };

export function CommentThread({ comments, currentUserId }: Props) {
  if (comments.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-slate-300 p-8 text-center text-sm text-muted-foreground">
        No replies yet. Be the first to respond.
      </div>
    );
  }

  return (
    <ul className="space-y-3">
      {comments.map((c, i) => {
        const fromAgent = c.authorUserId !== null;
        const isSelf = fromAgent && currentUserId && c.authorUserId === currentUserId;
        return (
          <motion.li
            key={c.id}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.05, duration: 0.3 }}
          >
            <article
              className={cn(
                'rounded-xl border p-4 transition-all duration-200',
                c.isInternal ? 'border-amber-200 bg-amber-50' :
                fromAgent ? 'border-violet-200 bg-violet-50/60' : 'border-slate-200 bg-white'
              )}
            >
              <header className="mb-2 flex items-center justify-between text-xs text-muted-foreground">
                <div className="flex items-center gap-2">
                  <div className={cn(
                    'flex h-6 w-6 items-center justify-center rounded-full text-[11px] font-medium',
                    fromAgent ? 'bg-gradient-to-br from-violet-600 to-indigo-500 text-white' : 'bg-slate-100 text-slate-600'
                  )}>
                    {(fromAgent ? 'A' : 'C')}
                  </div>
                  <span className="font-medium text-foreground">
                    {fromAgent ? (isSelf ? 'You' : 'Agent') : 'Customer'}
                  </span>
                  {c.isInternal && <Badge variant="warn">internal note</Badge>}
                </div>
                <span>{shortDateTime(c.createdAt)}</span>
              </header>
              <p className="whitespace-pre-wrap text-sm leading-relaxed">{c.body}</p>
            </article>
          </motion.li>
        );
      })}
    </ul>
  );
}
