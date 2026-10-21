import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

export function relativeTime(iso: string): string {
  const then = new Date(iso).getTime();
  let diff = (then - Date.now()) / 1000; // negative for past events
  // Server and browser clocks drift a few seconds; treat small future timestamps
  // (and anything under ~45s) as "just now" instead of "in 3 seconds".
  if (diff > 0 && diff < 45) diff = 0;
  const abs = Math.abs(diff);
  const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
  if (abs < 45) return rtf.format(0, 'second'); // "just now"
  // Descending thresholds: pick the largest unit whose whole count is >= 1.
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 31536000], ['month', 2592000], ['week', 604800], ['day', 86400],
    ['hour', 3600], ['minute', 60],
  ];
  for (const [name, secs] of units) {
    if (abs >= secs) return rtf.format(Math.round(diff / secs), name);
  }
  return rtf.format(Math.round(diff), 'second');
}

/** Formats an ISO timestamp for the ticket thread — "Mar 3, 09:14". */
export function shortDateTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString(undefined, {
    month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}

/** Compact countdown, e.g. "1h 42m" or "overdue 3h". */
export function countdown(msRemaining: number): { text: string; tone: 'ok' | 'warn' | 'danger' } {
  const overdue = msRemaining < 0;
  const abs = Math.abs(msRemaining);
  const mins = Math.floor(abs / 60000);
  const hours = Math.floor(mins / 60);
  const days = Math.floor(hours / 24);
  let text: string;
  if (days >= 1) text = `${days}d ${hours % 24}h`;
  else if (hours >= 1) text = `${hours}h ${mins % 60}m`;
  else text = `${mins}m`;
  text = overdue ? `overdue ${text}` : `${text} left`;
  const tone: 'ok' | 'warn' | 'danger' = overdue ? 'danger' : msRemaining < 30 * 60000 ? 'warn' : 'ok';
  return { text, tone };
}
