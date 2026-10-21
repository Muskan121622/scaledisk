import { motion } from 'framer-motion';

// The animated event-flow diagram is the single most honest thing on the landing page: it is a
// literal picture of the pipeline the backend already runs (ticket created → rule → assign →
// SLA → respond → notify → resolved). The pulse is a repeating animation on the connecting
// lines so a visitor can *see* the event hop from stage to stage.
const STAGES = [
  { label: 'Ticket created', caption: 'HTTP POST /tickets' },
  { label: 'Rule matches', caption: 'automation engine' },
  { label: 'Assigned to Billing', caption: 'RBAC-scoped' },
  { label: 'SLA timer starts', caption: '30m response · 4h resolve' },
  { label: 'Agent replies', caption: 'SignalR broadcast' },
  { label: 'Customer notified', caption: 'outbox → Kafka' },
  { label: 'Resolved', caption: 'domain event published' },
];

export function HeroEventFlow() {
  const W = 920;
  const H = 340;
  const pad = 40;
  const stepX = (W - pad * 2) / (STAGES.length - 1);
  const yBase = H / 2;

  return (
    <div className="relative mx-auto w-full max-w-5xl">
      <svg viewBox={`0 0 ${W} ${H}`} className="w-full">
        <defs>
          <linearGradient id="flowline" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#a855f7" stopOpacity="0.1" />
            <stop offset="50%" stopColor="#a855f7" stopOpacity="0.9" />
            <stop offset="100%" stopColor="#22d3ee" stopOpacity="0.1" />
          </linearGradient>
        </defs>

        {/* Base connecting line, subtle */}
        <line x1={pad} y1={yBase} x2={W - pad} y2={yBase} stroke="hsl(240 12% 28%)" strokeWidth={1.5} />

        {/* Animated traveling pulse — the eye reads this as an event moving through the pipeline */}
        {STAGES.slice(0, -1).map((_, i) => (
          <motion.line
            key={i}
            x1={pad + i * stepX}
            x2={pad + (i + 1) * stepX}
            y1={yBase}
            y2={yBase}
            stroke="url(#flowline)"
            strokeWidth={2}
            initial={{ opacity: 0 }}
            animate={{ opacity: [0, 1, 0] }}
            transition={{ duration: 1.6, repeat: Infinity, delay: i * 0.5, ease: 'easeInOut' }}
          />
        ))}

        {/* Nodes */}
        {STAGES.map((s, i) => {
          const cx = pad + i * stepX;
          return (
            <g key={s.label}>
              <motion.circle
                cx={cx}
                cy={yBase}
                r={10}
                fill="#181d3f"
                stroke="#8b5cf6"
                strokeWidth={2}
                initial={{ scale: 0.9 }}
                animate={{ scale: [0.9, 1.15, 0.9] }}
                transition={{ duration: 1.6, repeat: Infinity, delay: i * 0.5, ease: 'easeInOut' }}
              />
              <text x={cx} y={yBase + 3} textAnchor="middle" fontSize={9} fill="#c4b5fd" className="font-mono font-bold">
                {i + 1}
              </text>
              <text x={cx} y={yBase - 30} textAnchor="middle" fontSize={12} fill="hsl(240 20% 98%)" fontWeight={600}>
                {s.label}
              </text>
              <text x={cx} y={yBase + 34} textAnchor="middle" fontSize={10} fill="hsl(240 8% 62%)" className="font-mono">
                {s.caption}
              </text>
            </g>
          );
        })}
      </svg>
    </div>
  );
}
