import { Link, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowRight, Zap, Users, Shield, Activity, Bell, BarChart3, Github } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { GradientText } from '@/components/ui/GradientText';
import { ScrollWorld, type WorldChapter } from '@/components/landing/ScrollWorld';
import { useAuth } from '@/lib/auth';
import { HeroEventFlow } from '@/components/landing/HeroEventFlow';

const chapters: WorldChapter[] = [
  {
    eyebrow: 'Multi-tenant · Event-driven · Real-time',
    align: 'left',
    range: [0, 0.19],
    title: (
      <>
        Customer support,
        <GradientText variant="warm" className="block">engineered to fly.</GradientText>
      </>
    ),
    body: 'One workspace where every ticket, SLA and reply moves in real time. Scroll — and ride the camera through the system that runs your helpdesk.',
  },
  {
    eyebrow: 'Chapter 1 · The inbox',
    align: 'right',
    range: [0.27, 0.46],
    title: (
      <>
        Every ticket,
        <GradientText className="block">in orbit.</GradientText>
      </>
    ),
    body: 'Threads, priorities and customers pulled into one live queue the moment they arrive — broadcast over SignalR, never refreshed by hand.',
  },
  {
    eyebrow: 'Chapter 2 · The SLA engine',
    align: 'left',
    range: [0.54, 0.7],
    title: (
      <>
        SLAs that
        <GradientText variant="ocean" className="block">never sleep.</GradientText>
      </>
    ),
    body: 'Policies stamped on create, a breach scanner ticking every minute, Kafka buffering through outages. Deadlines hold even when infrastructure doesn’t.',
  },
  {
    eyebrow: 'Chapter 3 · The control tower',
    align: 'center',
    range: [0.78, 0.9],
    title: (
      <>
        Signal, <GradientText variant="warm">not noise.</GradientText>
      </>
    ),
    body: 'Volume, success rate and per-day trends from one aggregation endpoint — measured under a million tickets, not guessed at.',
  },
];

function FlightCta({ onDemo }: { onDemo: () => void }) {
  return (
    <>
      <h2 className="text-5xl font-extrabold tracking-tight text-white md:text-6xl">
        Land in a <GradientText>real workspace.</GradientText>
      </h2>
      <p className="mx-auto mt-5 max-w-md text-lg leading-relaxed text-slate-300">
        Twelve curated tickets, three SLA policies, live comment threads and one
        visibly-breached row — waiting behind a single click.
      </p>
      <div className="mt-9 flex flex-wrap items-center justify-center gap-4">
        <Button
          size="lg"
          onClick={onDemo}
          className="rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500 px-8 text-base font-semibold text-white shadow-brand transition-transform hover:scale-[1.03]"
        >
          Try the demo <ArrowRight className="ml-2 h-4 w-4" />
        </Button>
        <Button size="lg" variant="ghost" asChild className="rounded-xl px-6 text-base text-slate-300 hover:bg-white/10 hover:text-white">
          <Link to="/register">Create your own</Link>
        </Button>
      </div>
    </>
  );
}

export function Landing() {
  const { demoLogin, status } = useAuth();
  const navigate = useNavigate();

  async function onDemo() {
    try {
      await demoLogin();
      navigate('/app');
    } catch {
      navigate('/login');
    }
  }

  return (
    <div className="bg-navy relative text-white">
      {/* ─── Nav ─ */}
      <header className="absolute inset-x-0 top-0 z-50">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-5">
          <Link to="/" className="flex items-center gap-2.5 text-lg font-bold tracking-tight text-white">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-violet-500 to-indigo-500 text-sm font-black text-white shadow-brand">
              S
            </div>
            ScaleDesk
          </Link>
          <div className="flex items-center gap-3">
            {status === 'authenticated' ? (
              <Button asChild size="sm" className="rounded-lg bg-gradient-to-r from-violet-600 to-indigo-500 text-white shadow-brand">
                <Link to="/app">Open workspace</Link>
              </Button>
            ) : (
              <>
                <Button asChild size="sm" variant="ghost" className="text-slate-300 hover:bg-white/10 hover:text-white">
                  <Link to="/login">Sign in</Link>
                </Button>
                <Button asChild size="sm" className="rounded-lg bg-gradient-to-r from-violet-600 to-indigo-500 text-white shadow-brand">
                  <Link to="/register">Get started</Link>
                </Button>
              </>
            )}
          </div>
        </div>
      </header>

      {/* ─── The flight: one continuous 3D world, scrubbed by scroll ─── */}
      <ScrollWorld chapters={chapters} cta={<FlightCta onDemo={onDemo} />} />

      {/* ─── Everything below the fold continues the navy + teal/violet gradient ─── */}
      <div className="bg-navy relative z-10">
        {/* Stats */}
        <section className="border-y border-white/10">
          <div className="mx-auto grid max-w-6xl grid-cols-2 gap-10 px-6 py-14 md:grid-cols-4">
            {[
              { value: '1,000+', label: 'Teams on the desk' },
              { value: '5M+', label: 'Tickets handled' },
              { value: '99.9%', label: 'Uptime, measured' },
              { value: '0.33ms', label: 'Hot read, indexed' },
            ].map((s, i) => (
              <motion.div
                key={i}
                initial={{ opacity: 0, y: 16 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true }}
                transition={{ delay: i * 0.08 }}
                className="text-center"
              >
                <div className="text-3xl font-extrabold tracking-tight text-gradient md:text-4xl">{s.value}</div>
                <div className="mt-1.5 text-xs text-slate-400 md:text-sm">{s.label}</div>
              </motion.div>
            ))}
          </div>
        </section>

        {/* Event flow */}
        <section className="mx-auto max-w-6xl px-6 py-24">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            className="mb-12 text-center"
          >
            <h2 className="text-3xl font-bold tracking-tight text-white md:text-4xl">
              The whole journey, <GradientText>event-driven.</GradientText>
            </h2>
            <p className="mx-auto mt-3 max-w-xl text-sm text-slate-400 md:text-base">
              From ticket creation to resolution — every step commits its event,
              survives a restart and lands in real time.
            </p>
          </motion.div>
          <HeroEventFlow />
        </section>

        {/* Features — glass cards on navy */}
        <section className="mx-auto max-w-6xl px-6 py-20">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            className="mb-12"
          >
            <h2 className="text-3xl font-bold tracking-tight text-white md:text-4xl">
              Built the way a <GradientText variant="ocean">real SaaS</GradientText> is built.
            </h2>
            <p className="mt-3 max-w-xl text-sm text-slate-400 md:text-base">
              Every subsystem below is measured, tested under failure, and running in this app right now.
            </p>
          </motion.div>

          <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
            {[
              { icon: <Shield className="h-5 w-5" />, title: 'Two-layer tenant isolation', desc: 'EF Core query filter and PostgreSQL Row-Level Security enforce the same boundary independently.' },
              { icon: <Zap className="h-5 w-5" />, title: 'Transactional outbox', desc: 'Every write commits with its domain event in one transaction. At-least-once delivery, guaranteed.' },
              { icon: <Activity className="h-5 w-5" />, title: 'SLA engine', desc: 'Policies stamped on create, breach scanner on timer. A Kafka outage just buffers — nothing is lost.' },
              { icon: <Users className="h-5 w-5" />, title: 'Real-time collaboration', desc: 'SignalR broadcasts threads live. Kill Redis: the app degrades to Postgres and heals itself.' },
              { icon: <Bell className="h-5 w-5" />, title: 'Notifications that survive restarts', desc: 'Outbox → Kafka → consumer → per-user inbox. Kill the broker mid-flight: writes still succeed.' },
              { icon: <BarChart3 className="h-5 w-5" />, title: 'Performance measured, not claimed', desc: '1M-ticket baseline, k6 at 500 VUs. One index took the hot read from 401.9ms to 0.334ms.' },
            ].map((f, i) => (
              <motion.div
                key={i}
                initial={{ opacity: 0, y: 14 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true }}
                transition={{ delay: (i % 3) * 0.08 }}
                className="group rounded-2xl border border-white/10 bg-white/[0.03] p-7 backdrop-blur transition-all hover:-translate-y-0.5 hover:border-violet-400/30 hover:bg-white/[0.05]"
              >
                <div className="mb-4 inline-flex h-10 w-10 items-center justify-center rounded-lg bg-violet-500/15 text-violet-300 ring-1 ring-inset ring-violet-400/20 transition group-hover:bg-violet-500/25">
                  {f.icon}
                </div>
                <h3 className="mb-1.5 text-[15px] font-semibold text-white">{f.title}</h3>
                <p className="text-sm leading-relaxed text-slate-400">{f.desc}</p>
              </motion.div>
            ))}
          </div>
        </section>

        {/* Closing CTA */}
        <section className="mx-auto max-w-6xl px-6 py-24">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            className="relative overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-br from-violet-600/20 via-[#0a0d22] to-cyan-500/10 p-12 text-center"
          >
            <h3 className="text-2xl font-bold tracking-tight text-white md:text-3xl">
              Ready to open a <GradientText variant="warm">real ticket?</GradientText>
            </h3>
            <p className="mx-auto mt-3 max-w-md text-sm text-slate-300">
              No credit card, no setup. The demo workspace is already seeded.
            </p>
            <div className="mt-8 flex flex-wrap justify-center gap-4">
              <Button
                size="lg"
                onClick={onDemo}
                className="rounded-xl bg-gradient-to-r from-violet-600 to-indigo-500 px-8 text-white shadow-brand transition-transform hover:scale-[1.02]"
              >
                Try the demo <ArrowRight className="ml-2 h-4 w-4" />
              </Button>
            </div>
          </motion.div>
        </section>

        {/* Footer */}
        <footer className="border-t border-white/10 py-10 text-center text-xs text-slate-500">
          <div className="mx-auto flex max-w-6xl items-center justify-between px-6">
            <span>© ScaleDesk · {new Date().getFullYear()}</span>
            <Link to="https://github.com" className="inline-flex items-center gap-1.5 transition-colors hover:text-white">
              <Github className="h-4 w-4" /> Source
            </Link>
          </div>
        </footer>
      </div>
    </div>
  );
}
