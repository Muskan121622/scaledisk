import { useEffect, useRef } from 'react';
import type { ReactNode } from 'react';
import * as THREE from 'three';

/* ------------------------------------------------------------------ */
/*  ScrollWorld — a scroll-scrubbed camera flight through one          */
/*  continuous Three.js scene, rendered on a DEEP NAVY canvas.         */
/*  Additive-blended violet/purple/cyan particles glow against the     */
/*  navy background; cards are dark glass panels with white text.      */
/* ------------------------------------------------------------------ */

export interface WorldChapter {
  eyebrow: string;
  title: ReactNode;
  body: string;
  align: 'left' | 'right' | 'center';
  /** scroll window [start, end] in 0..1 flight progress */
  range: [number, number];
}

const clamp01 = (v: number) => Math.min(1, Math.max(0, v));
const smoothstep = (a: number, b: number, x: number) => {
  const t = clamp01((x - a) / (b - a));
  return t * t * (3 - 2 * t);
};
function mulberry32(seed: number) {
  return () => {
    seed |= 0; seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/* ── canvas textures ──────────────────────────────────────────────── */

function roundRectPath(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  ctx.beginPath();
  ctx.roundRect(x, y, w, h, r);
}

function makeCardTexture(accent: string): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 256; c.height = 160;
  const ctx = c.getContext('2d')!;
  // dark glass panel with subtle light border
  ctx.fillStyle = 'rgba(24,29,63,0.92)';
  roundRectPath(ctx, 4, 4, 248, 152, 16); ctx.fill();
  ctx.strokeStyle = 'rgba(255,255,255,0.12)'; ctx.lineWidth = 1.5;
  roundRectPath(ctx, 4, 4, 248, 152, 16); ctx.stroke();
  // accent top bar
  const g = ctx.createLinearGradient(18, 0, 150, 0);
  g.addColorStop(0, accent); g.addColorStop(1, '#6366f1');
  ctx.fillStyle = g;
  roundRectPath(ctx, 18, 20, 82, 7, 3.5); ctx.fill();
  // headline line (white) + body lines (muted slate)
  ctx.fillStyle = 'rgba(255,255,255,0.92)';
  roundRectPath(ctx, 18, 42, 168, 12, 6); ctx.fill();
  ctx.fillStyle = 'rgba(148,163,184,0.45)';
  roundRectPath(ctx, 18, 66, 214, 7, 3.5); ctx.fill();
  roundRectPath(ctx, 18, 80, 188, 7, 3.5); ctx.fill();
  roundRectPath(ctx, 18, 94, 140, 7, 3.5); ctx.fill();
  // accent dot + status pill
  ctx.fillStyle = accent;
  ctx.beginPath(); ctx.arc(224, 126, 9, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = 'rgba(255,255,255,0.08)';
  roundRectPath(ctx, 18, 118, 66, 14, 7); ctx.fill();
  const tex = new THREE.CanvasTexture(c);
  tex.anisotropy = 4;
  return tex;
}

function makeSoftTexture(inner: string, outer: string): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const ctx = c.getContext('2d')!;
  const g = ctx.createRadialGradient(64, 64, 0, 64, 64, 64);
  g.addColorStop(0, inner); g.addColorStop(0.4, outer); g.addColorStop(1, 'rgba(255,255,255,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 128, 128);
  return new THREE.CanvasTexture(c);
}

/* ── world stations ───────────────────────────────────────────────── */

const Z_INBOX = -46;
const Z_SLA = -92;
const Z_CHART = -136;

function buildStarfield(count: number): THREE.Points {
  const pos = new Float32Array(count * 3);
  const col = new Float32Array(count * 3);
  const rnd = mulberry32(7);
  const violet = new THREE.Color(0x8b5cf6);
  const purple = new THREE.Color(0xa855f7);
  const cyan = new THREE.Color(0x22d3ee);
  for (let i = 0; i < count; i++) {
    pos[i * 3] = (rnd() - 0.5) * 90;
    pos[i * 3 + 1] = (rnd() - 0.5) * 56;
    pos[i * 3 + 2] = 24 - rnd() * 200;
    const pick = rnd();
    const c = pick < 0.45 ? violet : pick < 0.75 ? purple : cyan;
    col[i * 3] = c.r; col[i * 3 + 1] = c.g; col[i * 3 + 2] = c.b;
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  geo.setAttribute('color', new THREE.BufferAttribute(col, 3));
  const mat = new THREE.PointsMaterial({
    size: 0.18, vertexColors: true, transparent: true, opacity: 0.85,
    depthWrite: false, blending: THREE.AdditiveBlending,
  });
  return new THREE.Points(geo, mat);
}

function buildInboxCluster() {
  const group = new THREE.Group();
  group.position.z = Z_INBOX;
  const rnd = mulberry32(42);
  const accents = ['#8b5cf6', '#a855f7', '#22d3ee', '#6366f1'];
  const cards: { mesh: THREE.Mesh; baseY: number; phase: number; speed: number }[] = [];
  const endpoints: THREE.Vector3[] = [];

  for (let i = 0; i < 14; i++) {
    const tex = makeCardTexture(accents[i % accents.length]);
    const mesh = new THREE.Mesh(
      new THREE.PlaneGeometry(4.4, 2.75),
      new THREE.MeshBasicMaterial({ map: tex, transparent: true }),
    );
    const p = new THREE.Vector3(
      (rnd() - 0.5) * 30,
      (rnd() - 0.5) * 14,
      (rnd() - 0.5) * 12,
    );
    mesh.position.copy(p);
    mesh.rotation.z = (rnd() - 0.5) * 0.24;
    group.add(mesh);
    endpoints.push(p);
    cards.push({ mesh, baseY: p.y, phase: rnd() * Math.PI * 2, speed: 0.4 + rnd() * 0.5 });
  }

  const linePos = new Float32Array(endpoints.length * 6);
  endpoints.forEach((e, i) => linePos.set([0, 0, 0, e.x, e.y, e.z], i * 6));
  const lineGeo = new THREE.BufferGeometry();
  lineGeo.setAttribute('position', new THREE.BufferAttribute(linePos, 3));
  group.add(new THREE.Line(lineGeo, new THREE.LineBasicMaterial({
    color: 0x8b5cf6, transparent: true, opacity: 0.35, depthWrite: false,
  })));

  const core = new THREE.Sprite(new THREE.SpriteMaterial({
    map: makeSoftTexture('rgba(168,85,247,0.6)', 'rgba(99,102,241,0.22)'),
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  core.scale.setScalar(7);
  group.add(core);

  return { group, cards, core };
}

function buildSlaEngine() {
  const group = new THREE.Group();
  group.position.z = Z_SLA;

  const mk = (r: number, tube: number, color: number, opacity: number) =>
    new THREE.Mesh(
      new THREE.TorusGeometry(r, tube, 12, 120),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity, depthWrite: false }),
    );
  const ringA = mk(6.4, 0.1, 0x8b5cf6, 0.85);
  const ringB = mk(4.6, 0.07, 0x22d3ee, 0.85);
  const ringC = mk(8.2, 0.04, 0xa855f7, 0.5);
  ringB.rotation.x = Math.PI / 2.6;
  ringC.rotation.y = Math.PI / 5;
  group.add(ringA, ringB, ringC);

  const core = new THREE.Sprite(new THREE.SpriteMaterial({
    map: makeSoftTexture('rgba(139,92,246,0.6)', 'rgba(34,211,238,0.22)'),
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
  }));
  core.scale.setScalar(5);
  group.add(core);

  const orbiters: { mesh: THREE.Mesh; radius: number; speed: number; phase: number; tilt: number }[] = [];
  const sphere = new THREE.SphereGeometry(0.3, 14, 14);
  [
    { color: 0x8b5cf6, radius: 6.4, speed: 0.55, phase: 0, tilt: 0 },
    { color: 0x22d3ee, radius: 4.6, speed: -0.8, phase: 2.1, tilt: Math.PI / 2.6 },
    { color: 0xa855f7, radius: 8.2, speed: 0.35, phase: 4.2, tilt: Math.PI / 5 },
  ].forEach((o) => {
    const mesh = new THREE.Mesh(sphere, new THREE.MeshBasicMaterial({ color: o.color }));
    group.add(mesh);
    orbiters.push({ mesh, ...o });
  });

  return { group, ringA, ringB, ringC, core, orbiters };
}

function buildAnalyticsSkyline() {
  const group = new THREE.Group();
  group.position.set(0, -2, Z_CHART);
  const rnd = mulberry32(99);
  const bars: { mesh: THREE.Mesh; baseH: number; phase: number }[] = [];
  const cols = 7, rows = 5;

  for (let x = 0; x < cols; x++) {
    for (let z = 0; z < rows; z++) {
      const h = 1.2 + rnd() * 7.5;
      const t = h / 8.7;
      const color = new THREE.Color(0x6366f1).lerp(new THREE.Color(0x22d3ee), t);
      const mesh = new THREE.Mesh(
        new THREE.BoxGeometry(1.25, h, 1.25),
        new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.5 + t * 0.4 }),
      );
      mesh.position.set((x - (cols - 1) / 2) * 2.35, h / 2, (z - (rows - 1) / 2) * 2.35);
      group.add(mesh);
      bars.push({ mesh, baseH: h, phase: rnd() * Math.PI * 2 });
    }
  }
  return { group, bars };
}

/* ── component ────────────────────────────────────────────────────── */

export function ScrollWorld({ chapters, cta }: { chapters: WorldChapter[]; cta: ReactNode }) {
  const canvasHostRef = useRef<HTMLDivElement>(null);
  const overlayRefs = useRef<Array<HTMLDivElement | null>>([]);
  const ctaRef = useRef<HTMLDivElement>(null);
  const hintRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const host = canvasHostRef.current;
    if (!host) return;

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.75));
    renderer.setSize(window.innerWidth, window.innerHeight);
    host.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.fog = new THREE.Fog(0x0a0d22, 30, 135);
    const camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 400);

    const isMobile = window.innerWidth < 768;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const camPath = new THREE.CatmullRomCurve3([
      new THREE.Vector3(0, 0.5, 18),
      new THREE.Vector3(4, 1.5, -12),
      new THREE.Vector3(-3, 2, Z_INBOX + 10),
      new THREE.Vector3(5, -1, Z_INBOX - 12),
      new THREE.Vector3(-4, 3, Z_SLA + 12),
      new THREE.Vector3(3, 0.5, Z_SLA - 10),
      new THREE.Vector3(-2, 3.5, Z_CHART + 16),
      new THREE.Vector3(0, 4.5, Z_CHART + 12),
    ]);
    const lookPath = new THREE.CatmullRomCurve3([
      new THREE.Vector3(0, 0, -8),
      new THREE.Vector3(0, 0, Z_INBOX),
      new THREE.Vector3(0, 0, Z_INBOX),
      new THREE.Vector3(0, 0, Z_SLA),
      new THREE.Vector3(0, 0, Z_SLA),
      new THREE.Vector3(0, 0, Z_CHART),
      new THREE.Vector3(0, -1, Z_CHART),
      new THREE.Vector3(0, -1.5, Z_CHART - 20),
    ]);

    const stars = buildStarfield(isMobile ? 1200 : 2400);
    scene.add(stars);

    const shell = new THREE.Mesh(
      new THREE.IcosahedronGeometry(3.4, 1),
      new THREE.MeshBasicMaterial({ color: 0x8b5cf6, wireframe: true, transparent: true, opacity: 0.5 }),
    );
    shell.position.set(7, 1, -8); // pushed right so it sits beside the headline, not behind it
    scene.add(shell);

    const inbox = buildInboxCluster();
    scene.add(inbox.group);
    const sla = buildSlaEngine();
    scene.add(sla.group);
    const chart = buildAnalyticsSkyline();
    scene.add(chart.group);

    let p = 0, pTarget = 0;
    let mx = 0, my = 0, smx = 0, smy = 0;

    const onScroll = () => { pTarget = clamp01(window.scrollY / (window.innerHeight * 4)); };
    const onPointer = (e: PointerEvent) => {
      mx = (e.clientX / window.innerWidth) * 2 - 1;
      my = (e.clientY / window.innerHeight) * 2 - 1;
    };
    const onResize = () => {
      camera.aspect = window.innerWidth / window.innerHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(window.innerWidth, window.innerHeight);
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('pointermove', onPointer, { passive: true });
    window.addEventListener('resize', onResize);
    onScroll();

    const clock = new THREE.Clock();
    const lookAt = new THREE.Vector3();
    let raf = 0;

    const frame = () => {
      raf = requestAnimationFrame(frame);
      const t = reduceMotion ? 0 : clock.getElapsedTime();

      p += (pTarget - p) * 0.075;
      smx += (mx - smx) * 0.05;
      smy += (my - smy) * 0.05;

      camPath.getPoint(clamp01(p), camera.position);
      lookPath.getPoint(clamp01(p), lookAt);
      if (!isMobile) {
        camera.position.x += smx * 1.6;
        camera.position.y += -smy * 1.0;
      }
      camera.lookAt(lookAt);

      shell.rotation.y = t * 0.12;
      shell.rotation.x = Math.sin(t * 0.2) * 0.12;
      stars.rotation.z = t * 0.004;

      inbox.cards.forEach((c) => {
        c.mesh.position.y = c.baseY + Math.sin(t * c.speed + c.phase) * 0.5;
        c.mesh.rotation.z = Math.sin(t * 0.3 + c.phase) * 0.05;
      });
      inbox.core.material.opacity = 0.7 + Math.sin(t * 2.2) * 0.15;

      sla.ringA.rotation.z = t * 0.35;
      sla.ringB.rotation.z = -t * 0.5;
      sla.ringC.rotation.x = t * 0.18;
      sla.core.material.opacity = 0.6 + Math.sin(t * 2.6) * 0.2;
      sla.orbiters.forEach((o) => {
        const a = t * o.speed + o.phase;
        o.mesh.position.set(
          Math.cos(a) * o.radius,
          Math.sin(a) * o.radius * Math.cos(o.tilt),
          Math.sin(a) * o.radius * Math.sin(o.tilt),
        );
      });

      chart.bars.forEach((b, i) => {
        const pulse = 1 + Math.sin(t * 1.4 + b.phase) * 0.07;
        b.mesh.scale.y = pulse;
        b.mesh.position.y = (b.baseH * pulse) / 2;
        if (i % 3 === 0) (b.mesh.material as THREE.MeshBasicMaterial).opacity = 0.55 + Math.sin(t * 1.4 + b.phase) * 0.2;
      });

      chapters.forEach((ch, i) => {
        const el = overlayRefs.current[i];
        if (!el) return;
        const [a, b] = ch.range;
        const fadeIn = a <= 0.001 ? 1 : smoothstep(a - 0.035, a + 0.02, p);
        const op = fadeIn * (1 - smoothstep(b - 0.02, b + 0.035, p));
        el.style.opacity = String(op);
        el.style.transform = `translateY(${(1 - op) * 26}px)`;
        el.style.visibility = op < 0.02 ? 'hidden' : 'visible';
      });
      if (ctaRef.current) {
        const op = smoothstep(0.93, 0.995, p);
        ctaRef.current.style.opacity = String(op);
        ctaRef.current.style.visibility = op < 0.02 ? 'hidden' : 'visible';
      }
      if (hintRef.current) {
        hintRef.current.style.opacity = String(1 - smoothstep(0.008, 0.05, p));
      }

      const past = clamp01((window.scrollY - window.innerHeight * 4) / window.innerHeight);
      host.style.opacity = String(1 - past);

      renderer.render(scene, camera);
    };
    frame();

    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener('scroll', onScroll);
      window.removeEventListener('pointermove', onPointer);
      window.removeEventListener('resize', onResize);
      scene.traverse((obj) => {
        const mesh = obj as THREE.Mesh;
        if (mesh.geometry) mesh.geometry.dispose();
        const mat = mesh.material as THREE.Material | THREE.Material[] | undefined;
        const list = Array.isArray(mat) ? mat : mat ? [mat] : [];
        list.forEach((m) => {
          const anyMat = m as THREE.Material & { map?: THREE.Texture };
          anyMat.map?.dispose();
          m.dispose();
        });
      });
      renderer.dispose();
      host.removeChild(renderer.domElement);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="bg-navy relative" style={{ height: '500vh' }}>
      <div ref={canvasHostRef} className="fixed inset-0 z-0" />

      {chapters.map((ch, i) => (
        <div
          key={i}
          ref={(el) => { overlayRefs.current[i] = el; }}
          className="pointer-events-none fixed inset-0 z-10 flex items-center"
          style={{ opacity: 0 }}
        >
          <div className="mx-auto w-full max-w-6xl px-6">
            <div
              className={
                ch.align === 'left' ? 'max-w-2xl' :
                ch.align === 'right' ? 'ml-auto max-w-2xl text-right' :
                'mx-auto max-w-3xl text-center'
              }
            >
              <div className="mb-5 text-xs font-semibold uppercase tracking-[0.28em] text-violet-300">
                {ch.eyebrow}
              </div>
              <h2 className="text-5xl font-extrabold leading-[1.03] tracking-tight text-white md:text-7xl">
                {ch.title}
              </h2>
              <p className="mt-6 max-w-xl text-lg leading-relaxed text-slate-300/90 md:text-xl">{ch.body}</p>
            </div>
          </div>
        </div>
      ))}

      <div ref={ctaRef} className="pointer-events-none fixed inset-0 z-10 flex items-center justify-center" style={{ opacity: 0 }}>
        <div className="pointer-events-auto mx-auto max-w-2xl px-6 text-center">{cta}</div>
      </div>

      <div ref={hintRef} className="pointer-events-none fixed bottom-8 left-1/2 z-10 -translate-x-1/2">
        <div className="flex flex-col items-center gap-2 text-[10px] uppercase tracking-[0.3em] text-slate-400">
          <span>Scroll to fly</span>
          <div className="h-8 w-px animate-pulse bg-gradient-to-b from-violet-400/80 to-transparent" />
        </div>
      </div>
    </div>
  );
}
