import type { DailyCount } from '@/types';

/**
 * Compact 14-point sparkline drawn as a filled area. Kept hand-rolled (no recharts on this
 * page) so the whole dashboard stays under a 4kb chart cost and the animation matches the
 * brand accent instead of a chart library default.
 */
export function Sparkline({ points, width = 320, height = 60 }: { points: DailyCount[]; width?: number; height?: number }) {
  if (points.length < 2) {
    return <div className="h-[60px] w-full rounded bg-muted/40" />;
  }
  const max = Math.max(...points.map((p) => p.count), 1);
  const stepX = width / (points.length - 1);
  const toY = (c: number) => height - 6 - (c / max) * (height - 12);

  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${(i * stepX).toFixed(1)},${toY(p.count).toFixed(1)}`).join(' ');
  const areaPath = `${linePath} L${width},${height} L0,${height} Z`;

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-full" preserveAspectRatio="none">
      <defs>
        <linearGradient id="spark" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="hsl(258 90% 66%)" stopOpacity="0.5" />
          <stop offset="100%" stopColor="hsl(258 90% 66%)" stopOpacity="0.02" />
        </linearGradient>
      </defs>
      <path d={areaPath} fill="url(#spark)" />
      <path d={linePath} fill="none" stroke="hsl(258 90% 66%)" strokeWidth={1.6} />
      {points.map((p, i) => (
        <circle key={p.date} cx={i * stepX} cy={toY(p.count)} r={2} fill="hsl(258 90% 66%)" opacity={0.7} />
      ))}
    </svg>
  );
}
